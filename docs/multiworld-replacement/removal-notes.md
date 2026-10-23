# Multiworld implementation removal notes

Date: 2026-10-23. Pre-removal branch head: `26335a9b50f24d10e4af865e7b55e86f5655c69b`.

## Preserved facts

The [architecture](README.md), [contracts](contracts.md), [implementation plan](implementation-plan.md) and [inspection inventory](inspection-inventory.md) preserve the findings needed to guide a newly written replacement. The inventory's former paths and blob IDs are historical identifiers, not current files or build inputs. No implementation bodies or IL are retained in these notes.

This final assembly-reference ledger preserves the version facts formerly held in the removed reference directory. These are embedded reference identities from the supplied 0.6.2 assembly, not sufficient proof of game-build compatibility. The game SDK manifest and file hashes remain in `config/dependencies.lock.json`.

| Referenced assembly | Embedded version |
| --- | --- |
| mscorlib | 4.0.0.0 |
| Assembly-CSharp | 0.0.0.0 |
| AstarPathfindingProject | 0.0.0.0 |
| System.Core | 4.0.0.0 |
| UnityEngine.CoreModule | 0.0.0.0 |
| UnityEngine.AnimationModule | 0.0.0.0 |
| UnityEngine.PhysicsModule | 0.0.0.0 |
| 0Harmony | 2.13.0.0 |
| Mono.Cecil | 0.11.5.0 |
| System.Xml | 4.0.0.0 |
| Noemax.GZip | 17.269.5336.0 |
| System | 4.0.0.0 |
| System.Xml.Linq | 4.0.0.0 |
| LogLibrary | 0.0.0.0 |
| Assembly-CSharp-firstpass | 0.0.0.0 |

The preserved native ID-mapping, character-overlay, packet ordering, per-world service ownership, transfer rollback and client cache invariants are the important reconstruction facts. Three constructor/thread patch attributes remain unresolved; the implementation plan lists the required engine investigation. The notes do not promise that every native patch is proven or that a replacement will work without integration testing.

## Removed material

Removed all 34 tracked files under `resources/multiworld/`: supplied archive, extracted DLL/metadata/configuration, reconstructed C# and project, IL listing, decompilation script, and superseded reference-directory reports. The repository ignore rule prevents accidental normal re-addition of that directory.

Our own `MultiWorldBridge`, compatibility contract and companion code remain because they are existing project integration code. They still require the upstream runtime until the independent backend replaces those integration points.

## External dependency workflow

The current companion's development checker/build still requires the pinned upstream DLL. Supply it separately through:

- `--upstream /path/to/LITWMultiWorldProbe.dll` on a check/build command;
- `MULTIWORLD_DLL` for development commands and metadata integration tests; or
- the ignored default `local-dependencies/multiworld/LITWMultiWorldProbe.dll`.

The existing SHA-256 and twelve-hook checks remain active. No command automatically retrieves the removed implementation or falls back to an old Git path. When the external fixture is missing, check/build exits with an explicit dependency message and five metadata integration tests report skips. The independent checksum test remains runnable.

Existing SevenDaysBeyond 0.6.0 ZIP packaging permits only our DLL, ModInfo and Config files, excluding the upstream DLL. Historical server-test deployment helpers still describe an operator-supplied server package containing both separately obtained mods; that deployment path is not an included upstream payload. Current deployment and runtime replacement are outside this removal.

## Removal boundary

The initial removal was a normal deletion commit. The repository history was subsequently rewritten to exclude `resources/multiworld/` from every reachable snapshot; see [history cleanup](history-cleanup.md). ANNWN’s installed files, live service, player data and backups are unchanged. No claim is made that the live companion can run without Multiworld.

When replacing the runtime later, update the backend, tools, deployment helpers and package prerequisites together, and use the migration/acceptance gates in the implementation plan. Do not delete save data when removing source references.
