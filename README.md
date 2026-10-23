# Seven Days Beyond

Seven Days Beyond is an experimental 7 Days to Die mod project for exploring a connected grid of pregenerated maps. The initial target is an 8 × 8 grid (64 maps) and up to six simultaneous players, with automatic travel to the adjacent map when a player reaches a boundary.

## Planned behavior

- Configure the map grid before starting the server.
- Cross a boundary to enter the corresponding edge of the adjacent map.
- Keep character progression, inventory, and the quest journal with the character; identify quest destinations by map as well as position.
- Use a shared server clock to evaluate quest expiration.
- Freeze unoccupied maps and resume them when a player enters, without simulating missed hordes or other elapsed activity.
- Preserve map-specific construction and other saved world state.

Vehicle and passenger transfers need separate investigation. Safe arrival, disconnect recovery, and simultaneous crossings must be validated before normal play.

## Status

The West/East prototype passed single-player manual and automatic crossings, inventory checks, reconnects, controlled restart persistence, empty-world suspension, and the corrected shared-clock HUD check. God Mode persistence also passed with the 0.4.1 client companion.

Companion 0.5.0 expands to a [2 × 2 grid and character quest book](docs/grid-quests.md), with north/south routing, map-qualified quest snapshots, an all-map quest list, a grantable native quest fixture and repeatable travel/deadline tests. Native quest-window presentation remains specific to the current map. Runtime deployment and client checks for 0.5.0 are pending. Two-player crossings, a merged native all-map journal UI, generic third-party quest deadline conversion, vehicles and the full 64-map scale remain future work.

See [development setup](docs/development.md) for dependency acquisition, build commands, and the first in-game checks, and [phase 1 findings](docs/phase-1.md) for integration points and remaining work.

The native server baseline passed a client connection/disconnect test and is preserved. ANNWN now runs the isolated [West/East MultiWorld test](docs/multiworld-test.md); see [the original server setup](docs/annwn-server.md) for installation details.

The starting reference is LITW MultiWorld 0.6.2, supplied as a compiled mod. Its existing configuration accepts 1–64 worlds, but it initializes and updates every configured world. It currently blocks transfers while riding a vehicle and restores destination-specific arrival positions rather than matching map edges.

## Repository layout

- `src/SevenDaysBeyond/` — compatibility checks and experimental two-world on-foot travel.
- `src/Shared/` — pinned upstream hook contract shared by the runtime diagnostic and metadata tool.
- `tools/` — dependency acquisition, metadata inspection, collection, and build commands.
- `config/dependencies.lock.json` — Steam manifest and dependency checksums.
- `docs/` — development setup, integration findings, and verification status.
- `resources/multiworld/original/` — supplied archive and unchanged extracted mod files.
- `resources/multiworld/decompiled/` — reconstructed C# for inspection, not the author's original source.
- `resources/multiworld/analysis/` — implementation findings and dependency information.
- `resources/multiworld/README.md` — provenance, decompilation procedure, and limitations.

See [the MultiWorld research notes](resources/multiworld/README.md) before attempting a build. Decompiled code is reference material; inclusion does not grant a new license to the upstream mod. Game and Harmony dependencies are not bundled.

-----

**AI disclaimer:** AI-assisted tools were used in development. The author reviewed and approved the code and documentation and remains responsible for the project.
