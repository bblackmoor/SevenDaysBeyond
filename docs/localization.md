# Localization and the shared package

Version 0.6.0 uses one `SevenDaysBeyond` mod folder and DLL on both the dedicated server and every PC client. Install the same release on both sides. LITW MultiWorld 0.6.2 and the game's bundled Harmony remain prerequisites; they are not redistributed in the Beyond download. The server's existing `BoundaryTest.xml` remains server configuration and is not included in the general client/server package. Do not remove that file when updating ANNWN.

The combined package contains English, Spanish (`spanish`) and Latin American Spanish (`latam`). The two Spanish columns currently share a translation. These are initial translations that welcome native-speaker review. Other game languages fall back to English; multilingual infrastructure does not imply completed translations for every game language.

## Runtime behavior

The dedicated server chooses what happened and sends `NetPackageSdbLocalizedMessage` containing a protocol version, translation key, literal values and, where needed, nested translation keys. Each receiving client looks up the message with the game's native `Localization.Get` and current language, then formats the whole sentence. The server's language never selects a player's Beyond message language. Direction and state labels are translated on the client too.

`Config/Localization.csv` is the native V3 localization table. It contains Beyond's border warnings, approach/cancellation messages, map and border status, suspension/shared-clock reports, quest-book diagnostics, travel-test messages, and native test quest name/description. Native quest XML uses `name_key` and `description_key`. Commands such as `/sdb approach east`, map identities such as `NorthEast`, quest IDs and objective IDs remain stable technical identifiers. Log tokens and developer log text remain stable for troubleshooting. Upstream MultiWorld's own messages and UI are outside this translation catalog; the only upstream interception translates Beyond's keyed arrival-ground cancellation while preserving MultiWorld's cleanup.

The stock network manager discovers the additional packet class from the mod assembly and negotiates package mappings. There are no hard-coded packet IDs and no chat-string encoding. Protocol 1 bounds message depth, node count, argument count and UTF-8 lengths before allocation/formatting. Client-directed packets are ignored on dedicated servers and outside a connected remote-client session. Player text is displayed without BBCode interpretation. A presentation failure logs a warning without disabling border safety or quest persistence.

English is present for every key. The native lookup falls back to English when a selected language has no translation; Beyond also retries English if a translation has an invalid format. Native quest definitions resolve their text when loaded, so restart the client after changing language for a complete language test. No quest/save migration is required. Old serialized English diagnostic summaries remain readable as save data but are not displayed; localized diagnostics are reconstructed from their native journal instead.

## Add or improve a translation

1. Edit `src/SevenDaysBeyond/Config/Localization.csv` as UTF-8 CSV. Keep the `Key` and `english` columns, and use the exact language column name used by the target game's localization file. Add a language column to the same file rather than making a separate mod or DLL.
2. Translate complete cells. Preserve numbered placeholders such as `{0}` and `{1}`, including their meaning. They may be reordered or repeated. Do not translate the key, commands, map IDs, quest IDs or objective IDs. Use doubled braces `{{` and `}}` for literal braces.
3. Leave an unfinished translation empty so it falls back to English. Do not fill untranslated cells by copying English, which hides missing work.
4. Use a CSV-aware editor: quote cells containing commas, quotes or line breaks. Save UTF-8 and preserve the header. No extra runtime translation dependency is needed.
5. Run `python tools/check-localization.py`. The normal build runs this automatically. It rejects duplicate keys/columns, missing English, incorrect CSV shape, missing source/XML keys, unsupported placeholder syntax and mismatched placeholders. Submit the table change through GitHub and rebuild/package the release.

## Verification

Automated tests round-trip the production binary codec, render the same packet independently in two languages, exercise reordered/repeated placeholders, literal braces and Unicode values, and verify English fallback. They reject bad protocol versions, malformed/truncated UTF-8, foreign keys and excessive sizes/depth/argument counts. Catalog tests cover partial translations and common translator errors. The mod must also compile against the pinned game assemblies.

Before marking the release tested in game:

1. Update the server and every client to 0.6.0 together. A 0.4.1 client companion cannot handle the new packet. ANNWN is updated; install the matching 0.6.0 client package before joining.
2. With an English client and a Spanish client connected to the same server, run `/sdb status`, `/sdb time`, `/sdb map`, and `/sdb quests`. Confirm each receives its own language regardless of server language.
3. Grant `givequest sdb_test_fieldwork` from F1 and check the native quest name and description. Start a travel test, cross and return; verify translated test states and persistent partial objective progress.
4. Check the 50 m/10 m warnings, closed-edge protection, approach cancellation and arrival-ground-change cancellation. Confirm debug modes, inventory and the shared clock still survive a crossing.
5. Use another game language without a Beyond translation and verify English fallback. Check client/server logs for message, initialization and packet errors, then repeat reconnect and restart checks.

No in-game multilingual or transfer results are claimed by the offline tests.
