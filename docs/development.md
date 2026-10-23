# Development setup

The project provides a dependency inspector, runtime compatibility checks, and an opt-in Phase 2 on-foot border companion. Without BoundaryTest.xml, the companion installs no travel, suspension or shared-calendar patches. Suspension additionally requires `freezeEmptyWorlds="true"`. The [shared calendar](shared-clock.md) requires suspension and `sharedClock="true"`. See [Phase 2](phase-2.md) for scope and client testing.

## Prerequisites

- Python 3.11 or later.
- .NET SDK 8, including its reference pack. A runtime alone is insufficient.
- LITW MultiWorld 0.6.2, preserved under `resources/multiworld/original/`.

The Python entry point invokes the SDK's Roslyn compiler directly. It compiles the development inspector against .NET 8, and the game mod against the actual game framework assemblies. It does not substitute .NET 8's framework for the game's Mono framework. This also works in environments where MSBuild's process inspection is unavailable. The inspector has no NuGet dependencies; its `.csproj` is supplied for conventional SDK/IDE builds as well.

## Obtain the game SDK

```sh
python tools/fetch-dependencies.py
```

The automatic downloader bootstrap supports Linux x64. On Windows or another platform, obtain DepotDownloader 3.4.0 from [its upstream releases](https://github.com/SteamRE/DepotDownloader/releases/tag/DepotDownloader_3.4.0) and pass its executable:

```powershell
python tools/fetch-dependencies.py --downloader C:\Tools\DepotDownloader\DepotDownloader.exe
```

The script uses an anonymous Steam dedicated-server subscription, requests app `294420`, Linux depot `294422`, manifest `3031329678505041777`, and downloads only the locked managed assemblies and bundled Harmony files. It does not download maps or assets, install or launch a server, or require a Steam password. Steam may restrict access to historical manifests in the future; the script reports download or checksum failures rather than silently switching builds.

The pinned source contains all 15 direct dependencies referenced by MultiWorld, including `Assembly-CSharp`, Unity modules, Harmony `2.13.0.0`, and Mono.Cecil `0.11.5.0`. There are 163 locked files, including framework facades and Harmony's additional runtime dependencies. `config/dependencies.lock.json` records the source manifest and SHA256 for each file. Downloads remain under the ignored `local-dependencies/` directory; the repository stores the reproducible acquisition recipe and checksums.

To verify an existing download without contacting Steam:

```sh
python tools/fetch-dependencies.py --verify-only
```

An installed client or server can also supply files for inspection. The collector creates `local-dependencies/game-sdk.zip` containing only DLLs and, when available, the server's Steam app manifest:

```powershell
python tools/dev.py collect --game-dir "C:\Program Files (x86)\Steam\steamapps\common\7 Days to Die"
```

Supply `--harmony DIRECTORY` if Harmony is installed somewhere other than `Mods/0_TFP_Harmony`. A different game build must be inspected before deliberately updating the lock. Identical assembly version numbers, especially `0.0.0.0`, do not establish identical game builds. The downloaded Linux SDK has not established Windows runtime compatibility.

## Inspect and build

```sh
python tools/dev.py check --reference-only
python tools/dev.py build --managed local-dependencies/server/7DaysToDieServer_Data/Managed --harmony local-dependencies/server/Mods/0_TFP_Harmony
```

Use `--dotnet /path/to/dotnet` or set `DOTNET` if the SDK is outside PATH. All commands run from the repository root, on Windows or Linux.

`--reference-only` verifies the pinned upstream binary and all 12 static hook signatures without requiring game dependencies. It deliberately reports missing dependencies; success in this mode is not a successful game build. A strict check or build refuses missing, conflicting, or version-mismatched direct references. The build additionally requires the resolved reference files to match the pinned Steam SDK hashes.

The diagnostic assembly and `ModInfo.xml` are written to `artifacts/mod/SevenDaysBeyond/`. The compiler treats warnings as errors. `artifacts/dependency-report.json` records resolved versions, module IDs, hashes, and validation status. Generated reports and binaries are ignored by Git.

```sh
python -m unittest discover -s tests -v
```

The tests exercise actual metadata, all hook signatures, missing dependencies, wrong and modified managed binaries, non-managed input, and checksum rejection. They do not mock the game API or claim to simulate the game.

## First in-game verification

The native baseline installation on ANNWN is documented in [annwn-server.md](annwn-server.md). Its client join/disconnect test and the subsequent two-world modded server startup have succeeded. The [West/East test](multiworld-test.md) also passed repeated manual transfers, inventory/location checks, reconnects, and a server restart. The checks below describe that baseline; automatic crossings require a separate [Phase 2 test](phase-2.md).

Use a separate test server and two disposable map saves with MultiWorld's normal runtime configuration. Keep its existing client/server installation requirements. Add the built `SevenDaysBeyond` directory to the test server's `Mods` directory.

1. Start the server and check for `UPSTREAM_CONTRACT_OK hooks=12` in its log. A failure logs the reason and prevents border patch activation.
2. Join using an appropriately modded client. Verify the normal `/world` transfer in both directions, inventory and progression, and each destination's saved position.
3. Disconnect, reconnect, restart the server, and confirm those baseline behaviors still work.
4. Record the exact game version, startup log, assembly module IDs, and results before enabling future travel changes.

The baseline in-game smoke test was run on ANNWN, not in this workspace. The selective dependency download does not contain a runnable dedicated server. A clean compile and metadata check establish build readiness, not runtime compatibility or reliable transfers.

## Improved decompilation

With ILSpy CLI 9.1.0.7988 installed, copy the resolved reference assemblies listed in `artifacts/dependency-report.json` into an ignored directory, then export into a fresh output directory:

```sh
ilspycmd --disable-updatecheck --referencepath local-dependencies/resolved --project --outputdir artifacts/multiworld-resolved resources/multiworld/original/LITW_MultiWorld/LITWMultiWorldProbe.dll
```

The phase 1 investigation performed this export successfully, removing the 464 unresolved-IL warning comments in the initial export. The regenerated C# is still reconstructed source. A trial full rebuild exposed decompiler artifacts, including an emitted compiler attribute and nullable `typeof` expressions, as well as additional framework facade references needed for that larger build. It is not a verified replacement DLL. The original archive, DLL, IL listing, and initial decompiled source remain unchanged.
