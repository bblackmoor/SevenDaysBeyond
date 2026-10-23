# Inspection inventory and evidence ledger

Baseline commit: `61b0e10cfd935d96f0f227d5591acb697962523b`. Analysis date: 2026-10-23.

This ledger preserves identifiers, coverage and unresolved items after source removal. It is not executable code. Line numbers refer only to the inspected reconstruction of MultiWorldProbe.cs, not to future replacement code. The behavior specifications can be used without keeping those files.

## Coverage and limits

- Inspected configuration and server-option parsing, all custom packet schemas, the core ownership/lifecycle/transfer/network/storage/cache implementations, and the current companion integration sources.
- Indexed all 119 nested Harmony patch classes and 176 top-level static methods, including diagnostic fixtures. An index entry is not evidence that its fixture ran.
- Three patch attributes are undecoded in the reconstructed file: pathfinder-thread construction, loot-manager construction, and trader-manager construction. Their method bodies identify the intended ownership binding, but exact constructor/overload signatures must be resolved from native metadata.
- No upstream source, binary, IL or archive is reproduced in this documentation. The symbol index is for tracing responsibilities, not for mechanically reconstructing the original class structure.
- No game build, client session, server restart or new runtime acceptance test was performed for this documentation. Existing test results are described separately in the implementation plan.
- Reconstructed expressions and the original decompiler framework/reference hints are not a reliable maintained build target.

## Main evidence regions

| Source region (baseline lines) | Responsibility | Documentation |
| --- | --- | --- |
| 37–353 | Context records, save/water/time/world lookups, worker ownership and shutdown | Architecture: ownership and lifecycle |
| 354–714 | Animation/scene placement, navigation ownership and constraints | Architecture: physics/navigation |
| 715–1083 | Shared checkpoint interception, context/coroutine scopes, world-filtered networking and world-info snapshots | Contracts: saves and routing |
| 1180–1315 | Transfer state record, spawn commit and checkpoint generation | Contracts: transfer state machine |
| 1316–2149 | Per-world service state, Unity callbacks, once-per-frame work, environment, chunk/stability and late combat safeguards | Architecture: isolation/scheduling |
| 2234–2719 | Session generations, client lifecycle, definitions/map cache hooks and quest overlays | Contracts: session and caches |
| 2722–3181, 3313–3441 | Logical parties, remote UI, allies/voice and empty-world time/horde guards | Architecture: social and clocks |
| 3442–3657 | Dedicated map generation hooks, global player list, catalogue and download hooks | Contracts: map acquisition |
| 3658–4018 | Runtime registries/options, initialization, error guard and event registration | Architecture: startup |
| 4019–4536 | Multiworld construction, scoped selection, cleanup, scenes and physics routing | Architecture: lifecycle/physics |
| 4617–5030 | Native reference rewriting and graph/node owner lookup | Architecture: mutable engine state |
| 5030–5175, 5254–5337 | Character save/restore and join/world resolution | Contracts: persistence and join |
| 5467–6206 | Commands, transfer/recovery, preferred-world persistence, service-slot setup | Contracts and architecture |
| 6367–6409, 7017–7210 | Scheduler, service validation, content mappings and sky-field initialization | Architecture and mapping safety |
| 7472–7488, 7598–8107 | Dead-attacker retention and full session/client/config handling | Architecture and contracts |
| 8108–8343 | Map catalogue, safe paths, RAM preload, stale-cache checking | Contracts: caching |
| 8775–9444 | Native options, trader-point overlay, social projections and shared allies | Contracts: options/progression; architecture: social |
| 10117–10163, 10337–10967 | Clock policy, map preparation, presence and verified downloads | Architecture and contracts |

Interspersed Verify/Test routines include navigation, collision, shared character, network routing, command/transfer/first-join, actor movement/combat, chunk lifecycle, warm-cache, capacity, quest-progress, vehicles, social and clock fixtures. They are upstream diagnostics to replace with our own behavior-based tests. Vehicle fixtures exercise local persistence/routing, not mounted cross-world travel.

## Dynamic hook families

