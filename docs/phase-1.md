# Phase 1: build foundation and integration points

## Outcome

- Added a clean companion-mod source tree and offline build tools.
- Downloaded the dedicated-server managed SDK and bundled Harmony from Steam.
- Verified all 15 direct MultiWorld references against the downloaded assembly identities.
- Pinned 163 SDK files to Steam depot `294422`, manifest `3031329678505041777`.
- Verified the supplied upstream DLL hash and 12 exact static method signatures using metadata without executing the mod.
- Compiled the diagnostic companion against the real game assemblies with warnings treated as errors.
- Ran six tooling tests successfully, including changed-DLL and missing-dependency rejection.
- Regenerated the reference C# using resolved dependencies; the initial 464 unresolved-IL warning comments are absent.

The downloaded game's `Constants` fields identify release type `V`, major `3`, minor `30`, and build `18`. These are raw field values, not an inferred display-version label. Their presence and the assembly version numbers cannot establish which exact game build the upstream author used. In-game compatibility and manual-transfer smoke tests remain outstanding. No gameplay changes are enabled.

## Chosen approach

Keep the supplied mod unchanged and build a companion assembly. Pin the upstream DLL by SHA256 and verify its private method signatures before future patches can be enabled. The companion's phase 1 implementation only reports compatibility at `GameStartDone`, after mods have initialized. It does not invoke the inspected private methods, alter player saves, or install any patches.

Retain existing transfer packets, token/epoch validation, checkpoint acknowledgement, timeouts, and rollback. The first functional prototype will add a server-owned arrival request associated with the existing transfer token, covering both success and recovery paths.

## Integration map

All names below refer to `MultiWorldProbe` in the supplied 0.6.2 DLL, unless stated otherwise. `src/Shared/MultiWorldContract.cs` is the executable contract used by both inspectors.

| Method or area | Existing behavior | Planned integration |
| --- | --- | --- |
| `OnWorldCommand(ref ModEvents.SChatMessageData)` | Parses `/world`, validates life/vehicle/cooldown state, creates a transfer request and sends prepare | Introduce a narrow transfer adapter shared by automatic crossings and manual travel; preserve validation |
| `ResolveClientWorld(ClientInfo)` | Resolves the player's current scoped world | Determine which grid cell owns the player; never assume `GameManager.World` is their world |
| `AcceptTransferCheckpoint(ClientInfo, string)` | Requires a fresh checkpoint and matching token, then begins the switch | Retain its ordering; do not detach a player before acknowledgement |
| `BeginSessionWorldChange(ClientInfo, TransferRequest, WorldContext)` | Detaches the player, increments epoch, selects destination and sends switch | Bind arrival override to token and the actual target; rollback to the source must not use destination arrival coordinates |
| `RestoreSharedCharacter(PlayerDataFile, string, WorldContext)` | Restores shared data then overwrites map-local position, journal, markers, beds, ownership and rental records | Apply a validated arrival override after restoration; journal portability is a separate later change |
| `SaveSharedCharacter(PlayerDataFile, string)` | Writes the shared checkpoint | Preserve authoritative save ordering |
| `HandleTransferTimeout(ClientInfo, TransferRequest)` | Aborts early or attempts source-world rollback | Release destination reservations and arrival overrides on every terminal path |
| `CompleteTransferAfterDisconnect(ClientInfo)` | Cleans transfer, session and checkpoint state after disconnect | Clean companion state without treating every disconnect as a successful crossing |
| `UpdateBothWorlds()` | Iterates all initialized worlds, scopes globals, runs updates and physics | Later add per-world eligibility while retaining global services once per frame |
| `SimulatePhysics(WorldContext)` | Advances physics for a scoped world | Include in later suspension gating, rather than only skipping `gmUpdate` |
| `ShouldAdvanceWorldTime(World)` | Controls an empty world's clock policy | Separate shared calendar time from suspended simulation and timestamp-driven activities |
| `PrepareClientTransfer(string, string)` | Freezes player input and sends character data plus checkpoint acknowledgement | Keep the existing client sequence for the first on-foot prototype |

## Constraints established by inspection

- The key APIs and nested context/request types are private. There is no supported public transfer API to call directly. Reflection/Harmony access must be centralized and checked against the pinned binary.
- Suppressing `UpdateBothWorlds` would suppress all maps. Suppressing `gmUpdate` alone leaves scoped physics and potentially independent workers running. Suspension needs a precise eligibility check and an audit of all update paths.
- The loop tracks `repeatedGlobalFrame`. If the first world is suspended, global services must still run once. Reservations must cover a destination being prepared and a source still needed for rollback.
- Global networking, social coordination, transfer deadlines and recovery must remain alive even with no occupied worlds.
- `DetachPlayerForWorldChange` calls quest disconnect and boss-despawn handlers. Carrying a journal alone does not establish portable active quests; those handlers, objective bindings and map IDs need review in the quest phase.
- The original scheduler stops on an exception to avoid invalid writes. Preserve that error behavior.
- Removing mounted-transfer guards does not implement vehicle transfer. Leave them in place for the on-foot prototype.

## Exit criteria

| Criterion | Status |
| --- | --- |
| Real managed/Harmony dependencies acquired and reproducible | Complete |
| Upstream hash and method contracts validated | Complete |
| Diagnostic companion compiles against actual game assemblies | Complete |
| Tooling negative cases tested | Complete |
| Dedicated-server startup with both mods | Complete; West/East ready, runtime hook check passed |
| Baseline client join and normal disconnect | Complete |
| Client joins and baseline manual transfers | Complete; user confirmed repeated transfers, inventory and return location |
| Disconnect/restart baseline recorded | Complete; user confirmed reconnect and game-server restart persistence |

Phase 2 boundary-crossing work should proceed after the baseline in-game checks establish a known working installation. See [development.md](development.md) for commands and the smoke-test procedure.

Phase 1 exit criteria were met on 2026-10-21. See [Phase 2](phase-2.md) for the first optional border implementation and its separate test status.
