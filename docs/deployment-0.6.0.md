# ANNWN deployment: 0.6.0

Deployed on October 23, 2026 to the existing isolated `seven-days-beyond.service` on ANNWN. The service was stopped cleanly (inactive, MainPID 0), the four-file shared package was installed with `tools/deploy-shared-mod.py`, then the same service was started again. No other services were restarted.

## Download and installation

The exact installed package is [downloads/SevenDaysBeyond-0.6.0.zip](../downloads/SevenDaysBeyond-0.6.0.zip). Its SHA-256 is `34e6623ff9c135621b6736cf150d29879a065196ee973a00f0b05dffe150e266`. The DLL SHA-256 is `a36c169edf965fcaadc6ed343e95f39a375020d50a09fa9054f153a332822769`.

Download the ZIP from its GitHub file page. With the game closed, copy the extracted `SevenDaysBeyond` folder into the client `Mods` directory, replacing the previous Beyond companion. Include `Config/quests.xml` and `Config/Localization.csv`, not just the DLL. Keep the existing LITW MultiWorld and bundled Harmony mods and launch without EAC as before. Source under `src/` is not the compiled installation.

## Preserved state

Before installation, a backup of test saves, configuration, both mod folders and the prior server log was written to `/srv/seven-days-beyond/backups/before-shared-mod-20261023T052355628403Z.tar.gz`. SHA-256 checks confirmed the upstream DLL, upstream world configuration, service game configuration and Beyond `BoundaryTest.xml` were unchanged. The installer verified every installed package member byte for byte and can restore the prior package files if installation itself fails.

The existing West, East, NorthWest and NorthEast saves were reused. The shared calendar reloaded at 42007; its enabled calendar-only startup test advanced it to 42019 and confirmed persistence while simulation clocks remained frozen. No quest/save migration was performed.

## Server checks

- Loaded `SevenDaysBeyond (0.6.0)` and its native localization table.
- `UPSTREAM_CONTRACT_OK hooks=12` and `BORDER_TRAVEL_ENABLED gridCells=4`.
- `MULTIWORLD_READY: count=4; West, East, NorthWest, NorthEast`.
- `QUEST_BOOK_NATIVE_SELF_TEST_PASS` and `QUEST_BOOK_ENABLED`.
- All four `BORDER_SAFETY_PROBE` results were `safe=True`.
- `SUSPENSION_SELF_TEST_PASS`: empty-world freeze, independent wake and refreeze.
- `SHARED_CLOCK_ADVANCE_PASS` and `SHARED_CLOCK_SELF_TEST_PASS`: common serialized time, paused empty server and persisted reload.
- Service active, MainPID 104402 at verification, UDP 26900 listening.

The code's pinned-SDK build and all 12 automated tests passed before deployment. This startup verification does not establish client packet-mapping, simultaneous per-player languages, UI layout or transfer regressions. After installing the matching client package, run `/sdb status`, `/sdb map`, `/sdb quests`, and the [localization/client checks](localization.md#verification).