| Family | Native surfaces selected at runtime | Replacement obligation |
| --- | --- | --- |
| OwnedWorkerScope | ChunkManager lighting/calculation/position/regeneration/bake workers; generated-world chunk worker | Carry owner across background execution |
| NavigationOwnerScope | Declared instance methods on AstarManager, AstarVoxelGrid, ASPPathFinder, ASPPathNavigate and ASPPathFinderThread, including nested types | Correct worker/callback owner |
| ScopeBroadcasts | ConnectionManager SendPackage overloads | Apply world recipient filtering |
| ServiceOwnerScope | Instance methods of registered owned-service types | Enter service's world with finalizer restoration |
| ScopeUnityCallbacks | Game MonoBehaviour update/collision/trigger/controller callbacks | Select component owner and reject closed/uninitialized state |
| OncePerServerFrame | Options, loaders, platform, invites, main-thread pump, global actions, pause | Run process-global work once |
| TraceChunkGenerationFailure | Generation/terrain/decorate/sync operations on generated/raw providers, ChunkCluster.AddChunkSync, ChunkManager.GetNextChunkToProvide, DynamicPrefabDecorator.DecorateChunk | Record owning stage and exception; diagnostics, not successful generation evidence |
| OwnedWorldEntityLifecycle | World entity lifecycle operations | Scope cleanup/spawn to explicit world |
| ScopeStabilityCalculation | Stability calculator operations | Use owned stability queues/world |
| ScopeServiceCoroutines / ScopeQuestBuildingReset | Owned service enumerators and World.ResetPOIS | Re-enter owner on continuation/disposal |
| SessionPacketIngress / Egress | Native processing and network send-queue call sites | Enforce epochs and packet ownership |
| CancelObsoleteClientLoad | worldInfoCo, chunkClusterInfoCo, startGameCo | Cancel superseded client loads |
| ConsolidateMapLoadCollections | ChunkProviderGenerateWorldFromRaw.Init enumerator MoveNext, two audited collection calls | Optional optimization only |
| SharedGlobalTraderPoints | EntityTrader.PopulateActiveQuests and GetQuestFactionPoints | Add overlay without persisting aggregate locally |
| UseLogicalPartyLeadershipInUi | PartyVoice.playerJoinedParty and XUiC_PlayersList.updatePlayersList | Respect logical leader outside local projection |
| Runtime physics/world routing installers | Engine physics wrappers and game methods accessing registered fields/getters/default scene | Complete audited isolation, including reads/writes/by-reference semantics |

Exact generated native method counts depend on game assemblies and must be measured during adapter installation, not assumed from this static class count.

## Harmony patch declaration index

