# Changelog

## 0.6.0

- Provide one client/server Beyond package with native game localization. Include English and initial Spanish/Latin American Spanish translations for all 56 Beyond text keys, with English fallback for other languages.
- Send message keys and values to clients for independent language selection. Translate directions and state labels, preserve canonical commands/map IDs, and bound the versioned packet format.
- Localize border/travel messages, grid and world reports, shared time, quest-book diagnostics and test-quest text. Preserve existing saves and the pinned upstream dependency.
- Validate translation references and placeholders during builds; add production codec, fallback and translator-error tests and contribution instructions.

Built against the pinned game assemblies and checked with the automated suite. In-game multilingual, packet-mapping and transfer regression checks remain pending. ANNWN remains on 0.5.1 until a coordinated client/server update.
