#!/usr/bin/env python3
"""Offline development checks and compilation. Requires Python 3 and .NET SDK 8."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
UPSTREAM = ROOT / "resources/multiworld/original/LITW_MultiWorld/LITWMultiWorldProbe.dll"


def version_key(path):
    return tuple(int(n) for n in re.findall(r"\d+", path.name))


def run(command):
    subprocess.run([str(x) for x in command], check=True)


def sdk_paths(dotnet):
    result = subprocess.run([dotnet, "--list-sdks"], check=True, capture_output=True, text=True)
    installed = []
    for line in result.stdout.splitlines():
        match = re.fullmatch(r"(8\.\d+\.\d+) \[(.+)\]", line)
        if match:
            installed.append(Path(match[2]) / match[1])
    if not installed:
        raise ValueError("Install .NET SDK 8; a runtime alone cannot compile this project.")
    sdk = max(installed, key=version_key)
    dotnet_root = sdk.parent.parent
    packs = list((dotnet_root / "packs/Microsoft.NETCore.App.Ref").glob("8.*"))
    if not packs:
        raise ValueError("The SDK installation is missing .NET 8 reference assemblies.")
    return sdk / "Roslyn/bincore/csc.dll", max(packs, key=version_key) / "ref/net8.0"


def compile_sources(dotnet, compiler, sources, references, output, executable=False, nullable=True):
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [dotnet, compiler, "-noconfig", "-nostdlib+", "-nologo", "-deterministic+",
               "-warnaserror+", "-target:" + ("exe" if executable else "library"),
               "-langversion:" + ("12" if executable else "7.3"), "-out:" + str(output)]
    if executable and nullable:
        command += ["-nullable:enable"]
    command += ["-reference:" + str(p) for p in references]
    command += list(sources)
    run(command)


def collect(args):
    game = args.game_dir.resolve()
    managed_options = [game / name / "Managed" for name in
                       ("7DaysToDieServer_Data", "7DaysToDie_Data")]
    managed = next((p for p in managed_options if (p / "Assembly-CSharp.dll").is_file()), None)
    harmony = args.harmony or game / "Mods/0_TFP_Harmony"
    if managed is None:
        raise ValueError("No game Managed directory with Assembly-CSharp.dll was found.")
    if not harmony.is_dir() or not list(harmony.rglob("0Harmony.dll")):
        raise ValueError("No 0Harmony.dll found; supply --harmony with the installed Harmony mod directory.")
    output = ROOT / "local-dependencies/game-sdk.zip"
    output.parent.mkdir(parents=True, exist_ok=True)
    # Include only assemblies and version evidence, never saves or player data.
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for label, directory in (("Managed", managed), ("Harmony", harmony)):
            for file in sorted(directory.rglob("*.dll")):
                archive.write(file, str(Path(label) / file.relative_to(directory)))
        manifest = game.parent.parent / "appmanifest_294420.acf"
        if manifest.is_file():
            archive.write(manifest, "appmanifest_294420.acf")
    print("Collected game dependencies: " + str(output))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("check", "build", "collect"))
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    parser.add_argument("--managed", type=Path)
    parser.add_argument("--harmony", type=Path)
    parser.add_argument("--game-dir", type=Path)
    parser.add_argument("--upstream", type=Path, default=UPSTREAM)
    parser.add_argument("--reference-only", action="store_true")
    args = parser.parse_args()
    if args.command == "collect":
        if args.game_dir is None:
            parser.error("collect requires --game-dir")
        collect(args)
        return 0
    if args.command == "build" and args.reference_only:
        parser.error("build cannot skip dependencies with --reference-only")
    dotnet = shutil.which(args.dotnet)
    if not dotnet:
        raise ValueError("Cannot find .NET; supply --dotnet with the SDK's dotnet executable.")
    compiler, tool_references = sdk_paths(dotnet)
    artifacts = ROOT / "artifacts"
    checker = artifacts / "tools/DependencyCheck.dll"
    compile_sources(dotnet, compiler,
                    sorted((ROOT / "tools/DependencyCheck").glob("*.cs")) + [ROOT / "src/Shared/MultiWorldContract.cs"],
                    sorted(tool_references.glob("*.dll")), checker, executable=True)
    checker.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
        "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"}}}), encoding="utf-8")
    report_path = artifacts / "dependency-report.json"
    command = [dotnet, str(checker), "--upstream", str(args.upstream.resolve()), "--output", str(report_path)]
    for flag, value in (("--managed", args.managed), ("--harmony", args.harmony)):
        if value is not None:
            command += [flag, str(value.resolve())]
    if args.reference_only:
        command += ["--reference-only"]
    result = subprocess.run(command)
    if result.returncode or args.command == "check":
        return result.returncode
    report = json.loads(report_path.read_text(encoding="utf-8"))
    lock = json.loads((ROOT / "config/dependencies.lock.json").read_text(encoding="utf-8"))
    pinned_hashes = {entry["sha256"] for entry in lock["files"]}
    for dependency in report["Dependencies"]:
        if dependency["Candidates"][0]["Sha256"] not in pinned_hashes:
            raise ValueError("Dependency differs from the inspected Steam manifest: " + dependency["Name"]
                             + ". Use the pinned download or inspect the new game build before updating the lock.")
    report["GameSdkPinned"] = True
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    # Unity/Harmony APIs expose netstandard facade types. Pin the game facade too.
    facade = args.managed.resolve() / "netstandard.dll"
    import hashlib
    if hashlib.sha256(facade.read_bytes()).hexdigest() not in pinned_hashes:
        raise ValueError("Game netstandard facade differs from the pinned SDK.")
    report["CompilerReferences"].append(str(facade))
    # Compile against the actual game's framework, never .NET 8 runtime assemblies.
    output = artifacts / "mod/SevenDaysBeyond/SevenDaysBeyond.dll"
    compile_sources(dotnet, compiler,
                    sorted((ROOT / "src/SevenDaysBeyond").rglob("*.cs")) + [ROOT / "src/Shared/MultiWorldContract.cs"],
                    [Path(p) for p in report["CompilerReferences"]], output)
    shutil.copy2(ROOT / "src/SevenDaysBeyond/ModInfo.xml", output.parent / "ModInfo.xml")
    report["ModBuildSucceeded"] = True
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("Travel mod built: " + str(output.parent))
    print("Game startup and manual transfers must still be tested; this command does not deploy anything.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("Development command failed: " + str(error), file=sys.stderr)
        sys.exit(1)
