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

Companion 0.5.1 expands to a [2 × 2 grid and character quest book](docs/grid-quests.md), with north/south routing, map-qualified quest snapshots, an all-map quest list, a grantable native quest fixture and repeatable travel/deadline tests. Native quest-window presentation remains specific to the current map. Version 0.5.1 is deployed on ANNWN: all four maps, native quest serialization, landing probes, suspension/wake and serialized shared-clock startup checks passed. Client grid-crossing and quest-progress checks are pending. Two-player crossings, a merged native all-map journal UI, generic third-party quest deadline conversion, vehicles and the full 64-map scale remain future work.

See [development setup](docs/development.md) for dependency acquisition, build commands, and the first in-game checks, and [phase 1 findings](docs/phase-1.md) for integration points and remaining work.

Version 0.6.0 adds a [shared client/server package and native localization](docs/localization.md): 56 Beyond text keys with English and initial Spanish/Latin American Spanish translations, per-player language selection, English fallback and build-time translation checks. ANNWN now runs 0.6.0. Install the matching [client/server ZIP](downloads/SevenDaysBeyond-0.6.0.zip) before connecting. The build, automated checks and server startup checks pass; in-game multilingual, client packet-mapping and transfer regression checks are pending. See the [changelog](CHANGELOG.md).

The native server baseline passed a client connection/disconnect test and is preserved. ANNWN now runs the isolated [West/East MultiWorld test](docs/multiworld-test.md); see [the original server setup](docs/annwn-server.md) for installation details.

The starting reference is LITW MultiWorld 0.6.2, supplied as a compiled mod. Its existing configuration accepts 1–64 worlds, but it initializes and updates every configured world. It currently blocks transfers while riding a vehicle and restores destination-specific arrival positions rather than matching map edges.

Current gameplay settings are documented in [ANNWN sandbox settings](docs/annwn-sandbox.md), with the versioned preset and native code under `config/`.

## Feature backlog

The following features are intended future work; detailed design and implementation order remain open.

- **In-game sandbox settings tool:** view and edit sandbox settings for other maps, and copy the current map's sandbox settings to one selected map or all other maps.
- **Graphical grid map:** an in-game view of every map in the grid, with zooming in and out and map markers. Marker types and further navigation controls will be defined during design.
- **All-map quest tracker:** an in-game graphical tracker showing quests across all maps, with sorting by map, tier or quest name. These are the initial proposed sort options and can be refined during design.

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

