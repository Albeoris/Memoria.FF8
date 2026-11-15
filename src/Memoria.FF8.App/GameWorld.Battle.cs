using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static byte[] EncodeScanName24(string name)
    {
        byte[] encoded = Albeoris.Games.FF8.TextEncoding.FF8Encoding.CreateEuropean(Albeoris.Games.FF8.TextEncoding.FF8TextLanguage.EN).GetBytes(name).Append((byte)0).ToArray();
        if (encoded.Length > 24)
        {
            throw new InvalidDataException("Scan player name exceeds its allocated twenty-four-byte owner.");
        }
        byte[] result = new byte[24];
        encoded.CopyTo(result, 0);
        return result;
    }

    private BattleAssets? battleAssets;
    private LiveMenuCatalog? battleCatalog;
    private BattleSession? battle;

    public BattleSession? Battle => battle;
    public BattleRewardSummary? LastBattleRewards { get; private set; }
    public BattleAssets BattleResources => battleAssets ??= new BattleAssets(assets.GameDirectory, resources, modelOverrides);

    public BattleRewardSummary PreviewBattleRewards()
    {
        return PreviewBattleResultOwners().Rewards;
    }

    internal BattleResultOwners PreviewBattleResultOwners()
    {
        BattleSession active = battle ?? throw new InvalidOperationException("Battle rewards require an active battle owner.");
        if (active.Outcome is not (BattleOutcome.Victory or BattleOutcome.Escaped))
        {
            throw new InvalidOperationException("Battle rewards are available after victory or escape.");
        }
        BattleEncounterOccurrence encounter = session.State.PendingBattleEncounter ?? throw new InvalidDataException("Battle rewards require the observed encounter occurrence.");
        return ProjectBattleResult(CaptureBattleOutcome(active, encounter));
    }

    private void ConfigureBattleHost()
    {
        BattleSnapshot? saved = session.State.Battle;
        RequireInstalledExpandedEnemyAi(saved?.Definition);
        if (saved is { Definition.NativeGil: null, NativeGil: null } && session.State.Economy is { } economy)
        {
            NativeBattleGil gil = new(economy.Gil, economy.PastGil);
            saved = saved with { Definition = saved.Definition with { NativeGil = gil }, NativeGil = gil };
        }
        if (saved is not null)
        {
            if (saved.Definition.UsesExpandedRoster)
            {
                NativeCharacterVitals current = session.State.CharacterVitals ?? throw new InvalidDataException("Saved expanded battle has no character owner.");
                byte[] boundRows = ExpandedBattlePartyRows.ReadBoundRows(saved.Definition, current);
                NativeCharacterVitals? frozen = saved.Definition.MagicVitals;
                saved = saved with
                {
                    Definition = saved.Definition with { MagicVitals = ExpandedBattlePartyRows.RecoverSelection(frozen!, boundRows) },
                    MagicVitals = saved.MagicVitals is { } magic ? ExpandedBattlePartyRows.RecoverSelection(magic, boundRows) : null
                };
            }
            saved = BattlePartyProjection.RestoreLegacyCommandProjection(saved, BattleResources);
            if (saved.Definition.UsesOriginalEnemySlots && saved.Definition.EncounterId is not null)
            {
                saved = saved with { Definition = BindOriginalEnemyAiResources(saved.Definition) };
            }
            if (saved.Definition.ExpandedEnemyLevels is null && saved.Definition.EncounterId is not null)
            {
                saved = saved with { Definition = BindExpandedEnemyLevels(saved.Definition) };
            }
            if (saved.Definition.EncounterId is not null)
            {
                BattleDefinition definition = saved.Definition;
                saved = saved with { Definition = BindExpandedEscapeResources(BindExpandedEnemyMug(BindExpandedEnemySpecials(definition)), definition.EncounterId!.Value) };
            }
        }
        battle = saved is null ? null : AttachNativeEffectResources(BattleSession.Restore(saved));
        synchronizedNativeBattleCallbacks = battle is null ? 0 : ReadExecutedBattleCallbacks(battle);
        if (battle is not null)
        {
            session.SetLiveBattle(battle);
        }
        Events.HostAutomaticBattles = true;
        Events.UseInteractiveBattle = (_, _) => session.State.FullBattlePlaythrough == true;
        Events.ValidateInteractiveBattle = (encounter, flags) =>
        {
            try
            {
                // Resource construction and validation belong to StartEncounter, before its atomic publication.
                _ = BattleResources.LoadEncounter(encounter);
                return null;
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or FileNotFoundException or ArgumentException)
            {
                return "Interactive battle cannot enter: " + exception.Message;
            }
        };
    }

    private BattleDefinition BindOriginalEnemyAiResources(BattleDefinition definition)
    {
        return definition with
        {
            Combatants = definition.Combatants.Select(Bind).ToArray()
        };

        BattleCombatantDefinition Bind(BattleCombatantDefinition actor)
        {
            if (actor.NativeAi is not { DeathSequenceReleasesAllocation: null } ai ||
                actor.SceneSlot is int sceneSlot && definition.AddedEnemySources?.ContainsKey(sceneSlot) == true)
            {
                return actor;
            }
            NativeBattleAiDefinition installed = BattleResources.ReadEnemyAi(actor.ModelId, actor.Level, includeDeathLifecycle: true);
            return actor with { NativeAi = ai with { DeathSequenceReleasesAllocation = installed.DeathSequenceReleasesAllocation } };
        }
    }

    public BattleDefinition CreateEncounterBattle(int encounterId, byte nativeSeed = 0, ushort activeFlags = 0, BattleDefinition? restoredDefinition = null)
    {
        RequireInstalledExpandedEnemyAi(restoredDefinition);
        NativeInputConfiguration input = session.InputConfiguration ?? throw new NotSupportedException("Battle command restrictions require the current native saved options owner.");
        input.Validate();
        byte fieldRules = input.CommandRestrictions;
        if ((fieldRules & 0x1F) != 0)
        {
            throw new NotSupportedException("Interactive battle requires native495530 sealed-command slot flags and admission; enabled commands cannot substitute for those restrictions.");
        }
        NativeCharacterVitals entryVitals = (session.State.CharacterVitals ?? throw new NotSupportedException("Battle entry requires the saved GF owner.")).RebuildParty();
        NativeEconomy entryEconomy = session.State.Economy ?? throw new NotSupportedException("Interactive battle requires the canonical economy owner.");
        NativeBattleModelCpuResources retainedCpu = session.State.NativeBattleMemory?.ModelCpuResources ?? throw new NotSupportedException("Battle entry requires its retained party CPU allocation.");
        string allocationImage = restoredDefinition is { UsesExpandedRoster: true }
            ? retainedCpu.Cpu.EffectiveLayout.ExecutableHash
            : restoredDefinition is not null
            ? ActiveBattleResourceCompatibility.AllocationImage(session.State.PendingBattleEncounter?.SourceScene == SceneKind.WorldMap ? null : Events.NativeRuntimeProfile, restoredDefinition.NativeGraphics?.Rules)
            : assets.NativeExecutableHash ?? throw new NotSupportedException("Battle entry requires an authenticated native image.");
        if (retainedCpu.Cpu.EffectiveLayout != NativeBattlePartyCpuLayout.ForImage(allocationImage) || restoredDefinition is { UsesExpandedRoster: true } && assets.NativeExecutableHash != allocationImage)
        {
            throw new NotSupportedException("Battle entry requires the edition's retained party CPU allocation; migrate this save explicitly.");
        }
        BattleEncounter encounter = effectPreviewRequest?.CreateArena() ?? BattleResources.LoadEncounter(encounterId);
        DiagnosticBattleRoster? diagnosticRoster = restoredDefinition is null ? diagnosticBattle?.Request.Roster : null;
        _ = BattleResources.LoadStage(encounter.StageId);
        BattleEncounterRosterAdmission rosterAdmission = restoredDefinition?.EncounterRosterAdmission ?? BattleEncounterRosterAdmission.Unrestricted;
        HashSet<int> reserveSlots = [];
        if (effectPreviewRequest is null && (restoredDefinition is null || restoredDefinition is { UsesExpandedRoster: true, UsesOriginalEnemySlots: true }))
        {
            (HashSet<int> referenced, BattleEncounterRosterAdmission inspected) = InspectEncounterRoster(encounter, model => BattleResources.ReadEnemyAi(model, 0), BattleResources.NativeActions);
            reserveSlots = referenced;
            if (restoredDefinition is null)
            {
                rosterAdmission = inspected;
            }
        }
        byte[] nativeParty = entryVitals.GetBattlePartyRows(3);
        byte[]? selectedParty = null;
        if (restoredDefinition is { UsesExpandedRoster: true })
        {
            selectedParty = ExpandedBattlePartyRows.ReadBoundRows(restoredDefinition, entryVitals);
            entryVitals = entryVitals with { AdditionalCombatParty = restoredDefinition.MagicVitals!.AdditionalCombatParty, AdditionalCombatPartyMinimumCapacities = restoredDefinition.MagicVitals.AdditionalCombatPartyMinimumCapacities };
        }
        else if (restoredDefinition is null && effectPreviewRequest is null)
        {
            selectedParty = diagnosticRoster is not null ? entryVitals.GetBattlePartyRows(NativeCharacterVitals.ExtendedRecordCount) :
                rosterAdmission.UsesPrimaryPartyOnly ? nativeParty : entryVitals.GetBattlePartyRows(EffectiveBattleAllyCount);
        }
        byte[]? expandedParty = selectedParty is not null && !selectedParty.SequenceEqual(nativeParty) ? selectedParty : null;
        int allyCount = expandedParty?.Length ?? nativeParty.Length;
        ImmutableSortedDictionary<int, BattleEnemyRosterSource>? addedEnemySources = restoredDefinition?.AddedEnemySources;
        if (diagnosticRoster is not null)
        {
            addedEnemySources = SelectDiagnosticRosterDonors(encounter, diagnosticRoster, reserveSlots);
        }
        else if (restoredDefinition is null && effectPreviewRequest is null && rosterAdmission.AllowsAdditionalEnemies)
        {
            int requested = Math.Max(defaultAdditionalEnemyCount, session.State.GameplayPreferences.GetEffectiveAdditionalEnemies(allyCount));
            if (requested > 0)
            {
                addedEnemySources = SelectAdditionalEnemies(encounter, requested, nativeSeed);
            }
        }
        bool expanded = restoredDefinition?.UsesExpandedRoster ?? (UseAttachedBattlePresentation || expandedParty is not null || addedEnemySources is { Count: > 0 });
        if (expanded && restoredDefinition is null)
        {
            addedEnemySources ??= ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty;
        }
        bool useOriginalEnemySlots = restoredDefinition?.UsesOriginalEnemySlots ?? expanded;
        BattleEnemyPlacement[]? expandedEnemies = null;
        if (expanded && useOriginalEnemySlots)
        {
            expandedEnemies = diagnosticRoster is null ? OriginalExpandedEnemyPlacements(encounter, restoredDefinition, reserveSlots) : SelectDiagnosticRosterOriginals(encounter, diagnosticRoster, reserveSlots);
            if (addedEnemySources is { Count: > 0 })
            {
                expandedEnemies = ResolveExpandedEnemyPlacements(encounter, addedEnemySources, originalSlots: true, originalPlacements: expandedEnemies, diagnosticRoster: diagnosticRoster is not null);
            }
        }
        else if (expanded)
        {
            expandedEnemies = ResolveExpandedEnemyPlacements(encounter, addedEnemySources ?? throw new NotSupportedException("Legacy expanded combat requires admitted enemy placement provenance."));
        }
        bool validExpandedRoster = expandedEnemies is not null && (useOriginalEnemySlots
            ? expandedEnemies.Length <= ExpandedBattleFormation.MaximumRewardSlots && ExpandedBattleFormation.SupportsRoster(allyCount, expandedEnemies.Count(enemy => enemy.Enabled))
            : ExpandedBattleFormation.SupportsRoster(allyCount, expandedEnemies.Length));
        if (expanded && !validExpandedRoster)
        {
            throw new InvalidDataException($"Expanded formation {encounterId} does not have a supported party and authored enemy roster.");
        }
        BattleEnemyPlacement[] entryPlacements = expanded ? expandedEnemies! : AuthoredEnemyPlacements(encounter).ToArray();
        foreach (BattleEnemyPlacement placement in entryPlacements)
        {
            RequireEnemyStatisticsContract(placement);
        }
        byte[] admittedParty = expandedParty ?? nativeParty;
        byte encounterAbilities = entryVitals.GetEncounterAbilityFlags(admittedParty);
        List<BattleCombatantDefinition> combatants = [.. CreateBattleParty(entryVitals, admittedParty)];
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(nativeSeed);
        InitializeNativePartyEntryGauges(combatants, ref random);
        byte[] partyLevels = combatants.Select(actor => checked((byte)actor.Level)).ToArray();
        foreach (BattleEnemyPlacement enemy in entryPlacements)
        {
            bool addedEnemy = expanded && addedEnemySources?.ContainsKey(enemy.Slot) == true;
            bool originalEnemy = useOriginalEnemySlots && !addedEnemy;
            _ = BattleResources.LoadEnemy(enemy.ModelId);
            byte roll = 0;
            byte? secondRoll = null;
            int randomBytes = enemy.Enabled ? BattleEnemyNativeStats.LevelRandomByteCount(checked((byte)enemy.Level)) : 0;
            if (randomBytes > 0)
            {
                random = random.Draw(out roll);
            }
            if (randomBytes == 2)
            {
                random = random.Draw(out byte second);
                secondRoll = second;
            }
            int level = enemy.Enabled ? (expanded ? NativeBattleEnemyLevelRules.ResolveExpanded(checked((byte)enemy.Level), partyLevels, roll, secondRoll) : BattleEnemyNativeStats.ResolveLevel(checked((byte)enemy.Level), partyLevels, roll, secondRoll)) : 0;
            byte entryRoll = 0;
            if (enemy.Enabled)
            {
                // Keep the allocation draw after its level rolls.
                random = random.Draw(out entryRoll);
            }
            BattleEnemyNativeProjection stats = BattleResources.ReadEnemyNativeStats(enemy.ModelId).Project(level);
            int initialAtb = enemy.Enabled ? BattleInitialAtb.Calculate(stats.Speed, entryRoll) : 0;
            NativeBattleStatusAllocation nativeStatuses = BattleResources.ReadEnemyNativeStatusData(enemy.ModelId);
            NativeBattleAiDefinition enemyAi = BattleResources.ReadEnemyAi(enemy.ModelId, level, includeDeathLifecycle: expanded && originalEnemy);
            if (expanded)
            {
                HashSet<string> blockers = new(StringComparer.Ordinal);
                ExpandedAiAdmission admission = NativeBattleExpandedAi.Evaluate(enemyAi, BattleResources.NativeActions, encounterId, blockers, originalEnemySlots: originalEnemy);
                if (admission != ExpandedAiAdmission.Supported)
                {
                    string detail = string.Join(", ", blockers.Order(StringComparer.Ordinal));
                    throw new NotSupportedException($"Expanded enemy AI for formation {encounterId}, scene slot {enemy.Slot}, model {enemy.ModelId}, level band {enemyAi.LevelBand} requires {admission}: {detail}.");
                }
            }
            combatants.Add(new BattleCombatantDefinition { Id = $"enemy-{enemy.Slot}", SceneSlot = enemy.Slot, InitialActive = enemy.Enabled, InitialAtb = initialAtb, NativeStatus0 = nativeStatuses.Saved, NativeStatus1 = nativeStatuses.Battle, NativeStatusResistances = nativeStatuses.Resistances.ToArray(), NativeCombatantFlags = BattleResources.ReadEnemyNativeCombatantFlags(enemy.ModelId), Information246 = expanded ? BattleResources.ReadEnemyNativeInformation246(enemy.ModelId) : null, Information254 = expanded ? BattleResources.ReadEnemyNativeEntryFlags(enemy.ModelId) : null, InitialHidden = enemy.Hidden, InitialUntargetable = enemy.Untargetable, InitialSuspended = enemy.InitiallySuspended, NativeAi = enemyAi, ExpandedEnemyBaseStatistics = expanded && !originalEnemy && (restoredDefinition is null || restoredDefinition.Combatants.Single(actor => actor.Id == $"enemy-{enemy.Slot}").ExpandedEnemyBaseStatistics.HasValue) ? stats.BaseStatistics.ToImmutableArray() : null, NativeTemplate = !expanded || originalEnemy ? BattleResources.ReadEnemyTemplate(enemy.ModelId, checked((byte)enemy.Level)) : null, Name = BattleResources.ReadEnemyName(enemy.ModelId), ModelId = enemy.ModelId, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Speed = Math.Max(1, stats.Speed), Attack = stats.Strength, Defense = stats.Vitality, Level = level, Magic = stats.Magic, Spirit = stats.Spirit, Evasion = stats.Evasion, PhysicalPower = 0, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeDrawSlots = BattleResources.ReadEnemyDrawSlots(enemy.ModelId, level), DrawSpells = BattleResources.ReadEnemyDrawSpells(enemy.ModelId, level), DrawGuardianForces = BattleResources.ReadEnemyDrawGuardianForces(enemy.ModelId, level), AbilityPoints = BattleResources.ReadEnemyAbilityPoints(enemy.ModelId), RewardSlot = enemy.Slot, Rewards = BattleResources.ReadEnemyRewards(enemy.ModelId) });
        }
        BattleDefinition definition = new() { MagicVitals = entryVitals.DeepCopy(), Name = $"Encounter {encounterId}", EncounterId = encounterId, StageId = encounter.StageId, Combatants = combatants.ToArray(), ActionDefinitions = CreateBattleActions(), NativeActions = BattleResources.NativeActions.ToArray(), NativeDevourEffects = BattleResources.ReadNativeDevourEffects(), NativeCards = session.State.CardInventory?.DeepCopy(), NativeCrtRandom = session.State.CrtRandom, NativeGlobalRandom = session.State.GlobalRandom, NativeElapsedCounter = ReadNativeElapsedCounter(), NativeCardNames = Enumerable.Range(0, 110).Select(id => battleCatalog!.CardName(id)).ToArray(), NativeItemNames = Enumerable.Range(0, 256).Select(id => battleCatalog!.ItemName(id)).ToArray(), NativeGlobalMessages = BattleResources.ReadNativeGlobalBattleMessages(), NativeScannedModels = session.State.ScannedModels, NativeKnownMagic = session.State.KnownMagic, NativeCommandNames = BattleResources.ReadNativeCommandNames(), NativeScanAlwaysShow = ((session.State.InputConfiguration ?? assets.ReadNewGameInputConfiguration()).Options & 0x100) != 0, GuardianForces = entryVitals.GuardianForces.Select((gf, id) => new BattleGuardianForceVital(id, gf.HitPoints, entryVitals.Definitions.GuardianForceMaximum(id, gf), (gf.Flags & 1) != 0) { KillCount = gf.KillCount, DeathCount = gf.DeathCount }).ToArray(), NativePocketStation = session.State.PocketStation, NativeChocoboName = Events.Scene.Names.GetValueOrDefault(GameNames.BokoSubject) ?? NativePocketStationText.ChocoboName(session.State.PocketStation), NativeBattleSpeed = session.State.InputConfiguration?.NativeBattleSpeedByte ?? assets.ReadNewGameInputConfiguration().NativeBattleSpeedByte, LimitProgress = session.State.LimitProgress, NativeShotAvailability = session.State.ShotAvailability, NativeFieldBattleRules = fieldRules, NativeLimitsDisabled = (fieldRules & 0x20) != 0, NativeStatusRules = BattleResources.ReadNativeStatusRules(session.State.InputConfiguration?.NativeBattleSpeedByte ?? assets.ReadNewGameInputConfiguration().NativeBattleSpeedByte ?? throw new InvalidDataException("Native battle status timing requires the original saved speed BYTE.")), NativeSeed = nativeSeed, NativeFlags = activeFlags, NativeFieldBattleMode = ReadNativeFieldBattleMode(), NativeMessageSpeed = session.State.InputConfiguration?.BattleMessageSpeed ?? assets.ReadNewGameInputConfiguration().BattleMessageSpeed, NativeExamTimerAtEntry = session.ExamTimer, NativeSavedBattleFlags = session.State.SavedBattleFlags?.Value ?? (byte)(session.State.CharacterVitals!.DreamEnabled ? 1 : 0), NativeSavedBattleFlagsKnownMask = session.State.SavedBattleFlags?.EffectiveKnownMask ?? (byte)1, InitialRandom = random, PersistentAiVariables = session.State.BattleVariables?.Values.ToArray(), RareItem = (encounterAbilities & 2) != 0, NativeItemInventory = entryEconomy.Items.ToArray() };
        definition = definition with
        {
            EncounterRosterAdmission = rosterAdmission,
            UsesExpandedRoster = expanded,
            UsesOriginalEnemySlots = expanded && useOriginalEnemySlots,
            AddedEnemySources = expanded ? addedEnemySources : null,
            NativeGil = new(entryEconomy.Gil, entryEconomy.PastGil),
            Tuning = session.State.GameplayPreferences.Tuning,
            StatusStatModifiers = restoredDefinition?.StatusStatModifiers ?? GameplayPolicy.StatusStatModifiers,
            ChocoboWorldMode = session.State.GameplayPreferences.ChocoboWorldMode,
            ChocoboRewards = session.State.ChocoboRewards,
            LimitOncePerTurn = session.State.GameplayPreferences.LimitOncePerTurn,
            SelphieCardSlots = session.State.GameplayPreferences.SelphieCardSlots,
            DrawOncePerSpell = session.State.GameplayPreferences.DrawOncePerSpell,
            GuardianFormsEnabled = restoredDefinition?.GuardianFormsEnabled ?? session.State.GameplayPreferences.GuardianFormsEnabled,
            GuardianFormResources = (restoredDefinition?.GuardianFormsEnabled ?? session.State.GameplayPreferences.GuardianFormsEnabled) ? [BattleResources.ReadIfritGuardianFormResources()] : [],
            NativeSavedOptions = (session.State.InputConfiguration ?? assets.ReadNewGameInputConfiguration()).Options,
            NativeScanNames = new Dictionary<byte, int> { [0x30] = 0, [0x34] = 1, [0x50] = GameNames.GrieverSubject }.ToDictionary(pair => pair.Key, pair => Albeoris.Games.FF8.TextEncoding.FF8Encoding.CreateEuropean(Albeoris.Games.FF8.TextEncoding.FF8TextLanguage.EN).GetBytes(Events.Scene.Names.GetValueOrDefault(pair.Value) ?? GameNames.DefaultName(pair.Value)).Append((byte)0).ToArray()),
            NativeScanActorNames = combatants.Where(actor => actor.IsPlayer).ToDictionary(actor => actor.Id, actor => EncodeScanName24(actor.Name)),
            NativeInputButtonMap = (session.State.InputConfiguration ?? assets.ReadNewGameInputConfiguration()).ButtonMap,
            NativeSupportRules = !expanded || (restoredDefinition is not null ? restoredDefinition.NativeSupportRules is not null :
                (definition.NativeSavedBattleFlagsKnownMask & 0x0A) == 0x0A && ((definition.NativeSavedBattleFlags.GetValueOrDefault() & 0x0A) != 0 ||
                    (definition.NativeSavedBattleFlagsKnownMask & 0x10) != 0 && (definition.NativeSavedBattleFlags.GetValueOrDefault() & 0x10) == 0 &&
                    ((definition.LimitProgress?.AngeloFlags ?? 0) & 0x0E) != 0 && combatants.Any(actor => actor is { IsPlayer: true, ModelId: 4, InitialActive: true })))
                ? BattleResources.ReadNativeSupportRules() : null,
            NativeGraphics = expanded ? null : BattleResources.ReadNativeGraphicsDefinition(definition.Combatants, assets.ReadNativeBattleGraphicsRules(), encounter),
            NativeClockRules = BattleResources.ReadNativeClockRules(),
            NativeRetainedMemory = session.CaptureNativeFieldMemory(),
            NativeVibrationProcess = session.State.VibrationProcess,
            NativeGlobalSoundProcess = session.State.GlobalSoundProcess,
            NativeEntry = new(encounter.Flags, encounterAbilities),
            NativeStageCameraBank = BattleResources.LoadStage(encounter.StageId).NativeCameraBank,
            NativeStageSource = BattleResources.LoadStage(encounter.StageId).NativeSource,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = BattleResources.LoadStage(encounter.StageId).NativeCameraControl,
            NativeRenzokukenResources = assets.ReadNativeRenzokukenResources(),
            NativeCommandSequences = assets.ReadNativeBattleCommandSequences(),
            NativeModuleResources = assets.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = assets.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = assets.ReadNativeEffectMeshes(),
            NativeEffectCatalog = assets.ReadNativeEffectCatalog(),
            NativeCommonEffectTexture = assets.ReadNativeCommonEffectTexture(),
            NativeEntryWaveFile = BattleResources.ReadNativeBattleFile(0xA5),
            NativeVictoryFile = BattleResources.ReadNativeBattleFile(766),
            NativeActionCameraRules = assets.ReadNativeActionCameraRules(),
            NativeBattleUiRandom = session.State.Playtime?.CreateBattleUiRandom(),
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { NativeRenzokukenMagicIds = BattleResources.ReadNativeRenzokukenMagicIds(actor.ModelId) }).ToArray()
        };
        if (effectPreviewRequest is null)
        {
            definition = BindExpandedEscapeResources(BindExpandedEnemyMug(BindExpandedEnemySpecials(BindExpandedEnemyLevels(definition))), encounterId);
            NativeBattleCompletion? request = Events.PendingInteractiveBattle;
            EncounterBindingContext context = request is not null && request.Encounter == encounterId ? new("Classic", session.State.FieldId, encounterId, activeFlags) : new("Classic");
            definition = ApplyEncounterBattlePolicy(definition, context, restoredDefinition, applyEncounterMods);
        }
        else
        {
            definition = definition with { EncounterId = null, Combatants = definition.Combatants.Select(actor => actor with { NativeAi = null, NativeTemplate = actor.NativeTemplate is { } template ? template with { NativeDrawTables = null } : null, NativeCombatantFlags = 1, NativeStatus0 = 0, NativeStatus1 = 0, InitialAtb = 0 }).ToArray() };
        }
        if (restoredDefinition is not null && !expanded)
        {
            definition = ActiveBattleResourceCompatibility.RetainAllocatedResources(definition, restoredDefinition, allocationImage, assets.NativeExecutableHash ?? throw new InvalidDataException("Restored battle requires its selected resource image identity."));
        }
        _ = new BattleScenePresentation(BattleResources, definition, true);
        return definition;
    }

    private static void RequireInstalledExpandedEnemyAi(BattleDefinition? definition)
    {
        if (definition is not { UsesExpandedRoster: true, EncounterId: int formation })
        {
            return;
        }
        BattleCombatantDefinition? primitive = definition.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.NativeAi is null);
        if (primitive is not null)
        {
            throw new NotSupportedException($"Saved expanded formation {formation}, scene slot {primitive.SceneSlot}, model {primitive.ModelId} uses the retired primitive enemy execution contract. Migrate this battle snapshot explicitly before restoring it.");
        }
        if (!definition.UsesOriginalEnemySlots && definition.AddedEnemySources is null)
        {
            throw new NotSupportedException($"Saved expanded formation {formation} uses the retired generated enemy roster without admitted placement provenance.");
        }
    }

    private BattleDefinition BindExpandedEscapeResources(BattleDefinition definition, int encounterId)
    {
        if (!definition.UsesExpandedRoster)
        {
            return definition;
        }
        return definition with
        {
            ExpandedFormationFlags = definition.ExpandedFormationFlags ?? BattleResources.LoadEncounter(encounterId).Flags,
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer || actor.NativeTemplate?.Information254 is not null || actor.Information254 is not null
                ? actor
                : actor with { Information254 = BattleResources.ReadEnemyNativeEntryFlags(actor.ModelId) }).ToArray()
        };
    }

    internal static BattleDefinition ApplyEncounterBattlePolicy(BattleDefinition definition, EncounterBindingContext context, BattleDefinition? restoredDefinition, Func<BattleDefinition, EncounterBindingContext, BattleDefinition>? currentPolicy)
    {
        if (restoredDefinition is null)
        {
            definition = currentPolicy?.Invoke(definition, context) ?? definition;
        }
        // An entered battle owns its compiled policy, including an explicitly absent policy.
        // Publication changes apply to new encounters; restore revalidates the installed native binding.
        else if (restoredDefinition.EncounterMod is { } retained)
        {
            EncounterBindingResult binding = EncounterBindingResolver.Resolve(retained.Encounter, definition, context);
            if (!string.Equals(retained.SourceProfile, context.Profile, StringComparison.OrdinalIgnoreCase) || retained.OriginalActorId != binding.OriginalActorId || retained.CastingSequence != binding.CastingSequence)
            {
                throw new InvalidDataException("Saved encounter policy differs from its validated installed native binding.");
            }
            definition = GameResourceHost.ApplyCompiledEncounterDefinition(definition, binding, retained);
        }
        if (definition.UsesExpandedRoster && definition.EncounterMod is { } mod)
        {
            ValidateExpandedEncounterPolicy(mod);
        }
        return definition;
    }

    private static void ValidateExpandedEncounterPolicy(EncounterCompiledDefinition mod)
    {
        List<string> unsupported = [];
        if (mod.NormalActionPolicies.Count != 0)
        {
            unsupported.Add("encounter AI and its native event callbacks");
        }
        if (mod.DamageRoutes.Count != 0)
        {
            unsupported.Add("routed damage through deferred native contacts");
        }
        if (unsupported.Count != 0)
        {
            throw new NotSupportedException($"Encounter policy '{mod.Encounter}' requires {string.Join(", ", unsupported)}, which are unavailable for an expanded roster.");
        }
    }

    private BattleActionDefinition[] CreateBattleActions()
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Battle actions require the canonical GF owner.");
        return BattlePartyProjection.CreateActions(vitals, BattleResources.ActionDefinitions);
    }

    private BattleDefinition BindExpandedEnemyLevels(BattleDefinition definition)
    {
        if (!definition.UsesExpandedRoster || !definition.Combatants.Any(actor => actor.IsPlayer && actor.NativeCommands.Any(binding => binding.Command is 33 or 34)))
        {
            return definition;
        }
        ImmutableSortedDictionary<int, ExpandedEnemyLevelProfile>.Builder profiles = ImmutableSortedDictionary.CreateBuilder<int, ExpandedEnemyLevelProfile>();
        foreach (int model in definition.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.ModelId).Distinct())
        {
            NativeBattleEnemyTemplate template = BattleResources.ReadEnemyTemplate(model, 0);
            profiles.Add(model, new(
                [.. template.Levels.Skip(1).Select(row => new ExpandedEnemyLevel(row.MaximumHp, row.BaseStatistics.ToImmutableArray(), row.AiLevelBand))],
                [.. template.NativeDrawTables!.Select(band => band.ToImmutableArray())]));
        }
        return definition with { ExpandedEnemyLevels = profiles.ToImmutable() };
    }

    private BattleDefinition BindExpandedEnemySpecials(BattleDefinition definition)
    {
        if (!definition.UsesExpandedRoster || !definition.Combatants.Any(actor => actor.IsPlayer && actor.NativeCommands.Any(binding => binding.Command is 7 or 29)))
        {
            return definition;
        }
        Dictionary<int, BattleNativeEnemySpecial> specials = [];
        return definition with
        {
            Combatants = definition.Combatants.Select(actor =>
            {
                if (actor.IsPlayer || actor.NativeSpecial is not null)
                {
                    return actor;
                }
                if (!specials.TryGetValue(actor.ModelId, out BattleNativeEnemySpecial? special))
                {
                    special = BattleResources.ReadNativeEnemySpecial(actor.ModelId);
                    specials.Add(actor.ModelId, special);
                }
                return actor with { NativeSpecial = special };
            }).ToArray()
        };
    }

    private BattleDefinition BindExpandedEnemyMug(BattleDefinition definition)
    {
        if (!definition.UsesExpandedRoster || !definition.Combatants.Any(actor => actor.IsPlayer && actor.NativeCommandSlots.Contains((byte)12)))
        {
            return definition;
        }
        return definition with
        {
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer || actor.NativeTemplate?.NativeMug is not null || actor.NativeMug is not null
                ? actor
                : actor with { NativeMug = BattleResources.ReadEnemyNativeMugData(actor.ModelId) }).ToArray()
        };
    }

    public BattleCombatantDefinition[] CreateBattleParty()
    {
        return CreateBattleParty(session.State.CharacterVitals ?? throw new NotSupportedException("Battle party requires its saved character owner."));
    }

    private BattleCombatantDefinition[] CreateBattleParty(NativeCharacterVitals vitals, IReadOnlyList<byte>? battleRows = null)
    {
        battleCatalog ??= LiveMenuCatalog.ReadInstalled(assets);
        return BattlePartyProjection.Create(session.State with { CharacterVitals = vitals }, Events.Scene.Names, battleCatalog, BattleResources, battleRows);
    }

    private void ValidateBattleHostState()
    {
        if (Events.PendingMenuBattle is not null)
        {
            ValidateMenuBattleHostState();
            if (battle is not null)
            {
                ValidateBattleMusicHostState(battle);
            }
            return;
        }
        if (battle is null)
        {
            return;
        }
        ValidateBattleMusicHostState(battle);
        if (session.State.RandomEncounters?.Pending is not null)
        {
            ValidateRandomBattleHostState();
            return;
        }
        NativeBattleCompletion request = Events.PendingInteractiveBattle ?? throw new InvalidDataException("Saved battle has no suspended interactive BATTLE owner.");
        if (SceneKind.Field != session.State.Scene || Events.Scene.NativeBattleWaits?.Values.Count(index => Events.Scene.NativeBattleCompletions![index] == request) != 1)
        {
            throw new InvalidDataException("Saved battle requires exactly one live field BATTLE owner.");
        }
        BattleSnapshot saved = battle.Capture();
        BattleDefinition current = BindBattleEntryClock(CreateEncounterBattle(request.Encounter, saved.Definition.NativeSeed, request.Flags, saved.Definition), saved, session.ExamTimer);
        long completedCallbacks = Events.GetCompletedBattleCallbackCount();
        long executedCallbacks = ReadExecutedBattleCallbacks(battle);
        if (Events.Scene.FieldCallbackClock!.BattleCallbacks != completedCallbacks + executedCallbacks)
        {
            throw new InvalidDataException("Saved battle disagrees with its executed native callback clock.");
        }
        ValidateBattleDefinition(current, saved.Definition);
    }

    internal static void ValidateBattleDefinition(BattleDefinition current, BattleDefinition saved)
    {
        System.Text.Json.JsonElement currentJson = System.Text.Json.JsonSerializer.SerializeToElement(current);
        System.Text.Json.JsonElement savedJson = System.Text.Json.JsonSerializer.SerializeToElement(saved);
        if (System.Text.Json.JsonElement.DeepEquals(currentJson, savedJson))
        {
            return;
        }
        string[] differences = currentJson.EnumerateObject().Where(property => !savedJson.TryGetProperty(property.Name, out System.Text.Json.JsonElement value) || !System.Text.Json.JsonElement.DeepEquals(property.Value, value)).Select(property => property.Name).ToArray();
        throw new InvalidDataException($"Saved battle definition differs from the installed encounter or saved battle-entry state: {string.Join(", ", differences)}.");
    }

    internal static BattleDefinition BindBattleEntryClock(BattleDefinition current, BattleSnapshot saved, NativeExamTimerState? timer)
    {
        current = current with { LimitOncePerTurn = saved.Definition.LimitOncePerTurn, SelphieCardSlots = saved.Definition.SelphieCardSlots, DrawOncePerSpell = saved.Definition.DrawOncePerSpell, GuardianFormsEnabled = saved.Definition.GuardianFormsEnabled, NativeGlobalSoundProcess = saved.Definition.NativeGlobalSoundProcess, NativeMusicProcess = saved.Definition.NativeMusicProcess, NativeRetainedMemory = saved.Definition.NativeRetainedMemory, NativeVibrationProcess = saved.Definition.NativeVibrationProcess, NativeBattleUiRandom = saved.Definition.NativeBattleUiRandom };
        if (saved.Definition.NativeExamTimerAtEntry is not { } entry)
        {
            return current with { NativeExamTimerAtEntry = null };
        }
        NativeExamTimerState advanced = entry;
        long remaining = saved.NativeGraphics is null ? saved.Frame : saved.NativeHostCallbacks ?? throw new NotSupportedException("Saved native battle requires its executed47CF60 callback ledger before host clock validation.");
        while (remaining > 0)
        {
            int callbacks = (int)Math.Min(remaining, 30000);
            advanced = advanced.AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks);
            remaining -= callbacks;
        }
        NativeExamTimerState canonical = saved.NativeGraphics is null ? advanced : saved.NativeExamTimer ?? throw new NotSupportedException("Saved native battle requires its canonical timer and display owners; explicitly migrate the named checkpoint.");
        if (!advanced.HasSameClock(canonical) || timer is null || !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(canonical), System.Text.Json.JsonSerializer.SerializeToElement(timer)))
        {
            throw new InvalidDataException("Saved battle's entry timer and combat callbacks disagree with the current process timer owner.");
        }
        return current with { NativeExamTimerAtEntry = entry };
    }

    public int ApplyBattleInput(BattleInput input, IReadOnlyDictionary<int, int>? guardianForceLearning = null)
    {
        input.Validate();
        if (diagnosticBattle is not null)
        {
            return ApplyDiagnosticBattleInput(input);
        }
        if (guardianForceLearning is { Count: > 0 } && input.Kind != BattleInputKind.Return)
        {
            throw new InvalidOperationException("Battle learning choices belong to result confirmation.");
        }
        if (input.Kind == BattleInputKind.ConfigurePlaythrough)
        {
            session.SetFullBattlePlaythrough(input.FullCombat);
            if (input.FullCombat && session.State.Scene == SceneKind.WorldMap && !HasPendingBattle)
            {
                InitializeWorldRandomEncounters();
            }
            if (!input.FullCombat)
            {
                if (battle is null && (Events.PendingMenuBattle is not null || Events.PendingInteractiveBattle is not null || session.State.RandomEncounters?.Pending is not null))
                {
                    ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: input.Mode));
                }
                if (battle?.Outcome == BattleOutcome.Active)
                {
                    ApplyBattleInput(new(BattleInputKind.InstantVictory));
                }
            }
            return 0;
        }
        if (input.Kind == BattleInputKind.ConfigureEncounters)
        {
            ApplyEncounterConfiguration(input);
            return 0;
        }
        ObservePendingBattle();
        if (input.Kind == BattleInputKind.StartEncounter && input.NativeSeed is null)
        {
            // TODO(TD-009): Original CRT entropy provenance remains unowned.
            input = input with { NativeSeed = (byte)System.Security.Cryptography.RandomNumberGenerator.GetInt32(256) };
        }
        if (TryApplyMenuBattleInput(input, out int menuAdvanced, guardianForceLearning))
        {
            return menuAdvanced;
        }
        if (TryApplyRandomBattleInput(input, out int randomAdvanced, guardianForceLearning))
        {
            return randomAdvanced;
        }
        NativeBattleCompletion? request = Events.PendingInteractiveBattle;
        if (input.Kind == BattleInputKind.StartEncounter)
        {
            if (battle is not null || request is null)
            {
                throw new InvalidOperationException("Start Encounter requires one suspended interactive BATTLE request.");
            }
            battle = StartNativeBattleSession(request.Encounter, request.Flags, input.NativeSeed!.Value, input.Mode);
            return 0;
        }
        BattleSession active = battle ?? throw new InvalidOperationException("No interactive battle is active.");
        long previous = active.Frame;
        if (input.Kind == BattleInputKind.Return)
        {
            if (!active.CanReturnToField || request is null)
            {
                throw new InvalidOperationException("The completed battle does not permit return to its suspended field request.");
            }
            CompleteBattle(active, guardianForceLearning);
            battle = null;
            PublishBattleSnapshot(null);
            return 0;
        }
        ExecuteBattleCommand(active, input);
        int advanced = checked((int)(active.Frame - previous));
        PublishBattleSnapshot(active);
        CompleteNativeDefeatIfReady();
        return advanced;
    }
    internal static int ExecuteBattleCommand(BattleSession active, BattleInput input)
    {
        long before = active.Frame;
        switch (input.Kind)
        {
            case BattleInputKind.Tick:
                for (int frame = 0; frame < input.Frames; frame++)
                {
                    active.Tick();
                }
                break;
            case BattleInputKind.Attack:
                if (input.SmartAttackPreview is { } preview)
                {
                    active.QueueSmartAttack(preview);
                }
                else
                {
                    active.QueueSmartAttack(input.ActorId!, input.TargetId!, input.ForcePhysical);
                }
                break;
            case BattleInputKind.Action:
                active.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, input.MagicTargets.IsEmpty ? null : input.MagicTargets, input.DrawCastTargetId);
                break;
            case BattleInputKind.AdvanceTurn:
                active.AdvanceTurn();
                break;
            case BattleInputKind.SetMode:
                active.SetMode(input.Mode);
                break;
            case BattleInputKind.SetGunbladeMode:
                active.SetGunbladeMode(input.GunbladeMode);
                break;
            case BattleInputKind.GunbladeTrigger:
                active.TriggerGunblade();
                break;
            case BattleInputKind.GuardianFormActivate:
                active.RequestGuardianForm(input.ActorId!, input.ActionId);
                break;
            case BattleInputKind.GuardianFormAction:
                active.QueueGuardianFormAction(input.ActorId!, input.TargetId!, (BattleGuardianFormAction)input.ActionId);
                break;
            case BattleInputKind.GuardianFormSkip:
                active.SkipGuardianFormFinisher();
                break;
            case BattleInputKind.GuardianBoost:
                active.PressNativeGuardianBoost();
                break;
            case BattleInputKind.ShotTrigger:
                active.TriggerNativeShot(input.ActorId ?? throw new InvalidDataException("Shot trigger requires its process owner."));
                break;
            case BattleInputKind.ScanInput:
                active.QueueNativeScanInput(input.ScanButton ?? throw new InvalidDataException("Scan input requires ScanButton."));
                break;
            case BattleInputKind.InstantDefeat:
                active.InstantDefeat();
                break;
            case BattleInputKind.InstantVictory:
                active.InstantVictory();
                break;
            default:
                throw new NotSupportedException($"Battle input {input.Kind} is unsupported.");
        }
        return checked((int)(active.Frame - before));
    }

    internal BattleResult CaptureBattleOutcome(BattleSession active, BattleEncounterOccurrence encounter, BattleSnapshot? completedSnapshot = null)
    {
        encounter.Validate();
        if (active.Outcome == BattleOutcome.Active || active.EncounterId != encounter.Formation)
        {
            throw new InvalidOperationException("A factual battle outcome can only be captured from its completed live encounter.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner.");
        NativeEconomy economy = session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical item owner.");
        NativeCharacterVitals? magicAfter = active.MagicVitals;
        List<BattlePartyResult> party = [];
        foreach (BattleCombatant player in active.Combatants.Where(actor => actor.IsPlayer))
        {
            int row;
            if (player.MagicRecordId is int boundRow)
            {
                row = boundRow;
            }
            else if (active.UsesExpandedRoster || !player.Id.StartsWith("party-", StringComparison.Ordinal) ||
                !int.TryParse(player.Id.AsSpan("party-".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out row) ||
                (uint)row >= vitals.Records.Length || !vitals.CombatParty.Contains((byte)row))
            {
                throw new InvalidDataException("Battle outcome lost its admitted character record.");
            }
            if ((uint)row >= vitals.Records.Length || player.Id != $"party-{row}" || player.ModelId != vitals.Records[row].CharacterId)
            {
                throw new InvalidDataException("Battle outcome character binding disagrees with its canonical record.");
            }
            NativeCharacterVital original = vitals.Records[row];
            if (original.HitPoints.Minimum != original.HitPoints.Maximum)
            {
                throw new NotSupportedException("Battle facts require the actual exact pre-battle HP operand.");
            }
            ushort status = player.PersistentStatus;
            List<BattleMagicChange> magic = [];
            foreach (int spell in (magicAfter is null ? original.MagicInventory : Array.Empty<NativeMagicStock>()).Where(stock => stock.SpellId != 0).Select(stock => (int)stock.SpellId).Concat(player.MagicStocks.Select(stock => stock.Id)).Distinct().Where(_ => magicAfter is null))
            {
                int before = original.MagicInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0;
                int after = player.MagicStocks.FirstOrDefault(stock => stock.Id == spell)?.Count ?? 0;
                if (before != after)
                {
                    magic.Add(new BattleMagicChange(checked((byte)spell), checked((byte)before), after - before));
                }
            }
            party.Add(new BattlePartyResult(checked((byte)row), original.CharacterId, original.HitPoints.Minimum, checked((ushort)player.CurrentHp), original.Status, status, magic.ToArray()) { MagicInventoryAfter = magicAfter is null ? player.NativeMagicSlots.ToArray() : null });
        }
        List<BattleItemConsumption> items = [];
        foreach (NativeItemStock stock in economy.Items.Where(stock => stock.ItemId != 0 && stock.Count != 0))
        {
            int after = active.Items.FirstOrDefault(item => item.Id == stock.ItemId)?.Count ?? 0;
            if (after < stock.Count)
            {
                items.Add(new BattleItemConsumption(stock.ItemId, stock.Count, checked((byte)(stock.Count - after))));
            }
        }
        BattleSnapshot completed = completedSnapshot ?? active.Capture();
        bool debugVictory = active.Events.Any(entry => entry.Kind == "debug-victory");
        int[] defeated = debugVictory ? [] : active.EnemyDefeats.Select(defeat => defeat.Slot).Distinct().ToArray();
        int[] eligible = active.Outcome is BattleOutcome.Victory or BattleOutcome.Escaped && !debugVictory ? CaptureRewardEligibleGuardianForces(vitals, party, completed) : [];
        int? expandedEnemyCount = active.UsesExpandedRoster ? completed.Definition.Combatants.Count(actor => !actor.IsPlayer) : null;
        BattleResult result = new(encounter, active.Outcome, party.ToArray(), items.ToArray(), defeated, eligible, debugVictory, debugVictory ? BattleRewardPolicy.DebugNoRewards : BattleRewardPolicy.NativeCombat, debugVictory ? [] : active.EnemyDefeats.ToArray()) { UsesExpandedRoster = active.UsesExpandedRoster, ExpandedEnemyCount = expandedEnemyCount, NativeResult = debugVictory ? null : active.NativeResult, Tuning = completed.Definition.Tuning, ChocoboRewardsBefore = completed.Definition.ChocoboRewards, ChocoboRewardsAfter = completed.ChocoboRewards, NativeKnownMagicBefore = completed.Definition.NativeKnownMagic, NativeKnownMagicAfter = completed.NativeKnownMagic, NativeScannedModelsBefore = completed.Definition.NativeScannedModels, NativeScannedModelsAfter = completed.NativeScannedModels, NativeItemInventoryAfter = completed.NativeItemInventory!.ToArray(), GuardianForceCompatibilityChanges = CaptureGuardianForceCompatibilityChanges(completed, party), GuardianForceCounterChanges = active.GuardianForceCounterChanges, GuardianForceChanges = active.GuardianForces.Where(gf => gf.HitPoints != completed.Definition.GuardianForces.Single(initial => initial.Id == gf.Id).HitPoints).Select(gf => new BattleGuardianForceChange(gf.Id, completed.Definition.GuardianForces.Single(initial => initial.Id == gf.Id).HitPoints, gf.HitPoints)).ToArray(), ScriptedItems = debugVictory ? [] : active.ScriptedRewards.ToArray(), NativeRewardItems = debugVictory ? [] : active.NativeRewardItems.ToArray(), PersistentAiVariablesBefore = debugVictory ? null : completed.Definition.PersistentAiVariables?.ToArray(), PersistentAiVariablesAfter = debugVictory ? null : completed.PersistentAiVariables?.ToArray(), NativeScriptEffects = debugVictory ? new() : active.NativeScriptState, NativeGrowthChanges = CaptureNativeGrowthChanges(active, completed), NativeDeathCountChanges = CaptureNativeDeathCounts(active, completed), NativeCardChange = completed.NativeCards is { } cards && completed.Definition.NativeCards is { } beforeCards ? new(beforeCards.DeepCopy(), cards.DeepCopy()) : null, NativeCrtRandomBefore = completed.Definition.NativeCrtRandom, NativeCrtRandomAfter = completed.NativeCrtRandom, NativeGlobalRandomChange = active.NativeGlobalRandomChange };
        result = result with { UsesOriginalEnemySlots = completed.Definition.UsesOriginalEnemySlots, AddedEnemySources = completed.Definition.AddedEnemySources };
        if (completed.Definition.NativeGil is { } gilBefore && completed.NativeGil is { } gilAfter && gilBefore != gilAfter)
        {
            result = result with { NativeGilChange = new(gilBefore, gilAfter) };
        }
        if (!debugVictory && completed is { EncounterPolicies: { TerminalStarted: true, TerminalReached: true, CleanedUp: true }, Definition.EncounterMod: { Encounter: EncounterBindingResolver.Disc1Edea, NativeEvents: not null } terminalMod })
        {
            BattleCombatantSnapshot original = completed.Combatants.Single(actor => actor.Id == terminalMod.OriginalActorId);
            if ((original.NativeStatus!.Saved & 1) == 0 || (original.NativeParticipant!.NativeCombatantFlags!.Value & 0x40) == 0)
            {
                throw new InvalidDataException("The completed encounter terminal lost its native original actor witness.");
            }
            result = result with { EncounterTerminal = new(terminalMod.Encounter, terminalMod.GenerationIdentity, terminalMod.OriginalActorId) };
        }
        if (magicAfter is not null)
        {
            result = result with { MagicSystemReceipt = new BattleMagicSystemReceipt(vitals.MagicSystem.DeepCopy(), magicAfter.MagicSystem.DeepCopy(), vitals.Records.Select(record => record.MagicInventory.ToArray()).ToArray(), magicAfter.Records.Select(record => record.MagicInventory.ToArray()).ToArray()) };
        }
        if (active.Outcome == BattleOutcome.Escaped)
        {
            result = result with
            {
                RemainingEnemyExperience = active.CaptureEscapeRemainingExperience()
            };
        }
        result.Validate();
        return result;
    }

    internal BattleResult CaptureAutomaticBattleOutcome(BattleEncounterOccurrence encounter)
    {
        encounter.Validate();
        NativeBattleCompletion current = Events.PendingHostedBattle ?? throw new InvalidOperationException("Automatic outcome requires its actual held native BATTLE operation.");
        if (current.Backend != NativeBattleBackend.AutomaticVictory || current.Encounter != encounter.Formation || encounter.ScriptedOwner is null)
        {
            throw new InvalidOperationException("Automatic outcome cannot be inferred from completed battle history or another backend.");
        }
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new InvalidDataException("Automatic outcome requires its current canonical party owner.");
        List<BattlePartyResult> party = [];
        foreach (byte row in vitals.CombatParty.Where(row => row != 255))
        {
            NativeCharacterVital member = vitals.Records[row];
            if (member.HitPoints.Minimum != member.HitPoints.Maximum)
            {
                throw new NotSupportedException("Automatic victory cannot invent an exact HP fact from an uncertain native HP interval.");
            }
            party.Add(new BattlePartyResult(row, member.CharacterId, member.HitPoints.Minimum, member.HitPoints.Minimum, member.Status, member.Status, []));
        }
        BattleResult result = new(encounter, BattleOutcome.Victory, party.ToArray(), [], BattleResources.LoadEncounter(encounter.Formation).Enemies.Select(enemy => enemy.Slot).ToArray(), BattleResultProjection.EligibleGuardianForces(vitals), false, BattleRewardPolicy.ResearchAutomaticApOnly, []) { Tuning = session.State.GameplayPreferences.Tuning };
        result.Validate();
        return result;
    }

    internal void ApplyBattleResult(BattleEncounterOccurrence expected, BattleResult result, IReadOnlyDictionary<int, int>? guardianForceLearning = null, BattleDefinition? admittedDefinition = null)
    {
        expected.Validate();
        result.Validate();
        if (result.Encounter != expected || session.State.PendingBattleEncounter != expected || session.State.AppliedBattleResultIds.Contains(expected.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Battle result requires its unapplied matching encounter occurrence.");
        }
        if (session.State.Scene != expected.SourceScene || session.State.FieldId != expected.SourceFieldId || !BattleResources.FormationSourceHash(expected.Formation).Equals(expected.FormationHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Battle result source scene or current original formation resource differs from its observed encounter.");
        }
        if (expected.ScriptedOwner is null && Events.PendingHostedBattle is not null || expected.ScriptedOwner is not null && session.State.RandomEncounters?.Pending is not null)
        {
            throw new InvalidDataException("Battle result conflicts with the current native battle owner.");
        }
        if (expected.ScriptedOwner is { } owner)
        {
            NativeBattleCompletion request = Events.PendingHostedBattle ?? throw new InvalidDataException("Scripted battle fact lost its original pending BATTLE owner.");
            if (request.FieldId != owner.FieldId || request.ObjectId != owner.ObjectId || request.ScriptId != owner.ScriptId || request.Operation != owner.Operation || request.Flags != owner.Flags || request.Encounter != expected.Formation || request.ProgramHash != owner.ProgramHash)
            {
                throw new InvalidDataException("Scripted battle result owner differs from the actual suspended BATTLE operation.");
            }
            if ((request.Backend == NativeBattleBackend.AutomaticVictory) != (result.RewardPolicy == BattleRewardPolicy.ResearchAutomaticApOnly))
            {
                throw new InvalidDataException("Battle reward policy differs from its actual hosted battle backend.");
            }
        }
        BattleResultOwners owners = ProjectBattleResult(result, admittedDefinition);
        if (guardianForceLearning is not null)
        {
            foreach ((int id, int ability) in guardianForceLearning)
            {
                if (!owners.Rewards.GuardianForceGrowth.Any(growth => growth.Id == id && growth.LearnedAbilityIds.Length > 0))
                {
                    throw new InvalidOperationException("Battle learning choices require a GF that learned an ability in this victory.");
                }
                owners = owners with { CharacterVitals = owners.CharacterVitals.SelectGuardianForceLearning(id, ability) };
            }
        }
        NativeBattleVariableState? variables = null;
        if (result.PersistentAiVariablesBefore is { } before)
        {
            NativeBattleVariableState current = session.State.BattleVariables ?? throw new InvalidDataException("Battle result lost its saved native variable bank.");
            if (!current.Values.SequenceEqual(before))
            {
                throw new InvalidDataException("Battle variable effects disagree with their current saved owner.");
            }
            variables = new NativeBattleVariableState(System.Collections.Immutable.ImmutableArray.CreateRange(result.PersistentAiVariablesAfter!));
        }
        NativeSavedBattleFlags? savedFlags = ProjectSavedBattleFlagEffect(result);
        byte achievementFlags = BattleResources.LoadEncounter(expected.Formation).Flags;
        session.CommitBattleResult(expected, owners.CharacterVitals, owners.Economy, owners.Cards, variables, savedFlags, result.NativeScriptEffects.SavedBattleFlagsBefore, result.NativeCrtRandomAfter, result.NativeCrtRandomBefore, result.NativeScriptEffects.SavedBattleFlagsSetMask, result.NativeScriptEffects.SavedBattleFlagsClearMask, result.NativeScannedModelsBefore, result.NativeScannedModelsAfter, result.NativeScriptEffects.LimitSpecialFlagsBefore, result.NativeScriptEffects.LimitSpecialFlagsAfter, result.NativeScriptEffects.LimitSpecialSetMask, result.NativeScriptEffects.ShotFlagsBefore, result.NativeScriptEffects.ShotFlagsAfter, result.NativeScriptEffects.ShotFlagsSetMask, result.NativeGlobalRandomChange, result.NativeKnownMagicBefore, result.NativeKnownMagicAfter, guardianForceCounters: result.GuardianForceCounterChanges, achievementDefeats: result.RewardPolicy == BattleRewardPolicy.NativeCombat ? result.EnemyDefeats : [], achievementOutcome: result.Outcome, achievementFormationFlags: achievementFlags, achievementRewardPolicy: result.RewardPolicy, chocoboRewardsBefore: result.ChocoboRewardsBefore, chocoboRewardsAfter: result.ChocoboRewardsAfter, nativeResult: result.NativeResult);
        Events.Scene.UnlockedTutorials.UnionWith(result.NativeScriptEffects.UnlockedTutorials);
        session.EnqueueEntityNames(result.NativeScriptEffects.AcquiredGuardianForces.Select(id => new PendingEntityName(GameNames.GuardianForceSubject(id), GameNames.DefaultName(GameNames.GuardianForceSubject(id)), "battle:" + expected.OccurrenceId)));
        LastBattleRewards = owners.Rewards;
    }

    private BattleResultOwners ProjectBattleResult(BattleResult result, BattleDefinition? admittedDefinition = null)
    {
        BattleEncounter formation = BattleResources.LoadEncounter(result.Encounter.Formation);
        BattleDefinition? admittedBattle = admittedDefinition ?? (result.UsesExpandedRoster ? battle?.ReadDefinition() : null);
        if (admittedBattle is not null && (admittedBattle.EncounterId != result.Encounter.Formation || admittedBattle.UsesExpandedRoster != result.UsesExpandedRoster ||
            admittedBattle.UsesOriginalEnemySlots != result.UsesOriginalEnemySlots || (admittedBattle.AddedEnemySources is null) != (result.AddedEnemySources is null) ||
            admittedBattle.AddedEnemySources is { } admittedSources && !admittedSources.SequenceEqual(result.AddedEnemySources!)))
        {
            throw new InvalidDataException("Battle result disagrees with its admitted encounter and enemy placement authority.");
        }
        IReadOnlyList<BattleEnemyPlacement> slots = result.AddedEnemySources is { } sources
            ? ResolveExpandedEnemyPlacements(formation, sources, originalSlots: result.UsesOriginalEnemySlots)
            : result.UsesExpandedRoster && !result.UsesOriginalEnemySlots
                ? throw new NotSupportedException("Legacy expanded rewards require admitted enemy placement provenance.")
                : formation.Slots;
        if (result.AddedEnemySources is not null && slots.Count != result.EffectiveEnemySlotCount)
        {
            throw new InvalidDataException("Battle result size disagrees with its selected enemy placements.");
        }
        if (result.DefeatedEnemySlots.Concat(result.RemainingEnemyExperience.Select(enemy => enemy.Slot)).Any(slot => !slots.Any(enemy => enemy.Slot == slot && enemy.NativeModelId >= 16 && enemy.NativeModelId != 255)) || result.RewardPolicy == BattleRewardPolicy.ResearchAutomaticApOnly && !formation.Enemies.Select(enemy => enemy.Slot).Order().SequenceEqual(result.DefeatedEnemySlots.Order()))
        {
            throw new InvalidDataException("Battle result defeated-enemy facts contradict the current original formation.");
        }
        BattleEnemyPlacement[] rewarded = slots.Where(enemy => result.DefeatedEnemySlots.Contains(enemy.Slot) || result.RemainingEnemyExperience.Any(remaining => remaining.Slot == enemy.Slot)).ToArray();
        Dictionary<int, int> currentAp = rewarded.ToDictionary(enemy => enemy.Slot, enemy => BattleResources.ReadEnemyAbilityPoints(enemy.ModelId));
        Dictionary<int, BattleEnemyRewardDefinition> currentRewards = rewarded.ToDictionary(enemy => enemy.Slot, enemy => BattleResources.ReadEnemyRewards(enemy.ModelId));
        bool noExperience = NativeBattleRewardDistribution.NoExperience(checked((ushort)(result.Encounter.ScriptedOwner?.Flags ?? 0)), formation.Flags);
        return BattleResultProjection.Apply(result, session.State.CharacterVitals ?? throw new InvalidDataException("Battle result lost its canonical party owner."), session.State.Economy ?? throw new InvalidDataException("Battle result lost its canonical economy owner."), currentAp, currentRewards, assets.ReadNativeBattleExperienceDefinition(), session.State.CardInventory, noExperience, NativeItemOrderAliases.From(session.State), admittedBattle);
    }
}
