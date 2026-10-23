# Multiworld independent replacement specification

Analysis date: 2026-10-23. Inspected repository snapshot: `61b0e10cfd935d96f0f227d5591acb697962523b`.

This documentation describes LITW MultiWorld 0.6.2 and the responsibilities an independently written replacement must assume. It preserves behavioral findings, game integration surfaces, data contracts, failure cases, and an implementation/verification plan. It contains no reproduced implementation bodies, decompiler project, IL, binary, or archive. Keep this directory when removing `resources/multiworld/`.

Read these documents in order:

1. This architecture and behavior specification.
2. [Transfer, network, and storage contracts](contracts.md).
3. [Replacement design, migration, and acceptance tests](implementation-plan.md).
4. [Inspection inventory](inspection-inventory.md), a traceability aid with source identifiers and static coverage limits.

## Evidence and intended use

**Observed** means found in the inspected reconstruction or current companion source. **Recorded test** means reported by existing project documents; no new game session or server test was run for this documentation. **Required** means a proposed contract for our independent replacement. **Open** means further engine investigation or live testing is necessary. These categories must not be collapsed when making release claims.

The inspected binary SHA-256 is `946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6`. Its supplied version is 0.6.2. No original source or debugging symbols were supplied. The reconstruction contains unresolved type expressions and three undecoded patch attributes. Names in the inventory identify evidence, not a class layout to reproduce. This is source-informed reverse-engineering documentation, not a claim that a formal clean-room process has occurred. An independent implementation should use its own organization and code and should verify engine behavior against the matching game assemblies.

The baseline dependency lock identifies Steam app `294420`, depot `294422`, Linux manifest `3031329678505041777`; `Assembly-CSharp.dll` SHA-256 is `e16ff9d539b8fbf64904c0bc090d502286a62464742a9396f0455936ad0beb87`. This is a pinned analysis target, not a claim about the newest game release. Harmony, Unity, A* pathfinding, Mono.Cecil and the game's managed assemblies participate in the inspected runtime. Keep the existing dependency lock for exact acquisition provenance, then remove its upstream-binary requirement when the replacement no longer needs that DLL.

## What Multiworld actually provides

It hosts multiple native `World` objects within one dedicated server process and one authenticated client connection. Maps can occupy identical local coordinates. They are not adjacent terrain meshes, separate OS processes, or separate server connections. A client unloads its current local world and loads another while keeping its network transport and authentication.

| Responsibility | Multiworld 0.6.2 behavior | Seven Days Beyond at the inspected snapshot |
| --- | --- | --- |
| World catalogue | 1–64 configured worlds; initialize all | Four configured test maps; eventual 8 × 8 goal |
| World isolation | Routes native global state into the owning world | Depends on Multiworld |
| Travel | `/world NAME`, on foot; previous destination position or spawn | Grid adjacency, border warnings, prepared safe edge arrival |
| Character | Shared character checkpoint plus destination-local fields | Depends on checkpoint/restore hooks |
| Active quests | Journal remains destination-local | Character quest book stores map-qualified native journal pages |
| Trader progression | Optional overlay of other maps' faction points | Still depends on upstream behavior |
| Empty maps | Calendar/native timer occupancy rules; scheduler still visits worlds | Audited gameplay, physics, water and component suspension |
| Calendar | Separate native world clocks | Shared server calendar, audited consumers and serialized client time |
| Client experience | Retained login, map download, load cancellation and caches | Localization and preservation of session debug modes |
| Parties/allies | Global logical membership, local party projections, shared allies | Depends on Multiworld |
| Vehicles | World-local vehicle services and diagnostic fixtures; mounted travel denied | Vehicle/passenger transfer remains future work |

A replacement that only implements travel and inventory will leave major missing behavior. Isolation, world ownership, safe networking, save routing, and client lifecycle form the foundation.

## World identity and ownership

Maintain distinct concepts for catalogue ID, display name, generated map name, save name/path, native world GUID, Unity scene, and live world object. Two saves may use the same map; the map/save pair must be unique. Display names are case-insensitive in the upstream catalogue. Entity IDs are allocated across loaded worlds without rolling the allocation counter backward during secondary initialization; coordinates alone cannot identify an entity or location.

Each live world owns its native `World`, chunk cluster and chunk manager, entities and entity-parent roots, physics scene and accumulator, native timer, water simulator, navigation manager/pathfinder, weather, loot and trader services, and mutable service state. It also has initialized/closed flags. A separate registry maps authenticated connections to their current world and records in-progress transfers.

Three independent ownership paths matter:

- Main-thread engine calls select a world context and restore the caller's context even on exceptions.
- Background work carries the owning world in thread-local scope; it must not rewrite another thread's active game world.
- Coroutines re-enter the correct context on every continuation, nested enumerator, and disposal. Selecting a world only before starting a coroutine is insufficient.

