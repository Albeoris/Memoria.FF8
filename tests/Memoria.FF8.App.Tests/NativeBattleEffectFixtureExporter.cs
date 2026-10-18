using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeEffectFixtureRequest(string Id, ushort Effect, byte Command, ushort ActionId, byte TargetActorIndex, int EntryMaximumCallbacks, int ProcessTimeoutSeconds)
{
    public int AdmissionProbeMaximumCallbacks { get; init; }
    public bool OrdinaryMagicAdmission { get; init; }
    public int PartyModelId { get; init; }
}
internal sealed record NativeEffectFixtureRequests(NativeEffectFixtureRequest[] Cases);
internal sealed record NativeEffectFixtureArtifact(string Boundary, string File, string Sha256, long Bytes, long Frame, long NativeCallback);
internal sealed record NativeEffectFixtureExportResult(string Id, ushort Effect, string Status, string Boundary, string? FirstBlocker, NativeEffectFixtureArtifact[] Artifacts)
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
        if (request.TargetActorIndex is not (0 or 3))
        {
            throw new InvalidDataException("This two-actor component supports explicit party0 or enemy3 contact targets only.");
        }
        if (request.PartyModelId is < 0 or > 10)
        {
            throw new InvalidDataException("The fresh component requires an explicit original party model index.");
        }
    }

    internal static BattleNativeAction SelectAction(IEnumerable<BattleNativeAction> actions, NativeEffectFixtureRequest request)
    {
        BattleNativeAction[] matches = actions.Where(action => action.Command == request.Command && action.ActionId == request.ActionId && action.MagicId == request.Effect).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("The fixture request does not identify exactly one original kernel command/action/effect row; effect aliases are not inferred.");
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
        try
        {
            Validate(request);
            Directory.CreateDirectory(outputDirectory);
            boundary = "original-action-row";
            using GameAssets game = new(gameDirectory);
            using BattleAssets assets = new(gameDirectory);
            BattleNativeAction native = SelectAction(assets.NativeActions, request);
            boundary = "original-resources";
            BattleEncounter formation = assets.LoadEncounter(94);
            BattleStage stage = assets.LoadStage(formation.StageId);
            BattleCombatantDefinition[] actors =
            [
                new() { Id = "party", Name = request.PartyModelId == 0 ? "Squall" : "Party", IsPlayer = true, ModelId = request.PartyModelId, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == request.PartyModelId).Id, MaximumHp = 1000, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0 },
                new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
            ];
            if (request.OrdinaryMagicAdmission)
            {
                if (request.Command != 2 || request.TargetActorIndex != 3 || request.AdmissionProbeMaximumCallbacks == 0)
                {
                    throw new InvalidDataException("The ordinary Magic component requires its original command2 row, enemy target and explicit producer budget.");
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
                NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
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
            boundary = "before-entry";
            BattleSession battle = new(definition, request.OrdinaryMagicAdmission ? BattleAtbMode.Normal : BattleAtbMode.TurnBased);
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
                battle.QueueAction("party", "enemy", BattleAbility.Magic, request.ActionId);
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
                        return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray()) { Scenario = "Fresh declared-stock original-resource Magic command; ordinary logical formula/header producer; no manually supplied contacts or descriptor104" };
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
                        return new(request.Id, request.Effect, "fixture-only", boundary, null, artifacts.ToArray()) { Scenario = "Fresh declared-stock original-resource Magic command; formula/header producer and stock2-to1; no manually supplied contacts or descriptor104", Pending = ["Independent next producer callback", "Whole ordinary action completion", "Native comparison"] };
                    }
                }
                Save("ordinary-producer-limit", battle.Capture());
                return new(request.Id, request.Effect, "blocked", boundary, "The ordinary producer did not enqueue action104 within its explicit diagnostic callback budget.", artifacts.ToArray()) { Scenario = "Fresh declared-stock original-resource Magic command; ordinary producer; no manually supplied contact" };
            }
            boundary = "controlled-post-formula";
            BattleCombatantSnapshot target = before.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex);
            NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The current contact target lacks its native status owner.");
            if (request.TargetActorIndex == 0)
            {
                target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
            }
            else
            {
                // Reuse the authenticated fresh manual-header input from the existing guardian fixture.
                byte remainingHits = NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game);
                target = target with { NativeParticipant = target.NativeParticipant! with { RemainingHits = remainingHits } };
            }
            before = before with { Combatants = before.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray() };
            NativeBattleActionPacketsState packets = battle.NativeActionPackets;
            byte sequence = SelectSequence(definition.NativeCommandSequences, native.Command, native.ActionId);
            (packets, int header) = packets.Prepare(new(0, native.Command, sequence, -1, native.ActionId, native.MagicId, null));
            // Explicit scenario input substitutes an already computed seven-HP damage contact.
            // The native kernel supplies command/action/effect/presentation; no formula or RNG is replayed.
            (packets, _) = packets.AppendContact(new(new(request.TargetActorIndex, native.DamagePresentation, request.TargetActorIndex == 3 ? (byte)1 : (byte)0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
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
                        return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray());
                    }
                    if (battle.NativeGraphics?.Callback != prior.NativeGraphics!.Callback + 1)
                    {
                        Save("last-complete-before-counter-blocker", prior);
                        throw new InvalidDataException("The diagnostic prefix did not advance exactly one native callback.");
                    }
                    if (battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: > 0 } selected && selected.Binding.Effect == request.Effect)
                    {
                        Save("initialized-prefix", battle.Capture());
                        return new(request.Id, request.Effect, "fixture-only", boundary, "The live initialized entry was reached; effect execution and termination acceptance remain pending.", artifacts.ToArray());
                    }
                }
                Save("diagnostic-prefix-limit", battle.Capture());
                return new(request.Id, request.Effect, "fixture-only", boundary, "The explicit diagnostic prefix budget ended without a live initialized effect; this is not an effect duration bound or completion claim.", artifacts.ToArray());
            }
            return new(request.Id, request.Effect, "fixture-only", boundary, "Effect execution and termination contract were not supplied; no effect completion was attempted or accepted.", artifacts.ToArray());
        }
        catch (Exception error)
        {
            return new(request.Id, request.Effect, "blocked", boundary, error.GetType().Name + ": " + error.Message, artifacts.ToArray());
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
