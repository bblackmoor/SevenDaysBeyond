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

Phase 1 build foundation is in place: real dedicated-server dependencies have been acquired, the 12 upstream hook signatures pass inspection, and a diagnostic-only companion mod compiles against the game assemblies. Startup and manual-transfer smoke tests are still pending. The planned gameplay features above have not been implemented or tested in-game.

See [development setup](docs/development.md) for dependency acquisition, build commands, and the first in-game checks, and [phase 1 findings](docs/phase-1.md) for integration points and remaining work.

The starting reference is LITW MultiWorld 0.6.2, supplied as a compiled mod. Its existing configuration accepts 1–64 worlds, but it initializes and updates every configured world. It currently blocks transfers while riding a vehicle and restores destination-specific arrival positions rather than matching map edges.

## Repository layout

- `src/SevenDaysBeyond/` — diagnostic companion mod; future gameplay implementation.
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
