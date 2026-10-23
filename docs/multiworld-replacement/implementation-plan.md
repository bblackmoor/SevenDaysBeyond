# Independent implementation and removal plan

This is a proposed design, not existing replacement code. It implements the requirements in [the architecture](README.md) and [contracts](contracts.md) using new components. Internal class names, implementation order and data layouts need not resemble the inspected mod.

## Design target

One dedicated process, multiple native isolated worlds, a matching shared client/server package, and authenticated on-foot map changes without reconnecting. Preserve the working companion's grid travel, safe landing, frozen empty maps, shared calendar, map-qualified quest book, localization and debug-mode continuity. Initially prove two worlds, then the four-map test grid; qualify the 8 × 8 catalogue separately. Target six concurrent players, with eight as a later validation target. Do not remove mounted-travel guards until vehicles and passengers have a separate transfer transaction.

Use a narrow backend interface so gameplay systems stop reflecting into upstream private state. This is an interface design, not a request to preserve the old private API:

| Component | Inputs and outputs | Owns |
| --- | --- | --- |
| Catalogue | Validated map/save definitions; lookup by stable ID | Configuration and identity, no Unity objects |
| World host | Open/close/status, enter owner scope, enumerate ready handles | Native objects, scenes, save roots, service ownership |
| Engine adapter | Pinned methods/fields and patch capabilities | All version-specific Harmony/native integration |
| Session router | Authenticated connection, world handle, epoch | Packet ownership, ingress/egress, connection lifetime |
| Transfer coordinator | Player, target, optional landing reservation | State machine, durable intent, deadlines and recovery |
| Character repository | Player identity, checkpoint, destination-local overlay | Atomic character storage and migration |
| Activity scheduler | Frame time and activity/reservation snapshot | Global work once, native steps, sleep/wake rebasing |
| Calendar | Connected-client activity and frame delta | Shared calendar/deadline persistence and client time |
| Quest service | Character and map IDs, native journal serialization | Journal pages, progression overlay and deadline policy |
| Social service | Authenticated identities and local projections | Party/allies/presence/voice continuity |
| Client world loader | Transfer token/epoch, verified map content | Retained connection, local lifecycle and load cancellation |
| Content service | Manifest and bounded file requests | Map verification/acquisition; optional warm/RAM caches |

World handles should expose identity, readiness, scope and reservations without exposing the old private context type. The engine adapter is the only layer that knows private native field names. Policy and persistence components should be testable without the Unity/game runtime. Define events for checkpoint saved, before detach, restoring destination player, entity spawned, transfer committed/recovered, and connection closed. Publish explicit result types distinguishing rejected, cancelled, committed, recovered-to-source and failed-needs-reconnect states.

## Ownership and lifecycle rules

Required world lifecycle: registered → validating → loading → ready → closing → closed, with an explicit failed state. Sleep is an activity state of a ready resident world, not the same as closed. Do not expose a half-created world as a travel target. Keep close idempotent and record which resources were actually acquired, so partial startup can unwind safely.

All Unity object creation/selection and transfer mutation runs on the main thread. Workers receive immutable owner identifiers or an explicit owner scope; they do not choose a world from a shared current pointer. Save/restore scopes must be nested and exception-safe. Every coroutine continuation must verify owner readiness and session generation. Closed-world callbacks are discarded or drained by a documented cleanup path.

Use reference-counted reservations, each with an owner and purpose. Transfer preparation reserves destination terrain and the rollback source. Join/loading reserves the selected map before a player entity exists. Landing observers are removed on success, cancellation, rollback and disconnect. If a reservation expires, log which owner failed to release it rather than allowing a permanent unexplained awake map.

Keep process-global work independent of world visitation. A sleeping first map must not prevent global tasks; zero awake maps must not stop authentication, transfer recovery, social updates, content downloads or deadlines. Native world updates, physics and water use their own clocks. The shared calendar advances exactly once and cannot be multiplied by the number of maps.

