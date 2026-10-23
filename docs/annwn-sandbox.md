# ANNWN sandbox settings

Applied October 23, 2026 from the supplied Sandbox Options screenshot with four requested exceptions: daytime trader hours, traders not claimable, crafting maximum quality 5, and daytime enemy respawning off. This is server configuration; no new client mod download is required.

The preset is stored in [annwn-sandbox.json](../config/annwn-sandbox.json) and the copyable native [sandbox code](../config/annwn-sandbox-code.txt). The active property is `SandboxCode` in `/srv/seven-days-beyond/config/multiworld.xml`. MultiWorld's four maps share these global sandbox rules; map identities and world saves are unchanged. Screenshot settings replace the earlier damage-boost testing preset; options not shown return to game defaults.

| Setting | Value |
|---|---|
| Ranged / melee damage | 85% / 85% |
| XP multiplier | 150% |
| Lose on death / death loss count | All categories / 1–3 items |
| Death degradation / amount | Durability / 5% |
| Drop on death | Backpack only |
| Stack size multiplier | 200% |
| Jar refund | 50% |
| Daytime biome enemy spawning | None; see below |
| Night enemy respawn | Very Slow |
| Enemy damage dealt | 125% |
| Entity health bars | Yes |
| AI smell mode | None |
| Zombie digging | No |
| Blood Moon frequency | Disabled |
| Air drop variance | Any |
| Storm frequency | 50% |
| Loot respawn | Disabled |
| Loot bag drop | 150% |
| Crafting max quality | 5 |
| Smelter type | Recipes |
| Repair degradation | 5% |
| Trader hours | Native daytime schedule: 04:05–21:50 |
| Trader protection | Not Claimable |
| Trader max quality | 3 |
| Trader item count | Low |
| Trader reset | 4 days |
| Trader buy limit | 7 |
| Quests per tier / per day | 5 / 6 |
| Base skill points | 5 |
| Vehicle fuel usage / self damage | 65% / 50% |

The installed version's Day Enemy Respawn setting only offers Default through Very Fast; it has no Off value. `BiomeDayEnemyDensity=-1` (None) prevents daytime ambient biome enemy spawning. The day respawn interval is left at Default and nighttime respawn at Very Slow. Existing enemies, POI sleepers and quest-triggered enemies are not removed by this biome setting.

Trader Hours uses the native XML schedule, rather than Always Open; the installed traders.xml defines 04:05–21:50. Trader Protection selects the explicit Not Claimable option (value 2), rather than Claimable.

All 36 explicit option values were round-tripped through the pinned game's actual `SandboxOptionManager.SetupOptions` and `LoadOptionsFromCode`, with exact type/value comparisons. The code uses the game's native option IDs and value indices, not the differing numbering on third-party editors.

Before editing the live configuration, the isolated service was stopped cleanly and saves, settings and the prior log were backed up to `/srv/seven-days-beyond/backups/before-sandbox-settings-20261023T054311814290Z.tar.gz`. Only the SandboxCode property changed, then the same service was restarted. Server startup logs provide the effective sandbox option values for verification.

After restart, all 36 effective option indices in the server’s own Sandbox Options log matched the verified preset. The service is active and all four worlds reached MULTIWORLD_READY; native quest serialization and all four safe-landing probes passed.