| Evidence class | Baseline line | Native target or selection |
| --- | --- | --- |
| `BindSecondWorld` | 79 | World.Init |
| `ScopedSavePath` | 92 | GameIO.GetSaveGameDir |
| `ScopedWater` | 107 | WaterSimulationNative.get_Instance |
| `ScopedTime` | 122 | GameTimer.get_Instance |
| `BoundAutomaticFrames` | 137 | GameManager.gmUpdate |
| `ForceExperimentalTicks` | 154 | GameTimer.updateTimer |
| `SelectWorldPreferences` | 175 | GamePrefs.GetString |
| `WorkerWorldAccessor` | 207 | GameManager.get_World |
| `OwnedWorkerScope` | 221 | Dynamic target enumeration; see family description |
| `CloseAllWorlds` | 253 | GameManager.SaveAndCleanupWorld |
| `CloseSharedNavigationServiceLast` | 310 | AstarManager.Cleanup |
| `InstantiateEntityInOwnedScene` | 433 | CreateEntityOperation.CompleteEntity |
| `MoveEntityRoots` | 460 | EntityFactory.Init |
| `MoveChunkToWorldScene` | 479 | ChunkGameObject.SetChunk |
| `BindNavigationGraph` | 602 | AstarVoxelGrid.Init |
| `BindNavigationManager` | 616 | AstarManager.Awake |
| `RestrictNavigationPath` | 629 | AstarPath.StartPath |
| `RestrictGraphUpdate` | 643 | AstarPath.UpdateGraphs (explicit overload) |
| `NavigationOwnerScope` | 656 | Dynamic target enumeration; see family description |
| `SaveSharedCheckpoint` | 715 | PlayerDataFile.Save |
| `ReadSharedCheckpoint` | 727 | PlayerDataFile.Load |
| `SharedCharacterExists` | 741 | PlayerDataFile.Exists |
| `RememberInitialWorld` | 753 | GameManager.PlayerSpawnedInWorld |
| `FilterClientListForWorld` | 851 | NetPackageClientInfo.Setup |
| `SendPeriodicClientInfoToEveryWorld` | 883 | ConnectionManager.SendPackage (explicit overload) |
| `ScopeIncomingPackets` | 917 | ConnectionManager.ProcessPackages |
| `ScopeClientDisconnect` | 932 | ConnectionManager.DisconnectClient |
| `ScopeJoinCoroutine` | 956 | GameManager.RequestToEnterGame |
| `ScopeBroadcasts` | 968 | Dynamic target enumeration; see family description |
| `ScopeDirectPackets` | 998 | ClientInfo.SendPackage |
| `SnapshotWorldInfo` | 1022 | NetPackageWorldInfo.Setup |
| `WriteOwnedWorldInfo` | 1056 | NetPackageWorldInfo.write |
| `TransferSpawning` | 1211 | GameManager.RequestToSpawnPlayer |
| `CommitSessionTransfer` | 1224 | GameManager.PlayerSpawnedInWorld |
| `WorldSwitchIsNotServerLogin` | 1262 | GameManager.DisplayGameMessage |
| `CountCharacterCheckpoints` | 1290 | GameManager.SavePlayerData |
| `ServiceOwnerScope` | 1350 | Dynamic target enumeration; see family description |
| `ScopeUnityCallbacks` | 1397 | Dynamic target enumeration; see family description |
| `OncePerServerFrame` | 1490 | Dynamic target enumeration; see family description |
| `ObserveAvatarUpdates` | 1540 | AvatarZombieController.Update |
| `ObserveRootMotion` | 1555 | EntityAlive.NotifyRootMotion |
| `ObserveActorPaths` | 1575 | ASPPathFinder.OnPathFinished |
| `CreateOwnedWeather` | 1646 | WeatherManager.Init (explicit overload) |
| `BindPathfinderAtConstruction` | 1669 | Undecoded attribute; resolve native metadata |
| `BindLootAtConstruction` | 1682 | Undecoded attribute; resolve native metadata |
| `BindTraderAtConstruction` | 1694 | Undecoded attribute; resolve native metadata |
| `BindWeatherBeforeInitialization` | 1706 | WeatherManager.Init (explicit overload) |
| `MultiWorldBrowserLabel` | 1718 | GameServerInfo.BuildGameServerInfo |
| `GlobalServerBrowserCount` | 1730 | GameServerInfo.UpdateGameTimePlayers |
| `SaveBothWorldsFromConsole` | 1747 | GameManager.SaveWorld |
| `PersistContentMappingsForBothWorlds` | 1770 | NameIdMapping.SaveIfDirty |
| `SaveContentMappingCheckpoints` | 1799 | World.Save |
| `ApplyConfiguredPrimaryWorld` | 1812 | GameManager.StartAsServer |
| `BindLoadedSky` | 1851 | SkyManager.Loaded |
| `UseOwnedSkyRoot` | 1870 | SkyManager.Init |
| `TraceChunkGenerationFailure` | 1890 | Dynamic target enumeration; see family description |
| `OwnedWorldEntityLifecycle` | 1930 | Dynamic target enumeration; see family description |
| `ResetChunkWithoutWorldNotification` | 1953 | Chunk.Reset |
| `RecordDeadMeleeAttacker` | 1997 | EntityAlive.SetDead |
| `RecordRemovedDeadAttacker` | 2009 | World.RemoveEntity |
| `ForgetReusedAttackerId` | 2024 | World.SpawnEntityInWorld |
| `RejectLateDeadMelee` | 2039 | NetPackageRangeCheckDamageEntity.ProcessPackage |
| `BindStabilityWorld` | 2069 | StabilityCalculator.Init |
| `ScopeStabilityCalculation` | 2083 | Dynamic target enumeration; see family description |
| `ScopeServiceCoroutines` | 2111 | Dynamic target enumeration; see family description |
| `ScopeQuestBuildingReset` | 2140 | World.ResetPOIS |
| `SessionPacketIngress` | 2279 | ConnectionManager.ProcessPackages |
| `SessionPacketEgress` | 2303 | Dynamic target enumeration; see family description |
| `PreserveAuthenticatedLobby` | 2341 | GameManager.ShutdownMultiplayerServicesNow |
| `PreserveSessionResources` | 2350 | GameManager.LoadRemoteResources |
| `StartNextClientWorld` | 2359 | GameManager.StartAsClient |
| `ResetSessionForNormalLogin` | 2386 | ConnectionManager.Connect |
| `SpawnDuringSessionChange` | 2402 | GameManager.DoSpawn |
| `KeepAlreadyAcceptedServerRules` | 2421 | XUiC_ServerJoinRulesDialog.Show (explicit overload) |
| `CancelObsoleteClientLoad` | 2476 | Dynamic target enumeration; see family description |
| `ReuseSessionDefinitions` | 2497 | WorldStaticData.handleReceivedConfigs |
| `PreventSpawnAfterConfigFailure` | 2512 | WorldStaticData.AllConfigsReceivedAndLoaded |
| `TrackDefinitionMappings` | 2526 | GameManager.IdMappingReceived |
| `ReleaseCacheWhenLeavingServer` | 2549 | GameManager.SaveAndCleanupWorld |
| `OpenPreloadedMap` | 2564 | SdFile.OpenRead (explicit overload) |
| `ReadPreloadedMap` | 2579 | SdFile.ReadAllBytes (explicit overload) |
| `ReuseVerifiedMapChecksum` | 2594 | IOUtils.CalcCrcCoroutine |
| `ConsolidateMapLoadCollections` | 2615 | Dynamic target enumeration; see family description |
| `SharedTraderFactionPoints` | 2658 | QuestJournal.GetQuestFactionPoints |
| `SharedGlobalTraderPoints` | 2674 | Dynamic target enumeration; see family description |
| `CopyQuestOverlay` | 2698 | QuestJournal.Clone |
| `BindReceivedQuestProgress` | 2711 | GameManager.SavePlayerData |
| `CaptureAcceptedParty` | 2734 | Party.ServerHandleAcceptInvite |
| `CaptureAutoParty` | 2743 | Party.ServerHandleAutoJoinParty |
| `GenuinePartyDisconnect` | 2779 | EntityPlayer.PartyDisconnect |
| `LogicalPartyLeader` | 2801 | EntityPlayer.IsPartyLead |
| `ValidateLogicalPartyActions` | 2825 | NetPackagePartyActions.ProcessPackage |
| `LogicalLeaderWindowBinding` | 2920 | XUiC_PartyWindow.GetBindingValueInternal |
| `FillRemotePartyRows` | 2963 | XUiC_PartyEntryList.RefreshPartyList |
| `RemotePartyRowBindings` | 3028 | XUiC_PartyEntry.GetBindingValueInternal |
| `ClearRemotePartyRow` | 3093 | XUiC_PartyEntry.SetPlayer |
| `RemotePartyRowUpdate` | 3102 | XUiC_PartyEntry.Update |
| `MirrorAlliedPlayers` | 3146 | AllyStore.AllyUpdateResponse |
| `ObserveTestVehicleUnload` | 3198 | VehicleManager.RemoveTrackedVehicle |
| `TraceCombatAttack` | 3220 | EntityAlive.Attack |
| `TraceCombatRay` | 3240 | ItemActionMelee.GetExecuteActionTarget |
| `TraceCombatImpact` | 3279 | AvatarZombieController.SetAttackImpact |
| `ObserveCombatTestAttack` | 3296 | EntityPlayer.ServerNetSendRangeCheckedDamage |
| `UseLogicalPartyLeadershipInUi` | 3313 | Dynamic target enumeration; see family description |
| `PromoteLogicalVoiceLeader` | 3337 | PartyVoice.promoteLeader |
| `KeepVoiceLobbyDuringWorldSwitch` | 3365 | PartyVoice.gameEnded |
| `EmptyWorldDayClock` | 3392 | GameManager.updateTimeOfDay |
| `BloodMoonRequiresLocalPlayer` | 3416 | AIDirectorBloodMoonParty.SpawnZombie |
| `EmptyBloodMoonPartyDoesNotSpawn` | 3430 | AIDirectorBloodMoonParty.Tick |
| `GenerateConfiguredWorldMaps` | 3442 | MainMenuMono.startDedicatedServer |
| `GlobalPlayersList` | 3454 | XUiC_PlayersList.updatePlayersList |
| `InviteGlobalAlly` | 3535 | XUiC_PlayersList.AddInvitePress |
| `SendCatalogueBeforeWorldInfo` | 3558 | ClientInfo.SendPackage |
| `ResetJoinMapCatalogue` | 3570 | ConnectionManager.Connect |
| `DownloadEveryMapBeforeSpawning` | 3584 | GameManager.worldInfoCo |
| `ScopeNativeMapPreparation` | 3596 | NetPackageWorldFolder.prepareWorldFolderData |
| `ScopeNativeMapSending` | 3609 | NetPackageWorldFolder.sendPacketsToClient |
| `PauseBackgroundOnWorldCleanup` | 3622 | GameManager.SaveAndCleanupWorld |
| `RevalidateBackgroundMapVersion` | 3634 | GameManager.WorldInfo |