Ownership may be determined from an explicit world, an entity's world, a registered service, a chunk manager/provider, a navigation graph, or the component's Unity scene. An unknown owner must be handled explicitly; silently attaching it to whichever map ran last risks corruption. Some upstream paths deliberately fall back to native behavior before initialization. The replacement must distinguish initialization fallback from normal gameplay routing.

## Mutable engine state to isolate

The following is the engine surface observed in this version, not a complete promise for later versions or third-party mods.

| Surface | State/behavior requiring world ownership |
| --- | --- |
| Core lookup | `GameManager.World`, direct `m_World` reads, world/save/mode string preferences, `GameIO.GetSaveGameDir()` |
| Environment | Native water singleton, `GameTimer.Instance`, weather manager plus mutable world time/random/current-weather/storm/load state |
| Entities | EntityFactory parent map, correct scene during entity construction, correct root for chunk objects |
| Navigation | AstarManager and PathFinderThread instances, graph ownership, nearest-node constraints, traversal providers, graph-update restrictions |
| Gameplay services | PowerManager, VehicleManager, DroneManager, DecoManager, LockManager, QuestEventManager, TurretTracker, RaycastPathManager, TokenManager, BlockLimitTracker, GameEventManager, GameRandomManager, PartyManager, MultiBlockManager |
| Player/persistence | Per-world PersistentPlayerList and game-state manager, local spawn/waypoint/quest/owned-entity data |
| Game stats/events | GameStats property values; cloned mutable game-event sequences; vehicle/drone counts |
| GameManager work state | Update/deco/time-sync/game-state timestamps, elapsed milliseconds, countdowns, last player count, pool-clear timer, explosion work, falling groups, block particle lists, positional sound map, music cleanup list, prefab LOD manager |
| Stability | World reference and checked/pending/next/placed positions; owning-world scope for calculation |
| Native map transmission | Compressed map chunks, cached world name, preparation coroutine and send delay |
| Sky | All nonconstant writable static SkyManager fields, including root, sun/moon axes, lights and clock |

Upstream uses a mixture of selected native references, per-world value slots, owner registries, accessor patches and assembly-wide rewriting of reads/writes. It handles reads, writes and static field addresses, not just singleton getters. A replacement can use a different mechanism, but must preserve address-taking semantics for by-reference updates. Shared immutable definitions and mutable instance state must be distinguished: GameStats definition metadata is shared while values are cloned; event sequences are cloned; sky arrays are cloned when seeding a world. Null initial slots must not accidentally reuse primary-world objects.

The assembly scan excludes static constructors and open generic methods it cannot patch. Those exclusions are audit items for a new adapter. Other mods' compiled calls, reflection, native code and dynamically generated methods do not automatically become safe because the game's assembly was scanned.

## Physics, animation and navigation

Create a separate Unity local 3D physics scene for each world. Move entity roots and displayed chunk objects to that scene; new entity creation and chunk attachment must continue using it. Existing animator state is captured/restored when moving initialized entity roots, because moving objects can otherwise disturb animation and root motion.

The inspected simulator accumulates `Time.deltaTime`, advances by `Time.fixedDeltaTime`, and performs at most eight steps per world per invocation. The remaining accumulator is retained. Static Physics wrappers that refer to the default scene and game call sites that use that scene are redirected to the scoped physics scene. Merely moving colliders into another scene without routing queries leaves raycasts, melee, ground tests and vehicle collision incorrect.

Navigation requires graph ownership rather than a small graph-index bitmask. A path must select only its world's graphs and nodes, while preserving the original tag, walkability, cost and traversal rules. Graph updates must be confined the same way. Navigation workers and callbacks need owner scope even after the main thread switches maps. Test same-coordinate geometry that differs between maps, including a path above graph index 31, before claiming 64-world support.

World-owned Unity `Update`, `FixedUpdate`, `LateUpdate`, collision, trigger and controller-collision callbacks must run under the owner's context and be blocked for uninitialized/closed worlds. Global components continue independently. The companion additionally suppresses these callbacks for sleeping maps.

## Startup and world generation

Observed startup order:

1. Read the mod-local world catalogue and dedicated-server options. Resolve the configured primary world and generation parameters.
2. Before native dedicated startup, validate each installed map or generate explicitly configured `RWG` maps. Generation uses a temporary sibling directory; validate before renaming it into place. Preserve an incomplete existing destination for recovery rather than overwriting it. Missing named maps stop startup.
3. Allow native initialization of the primary world. Capture its services and isolate its scene.
4. Validate shared block/item name-to-ID mappings before loading the remaining saves.
5. Initialize each additional world's scene, native timer, water and service slots, invoke native world creation sequentially, bind created services and preserve a monotonically increasing entity allocation counter.
6. Validate save routing, service/sky ownership and the loaded set. Restore a valid selected context.
7. Publish readiness and allow waiting client join coroutines to proceed in their selected worlds.

