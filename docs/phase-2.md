# Phase 2: two-world border travel

## Scope and baseline

The user confirmed repeated West/East manual transfers, inventory preservation, return locations, reconnect persistence, and persistence after a controlled game-server restart on 2026-10-21. Game V3.3.0 (b18), unchanged LITW MultiWorld 0.6.2, and isolated `data-multiworld` saves remain the baseline.

Companion 0.2.0 introduces the first server-only on-foot border prototype. It does not require a new client DLL. The original MultiWorld client installation is still required.

- West's east edge connects to East's west edge; the reverse route connects East back to West.
- Bounds come from each actual world's chunk provider. Latitude is mapped proportionally between maps.
- Warn 700 m inside the map extent, trigger 500 m inside, arrive 600 m inside. These deliberately conservative test margins avoid the outer danger zone. They are not final map seams or matching roads.
- Closed edges warn and attempt an inland retreat. Mounted players receive a warning and cannot cross; vehicles are not moved or transferred by the companion.
- Before preparation, retreat to checked, loaded ground in the source world. Preload the destination with a temporary chunk observer and search within 32 m of the intended arrival. Abort after 30 seconds if no safe surface is available. Moving more than 16 m from the holding point cancels preparation.
- Require present chunks without pending decoration or decoration/regeneration/unload in progress, a 3x3 terrain platform, negative solid density, three clear blocks of headroom, no water or damaging/buffing ground, no radiation, and no trader area. This conservatively rejects some otherwise usable slopes and roads. It is not a guarantee against nearby enemies or later world changes.
- Recheck the same ground immediately before upstream detachment. If it changed, cancel while still in the source world.
- Reuse the existing fresh-checkpoint/token/epoch/cooldown/timeout/rollback protocol. The arrival override is bound to the exact upstream request object, player ID, and destination. Rollback and manual `/world` travel retain upstream saved-position behavior.
- Arrival is written into destination player data before spawn, rather than teleporting across worlds after spawning. A 20-second companion cooldown prevents rapid repeat attempts.

The companion monitors a postfix of the pinned `UpdateBothWorlds` method at most four times per second. It does not change the upstream scheduler, empty-world simulation, quests, or clocks. Runtime access to private upstream state is centralized in `MultiWorldBridge`; missing members or incompatible patches prevent activation. Unhandled monitoring errors disable further automatic starts and log `BORDER_TRAVEL_DISABLED`; already-started upstream recovery remains in control.

## Opt-in configuration

Copy `config/BoundaryTest.xml` beside the server companion DLL. Without the file, automatic travel stays disabled. `phase2="true"` enables this disposable two-world prototype. `allowApproachCommand="true"` enables the test convenience command for connected players; disable it before wider use. It is intentionally not a general player travel feature.

The companion's startup safety probes preload and inspect one representative arrival area per world and reject the exact outer edge. Probe observers are released on completion. A successful representative probe does not validate every latitude.

## Client test

1. Connect to `192.168.0.90:26900`, without EAC, using the existing client mod.
2. Carry recognizable items. Enter `/world` to identify the current map.
3. Enter `/sdb approach`. Wait for the preparation message and relocation to checked ground about 560 m inside the linked border. The search may offset this by up to 32 m.
4. If in East, walk **west**; if in West, walk **east**. Walk normally. A warning should appear, then the server should retreat you briefly while checking the other side and start the normal loading sequence automatically. Stay nearby during the ground check.
5. After arrival, enter `/world`, verify the opposite map and inventory, and check that the arrival is near the corresponding border. `/sdb status` reports the current world and nearest border distance in English.
6. Wait at least 20 seconds, then use `/sdb approach` and walk back. Repeat inventory and location checks.
7. Check reconnect and restart persistence again after successful automatic crossings. Separately test closed edges, cancellation, rejection of unsafe terrain, and two players crossing in opposite directions. Those cases remain unverified until exercised.

Automatic crossings intentionally override the destination's previous saved location with the border arrival. Manual `/world` commands still return to that world's saved location.

## Verification and deployment

The game-framework build treats compiler warnings as errors and pins the game's netstandard facade as well as the upstream direct dependencies. All seven tests pass, including 979 assertions exercising production route geometry and ground rejection rules. These are not substitutes for client transfer testing.

`tools/deploy-border-test.py` accepts a whole ZIP with the companion DLL, ModInfo, and BoundaryTest.xml plus its expected SHA256. It requires the exact test service to be inactive, verifies the original upstream hash and package membership, backs up `data-multiworld`, configuration, old companion, and log, then replaces only the companion files. It does not alter saves or start the service.

Before this deployment, the server stopped cleanly. The backup is `/srv/seven-days-beyond/backups/before-border-travel-20261021T205417712861Z.tar.gz`.

Runtime startup and representative landing probe results are recorded below after deployment. Real automatic player crossings remain pending.

## Rollback

Stop the game service cleanly. Move `server/Mods/SevenDaysBeyond/BoundaryTest.xml` outside that mod directory, then restart: the companion will validate compatibility but install no border patches, leaving manual MultiWorld travel available. To restore the prior companion exactly, extract only `server/Mods/SevenDaysBeyond` from the backup and ensure the new opt-in file is moved away first. Restore save data only if specifically needed; normal code rollback does not require reverting player progress.

The first runtime probes caught an overly strict readiness check: `Chunk.IsInitialized` waits for `NeedsLightCalculation` to clear, which does not happen for these nonvisual headless observers. The companion checks terrain lifecycle flags instead. Lighting readiness is not terrain availability, and all terrain, water, headroom, density and bounds checks remain required.

## Deployed result (2026-10-21)

The corrected companion loaded on ANNWN and reported `UPSTREAM_CONTRACT_OK hooks=12` and `BORDER_TRAVEL_ENABLED`. Both worlds reached `MULTIWORLD_READY`. Actual extents for each map were `(-3072,0,-3072)` to `(3072,255,3072)`. The safety probes passed at West `(2468.50,95.10,-7.50)` and East `(-2471.50,43.10,0.50)`. The service remained active, with zero automatic restarts and roughly 3 GB systemd-accounted memory at the check. No managed exception or companion initialization failure was observed.

Deployed companion SHA256: `28ae2b0746232d6fee83a3bc3d16bf9b11adec5e72458d35d68e69ec5ffde2a6`. Final whole-package SHA256: `e5a6d99878410ac6f01ecbfbe2e2ab7e094c11a98e3671aa14c8c32a22bc131f`. The package is retained at `/srv/seven-days-beyond/tools/SevenDaysBeyond-Phase2-Server.zip`. No real client automatic crossing has yet been observed; the next step is the client test above.

## Client results and next prototype

The user confirmed automatic crossings in both directions with newly collected items preserved, followed by successful reconnect and game-server restart persistence checks. Two-player crossings remain untested by explicit user choice. The subsequent [empty-world suspension prototype](world-suspension.md) uses companion 0.3.0 and requires a new client regression check despite retaining the same client package.
