using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeEffectFixtureRequest(string Id, ushort Effect, byte Command, ushort ActionId, byte TargetActorIndex, int EntryMaximumCallbacks, int ProcessTimeoutSeconds)
{
    public ushort? ControlledShellEffect { get; init; }
    public int AdmissionProbeMaximumCallbacks { get; init; }
    public bool OrdinaryMagicAdmission { get; init; }
    public bool DeferredGuardianFormulaAdmission { get; init; }
    public int PartyModelId { get; init; }
    public int[]? AdditionalPartyModelIds { get; init; }
    public byte? CrisisLevel { get; init; }
    public bool HealingContact { get; init; }
    public ushort? ControlledContactValue { get; init; }
    public bool ControlledDrainContact { get; init; }
    public bool RevivalContact { get; init; }
    public bool PreserveOriginalFormationEnemies { get; init; }
    public int EncounterId { get; init; } = 94;
    public byte CasterActorIndex { get; init; }
}
internal sealed record NativeEffectFixtureRequests(NativeEffectFixtureRequest[] Cases);
internal sealed record NativeEffectFixtureArtifact(string Boundary, string File, string Sha256, long Bytes, long Frame, long NativeCallback);
internal sealed record NativeEffectFixtureSelection(bool OriginalKernelEffectReference, byte ShellCommand, ushort ShellActionId, ushort ShellEffect, int KernelSection, int KernelIndex, ushort RequestedModuleEffect);
internal sealed record NativeEffectFixtureExportResult(string Id, ushort Effect, string Status, string Boundary, string? FirstBlocker, NativeEffectFixtureArtifact[] Artifacts, NativeEffectFixtureSelection? Selection = null)
{
    public string Scenario { get; init; } = "Fresh installed-resource component; controlled post-formula seven-HP damage; manual action104 descriptor; no story continuation or formula admission";
    public string[] Pending { get; init; } = ["Effect termination contract", "Effect initialization and completion", "Native comparison", "Significant graphics", "Camera", "Sound", "Independent cold next callback", "Ordinary command admission"];
}