## Durable travel proposal

Upstream has atomic individual files but no complete crash transaction. The new coordinator should add a small versioned travel record keyed by authenticated player and transaction ID, containing source and intended destination stable IDs, source/destination-local placement references, checkpoint generation/hash, phase, and last committed world. Store wall-clock diagnostic timestamps separately from gameplay calendar deadlines.

Required ordering:

1. Validate player, source and target; reserve both worlds; prepare destination chunks and safe arrival while source remains playable/attached.
2. Freeze input and obtain fresh server-accepted native player data. Save the checkpoint durably, then persist a prepared intent referencing that checkpoint.
3. Revalidate ground and player attachment/death state. Detach source once, advance the connection generation and begin destination load.
4. Load the same shared checkpoint plus the correct destination-local overlay. Override only placement with the reserved, rechecked landing. Confirm native spawned entity and uniqueness.
5. Durably commit destination preference and transaction completion, then announce success, clear client flags and release reservations.

On startup/reconnect, a prepared but uncommitted record resolves deterministically to the last committed/source world with its saved checkpoint, unless a durable destination commit exists. Avoid guessing from whichever map happened to have the newest local `.ttp`. A completed transaction must be safe to replay without awarding items, quest rewards or progression twice. Use an atomic single-record commit or an explicit generation scheme so world selection and transaction status cannot disagree after a crash. Save backups are still needed because native character and region data span more than one file.

This is stronger proposed behavior than the inspected mod. It requires fault injection at every write boundary and cannot be declared complete from an ordinary restart test.

## Current companion dependencies to replace

The dependency is primarily reflection/Harmony against `MultiWorldProbe`, so a project can compile without a direct reference and still be completely dependent at runtime.

| Current file | Dependency to remove or reroute | Replacement integration |
| --- | --- | --- |
| `src/Shared/MultiWorldContract.cs` | DLL SHA, upstream type identity, twelve private hook signatures | Game-adapter capabilities and protocol compatibility checks |
| `src/SevenDaysBeyond/ModApi.cs` | Loaded upstream assembly discovery and compatibility gate | Initialize owned backend; refuse unsafe partial installation |
| `MultiWorldBridge.cs` | Context/transfer dictionaries, fields, private methods, lease constructor, fabricated chat command | Typed backend methods and world/transfer handles |
| `BorderTravel.cs` | UpdateBothWorlds, RestoreSharedCharacter, BeginSessionWorldChange and transfer internals | Frame event, destination restore placement event, before-detach validation |
| `WorldSuspension.cs` | Upstream scheduler, physics function, service ownership and callback target enumeration, timer/value slots | Scheduler/activity service with native engine adapter gates |
| `SharedClock.cs` | Context readiness, scheduler before/after events, transfer-result callback | Backend lifecycle/frame/arrival events plus audited native wire hook |
| `QuestBook.cs` | Shared-save prefix, restore postfix and scheduler tick | Character repository/journal events; global quest tick |
| `ClientDebugState.cs` | Client token/epoch fields, SwitchClientWorld, ClientWorldResult | Client loader's session-bound snapshot/restore events |
| `tools/dev.py`, `tools/DependencyCheck/*`, tests | Pinned binary validation/copy and reflection contracts | Native adapter validation; replacement-only builds |
| Deployment/configuration/docs | Separate prerequisite/mod directory and copied catalogue | Own catalogue/importer and matching client/server deployment |

Retain BorderPolicy/GridLayout, SafeLanding, CalendarState, QuestBookState, localization and their useful tests where their contracts are independent. Review each file before reuse; the table identifies expected boundaries, not a claim that all files are already decoupled. Source-independent companion features are our existing work, while the replacement engine/backend must be newly implemented.

