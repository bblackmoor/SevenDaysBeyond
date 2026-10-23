# Empty-world suspension prototype (companion 0.3.0)

## Starting point

The user confirmed automatic on-foot travel in both directions, preservation of items collected on a map, correct world/location/inventory after reconnect, and persistence after a controlled game-server restart. Two-player opposite-direction crossings remain **untested**; the user chose to proceed without that test. Passing single-player tests does not establish concurrent-player correctness.

## Wake and sleep rules

With `freezeEmptyWorlds="true"` in the server's `BoundaryTest.xml`, each loaded world stays awake when it has player entities, an authenticated client joining/loading there, an upstream transfer using it as either source or destination, or a companion preparation/landing probe reservation. Source worlds remain reserved during rollback as well as forward travel. After the last reason disappears, a ten-second grace period allows normal save, entity cleanup and queued work to drain before suspension.

The next server frame sees new join or reservation reasons. Both sides can be awake independently. The eligibility snapshot is published for component callbacks without mutating that snapshot afterward. Unknown or not-yet-initialized worlds retain upstream behavior.

The optional `suspensionSelfTest="true"` runs an idle-server startup test. Keep it confined to this disposable test server. A client joining cancels the test and removes its transient fixtures and reservations.

## Simulation and global services

The original scheduler still iterates the worlds, maintains scoped services, and handles its own failure logic. A checked Harmony transpiler adds one gate to the actual game `gmUpdate`, immediately before the first world-owned service (`LockManager.Instance`). A missing or changed anchor prevents installation. All suspension hooks install only after the upstream worlds-ready flag is set, keeping world construction and its isolation checks unchanged. The original global prologue still runs, including pause handling, global actions, loading/assets, platform and invitations. If the first world sleeps, the gate explicitly pumps the later `ThreadManager.UpdateMainThreadTasks` call before returning. The upstream once-per-frame guard keeps that call from repeating in later worlds. The global UnityUpdate event and other retained prologue behavior follow upstream ordering.

For a sleeping world, the remaining world core is skipped: AI/entities, block ticks/falling blocks, water updates, world events, power, vehicles, drones, quests and other services driven by that core. A separate prefix gates the upstream isolated physics simulator. Native water is paused while sleeping, preserving its previous pause setting for wake. Additional prefixes gate the same Unity callback targets that upstream scopes: world-owned Update/FixedUpdate/LateUpdate and collision/trigger callbacks stop in suspended worlds; global components without a known world owner or scene continue. The upstream lease prefix and cleanup are left intact.

Time rebasing preserves tick counters and calendar time, clears elapsed/fractional deltas and physics accumulation, and updates the scoped `GameManager::lastTime`. This avoids injecting elapsed idle wall time into native GameTimer, physics, or World.OnUpdateTick when waking. It does not advance the world's calendar to another map's clock. Companion 0.4.0 adds a separate [shared calendar](shared-clock.md) without advancing these native simulation clocks; sleeping maps do not deliberately simulate missed hordes.

Chunk generation, lighting, pathfinding and disk workers are not abruptly suspended or terminated. Already-queued jobs may drain, and chunk preparation remains possible while a reserved world is awake. This is suspension of the audited gameplay entry points, not a blanket operating-system thread freeze or an assertion about every third-party coroutine. Coroutine behavior from additional mods needs review before adding them. Maps remain initialized in memory; this does not establish that 64 worlds fit in ANNWN's RAM.

Errors in the companion monitor disable suspension and restore upstream scheduling and water pause state. Border transfer compatibility checks remain required. This fallback favors keeping clients and recovery running over silently claiming a frozen world.

## Validation

The production geometry, landing-ground rules and wake policy pass 988 assertions. The companion compiles with warnings treated as errors against the pinned game framework. Existing upstream contract checks remain 12/12, with all 15 direct dependencies resolved.

The server test creates one temporary, unsaved Rigidbody in each actual isolated world scene and checks:

1. Both worlds sleep with unchanged native ticks, calendar time, world-core/physics counts and body positions; the global main-thread task pump continues.
2. A reservation wakes West: its core and physics counters advance and its body falls, while East remains frozen.
3. Releasing the reservation lets West sleep again after grace; both bodies and clocks remain stable.
4. Fixtures and test reservations are removed. A client joining at any stage cancels and cleans up the test instead.

These checks exercise real game and Unity code, but do not replace a client join/crossing regression test on the new companion. Log lines `WORLD_SUSPENDED`, `WORLD_RESUMED`, `WORLD_ACTIVITY` and `SUSPENSION_SELF_TEST_PASS` distinguish observed behavior from expectations. `/sdb status` reports both worlds' current sleep states to the connected player.

## Deployment and rollback

Before updating, ANNWN's game service stopped cleanly. The previous companion, isolated saves, configuration and log were archived at `/srv/seven-days-beyond/backups/before-companion-update-20261021T213541575057Z.tar.gz`. The new package is `/srv/seven-days-beyond/tools/SevenDaysBeyond-Suspension-Server.zip`.

To disable just suspension, stop the service cleanly, set `freezeEmptyWorlds="false"` in `server/Mods/SevenDaysBeyond/BoundaryTest.xml`, and restart. Border travel remains enabled. To restore 0.2.0 exactly, stop cleanly and restore the prior companion directory from that backup; restoring save data is unnecessary for ordinary code rollback. Keep the backup.

## Observed ANNWN result (2026-10-21)

The final build installs its hooks from a Unity coroutine after worlds are ready, outside the active game-update call stack. It reported one validated core gate and 278 component gates. Both original map-isolation and landing probes passed.

At 17:49:18 America/New_York, both maps suspended. Their native ticks/calendar values remained East `34186/17275` and West `17909/12385` throughout the test. `SUSPENSION_ALL_EMPTY_PASS` confirmed unchanged clocks, gameplay/physics counts and rigid bodies while the global task pump continued. A reservation resumed West at 17:49:20; `SUSPENSION_RESERVATION_WAKE_PASS` confirmed its simulation and falling body while East stayed frozen. West suspended again at 17:49:32, and `SUSPENSION_SELF_TEST_PASS` at 17:49:35 confirmed release/refreeze and fixture cleanup. The activity counters also recorded suppressed owned component callbacks.

Final companion SHA256: `53ceec552524193d05e8e2834a8e324dfdb2a9150fb258f4fcc7708f1300e989`. Final package SHA256: `936ded9195965e9edf7bd44346141da8802437893fb638d79b0b0d4560d0e1fd`.

The user subsequently reported that the 0.3.0 single-player join/crossing regression test passed. Concurrent players remain untested. Companion 0.4.0 retains these suspension gates and adds a separate [shared calendar](shared-clock.md); its client clock check is pending.
