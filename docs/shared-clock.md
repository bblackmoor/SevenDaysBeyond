# Shared calendar prototype (companion 0.4.0)

## Behavior

West and East now use one server calendar for client time packets, daylight, darkness, trader opening hours, timer relays, audited day/time conditions, blood-moon calendar decisions, and selected quest deadline/reset decisions. It advances once per server frame at the configured game time rate while an authenticated player is connected, including loading and transfers. It pauses when everyone is logged out and does not catch up from real-world time after downtime.

Each map retains its own native simulation timestamps and GameTimer. Empty maps still suspend their gameplay, physics, water and owned component callbacks. Advancing the shared calendar does not age their crops, loot timers or other native simulation timestamps, or run skipped world updates. On wake, the blood-moon component recalculates against the current calendar rather than replaying every missed day. A horde on the current night can still activate normally; skipped-day and actual horde behavior need a client test.

This is a checked adaptation of 28 specific game methods, plus existing join/time packet setup and server time reporting. It is not a replacement of every use of `World.worldTime`. Native timestamps, wandering-horde simulation, saves and untargeted consumers remain local. Additional mods, legacy timer displays and administrative time-setting commands require a separate audit. Portable quest journals and the full map grid are still future work; the shared `Expired` helper supplies a calendar deadline boundary for that later work.

## Configuration and persistence

Enable `sharedClock="true"` and `freezeEmptyWorlds="true"` in the server companion's `BoundaryTest.xml`. Missing or failed suspension prevents clock installation. Hooks install from a Unity coroutine after world initialization, outside the active game-update stack. Expected calendar read counts are validated while installing; changed game methods reject installation rather than silently receiving partial conversions.

The initial calendar starts at the latest native map time. Thereafter it loads the checkpoint, using the latest native time as a lower bound to avoid moving backward. On ANNWN the checkpoint is:

`/srv/seven-days-beyond/data-multiworld/Saves/SevenDaysBeyond/SharedCalendar.xml`

The schema stores integer ticks and fractional carry with invariant formatting. Saves run every ten seconds and on clean shutdown, using a flushed temporary file and atomic replacement with a `.bak` previous version. A crash can lose progress since the last checkpoint. Invalid XML/checkpoint data rejects initialization. Runtime errors disable shared-calendar behavior and log the reason; native map clocks remain available.

`/sdb time` reports the shared day/time and whether it is running. `/sdb status` includes the same report alongside map suspension state. Existing client time packets carry the common value, so this release needs no new client DLL.

The opt-in `clockSelfTest="true"` is only for this isolated test server. It waits for both worlds to suspend, briefly advances only the calendar for two seconds, verifies both actual time packets, saves and reloads the checkpoint, then verifies that an empty server pauses again. A joining client cancels the test. The test deliberately advances the calendar slightly even with no players.

## Verification

Production calendar tests passed 87 assertions covering fractional carry, occupied/empty pause, a single advancement independent of map count, deadline boundaries, persistence and backups, German-culture round trips, overflow/nonfinite rejection, and prohibited XML DTDs. The companion builds with warnings treated as errors against the pinned game assemblies; upstream checks remain 12/12 and direct dependencies 15/15. Existing border and suspension policy tests cover 988 assertions.

ANNWN loaded 0.4.0 on 2026-10-21. At 18:10:30 America/New_York it logged `SHARED_CLOCK_ENABLED`, calendar `17799`, and 28 calendar methods. Map isolation and representative landing probes passed. Suspension tests passed again at 18:10:57, including independent reservation wake and release/refreeze.

At 18:10:59, `SHARED_CLOCK_ADVANCE_PASS` confirmed common time advanced from `17799` to `17811` while both native simulation clocks, game ticks, core and physics counts remained frozen. Actual time packets from both world scopes matched the shared calendar, and the checkpoint round trip passed. At 18:11:01, `SHARED_CLOCK_SELF_TEST_PASS` confirmed subsequent empty-server pause. Through 18:12:45, common time stayed `17811`, East's native time stayed `17799`, and West's stayed `13275`; global task pumping continued. No shared-clock disable or installation failure was observed.

The user subsequently reported that time appeared to work correctly during the 0.4.0 client crossing test. The user also passed the earlier 0.3.0 single-player regression. Two-player crossings, skipped calendar days/hordes, and an actual restart of 0.4.0 remain untested.

## Client check