Current suspension inserts a checked gate before `LockManager.Instance` in `GameManager.gmUpdate`; it keeps the global prologue and ensures the later main-thread task pump runs if that world's core is skipped. Preserve the outcome while simplifying control in the owned scheduler. Current calendar patches audit exact native reads in 28 consumers, plus packet/browser and arrival hooks; preserve that audit and serialized-byte tests instead of replacing every `worldTime` access globally. Current storage discovers its root through a map named West; remove that hardcoded catalogue-name assumption when defining the backend save root.

Debug-mode preservation is session-only: God, flight, no-collision, speed modifier and debug-menu state are captured together, restored independently to a newly created local player, and tied to the same authenticated transport/transfer token. Recovery keeps the original snapshot. Ordinary login clears it. Do not serialize those modes into portable character saves or grant them on unrelated connections.

## Delivery stages and exit criteria

| Stage | Implement | Exit evidence |
| --- | --- | --- |
| A: engine contracts | Independent native dependency checker, ownership registry, fail-closed patch installation | Exact supported game assemblies; expected patch anchors; negative mismatches rejected before play |
| B: isolated two-world host | Initialization, service/scene/physics/nav/save isolation, orderly close | Different terrain at same coordinates, independent services/saves and exception-safe owner restoration |
| C: session networking | Epoch wrapper, world routing, pooled package lifetime, initial native join | Two clients in separate maps; actual serialized packets; no wrong-world replication |
| D: character and transfer | Fresh checkpoint, destination overlay, client retained-session load, durable recovery | Repeated round trips, all phase failures and crash/reconnect tests |
| E: companion integration | Typed backend, borders/landing, suspension, calendar, journal, localization/debug state | Existing confirmed single-player behaviors pass with upstream DLL absent |
| F: social and persistence | Global allies/parties/presence, voice, save-all and restart | Two-player party travel/action tests and persistence checks |
| G: four-map release candidate | Complete native map distribution and regression suite | Four-map client journeys, quests and simultaneous transfers; no runtime upstream lookup |
| H: scale/optimization | Six players, 64 catalogue entries, measured residency; optional caching | Measured RAM/startup/frame/save/network budgets; explicit capacity report |

Do not load both competing isolation implementations into one process. A temporary upstream backend can coexist as an alternative implementation behind the typed interface, but only one engine host/router/patch set may own a running server. Validate the new backend on disposable copied saves and a separate test service first. No live ANNWN changes are part of this documentation task.

## Acceptance suite

The following tests define required behavior. They are a specification to implement, not tests run during this analysis. Unit tests alone cannot validate Unity scenes, game serializers or native lifecycle.

