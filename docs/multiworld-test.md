# Two-world test on ANNWN

## Current state

The unmodified baseline client connection and normal disconnect succeeded. The baseline server then stopped cleanly with exit status 0. Its data, configuration and log were archived at `/srv/seven-days-beyond/backups/baseline-before-multiworld-20261021T200232Z.tar.gz`.

The active `seven-days-beyond.service` now uses:

- Server configuration: `/srv/seven-days-beyond/config/multiworld.xml`
- Separate user data: `/srv/seven-days-beyond/data-multiworld`
- Log: `/srv/seven-days-beyond/logs/multiworld.log`
- Unmodified LITW MultiWorld 0.6.2 and SevenDaysBeyond 0.2.0 border companion in the server's `Mods` directory

| World command | Map | Disposable save |
| --- | --- | --- |
| `/world West` | Navezgane | SDB_Test_West |
| `/world East` | Pregen06k01 | SDB_Test_East |

The mod configuration is maintained in `config/multiworld-test.xml`. It uses runtime mode, two worlds, a separate storage ID, shared character progression, and West for first arrival. The opt-in [Phase 2 companion](phase-2.md) now links West's east border to East's west border.

## Server verification

- Both mod assemblies loaded and initialized successfully on game V 3.3.0 (b18).
- The companion reported `UPSTREAM_CONTRACT_OK hooks=12` against the actual running game.
- MultiWorld reported `SAVE_ROUTE_ISOLATED=True`.
- Startup completed with `MULTIWORLD_READY: count=2; West, East; /world NAME enabled.`
- No probe failure, scheduler failure, or managed exception was observed during startup.
- The service remained active without restarting. Initial idle memory use was approximately 4 GB; this does not predict 64-world capacity.

The existing LAN firewall rules and port 26900 are retained. The baseline save is preserved separately; connecting to the two-world test starts a new character in new saves. Empty-world calendar advancement is disabled by MultiWorld's existing setting, but full simulation suspension is not implemented by this setting.

## Client installation and manual transfer test

The private client test ZIP contains the unmodified upstream DLL, its ModInfo, the two-world configuration, and installation instructions. The diagnostic companion is server-only for this test.

1. Exit the game. In Steam, use the game's Installed Files > Browse action.
2. Create or open the game's `Mods` directory. Extract `LITW_MultiWorld` into it, so `Mods/LITW_MultiWorld/ModInfo.xml` and the DLL are directly beside each other. Preserve the game's shipped `0_TFP_Harmony` mod.
3. Use the game launcher to start without Easy Anti-Cheat, using V 3.3.0 (b18).
4. Connect to ANNWN's LAN IP on port 26900. First arrival should be West.
5. Carry a few recognizable items. Enter `/world East` in chat and wait for loading to finish.
6. After at least ten seconds, enter `/world West`. Confirm inventory, character progression, and the saved West location.
7. Disconnect normally, reconnect, and check inventory and world selection. Tell the assistant when ready for a controlled server restart and another check.

The user confirmed repeated manual transfers, inventory preservation, return locations, reconnect persistence, and controlled server-restart persistence. Automatic on-foot travel is now installed for the next client test; follow [Phase 2 instructions](phase-2.md). The upstream mod still blocks mounted transfers.

## Rollback to baseline

Stop the service cleanly. Move the two added mod directories out of the game's `Mods` directory without deleting them, restore `/srv/seven-days-beyond/config/seven-days-beyond.baseline.service` to `/etc/systemd/system/seven-days-beyond.service`, reload systemd, and start it. This returns to `baseline.xml`, the original data directory, and the baseline log. Keep both save directories and the backup archive.

Do not restore the baseline unit while leaving MultiWorld enabled: its primary-world patch intentionally overrides the base game's configured map/save.

## Deployment helper

`tools/configure-multiworld-test.py` prepares the test only while the service is inactive. It checks the package file set, verifies the upstream DLL hash, refuses existing mod/test files, archives the baseline, and writes a separate configuration and service definition. Activating that service definition is a separate administrative step.