1. Join the existing ANNWN server with the existing client mod. Run `/sdb time` and compare it with the HUD clock.
2. Cross from West to East or East to West using the previously tested route. Run `/sdb time` again. Time should advance normally through loading rather than jump to the destination map's old time. Check the HUD, world and inventory.
3. Return and repeat. After everyone disconnects, server logs should show a paused calendar and both maps suspended after their grace periods.

## Deployment and rollback

ANNWN stopped cleanly before deployment. The known working 0.3.0 companion, saves, configuration and log are backed up at:

`/srv/seven-days-beyond/backups/before-companion-update-20261021T220859559056Z.tar.gz`

The deployed package is `/srv/seven-days-beyond/tools/SevenDaysBeyond-Calendar-Server.zip`, SHA256 `f693ab5e3c27d2e8048f43f3b80b6b8f97a1b3bcf57efcd1f108758415e36cc4`. Companion DLL SHA256: `4e9419ae5e111581d36cf8e752867f21a7f72582ab3264c315a9759eb963eaca`. The upstream mod and client package are unchanged.

To disable only the calendar, stop the service cleanly, set `sharedClock="false"`, and restart. Native map calendars then resume their local behavior; suspension and border travel retain their separate settings. To restore 0.3.0 exactly, stop cleanly and restore only its companion directory from the backup. Save rollback is unnecessary for ordinary code rollback. Keep the shared checkpoint if the calendar will be enabled again.

## Client mismatch and 0.4.2 correction

A later screenshot superseded the initial positive client-clock report: after East-to-West travel, the West HUD showed day 2 at 03:11 while `/sdb time` reported day 2 at 12:06. Server logs showed the common calendar continuing through the switch, separately from native West/East simulation time. This is a failed client synchronization check, not a passed calendar/HUD test.

The old self-test inspected the result of packet Setup, without testing actual serialized bytes or reception after local world loading. The stock client loads a map's local world time; the `ticks` argument of GameManager.WorldInfo is not used to set the client calendar. A Setup-only patch is therefore insufficient evidence of end-to-end synchronization, including callers already compiled before runtime patch installation.

Companion 0.4.2 enforces the common calendar at NetPackageWorldTime.write, checking the single audited field read, and sends the stock time packet directly to each authenticated, attached client after spawn, after a successful transfer result, and once per second outside active transfers. The wire hook only selects the serialized UInt64; it does not modify a map's native worldTime or the packet's cached field. Existing session envelopes supply the destination epoch. Server-browser argument replacement runs after the upstream browser prefix. The obsolete join-ticks override is removed.

The extended startup test deliberately puts a native map time into a real packet after Setup, writes it through the actual game serializer, and verifies the package header and common UInt64 payload while both native worlds remain frozen. It must pass on the server before claiming runtime verification. `wirePackets` in the shared-clock log distinguishes actual serialization from packet construction. The stale 700 m border startup banner is corrected in this build as well.

The build passed 12 upstream hooks and 15 direct dependencies with warnings treated as errors; the 87 production calendar assertions passed. Server package `SevenDaysBeyond-Server-0.4.2.zip` SHA256: `9251fda83f3340793b5d80e2fb5ec67210f955b63c2860ea4913569240ebf94e`. Gateway cleared the package check, and 0.4.2 was installed after a clean stop while everyone was logged out. The prior 0.4.1 companion and saves are backed up at `/srv/seven-days-beyond/backups/before-companion-update-20261022T020535638152Z.tar.gz`. Installed DLL SHA256: `ac5ff95012915b127c7accdf24090c262848476dd75383c4fa74a2729c05e344`. The service restarted successfully; runtime serialization test results are recorded below once available. The working 0.4.1 God Mode client update does not need replacement for this server clock correction.

At 22:07:30 America/New_York, the 0.4.2 suspension test passed. At 22:07:34, `SHARED_CLOCK_SELF_TEST_PASS` explicitly confirmed equal serialized packet bytes, independent calendar advancement, frozen native worlds, empty-server pause and checkpoint reload. The test advanced the common calendar `37860 -> 37873`; East stayed at native `20475` and West at `28896`. `wirePackets=4` recorded the real serializer invocations, including packets whose cached fields were deliberately set to the old native times. Both landing probes passed and the new 50 m/10 m startup banner was correct. No clock-disable or installation error was observed. Actual HUD reception and a live crossing remain pending the next client test.