| ID | Scenario | Pass condition |
| --- | --- | --- |
| CFG-01 | Valid 1/2/4/64-world catalogues and invalid 0/65 | Correct limits; stable default ordering and first-join choice |
| CFG-02 | Duplicate case variants, map/save collisions, unsafe names, duplicate/unknown settings, DTD | Reject before world/save writes |
| HOST-01 | Same coordinates, different block/collider layouts in two maps | Raycasts/collision/melee/ground checks see only owning scene |
| HOST-02 | Nested scopes, thrown native call, worker callback, coroutine yields/disposal | Caller world restored; worker ownership never leaks |
| HOST-03 | Path on map B through coordinates blocked only on map A | Owned graph/node/traversal rules; no cross-world graph update |
| HOST-04 | More than 32 graph owners | Routing remains correct without truncated masks |
| HOST-05 | Power, vehicle, drone, weather, loot, traders, quests, stability and sky updated separately | Mutable services/queues/counters and save paths remain isolated |
| HOST-06 | Failure during each secondary startup stage; repeated shutdown | Acquired resources released once; no callbacks target closed worlds |
| HOST-07 | Entity loading allocates IDs in several saves | No simultaneous live ID collision or counter rollback |
| SAVE-01 | Build different structures in both maps, save-all, restart | Correct structures, player-local data and shared character in each |
| SAVE-02 | Secondary ID mapping conflicts or corrupt character checkpoint | Reject safely; no overwrite of good mappings/checkpoints |
| SAVE-03 | First-visit destination vs previously visited destination | Shared inventory/progression; correct local spawn/markers/owned entities/rentals |
| SAVE-04 | Migrate known old identity and missing preferred-world entry | Same character found by authenticated hash; documented first-join fallback |
| NET-01 | Client A and B in different maps at same position | No chunk/entity/damage packet crosses ownership boundary |
| NET-02 | Delayed epoch N packet after switching to N+1 | Discard before mutation; control/authentication still works |
| NET-03 | World-info created under A then serialized while B active | Wire hashes/size still describe A |
| NET-04 | Nested/wrong-direction/unwrapped gameplay; short/oversized input; decompression abuse | Bounded rejection with no partial mutation or pool leak |
| NET-05 | Packet drop, skipped handling, enqueue/serialization exception and pool reuse | Balanced package references; no stale payload on reuse |
| TR-01 | Repeated A→B→A on foot | Same transport, one player entity, correct inventory/progression/local data |
| TR-02 | Ack before fresh data, stale/duplicate token, duplicate ready/result | No detach or duplicate spawn/commit |
| TR-03 | Death, mount attachment, disconnect or terrain change during preparation | Cancel safely; release reservations and observers |
| TR-04 | Timeout during checkpoint/unload/load/spawn and forced client XML failure | Predetach cancel or return-to-source; matching epoch only |
| TR-05 | Recovery also fails | Saved checkpoint retained, clear terminal result, gameplay blocked, reconnect recovery deterministic |
| TR-06 | Kill server after each durable transaction write and after detach/spawn | Exactly one committed world/character; no item duplication or unrecorded loss |
| TR-07 | Two players cross in opposite directions or enter same target together | Independent tokens/epochs, no wrong-world saves or duplicate entities |
| LAND-01 | Every grid edge, unequal bounds, corner tie, coast/water, blocked ground | Correct adjacent map/edge and safe valid location; deterministic refusal otherwise |
| SLEEP-01 | All maps empty; first map asleep and later map occupied | Global pump runs once; only awake simulation advances |
| SLEEP-02 | Join/loading and forward/rollback/terrain reservations | Required maps wake before any gameplay/physics access |
| SLEEP-03 | Release last reservation, wait grace, then wake after long idle | Native ticks/physics/callbacks freeze; wake has no accumulated catch-up |
| TIME-01 | Connected, transferring, all disconnected, controlled restart | One shared calendar; no multiplication by maps or downtime catch-up |
| TIME-02 | Serialize packet with deliberately stale cached map time; live arrival HUD | Wire carries shared time and client HUD receives it |
| TIME-03 | Sleep through several days then wake on/after blood moon | No replay of missed hordes; present-night eligibility evaluated correctly |
| QUEST-01 | Native fixture progression before/after leaving and returning | Correct map journal page and native objective state; no cross-map target match |
| QUEST-02 | Deadline expires while away; reward claimed after return | Shared-calendar policy correct; no duplicate completion/reward |
| QUEST-03 | Trader sharing on/off, repeated crossings and new completion | Sum other-map points exactly once; local serialized points unchanged |
| SOCIAL-01 | Leader remote, invite/kick/promote/leave, cross and return | Logical membership/leadership consistent; native projections only local |
| SOCIAL-02 | Genuine logout vs transfer detach, ally change/restart, voice switch | Correct membership removal, persistent allies, retained authenticated voice lobby |
| MAP-01 | Fresh client, incomplete cache, bad CRC, changed GUID/manifest | Verified maps before use; no premature completion marker |
| MAP-02 | Wrong response tuple, timeout, negative offset, path traversal, disconnect | Bounded rejection/cancellation, no arbitrary file read/write or leaked temporary |
| CACHE-01 | Changed bytes/mappings/conditional XML/disk metadata, cache off | Full safe fallback, no reuse of stale definitions/map bytes |
| CLIENT-01 | God on with flight off; rollback; normal reconnect | Independent session modes preserved only for that transfer/session |
| OPS-01 | Mismatched client/protocol/game contract and scheduler exception | Clear failure, no partial multiworld gameplay or further unsafe writes |
| SCALE-01 | 64 definitions with measured loaded/awake worlds and six clients | Report actual RAM/frame/startup/save/download costs; no capacity claim based on config alone |