internal static class NativeBattleEffectFixtureExporter
{
    internal static void Validate(NativeEffectFixtureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || request.Effect is < 1 or > 400 || request.EntryMaximumCallbacks <= 0 || request.ProcessTimeoutSeconds <= 0 || request.AdmissionProbeMaximumCallbacks < 0)
        {
            throw new InvalidDataException("A fixture request requires identity, native effect, positive entry callback budget and external process deadline.");
        }
        if (request.TargetActorIndex is not (0 or 3) && !(request.RevivalContact && request.TargetActorIndex == 1))
        {
            throw new InvalidDataException("This two-actor component supports explicit party0 or enemy3 contact targets only.");
        }
        if (request.ControlledDrainContact && (request.Command != 32 || request.Effect != 56 || request.ControlledContactValue is not null || request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.HealingContact || request.RevivalContact))
        {
            throw new InvalidDataException("The controlled Absorb pair requires its original command32/module56 without another formula or contact override.");
        }
        if (request.ControlledContactValue is not null && (request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.HealingContact || request.RevivalContact))
        {
            throw new InvalidDataException("An explicit contact value requires the controlled post-formula component.");
        }
        if (request.RevivalContact && (!request.HealingContact || request.TargetActorIndex != 1 || request.OrdinaryMagicAdmission))
        {
            throw new InvalidDataException("The controlled revival component requires its second party target, healing contact and explicit post-formula input.");
        }
        if (request.DeferredGuardianFormulaAdmission && (request.Command != 254 || request.TargetActorIndex != 3 || request.HealingContact || request.RevivalContact || request.OrdinaryMagicAdmission))
        {
            throw new InvalidDataException("The deferred guardian formula component requires its original command254 row and enemy target without a post-formula healing or Magic input.");
        }
        if (request.ControlledShellEffect is ushort shell && (shell is < 1 or > 400 || request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.HealingContact || request.RevivalContact))
        {
            throw new InvalidDataException("Explicit module selection requires a valid original shell effect and a controlled post-formula damage component; ordinary, deferred, healing and revival admission cannot use it.");
        }
        if (request.PreserveOriginalFormationEnemies && (request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.RevivalContact))
        {
            throw new InvalidDataException("Preserving original formation enemies requires the controlled component producer without an ordinary, guardian or revival override.");
        }
        if (request.PartyModelId is < 0 or > 10)
        {
            throw new InvalidDataException("The fresh component requires an explicit original party model index.");
        }
        if (request.AdditionalPartyModelIds is { } additional && (additional.Length > 2 || additional.Any(model => model is < 0 or > 10) || additional.Length > 0 && (request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.RevivalContact || request.ControlledDrainContact)))
        {
            throw new InvalidDataException("Additional original party fixtures require at most two original model indices and the controlled post-formula producer.");
        }
        if (request.EncounterId is < 0 or >= 1024 || request.CasterActorIndex is not (0 or >= 3 and <= 6) || request.CasterActorIndex >= 4 && !request.PreserveOriginalFormationEnemies || request.CasterActorIndex != 0 && (request.OrdinaryMagicAdmission || request.DeferredGuardianFormulaAdmission || request.RevivalContact))
        {
            throw new InvalidDataException("The fresh component requires an original formation and party0 or original enemy3..6 caster; rows4..6 require the preserved original formation, and ordinary, guardian and revival producers retain their party caster.");
        }
    }

    internal static BattleEnemyPlacement[] SelectEnemies(BattleEncounter formation, bool preserveOriginalFormationEnemies)
    {
        BattleEnemyPlacement[] enemies = formation.Enemies.Where(enemy => enemy.Enabled && enemy.IsEnemyModel).ToArray();
        if (preserveOriginalFormationEnemies)
        {
            if (enemies.Length is < 1 or > 4 || enemies.Select(enemy => enemy.Slot).Distinct().Count() != enemies.Length)
            {
                throw new InvalidDataException("The preserved formation requires one to four distinct original enemy placements.");
            }
            return enemies.OrderBy(enemy => enemy.Slot).ToArray();
        }
        return enemies.Length == 1 ? enemies : throw new InvalidDataException("The two-actor component requires exactly one enabled original enemy placement.");
    }

    internal static BattleEnemyPlacement SelectEnemy(BattleEncounter formation, bool preserveOriginalFormationEnemies = false)
    {
        return SelectEnemies(formation, preserveOriginalFormationEnemies)[0];
    }

    internal static string ComponentEnemyName(string originalName, int modelId)
    {
        // An anonymous original DAT keeps its native name; this label identifies only the controlled host fixture row.
        return string.IsNullOrWhiteSpace(originalName) ? $"Original enemy model {modelId}" : originalName;
    }

    internal static BattleEnemyPlacement SelectCasterEnemy(BattleEncounter formation, NativeEffectFixtureRequest request)
    {
        if (request.CasterActorIndex is < 3 or > 6 || request.CasterActorIndex >= 4 && !request.PreserveOriginalFormationEnemies)
        {
            throw new InvalidDataException("An enemy caster requires its supported original physical formation row.");
        }
        BattleEnemyPlacement[] enemies = SelectEnemies(formation, request.PreserveOriginalFormationEnemies);
        if (!request.PreserveOriginalFormationEnemies)
        {
            return enemies[0];
        }
        int index = request.CasterActorIndex - 3;
        return index < enemies.Length ? enemies[index] : throw new InvalidDataException("The selected physical caster row has no enabled original formation placement.");
    }

    internal static byte SelectEnemySequence(NativeBattleAiDefinition ai, BattleNativeAction action)
    {
        byte[] sequences = ai.Abilities.Where(ability => ability.Command == action.Command && ability.Id == action.ActionId).Select(ability => ability.Sequence).Distinct().ToArray();
        return sequences.Length == 1 ? sequences[0] : throw new InvalidDataException("The enemy component requires one original AI sequence for its selected kernel command/action row.");
    }

    internal static BattleNativeAction SelectAction(IEnumerable<BattleNativeAction> actions, NativeEffectFixtureRequest request)
    {
        BattleNativeAction[] matches = actions.Where(action => action.Command == request.Command && action.ActionId == request.ActionId && action.MagicId == request.Effect && (request.CrisisLevel is null || action.CrisisLevel == request.CrisisLevel)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("The fixture request does not identify exactly one original kernel command/action/effect row; effect aliases are not inferred.");
    }

    internal static BattleNativeAction SelectComponentAction(IEnumerable<BattleNativeAction> actions, NativeEffectFixtureRequest request)
    {
        Validate(request);
        return SelectAction(actions, request.ControlledShellEffect is ushort shell ? request with { Effect = shell } : request);
    }

    internal static ushort SelectHeaderEffect(BattleNativeAction action, NativeEffectFixtureRequest request)
    {
        if (action.Command != request.Command || action.ActionId != request.ActionId || action.MagicId != (request.ControlledShellEffect ?? request.Effect))
        {
            throw new InvalidDataException("The controlled component header requires its actual selected original kernel shell.");
        }
        return request.ControlledShellEffect is null ? action.MagicId : request.Effect;
    }

    internal static byte SelectSequence(ImmutableArray<byte> commandSequences, byte command, ushort action)
    {
        byte sequence;
        if (command < 39)
        {
            if (commandSequences.Length != 39)
            {
                throw new InvalidDataException("The fixture requires the original39-byte command sequence table.");
            }
            sequence = commandSequences[command];
        }
        else
        {
            sequence = command switch
            {
                236 or 240 or 245 => 0,
                239 => 28,
                244 or 254 => 14,
                247 => 11,
                _ => throw new NotSupportedException("The requested component command lacks an existing proved player sequence selection.")
            };
        }
        return command is 244 or 254 && action is 15 or 70 ? (byte)11 : sequence;
    }

    internal static NativeEffectFixtureExportResult Export(NativeEffectFixtureRequest request, string gameDirectory, string outputDirectory)
    {
        string boundary = "request";
        List<NativeEffectFixtureArtifact> artifacts = [];
        NativeEffectFixtureSelection? selection = null;
        try
        {
            Validate(request);
            Directory.CreateDirectory(outputDirectory);
            boundary = "original-action-row";
            using GameAssets game = new(gameDirectory);
            using BattleAssets assets = new(gameDirectory);
            BattleNativeAction native = SelectComponentAction(assets.NativeActions, request);
            selection = new(request.ControlledShellEffect is null, native.Command, native.ActionId, native.MagicId, native.KernelSection, native.KernelIndex, request.Effect);
            boundary = "original-resources";
            BattleEncounter formation = assets.LoadEncounter(request.EncounterId);
            BattleEnemyPlacement[] enemyPlacements = SelectEnemies(formation, request.PreserveOriginalFormationEnemies);
            BattleEnemyPlacement enemyPlacement = enemyPlacements[0];
            BattleStage stage = assets.LoadStage(formation.StageId);
            BattleCombatantDefinition[] actors =
            [
                new() { Id = "party", Name = request.PartyModelId == 0 ? "Squall" : "Party", IsPlayer = true, ModelId = request.PartyModelId, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == request.PartyModelId).Id, MaximumHp = 1000, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
                new() { Id = "enemy", Name = ComponentEnemyName(assets.ReadEnemyName(enemyPlacement.ModelId), enemyPlacement.ModelId), ModelId = enemyPlacement.ModelId, SceneSlot = enemyPlacement.Slot, Speed = 1, InitialHidden = enemyPlacement.Hidden, InitialUntargetable = enemyPlacement.Untargetable, InitialSuspended = enemyPlacement.InitiallySuspended }
            ];
            if (request.PreserveOriginalFormationEnemies)
            {
                actors = [actors[0], .. enemyPlacements.Select((enemy, index) => new BattleCombatantDefinition { Id = index == 0 ? "enemy" : $"enemy-{enemy.Slot}", Name = ComponentEnemyName(assets.ReadEnemyName(enemy.ModelId), enemy.ModelId), ModelId = enemy.ModelId, SceneSlot = enemy.Slot, Speed = 1, InitialHidden = enemy.Hidden, InitialUntargetable = enemy.Untargetable, InitialSuspended = enemy.InitiallySuspended })];
                File.WriteAllText(Path.Combine(outputDirectory, "original-formation.json"), JsonSerializer.Serialize(new { formation.Id, formation.StageId, Enemies = enemyPlacements }));
            }
            File.WriteAllText(Path.Combine(outputDirectory, "original-enemy-names.json"), JsonSerializer.Serialize(enemyPlacements.Select(enemy => new { enemy.Slot, enemy.ModelId, RawSceneLevel = enemy.Level, OriginalName = assets.ReadEnemyName(enemy.ModelId), ComponentName = ComponentEnemyName(assets.ReadEnemyName(enemy.ModelId), enemy.ModelId) })));
            if (request.AdditionalPartyModelIds is { Length: > 0 } additionalParty)
            {
                actors = [actors[0], .. additionalParty.Select((model, index) => new BattleCombatantDefinition { Id = $"party-{index + 1}", Name = "Party", IsPlayer = true, ModelId = model, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == model).Id, MaximumHp = 1000, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 }), .. actors.Where(actor => !actor.IsPlayer)];
            }
            if (request.ControlledDrainContact)
            {
                actors = actors.Select(actor => actor.IsPlayer ? actor : actor with { NativeStatusResistances = assets.ReadEnemyNativeStatusData(actor.ModelId).Resistances.ToArray() }).ToArray();
            }
            if (request.Effect == 62)
            {
                actors = actors.Select(actor => actor.IsPlayer ? actor : actor with { NativeSpecial = assets.ReadNativeEnemySpecial(actor.ModelId) }).ToArray();
            }
            if (request.RevivalContact)
            {
                actors = [actors[0], new() { Id = "target", Name = "Target", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 7, NativeDeathCount = 0 }, actors[1]];
            }
            if (request.DeferredGuardianFormulaAdmission)
            {
                actors[1] = actors[1] with { MaximumHp = 10000, CurrentHp = 10000 };
            }
            if (request.OrdinaryMagicAdmission)
            {
                if (request.Command != 2 || request.TargetActorIndex is not (0 or 3) || request.AdmissionProbeMaximumCallbacks == 0)
                {
                    throw new InvalidDataException("The ordinary Magic component requires its original command2 row, explicit party0/enemy3 target and producer budget.");
                }
                actors[0] = actors[0] with { Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(request.ActionId, 2)], Magic = 30, NativeStatusResistances = new byte[40] };
                actors[1] = actors[1] with { MaximumHp = 10000, CurrentHp = 10000, Spirit = 5, NativeStatusResistances = new byte[40] };
            }
            // The same declared fresh cold owners as the existing Thunder and guardian components.
            // This creates an independent fixture; it never fills missing fields in a saved game.
            BattleDefinition definition = new()
            {
                Combatants = actors,
                EncounterId = formation.Id,
                StageId = formation.StageId,
                ActionDefinitions = BattlePartyProjection.CreateActions(game.ReadNewGameCharacterVitals(), assets.ActionDefinitions),
                NativeActions = assets.NativeActions.ToArray(),
                NativeDevourEffects = request.Effect == 62 ? assets.ReadNativeDevourEffects() : [],
                NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
                NativeClockRules = request.OrdinaryMagicAdmission || request.Effect == 77 ? assets.ReadNativeClockRules() : null,
                NativeBattleSpeed = request.Effect == 77 ? game.ReadNewGameInputConfiguration().NativeBattleSpeedByte : null,
                NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
                NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
                NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
                NativeBattleUiRandom = new(0),
                NativeFlags = 0x80,
                NativeEntry = new(0, 0),
                NativeGlobalRandom = new(0),
                NativeCrtRandom = new(1),
                NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
                NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
                NativeModuleResources = game.ReadNativeBattleModuleResources(),
                NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
                NativeEffectMeshes = game.ReadNativeEffectMeshes(),
                NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([request.Effect]),
                NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
                NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
                NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
                NativeScannedModels = game.ReadNewGameScannedModels(),
                NativeScanAlwaysShow = (game.ReadNewGameInputConfiguration().Options & 0x100) != 0,
                NativeActionCameraRules = game.ReadNativeActionCameraRules(),
                NativeStageSource = stage.NativeSource,
                NativeStageCameraBank = stage.NativeCameraBank,
                NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
                NativeStageCameraControl = stage.NativeCameraControl
            };
            if (request.OrdinaryMagicAdmission)
            {
                definition = definition with { NativeStatusRules = assets.ReadNativeStatusRules(0), NativeBattleSpeed = 0 };
            }
            definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
            File.WriteAllText(Path.Combine(outputDirectory, "combatant-inputs.json"), JsonSerializer.Serialize(definition.Combatants.Select(actor => new { actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.SceneSlot, actor.Level, actor.InitialActive, actor.InitialHidden, actor.InitialSuspended, actor.InitialUntargetable, actor.MaximumHp, actor.CurrentHp, actor.Speed, actor.Attack, actor.Defense, actor.Magic, actor.Spirit, actor.PhysicalPower, actor.Hit, actor.Luck, actor.Evasion, actor.Costume, actor.WeaponId, actor.EquipmentIds, actor.InitialAtb, actor.ElementAttack, actor.ElementAttackPercent, actor.DrainPercent, actor.ElementDefenses, actor.DrawSpells })));
            boundary = "before-entry";
            BattleSession battle = new(definition, request.OrdinaryMagicAdmission ? BattleAtbMode.Normal : BattleAtbMode.TurnBased);
            BattleSnapshot fresh = battle.Capture();
            NativeBattleGraphicsState freshGraphics = fresh.NativeGraphics ?? throw new InvalidDataException("The fresh component requires its native graphics owner.");
            // Establish the fresh typed outcome owner without inventing values for its unknown nullable bytes.
            // Native entry and outcome callbacks publish their own reached fields.
            battle = BattleSession.Restore(fresh with { NativeGraphics = freshGraphics with { NativeOutcome = new NativeBattleOutcomeState() } });
            Save(boundary, battle.Capture());
            // Continue this component's actual entry producer; never replay travel or restore a partial owner.
            boundary = "battle-entry";
            for (int tick = 0; tick < request.EntryMaximumCallbacks && !battle.NativeEntryReady; tick++)
            {
                battle.Tick();
            }
            if (!battle.NativeEntryReady)
            {
                Save("entry-limit", battle.Capture());
                throw new InvalidDataException("The component did not reach NativeEntryReady within its explicit entry callback budget; this is not an effect duration bound.");
            }
            boundary = "after-entry";
            BattleSnapshot before = battle.Capture();
            Save(boundary, before);
            if (request.OrdinaryMagicAdmission)
            {
                boundary = "before-ordinary-input";
                Save(boundary, before);
                string targetId = before.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).Id;
                battle.QueueAction("party", targetId, BattleAbility.Magic, request.ActionId);
                for (int count = 0; count < request.AdmissionProbeMaximumCallbacks; count++)
                {
                    BattleSnapshot prior = battle.Capture();
                    try
                    {
                        battle.Tick();
                    }
                    catch (Exception error)
                    {
                        Save("before-ordinary-blocker", prior);
                        return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray(), selection) { Scenario = "Fresh declared-stock original-resource Magic command; ordinary logical formula/header producer; no manually supplied contacts or descriptor104" };
                    }
                    BattleSnapshot current = battle.Capture();
                    if (current.NativeGraphics!.Descriptors.Queue.Any(slot => current.NativeGraphics.Descriptors.Packets[slot].Command == 104))
                    {
                        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party");
                        if (caster.MagicStocks.Single(stock => stock.Id == request.ActionId).Count != 1)
                        {
                            throw new InvalidDataException("The ordinary producer did not consume exactly one declared spell stock.");
                        }
                        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(current))!);
                        if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(independent.Capture()))
                        {
                            throw new InvalidDataException("The ordinary produced-header snapshot failed independent JSON restore.");
                        }
                        boundary = "ordinary-produced-action104";
                        Save(boundary, current);
                        return new(request.Id, request.Effect, "fixture-only", boundary, null, artifacts.ToArray(), selection) { Scenario = "Fresh declared-stock original-resource Magic command; formula/header producer and stock2-to1; no manually supplied contacts or descriptor104", Pending = ["Independent next producer callback", "Whole ordinary action completion", "Native comparison"] };
                    }
                }
                Save("ordinary-producer-limit", battle.Capture());
                return new(request.Id, request.Effect, "blocked", boundary, "The ordinary producer did not enqueue action104 within its explicit diagnostic callback budget.", artifacts.ToArray(), selection) { Scenario = "Fresh declared-stock original-resource Magic command; ordinary producer; no manually supplied contact" };
            }
            boundary = "controlled-post-formula";
            BattleCombatantSnapshot target = before.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex);
            NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The current contact target lacks its native status owner.");
            if (request.TargetActorIndex < 3)
            {
                ushort priorSaved = request.RevivalContact ? (ushort)(status.Saved | 1) : status.Saved;
                target = target with { NativeStatus = status with { Presentation = new(priorSaved, status.Battle) } };
                if (request.RevivalContact)
                {
                    // This fresh controlled post-formula component keeps the prior KO display while the logical result already holds seven HP.
                    NativeBattlePartyPresentation party = before.NativePartyPresentation ?? throw new InvalidDataException("The revival fixture requires its initialized party display.");
                    before = before with { NativePartyPresentation = party with { Rows = party.Rows.SetItem(1, party.Rows[1]! with { HitPoints = 0 }) }, NativePartyStatusMirrors = before.NativePartyStatusMirrors!.Observe(1, priorSaved, status.Battle) };
                    NativeBattleGraphicsState priorGraphics = before.NativeGraphics!;
                    NativeBattleGraphicsModel targetModel = priorGraphics.Models[1];
                    uint flags = definition.NativeGraphics!.Rules.Project(priorSaved, status.Battle);
                    uint added = flags & ~(targetModel.Flags8 ?? throw new InvalidDataException("The revival fixture requires its existing graphics status."));
                    ImmutableArray<byte?> timers = targetModel.StatusTimers;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        if ((added & (1u << bit)) != 0)
                        {
                            timers = timers.SetItem(bit, 0);
                        }
                    }
                    before = before with { NativeGraphics = priorGraphics with { Models = priorGraphics.Models.SetItem(1, targetModel with { Flags8 = flags, StatusTimers = timers }) } };
                }
            }
            else
            {
                // Reuse the authenticated fresh manual-header input from the existing guardian fixture.
                byte remainingHits = NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game);
                target = target with { NativeParticipant = target.NativeParticipant! with { RemainingHits = remainingHits } };
            }
            before = before with { Combatants = before.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray() };
            NativeBattleActionPacketsState packets = battle.NativeActionPackets;
            BattleCombatantSnapshot selectedCaster = before.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.CasterActorIndex);
            byte sequence;
            if (request.CasterActorIndex >= 3)
            {
                BattleEnemyPlacement selectedEnemy = SelectCasterEnemy(formation, request);
                BattleCombatantDefinition selectedDefinition = definition.Combatants.Single(actor => actor.Id == selectedCaster.Id);
                if (selectedDefinition.IsPlayer || selectedDefinition.ModelId != selectedEnemy.ModelId || selectedDefinition.SceneSlot != selectedEnemy.Slot || before.NativeGraphics!.Models[request.CasterActorIndex].ModelId4 != selectedEnemy.NativeModelId)
                {
                    throw new InvalidDataException("The selected enemy caster's physical row contradicts its original formation model owner.");
                }
                sequence = SelectEnemySequence(assets.ReadEnemyAi(selectedEnemy.ModelId, selectedEnemy.Level), native);
            }
            else
            {
                sequence = SelectSequence(definition.NativeCommandSequences, native.Command, native.ActionId);
            }
            (packets, int header) = packets.Prepare(new(request.CasterActorIndex, native.Command, sequence, -1, native.ActionId, SelectHeaderEffect(native, request), null));
            // Deferred guardian admission replaces this placeholder through the actual Boost/formula callback.
            // Other component scenarios supply their declared post-formula contact.
            NativeBattleContactHalf secondary = new(255, 0, 0, 0, 0, 0, 0);
            if (request.ControlledDrainContact)
            {
                if ((native.Statuses1 & 0x8000) == 0 || native.StatusAttack != 200 || status.Resistances is not { Length: > 23 })
                {
                    throw new InvalidDataException("The selected controlled Absorb pair requires the actual drain flag, percent and target resistance owner.");
                }
                BattleCombatantSnapshot casterSnapshot = before.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.CasterActorIndex);
                NativeBattleStatusSnapshot casterStatus = casterSnapshot.NativeStatus ?? throw new InvalidDataException("The controlled drain caster lacks its current status owner.");
                // The explicit post-formula fixture publishes47E2D0's current cached status before its secondary contact.
                casterStatus = casterStatus with { Presentation = new(casterStatus.Saved, casterStatus.Battle) };
                before = before with { Combatants = before.Combatants.Select(actor => actor.Id == casterSnapshot.Id ? actor with { NativeStatus = casterStatus } : actor).ToArray() };
                if (((casterStatus.Saved ^ status.Saved) & 0x40) != 0)
                {
                    throw new InvalidDataException("This controlled healing pair requires the actual caster and target without reversed zombie drain.");
                }
                secondary = new(request.CasterActorIndex, 0, 0, 1, casterStatus.Saved, checked((ushort)Math.Min(9999, 7 * Math.Max(0, native.StatusAttack - status.Resistances[23]) / 100)), casterStatus.Battle);
            }
            (packets, _) = packets.AppendContact(new(new(request.TargetActorIndex, native.DamagePresentation, request.TargetActorIndex == 3 ? (byte)1 : (byte)0, request.HealingContact ? (byte)1 : (byte)0, status.Saved, request.ControlledContactValue ?? 7, status.Battle), secondary));
            (packets, _) = packets.Publish(1);
            NativeBattleGraphicsState graphics = before.NativeGraphics ?? throw new InvalidDataException("The current entry lacks its native graphics owner.");
            (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
            BattleSnapshot pending = before with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
            boundary = "queued-action104";
            Save(boundary, pending);
            if (request.AdmissionProbeMaximumCallbacks > 0)
            {
                battle = BattleSession.Restore(pending);
                boundary = "diagnostic-admission-prefix";
                TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
                for (int count = 0; count < request.AdmissionProbeMaximumCallbacks; count++)
                {
                    BattleSnapshot prior = battle.Capture();
                    try
                    {
                        battle.AdvanceNativeGraphicsPresentation(callback);
                    }
                    catch (Exception error)
                    {
                        Save("last-complete-before-blocker", prior);
                        return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray(), selection);
                    }
                    if (battle.NativeGraphics?.Callback != prior.NativeGraphics!.Callback + 1)
                    {
                        Save("last-complete-before-counter-blocker", prior);
                        throw new InvalidDataException("The diagnostic prefix did not advance exactly one native callback.");
                    }
                    if (battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0 } selected && selected.Binding.Effect == request.Effect)
                    {
                        Save("initialized-prefix", battle.Capture());
                        return new(request.Id, request.Effect, "fixture-only", boundary, "The live initialized entry was reached; effect execution and termination acceptance remain pending.", artifacts.ToArray(), selection);
                    }
                }
                Save("diagnostic-prefix-limit", battle.Capture());
                return new(request.Id, request.Effect, "fixture-only", boundary, "The explicit diagnostic prefix budget ended without a live initialized effect; this is not an effect duration bound or completion claim.", artifacts.ToArray(), selection);
            }
            return new(request.Id, request.Effect, "fixture-only", boundary, "Effect execution and termination contract were not supplied; no effect completion was attempted or accepted.", artifacts.ToArray(), selection)
            {
                Scenario = request.ControlledDrainContact ? $"Fresh installed-resource Absorb component; controlled post-formula primary7 and healing secondary{secondary.Amount6}, derived from original target drain resistance; manual action104 descriptor; no ordinary formula or menu admission" : request.ControlledContactValue is ushort contactValue ? $"Fresh installed-resource component; controlled post-formula contact value{contactValue}; manual action104 descriptor; no story continuation or formula admission" : request.ControlledShellEffect is not null ? "Explicit direct-module selection using an authenticated original command shell; controlled seven-HP contact; no original kernel reference to the requested module, formula or ordinary command admission" : request.DeferredGuardianFormulaAdmission ? "Fresh installed-resource guardian component; enemy starts at10000HP; manual action104 header contains a seven-HP placeholder, replaced by the actual deferred Boost4850A0 formula callback; ordinary command/UI producer unproved" : request.RevivalContact ? "Fresh installed-resource three-actor component; controlled post-formula seven-HP revival with prior KO display; manual action104 descriptor; no story continuation or formula admission" : request.HealingContact ? "Fresh installed-resource component; controlled post-formula seven-HP healing; manual action104 descriptor; no story continuation or formula admission" : "Fresh installed-resource component; controlled post-formula seven-HP damage; manual action104 descriptor; no story continuation or formula admission"
            };
        }
        catch (Exception error)
        {
            return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray(), selection);
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            string filename = name + ".battle.json";
            string path = Path.Combine(outputDirectory, filename);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
            using (FileStream stream = new(path, FileMode.CreateNew))
            {
                stream.Write(bytes);
            }
            BattleSession restored = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(path));
            if (JsonSerializer.Serialize(snapshot) != JsonSerializer.Serialize(restored.Capture()))
            {
                throw new InvalidDataException("The exported complete standalone changes during its immediate independent load.");
            }
            artifacts.Add(new(name, filename, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength, snapshot.Frame, snapshot.NativeGraphics?.Callback ?? throw new InvalidDataException("The exported component lacks its callback owner.")));
        }
    }
}