Required map files are nonempty `map_info.xml`, `main.ttw`, `biomes.png`, `prefabs.xml`, `spawnpoints.xml`, plus terrain `dtm.raw` or `dtm_processed.raw`, and each of `splat3.png`/`splat3_processed.png` and `splat4.png`/`splat4_processed.png`.

The upstream initializes every configured map and keeps it resident. The companion's freeze feature does not unload them. Neither the schema's 64-world limit nor static capacity fixtures establish acceptable memory use or startup time at that scale. Lazy loading/unloading would be an additional design, with new lifecycle and recovery work.

## Scheduler and clocks

Native automatic `gmUpdate` is intercepted once multiworld mode takes ownership. The scheduler visits each initialized, open world, enters its context, invokes game update, synchronizes transforms, simulates that world's physics, and restores prior context. It suppresses repeated execution of process-wide work for subsequent maps in a frame: options/resolution, loaders, platform, invites, main-thread task pump, global actions and pause handling. Social coordination runs globally.

If the scheduler throws, upstream clears readiness and stops further managed world ticking to avoid additional invalid writes. The replacement must log the owner and stage, reject new travel, and avoid continuing with a partially selected context.

Upstream's empty-world option controls time advancement, not comprehensive suspension. A populated map advances. An empty map advances only when that option is enabled and another initialized open map has player entities. If all are empty it does not advance. Blood-moon targeting additionally requires a real spawned player in that same world; stale or remote party members cannot serve as targets.

Seven Days Beyond adds two distinct systems that must survive replacement:

- **Simulation suspension:** keep a map awake for players, authenticated clients still joining/loading, transfer source/destination including rollback, and explicit landing/preparation reservations. Allow a ten-second drain grace. Gate world gameplay, isolated physics, native water and owned component callbacks. Keep networking, deadlines, social services and main-thread tasks running. Do not abruptly terminate chunk/lighting/path/disk workers; queued work can drain. Rebase native timing deltas and the physics accumulator on wake, preserving native tick counters. Resident maps remain in memory.
- **Shared calendar:** one persistent calendar advances once per server frame while any authenticated client is connected, including during loading/travel, and pauses when everyone disconnects. It does not catch up across downtime. Audited daylight/trader/blood-moon/quest consumers use it; native simulation timestamps, block/loot aging and other unaudited consumers remain local. Serialize the common time into real time packets and synchronize clients after spawn/travel and periodically. The earlier Setup-only clock check missed a real HUD error; preserve the serialized-byte and live-HUD regression tests.

## Saving and shutdown

Console/native save operations must reach every configured live world under its own save scope. World saves also persist common content-ID mappings. A save must never select its path from a preference last written by another map.

Shutdown stops statistics and new work, disconnects active server clients, cleans additional worlds in their own contexts, then lets native cleanup finish the selected world. Complete pending entity creation; unload/persist vehicles, drones and entities; save world, chunks and persistent players before disposing their services. Stop navigation callbacks/workers and detach chunk delegates, then release chunk systems, entity distribution/queues, biome spawning, block ticking, decoration, sky/environment and water. Shared navigation infrastructure must not be destroyed while another world's cleanup still needs it. Mark a world closed only as its lifecycle progresses; cleanup must tolerate a repeated request.

Open: the reconstruction's additional-world cleanup does not provide enough evidence to promise scene unloading or complete native-memory reclamation. A replacement must explicitly test scene/object/worker counts after repeated create/close cycles.

## Social behavior and ancillary fixes

Maintain a logical party independent of transient entity instances. Project only members actually present into each world's native party. Preserve the logical leader and voice lobby even when the leader is elsewhere; display remote rows with map name and vitals. Do not create fake world entities for remote members. Genuine logout removes a logical member; detaching for travel does not. Validate invite/kick/leadership actions against logical membership. Upstream allows at most eight party members and refreshes global snapshots approximately once per second.

Allies are persisted globally and mirrored into each world's persistent-player list. Global player presence includes identity, current map, level/game stage, kills, deaths, ping and administrator status; local simulation client lists remain world-filtered. Browser player count is server-wide. Voice-lobby retention and remote UI bindings need actual multiplayer tests.

Ancillary observed safeguards include suppressing repeated login/logout announcements for intra-session travel, isolating chunk stability reset, and rejecting late melee damage from known dead nonplayer attackers. Death records are world-local, expire after 120 seconds of monotonic time, are bounded to 4096 queued entries and are cleared when an ID is reused by a living entity. These are separate regression concerns, not the definition of map travel.

## Scope boundary

This package is a functional reconstruction guide, not an assertion that a replacement is already implemented or that all native game internals are known. The engine adapter, partial-load cleanup, packet pooling, simultaneous players and large-scale residency remain the highest-risk implementation work. The following documents define the contracts and concrete tests that turn those unknowns into reviewable milestones.