For each integration run record game/build hashes, replacement client/server version, catalogue, active/sleeping maps, player identities anonymized consistently, operation/token/epoch, source/target, checkpoint generation and outcome. Include restart/crash recovery logs and before/after inventory assertions. Avoid logging authentication secrets or full character blobs.

## What has actually been established already

Existing repository documents record single-player native baseline joins, repeated manual/automatic West/East crossings, inventory, reconnect and controlled restart persistence; suspension/reservation tests using real physics bodies; corrected clock serialization and user-confirmed HUD synchronization; and user-confirmed God Mode continuity. Four-map server startup, native quest serialization and landing/suspension checks are recorded. Version 0.6.0's localization/package build checks are recorded.

Those are tests of the upstream-based prototype. They do not prove the independent replacement works. Client four-map quest progression, multilingual/packet-mapping regressions, opposite-direction multiplayer, voice/social correctness, crash-atomic recovery, vehicles and 64-map operational scale remain unestablished by the inspected documents. Upstream methods named Verify/Test are fixtures, not evidence those tests ran on ANNWN.

## Repository removal checklist

Documenting the mod and removing stored third-party material are separate from removing the live runtime dependency. The bundled reference directory has now been removed; see [removal notes](removal-notes.md). A later [history cleanup](history-cleanup.md) filters the reference directory from the reachable branch history. No runtime replacement, deployment or ANNWN restart was performed.

When removing stored material, retain this directory, its provenance hashes and our own project code; remove the archive, extracted DLL, reconstructed code, IL, decompilation script/project and obsolete research outputs from `resources/multiworld/` as appropriate. Update README/layout links and any tooling that assumes those paths. A normal deletion alone affects the branch tip. The subsequent [history cleanup](history-cleanup.md) also filters the historical snapshots; original commit identifiers in the inspection ledger are retained only as provenance.

If runtime use must also end, complete at least stages A–G before switching ANNWN. Remove upstream discovery/reflection/contracts from our code and dependency tools, update deploy/package documentation and prerequisites, and verify builds/tests using an environment with no upstream archive/DLL/source available. Preserve existing native saves, shared characters, preferences, allies, calendar and quest books for explicit migration; deleting reference materials must not delete player data.

Cut over with a clean server stop and a verified save/config backup. Deploy matching replacement client/server packages to the isolated test first, run the acceptance gates, then deploy to ANNWN. Keep the old full deployment with its save snapshot as the rollback unit. Do not run the old runtime against newly migrated/written saves unless backward compatibility is demonstrated; restore the matching save snapshot for a true rollback.

## Remaining engine investigations

- Resolve the three undecoded constructor/thread patch attributes against the pinned binary/native metadata before claiming equivalent hook coverage.
- Confirm the complete installed-game service field/call-site set, including indirect generic/reflection/native accesses and other mods.
- Define cleanup and scene unloading for partially loaded worlds and quantify background worker/native memory lifetime.
- Test packet pooling and multi-channel ordering through actual game serialization and network processing.
- Audit all PlayerDataFile fields, owned-entity references, native identity formats and journal serialization for the supported engine build.
- Specify active-quest disconnect/listener semantics and third-party deadline conversions; our current quest book is not a universal all-map native quest UI.
- Measure catalogue/download/residency cost before choosing eager or neighbor-only map acquisition and before promising 64 resident maps.

These are concrete implementation/validation tasks. They do not require preserving or copying the upstream implementation.
