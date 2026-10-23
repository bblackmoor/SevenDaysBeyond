# Multiworld history cleanup

Date: 2026-10-23. Branch before cleanup: `1d48c71a125adfc03a94a431d862998e091d9f1a`.

## Scope and verification

The repository had one branch, `main`, and no tags, pull requests or releases at inspection. Its 23 reachable commits were inspected. The initial clean root is retained; 22 subsequent commits are rebuilt so no historical tree contains `resources/multiworld/`.

All 34 distinct blob objects from that directory (archive, binary, reconstructed source, IL, configuration and old reports) are absent from every rebuilt snapshot, including under alternate paths. Every other file path, mode and object ID is identical to its corresponding original snapshot. Before this documentation update, the rebuilt tip's complete tree is identical to the previous tip. The final documentation commit records this cleanup.

Original commit messages remain. The connected GitHub commit-creation interface generates new commit timestamps, so the original author, committer and both dates are preserved explicitly in each rewritten commit's trailers. The table below preserves the identifier mapping. No branch/tag is created to retain the old contaminated lineage. The branch replacement uses an expected-head lease and aborts if another writer changes `main`.

## Existing clones and hosting retention

Replace old clones with a fresh clone of the rewritten branch. Do not merge an old branch or push an old tag back into the repository; that can restore the removed lineage. Preserve any uncommitted project work separately before replacing a clone.

This removes the material from reachable repository history. It does not erase existing clones, installed ANNWN files, backups, or GitHub's potentially retained unreachable/cached objects. GitHub controls server-side object retention; a force update cannot promise physical deletion of every hosted copy. The original IDs retained here and in the evidence inventory are provenance text, not Git parent/object references.

ANNWN was not accessed or changed. No new runtime build is required because the working source/package content is unchanged.

## Commit identifier mapping

| Original commit | Rewritten commit |
| --- | --- |
| `04ba7ca9e452f5cf0ba2c2d05fe2f350b3451b01` | `04ba7ca9e452f5cf0ba2c2d05fe2f350b3451b01` |
| `676ac5ce8c8e1692a775ffbe330c526885174c60` | `48ddcc76fda8cb9b7528abc4a1d1bbb52cd36ebc` |
| `2e1bead3cc652aec242cd839f96793627f256eb2` | `06bc03435259cacf138157af263fe4a438b90a82` |
| `b9386718d4fbe42f19fd6a72190efb1979d62801` | `cfce6f3012d887d3477e7ab75b67317fbd98d1ba` |
| `3673f05eea3aab93eb4e8b4dcb75db84b17efa13` | `5b50541defa13af52f5c74f731b706a95828446b` |
| `a53d7fd62d2068a7b13ced9b38cdb5a86b2d489b` | `a1fd026d2517148bae8e2a6c9284921d6a3d0543` |
| `89aaef630fd7e3e2b0a84480f5929329a2dc8228` | `1b737b416b4112ed15336495a3158b8443dc1944` |
| `725cc78226d92a14345e2524ede735d19c87ad79` | `5568a836eba751b37924ee650cf238aa96290d1b` |
| `0971ad4674dc950e0c63ab6f9bfb894c74f10c65` | `114cd5e24c10deef9fd301891124eb68481b0f9c` |
| `082357527124e140b2f0f29a54fc1d84c9c645e4` | `26021c52143656db8ac0dceb4135c6db18420551` |
| `9c12eda9fea2af23407f3396c3c0021d7e231321` | `cc653723be379ba13efe5da02dae64e039f62a6f` |
| `e743f4b8c08090ef2cee7e81b58e437aff921238` | `74b2b1d22b594ee189c807b3bcce27fe95bc5265` |
| `d82fb75a9ad628c76e68415dcdce63f55db08be1` | `f24fb17c6f10c440bbd8d78ed0bf5fc9bb8bd7e2` |
| `cccc161b0557e4dccaceadc77a7d17d6b4de698b` | `5718f036ca39bfcb5a7afd85c247ee879aa39c40` |
| `21915d1fa19ebc81a4b5973fb5b715c6a85ec385` | `4ffc5a7f3038d1b42b9c6a4c331c89d7ee7f6368` |
| `583daae9317a77cbb47ae0092b40124e9433d18a` | `250b14519c38832646e38865d6ef1c30e7666b51` |
| `e78c5ac7e84f7e0e5eee450bff349dc4575c9c0f` | `87df8488d80a5d5efea931f2d6c9648a959b68cb` |
| `fb75d659bf331ce2c86659bcd96922a3ab004174` | `1b4ff02f527e6d314619c3209e34e08ed7bec753` |
| `aae4aaf66601658fccc34b2af8e26028300ab05e` | `d9e9d5c631a0cf7192af497e88aeb6a6580fc4c5` |
| `3d94f62bd4404302b5d1e82fb21aa384e63149ba` | `a74cdf48d82438a824522797a24bc28da68ec071` |
| `61b0e10cfd935d96f0f227d5591acb697962523b` | `94ff9827da3a8eef8c4e0a50e0e7200bff25f751` |
| `26335a9b50f24d10e4af865e7b55e86f5655c69b` | `30b5ebd4da8e13f735100625cc74f64df227d893` |
| `1d48c71a125adfc03a94a431d862998e091d9f1a` | `4e94456a1ddb3981d4847a6b742d7fc8d1204358` |
