#!/usr/bin/env python3
"""Retrieve the pinned dedicated-server SDK from Steam, without installing a server."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / "config/dependencies.lock.json"
DOWNLOADER_URL = "https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_3.4.0/DepotDownloader-linux-x64.zip"
DOWNLOADER_SHA256 = "a999dec66b4850fc961bd50366696d23c2d0fad7b18790e6a5647b2f19097a53"


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify(directory, lock):
    failures = []
    for entry in lock["files"]:
        path = directory / entry["path"]
        if not path.is_file():
            failures.append(entry["path"] + ": missing")
        elif sha256(path) != entry["sha256"]:
            failures.append(entry["path"] + ": SHA256 mismatch")
    if failures:
        raise ValueError("Dependency verification failed:\n" + "\n".join(failures))
    print("Verified " + str(len(lock["files"])) + " pinned dependency files.")


def obtain_downloader():
    if platform.system() != "Linux" or platform.machine().lower() not in ("x86_64", "amd64"):
        raise ValueError("Supply --downloader with DepotDownloader 3.4.0 for your platform, or collect your installed game using tools/dev.py collect.")
    directory = ROOT / "local-dependencies/tools/depotdownloader-3.4.0"
    archive = directory / "download.zip"
    directory.mkdir(parents=True, exist_ok=True)
    if not archive.exists() or sha256(archive) != DOWNLOADER_SHA256:
        with urllib.request.urlopen(DOWNLOADER_URL, timeout=60) as response, archive.open("wb") as target:
            shutil.copyfileobj(response, target)
    if sha256(archive) != DOWNLOADER_SHA256:
        raise ValueError("DepotDownloader archive checksum mismatch.")
    with zipfile.ZipFile(archive) as package:
        # Extract only the known executable and license, avoiding arbitrary archive paths.
        for name in ("DepotDownloader", "LICENSE"):
            (directory / name).write_bytes(package.read(name))
    executable = directory / "DepotDownloader"
    executable.chmod(0o755)
    return executable


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--downloader", type=Path)
    parser.add_argument("--directory", type=Path, default=ROOT / "local-dependencies/server")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    directory = args.directory.resolve()
    if not args.verify_only:
        downloader = args.downloader.resolve() if args.downloader else obtain_downloader()
        filelist = ROOT / "local-dependencies/dependency-files.txt"
        filelist.parent.mkdir(parents=True, exist_ok=True)
        filelist.write_text("\n".join(f["path"] for f in lock["files"]) + "\n", encoding="utf-8")
        subprocess.run([str(downloader), "-app", str(lock["appId"]), "-depot", str(lock["depotId"]),
                        "-manifest", lock["manifestId"], "-os", lock["os"], "-filelist", str(filelist),
                        "-dir", str(directory), "-max-downloads", "4"], check=True)
    verify(directory, lock)
    print("Next: python tools/dev.py build --managed " + str(directory / "7DaysToDieServer_Data/Managed")
          + " --harmony " + str(directory / "Mods/0_TFP_Harmony"))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, subprocess.CalledProcessError, zipfile.BadZipFile) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