## Top-level method index

Only symbol names and positions are retained; no method bodies. Fixture names identify diagnostic scope, not a passing test.

| Symbol | Baseline line |
| --- | --- |
| `ActiveContext` | 3918 |
| `OnStart` | 3944 |
| `Write` | 3959 |
| `Guard` | 3966 |
| `Run` | 4019 |
| `Select` | 4158 |
| `RunActiveTicks` | 4184 |
| `CloseAdditionalWorld` | 4295 |
| `InitializeWorldScene` | 4419 |
| `GetScopedPhysicsScene` | 4467 |
| `SimulatePhysics` | 4479 |
| `InstallPhysicsRouting` | 4490 |
| `PhysicsSceneTranspiler` | 4523 |
| `VerifyPhysicsRouting` | 4537 |
| `ScopedAstar` | 4617 |
| `ScopedPathfinder` | 4622 |
| `ScopedWeather` | 4627 |
| `ScopedEntityParents` | 4637 |
| `ScopedWorldField` | 4642 |
| `WalkTypes` | 4647 |
| `InstallWorldReferenceRouting` | 4659 |
| `WorldReferenceTranspiler` | 4808 |
| `OwnerOf` | 4845 |
| `GraphBelongsTo` | 4907 |
| `NodeBelongsTo` | 4916 |
| `VerifyWorldNavigation` | 4926 |
| `CharacterKey` | 5030 |
| `SaveSharedCharacter` | 5040 |
| `CopyCharacterFields` | 5088 |
| `RestoreSharedCharacter` | 5100 |
| `VerifySharedCharacter` | 5176 |
| `DefaultContext` | 5254 |
| `ResolveClientWorld` | 5266 |
| `CanBroadcastTo` | 5297 |
| `WaitForWorldsAndJoin` | 5315 |
| `VerifyNetworkRouting` | 5338 |
| `ParseWorldCommand` | 5467 |
| `Reply` | 5491 |
| `WorldLabel` | 5503 |
| `AnnounceWorldArrival` | 5508 |
| `SendWorldCatalogue` | 5532 |
| `OnWorldCommand` | 5546 |
| `WorldListLines` | 5647 |
| `TransferTimeout` | 5666 |
| `SendTransferResult` | 5684 |
| `HandleTransferTimeout` | 5697 |
| `PrepareClientTransfer` | 5718 |
| `ClientTransferTimeout` | 5740 |
| `AcceptTransferCheckpoint` | 5757 |
| `DetachPlayerForWorldChange` | 5772 |
| `BeginSessionWorldChange` | 5808 |
| `AcceptClientWorldReady` | 5824 |
| `AcceptClientLoadFailure` | 5835 |
| `AcceptTransferGameplay` | 5844 |
| `CompleteTransferAfterDisconnect` | 5865 |
| `LoadPreferredWorlds` | 5876 |
| `SavePreferredWorld` | 5921 |
| `FieldKey` | 5978 |
| `AddServiceField` | 5983 |
| `ConfigureServiceFields` | 5993 |
| `OwnService` | 6065 |
| `CapturePrimaryServices` | 6134 |
| `InitializeSecondaryServiceSlots` | 6144 |
| `VerifyServiceIsolation` | 6207 |
| `InitializeSecondaryServices` | 6245 |
| `RewriteServiceField` | 6268 |
| `FindServiceOwner` | 6292 |
| `VerifyPhysicalCollisions` | 6310 |
| `UpdateBothWorlds` | 6367 |
| `VerifyContinuousScheduler` | 6410 |
| `VerifyWorldCommand` | 6441 |
| `TransferTestStep` | 6542 |
| `VerifyFirstJoinWorld` | 6694 |
| `RunActorBaseline` | 6761 |
| `SpawnTestActors` | 6800 |
| `RemoveTestActors` | 6994 |
| `ValidateWorldServices` | 7017 |
| `EnsureAllContentMappings` | 7026 |
| `ReadMappingEntries` | 7072 |
| `ValidateMappingSubset` | 7104 |
| `WriteMappingAtomically` | 7116 |
| `PersistSharedMapping` | 7149 |
| `ConfigureSkyFields` | 7187 |
| `InitialSkyValue` | 7201 |
| `VerifySkyIsolation` | 7211 |
| `ReportChunkFailure` | 7276 |
| `SetResetStabilityState` | 7296 |
| `ValidateMultiBlockService` | 7308 |
| `VerifyChunkLifecycle` | 7341 |
| `RememberDeadAttacker` | 7472 |
| `VerifyServerSpawnChunks` | 7489 |
| `IsConnectionPacket` | 7598 |
| `IsTransferControl` | 7607 |
| `SessionEpoch` | 7616 |
| `QueueSessionPacket` | 7621 |
| `ProcessSessionPacket` | 7667 |
| `ProcessSessionBytes` | 7701 |
| `ShouldProcessSessionPackage` | 7740 |
| `SwitchClientWorld` | 7749 |
| `ChangeClientWorld` | 7762 |
| `AssertClientConnectionRetained` | 7794 |
| `ClientWorldResult` | 7811 |
| `CaptureWarmDefinitions` | 7847 |
| `SameBytes` | 7898 |
| `CanReuseWarmDefinitions` | 7918 |
| `LoadWarmWorldConfigs` | 7944 |
| `GuardWarmConfigLoad` | 8050 |
| `ReceiveMapCatalogue` | 8108 |
| `ResetMapPreload` | 8123 |
| `PlainPathPart` | 8134 |
| `PreloadClientMaps` | 8143 |
| `PreloadMapFiles` | 8186 |
| `TryMapFile` | 8289 |
| `TryCurrentMapFile` | 8299 |
| `CollectBetweenWorldLoadStages` | 8336 |
| `VerifyWarmCacheSafety` | 8344 |
| `VerifyWorldCapacityContracts` | 8481 |
| `Verify64NavigationOwners` | 8650 |
| `ExpectInvalidConfiguration` | 8691 |
| `VerifyLoadedWorldSet` | 8704 |
| `VerifyAllWorldsScheduler` | 8741 |
| `LoadServerOptions` | 8775 |
| `LocalQuestPoints` | 8804 |
| `ReadLocalQuestPoints` | 8813 |
| `BuildQuestOverlay` | 8856 |
| `BindQuestOverlay` | 8878 |
| `BindServerQuestProgress` | 8887 |
| `GetQuestOverlay` | 8895 |
| `ReceiveQuestProgress` | 8916 |
| `SendQuestProgress` | 8932 |
| `TotalQuestPoints` | 8951 |
| `ClientForPlayer` | 8965 |
| `SocialPlayer` | 8981 |
| `CaptureSocialParty` | 8996 |
| `DetachSocialPlayer` | 9049 |
| `RemoveSocialMember` | 9057 |
| `TickSocialParties` | 9090 |
| `RefreshSocialParties` | 9100 |
| `ApplyPartySnapshot` | 9249 |
| `InitializeSharedAllies` | 9338 |
| `PersistSharedAllies` | 9410 |
| `VerifyQuestProgressToggles` | 9445 |
| `AddCompletedProgressQuest` | 9565 |
| `VerifyVehicleCallRouting` | 9576 |
| `HandleVehicleTestCommand` | 9621 |
| `VerifyVehicleRestart` | 9720 |
| `HandleCombatTestCommand` | 9762 |
| `VerifySocialTransfers` | 9947 |
| `ClientLogicalLeader` | 10093 |
| `ShouldAdvanceWorldTime` | 10117 |
| `DedicatedWorldTimePaused` | 10141 |
| `IsPresentBloodMoonTarget` | 10155 |
| `VerifyEmptyWorldTime` | 10164 |
| `VerifyClockWindow` | 10298 |
| `PrepareWorldMaps` | 10337 |
| `GenerateWorldMaps` | 10357 |
| `ValidateMapFiles` | 10417 |
| `CollectPlayerPresence` | 10443 |
| `ApplyPlayerPresence` | 10499 |
| `Presence` | 10516 |
| `SortGlobalPlayers` | 10530 |
| `PrepareJoinMaps` | 10557 |
| `DownloadJoinCatalogue` | 10587 |
| `TrackNativeMapSend` | 10604 |
| `MapGuidValid` | 10623 |
| `MapFileValid` | 10629 |
| `MapManifest` | 10648 |
| `SendBackgroundMap` | 10678 |
| `ReceiveBackgroundMap` | 10736 |
| `StopBackgroundMaps` | 10753 |
| `DownloadActive` | 10767 |
| `RequestBackgroundMap` | 10776 |
| `ManifestStamp` | 10799 |
| `DownloadMapCatalogue` | 10805 |
| `VerifyMapOnDisk` | 10944 |
| `VerifyMapFile` | 10949 |

