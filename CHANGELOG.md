# Changelog

## Unreleased

- Remove bundled LITW MultiWorld archive, DLL, decompiled C#, IL and reference scripts from the current branch after preserving the replacement analysis and final dependency notes.
- Accept the current companion’s external DLL through `--upstream`, `MULTIWORLD_DLL`, or ignored `local-dependencies/multiworld/`; explicitly skip upstream metadata integration tests when that fixture is absent.
- Keep independent replacement documentation and existing companion/gameplay code. The deployed runtime and Git history are unchanged.

## 0.6.0

- Provide one client/server Beyond package with native game localization. Include English and initial Spanish/Latin American Spanish translations for all 56 Beyond text keys, with English fallback for other languages.
- Send message keys and values to clients for independent language selection. Translate directions and state labels, preserve canonical commands/map IDs, and bound the versioned packet format.
- Localize border/travel messages, grid and world reports, shared time, quest-book diagnostics and test-quest text. Preserve existing saves and the pinned upstream dependency.
- Validate translation references and placeholders during builds; add production codec, fallback and translator-error tests and contribution instructions.

Built against the pinned game assemblies and checked with the automated suite. Deployed and restarted on ANNWN: native localization, all four worlds, native quest serialization, safe landing probes, suspension and shared-clock self-tests passed startup checks. In-game multilingual, client packet-mapping and transfer regression checks remain pending. Download the matching shared ZIP from `downloads/` before joining.

Deployment evidence and backup details: [ANNWN 0.6.0](docs/deployment-0.6.0.md).
