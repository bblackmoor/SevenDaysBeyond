# Four-map grid and character quest book (0.5.0)

## Test layout

| Map | Grid cell | Game map | Size |
| --- | --- | --- | --- |
| West | (0, 0) | Navezgane | 6144 m |
| East | (1, 0) | Pregen06k01 | 6144 m |
| NorthWest | (0, 1) | Pregen06k02 | 6144 m |
| NorthEast | (1, 1) | Pregen08k01 | 8192 m |

West/East keep their existing map/save identities. The two northern maps use new isolated saves. Positive grid Z is north. Cardinal neighbors connect; outer edges are closed. North/south arrivals preserve longitude proportionally, just as east/west arrivals preserve latitude. The different northeast map size deliberately exercises unequal map extents. The warning distances remain 50 m and 10 m before the travel line, with the same conservative outer-terrain margins, ground checks and on-foot restrictions.

`/sdb map` lists cells and links. `/sdb approach east`, `west`, `north` or `south` prepares a safe test point near that side; the player still walks across the line. Without a direction, it selects a linked side. An outer-side approach is useful for testing closed-edge retreat. The test convenience command remains opt-in.

All four maps remain loaded in memory, and empty maps suspend. Startup suspension and serialized-calendar checks now cover every initialized map, with one reserved map waking independently. This is a four-map load test, not a demonstrated capacity for 64 maps or map unloading.

## Quest book

The server stores one character-owned book containing a distinct serialized native quest journal for each map. The map name qualifies its quest IDs, positions, trader data and progress. A fresh source character checkpoint updates that map's page; loading a destination restores its own page. Existing map journals are imported when first encountered. The native journal window displays the current map's quests; `/sdb quests` lists saved quest summaries, objective counters and map-qualified target coordinates across the book.

This keeps map-bound objectives inactive while away without treating another map's same coordinates or trader/entity ID as a destination. Returning restores the native journal and the game's normal hooks. Normal game POI rally-reset and party/shared-quest rules still apply; this build does not promise preservation of an actively running POI encounter across travel. It does not merge trader locations or execute off-map objectives. Vanilla challenge-journal behavior remains the upstream shared-character behavior.

Snapshots use the game's real QuestJournal.Write/Read format. The character ID is checked inside the XML envelope and hashed for the filename. Files are atomically replaced with a prior-version backup under the common `Saves/SevenDaysBeyond/QuestBooks` directory. Existing native map saves remain in place. Errors disable quest-book overrides and log the cause, leaving the original native journal behavior available. A summary reflects the latest character checkpoint; the transfer handshake requests a fresh checkpoint before detaching.

The all-map book and travel fixture are the first portable-journal implementation. A merged native all-map UI and generic conversion of third-party quest deadlines are outside this prototype.

## Repeatable travel/deadline test

Use chat commands:

1. `/sdb questtest start 300 NorthWest` creates a five-minute-at-current-game-rate test: visit NorthWest, then return to the starting map. Choose a different configured target if starting there. With no target argument, the first linked neighbor is selected.
2. `/sdb questtest status` shows the target map, completed legs and shared-calendar ticks remaining. Other maps along the route do not count as the target. Both manual `/world NAME` and automatic crossings can exercise this test.
3. Reach the named target, confirm one completed leg, then return home and confirm `complete` with two legs.
4. Start another test, reach its target, log out and reconnect. The route, partial progress and deadline should remain. When everyone is out, the shared calendar pauses; downtime does not add elapsed time.
5. `/sdb questtest start 30` creates a short expiry test. Stay connected without completing the route. It must report `expired`, and arriving afterward must not complete it. Cancel an existing active test first with `/sdb questtest cancel`.

The budget is converted from seconds to game-calendar ticks using the current rate. Changing that rate changes how quickly the remaining budget elapses. These fixtures award no items or XP and never remove carried items. They are server test records, not native quests in the L window. Starting/cancelling a fixture affects only the requesting character.

## Native quest progress test

The package adds `sdb_test_fieldwork`, a stock EntityKill quest with three zombie kills and no rewards. Its XML is distributed through the game's normal server configuration exchange, so the existing client DLL can remain installed.

1. On West, press F1 and run `givequest sdb_test_fieldwork`. This uses the administrator access already configured. Close the console and check the native quest journal.
2. Kill one zombie on West and check native progress is 1/3. God Mode may remain on. With debug mode enabled, the game's F6 spawn menu can provide a zombie for this test; the companion does not automatically spawn enemies.
3. Travel to East. Run `/sdb quests`: West's saved page should retain the partial quest. Kill a zombie on East; the West page must not gain credit from it.
4. Return to West. The native quest should still have 1/3 progress. Kill two more zombies and check completion.
5. Repeat with a disconnect/reconnect and, separately, a controlled server restart while partial progress is saved. Cancel/remove the test entry in the native journal before granting another copy if necessary.

No trader hunting or suitable random quest is required. This test exercises the real game quest serializer and client journal; it does not validate every vanilla POI type or party interaction.

## Grid crossing test

Carry recognizable items and use God Mode if desired. Starting on West:

1. `/sdb approach east`, walk east to East.
2. `/sdb approach north`, walk north to NorthEast.
3. `/sdb approach west`, walk west to NorthWest.
4. `/sdb approach south`, walk south to West.

At each stop, check `/world`, `/sdb status`, inventory, arrival ground and `/sdb time` against the HUD. Reverse the circuit to exercise all eight directed links. Test an outer edge separately. Concurrent opposite-direction players remain untested.

## Verification and deployment

Production routing/landing/suspension rules pass 3,828 assertions, quest-book map identity/progress/deadline/backup rules pass 19, and the existing calendar rules pass 87. The pinned game build passes 12 upstream hooks and 15 direct references with warnings treated as errors. Startup additionally creates an unsaved native quest with partial progress and serializes/deserializes it without changing a player.

The whole server package contains the companion DLL, metadata, boundary/grid config, native test-quest XML and upstream four-map configuration. The deployment helper refuses a running service, verifies exact archive membership and the original upstream DLL, checks old West/East identities and properties, and backs up saves, both mod directories, config and log before replacement. It does not replace the upstream DLL or delete existing maps/saves.

Runtime readiness, memory observations and client test outcomes must be recorded after deployment. For code rollback, stop cleanly and restore both mod directories and any changed server configuration from the new backup; reverting player save data is a separate decision and is unnecessary for ordinary code rollback.