## Source provenance ledger

Git blob IDs identify the exact inspected snapshot files. They are identifiers, not dependencies required by the independent replacement. The DLL SHA-256 and archive SHA-256 are retained below for externally obtained reference identification.

- Upstream listing: https://www.nexusmods.com/7daystodie/mods/13135 (provenance only; not queried for current permissions/version during this task).
- Supplied DLL SHA-256: `946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6`.
- Supplied archive SHA-256: `7325f3992ee269d044986feccf1e1bade8783bc562fe4ae81686e2594f97ac33`.

| Original repository path | Git blob ID | Bytes |
| --- | --- | --- |
| `resources/multiworld/README.md` | `38420803237263914ff030336829d20ac15e3607` | 3094 |
| `resources/multiworld/analysis/LITWMultiWorldProbe.il` | `4cb229c65ad6bd080412ede24fba7edf7b4ff334` | 2805185 |
| `resources/multiworld/analysis/SHA256SUMS` | `9e52c9deb135503c4695c0942292cd9c07e4f811` | 427 |
| `resources/multiworld/analysis/assembly-references.json` | `f48b38e0efa0a5cad065dd8398f87a6528e0e16d` | 987 |
| `resources/multiworld/analysis/decompilation.json` | `043616a7549334952a2e1216abe3b1feca51ecf0` | 600 |
| `resources/multiworld/analysis/feasibility.md` | `45e5d46ab9e2cc127d7794a2448b7ad260d8466d` | 2953 |
| `resources/multiworld/decompile.sh` | `52a996adb418fb5a6bff714994924f5b1ce4613c` | 471 |
| `resources/multiworld/decompiled/LITWMultiWorldProbe.csproj` | `58775a739e030ad019ba17a287509a941b6cf03d` | 1417 |
| `resources/multiworld/decompiled/MapCacheTestStream.cs` | `df8ebd43f88a4e9d8dd98409aff8732711fa06d3` | 268 |
| `resources/multiworld/decompiled/MultiWorldConfiguration.cs` | `6e9f0e786e00bc129e34665fbfbcd13c7614bfd3` | 7760 |
| `resources/multiworld/decompiled/MultiWorldMapFile.cs` | `8d8e2eca9f4c318eff2c37801be2fdc36dabef17` | 108 |
| `resources/multiworld/decompiled/MultiWorldPartyMember.cs` | `53899c28de44c90af785389761cf942c9aff597e` | 395 |
| `resources/multiworld/decompiled/MultiWorldPlayerPresence.cs` | `510564a0f71773bea3e9dd35cd4ea7f742516017` | 326 |
| `resources/multiworld/decompiled/MultiWorldProbe.cs` | `ba8192f31cdd2b0106a586a86dcfe8b523895826` | 379850 |
| `resources/multiworld/decompiled/MultiWorldServerOptions.cs` | `15f3c3383cfc3909ef84ca8d986af1f2c8156680` | 9284 |
| `resources/multiworld/decompiled/NetPackageMultiWorldCatalogue.cs` | `9aa19d5b17900336ac707e160ce3ac05e0d34950` | 1631 |
| `resources/multiworld/decompiled/NetPackageMultiWorldCheckpointAck.cs` | `4d6acf10f1ffa9b5dda196c43f1a131cc665f6dd` | 655 |
| `resources/multiworld/decompiled/NetPackageMultiWorldEnvelope.cs` | `14aa6d5830fd499ea9b9ed832079eef619965780` | 2870 |
| `resources/multiworld/decompiled/NetPackageMultiWorldLoadFailed.cs` | `16e24ab26f3315d9dd6fc7fb57e465d64ec4ebd2` | 781 |
| `resources/multiworld/decompiled/NetPackageMultiWorldMapData.cs` | `859eba9e048bb26022ffde26a7fa2d2e64f2f5bf` | 3176 |
| `resources/multiworld/decompiled/NetPackageMultiWorldMapRequest.cs` | `f3bdcffcf928d91bebee6368b27f77249c5e7819` | 1068 |
| `resources/multiworld/decompiled/NetPackageMultiWorldParty.cs` | `90ff3f20b12e7e7c5cdba1b68198e95237efee4b` | 3611 |
| `resources/multiworld/decompiled/NetPackageMultiWorldPlayers.cs` | `e9f712710c303f6d20d37b4a4a0f1fb2847bd26c` | 2833 |
| `resources/multiworld/decompiled/NetPackageMultiWorldPrepare.cs` | `7d988f1894e90301ab8fd750dbd028124421f34f` | 777 |
| `resources/multiworld/decompiled/NetPackageMultiWorldQuestProgress.cs` | `b3269a02d100c4505924b15d175fac970a92808b` | 1957 |
| `resources/multiworld/decompiled/NetPackageMultiWorldReady.cs` | `7304520312c4307b49deaccdfc853fd00ead543f` | 775 |
| `resources/multiworld/decompiled/NetPackageMultiWorldResult.cs` | `f500959213de5c00b38e61329e72c8cdf97047b9` | 897 |
| `resources/multiworld/decompiled/NetPackageMultiWorldSwitch.cs` | `14265e7cee5881fbdf5c26e491901b18e3b1745c` | 902 |
| `resources/multiworld/decompiled/NetPackageMultiWorldTest.cs` | `36fbcf5b3bae710d73c5a280a14bb03b96a886df` | 406 |
| `resources/multiworld/decompiled/Properties/AssemblyInfo.cs` | `a79df310aaceda1beb1ad966bc5fc4f919316997` | 159 |
| `resources/multiworld/original/LITW_MultiWorld-0.6.2.rar` | `507357b505ec1c446738c76658ac999a3e07b7f4` | 111264 |
| `resources/multiworld/original/LITW_MultiWorld/LITWMultiWorldProbe.dll` | `019a7d8678eb1391e0b731d75ab508d98891fabc` | 291840 |
| `resources/multiworld/original/LITW_MultiWorld/ModInfo.xml` | `f8e8e3cf557cbec34e4994d8f2a011d3f6aac315` | 368 |
| `resources/multiworld/original/LITW_MultiWorld/serverconfig.xml` | `0ae1d6ee6856babd63e12c754af29c636488a81f` | 1357 |

