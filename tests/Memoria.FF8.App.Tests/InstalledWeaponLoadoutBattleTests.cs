using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Installed native host checks from an externally selected standalone field save.</summary>
public sealed class InstalledWeaponLoadoutBattleTests
{
    private const ushort Formation = 87;
    public static IEnumerable<object[]> StylePairs()
    {
        foreach (int recipient in new[] { 0, 6 })
        {
            foreach (int donor in Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount))
            {
                yield return [recipient, donor];
            }
        }
        yield return [4, 1];
        yield return [10, 9];
    }

    [Fact]
    public void Independent_style_rows_cover_every_actual_installed_weapon_owner()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        int[] actual = assets.WeaponDefinitions.Select(weapon => weapon.CharacterId).Distinct().Order().ToArray();
        int[] tested = StylePairs().Where(pair => (int)pair[0] == 0).Select(pair => (int)pair[1]).Distinct().Order().ToArray();
        Assert.Equal(actual, tested);
    }

    [Theory]
    [MemberData(nameof(StylePairs))]
    public void Every_installed_weapon_style_enters_and_attacks_without_changing_recipient_body(int recipient, int donor)
    {
        Run(recipient, donor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Native_no_override_baseline_enters_attacks_and_returns(int recipient)
    {
        Run(recipient, recipient, assignOverride: false);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void Native_ready_grip_probe_when_requested(int character)
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_READY_PROBE")), "Set MEMORIA_FF8_LOADOUT_READY_PROBE to an external compact grip-probe output directory.");
        Run(character, character, assignOverride: false, readyProbeOnly: true);
    }

    private static void Run(int recipient, int donor, bool assignOverride = true, bool readyProbeOnly = false)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("weapon-limit-field-source");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets game = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameState loaded = PlayerStateLoader.Load(source, directory, sharedAssets: game, resources: selected.Resources);
        Assert.Equal(SceneKind.Field, loaded.Scene);
        Assert.Null(loaded.Battle);
        Assert.Null(loaded.PendingBattleEncounter);
        GameSession session = new(loaded);
        using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false);
        world.EnsureField();
        Assert.False(session.CharacterVitals!.DreamEnabled);
        NativeCharacterVitals vitals = session.CharacterVitals.WithExtendedRoster(true);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[recipient] = records[recipient] with { MemberFlags = (ushort)(records[recipient].MemberFlags | 1), Status = 0 };
        session.SetCharacterVitals((vitals with { Records = records, CombatParty = [checked((byte)recipient), 255, 255], AdditionalCombatParty = null, AdditionalCombatPartyMinimumCapacities = null }).RebuildParty().Rest());
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 0 });
        BattleAssets resources = world.BattleResources;
        int weapon = assignOverride ? resources.WeaponDefinitions.First(definition => definition.CharacterId == donor).Id : session.CharacterVitals.GetBattleWeapon(recipient);
        donor = resources.WeaponDefinitions[weapon].CharacterId;
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft([new(recipient, session.CharacterVitals.GetBattleWeapon(recipient), BattleLimitFamilyRules.NativeFamilies(recipient))]);
        if (assignOverride)
        {
            draft.AssignWeapon(recipient, weapon);
            session.ApplyBattleLoadoutDraft(draft);
        }
        BattleLoadoutOverrides? assignments = session.State.BattleLoadouts;
        List<long[]> damageTimings = [];
        int sourceField = session.State.FieldId;

        for (int encounter = 0; encounter < 2; encounter++)
        {
            RequestEncounter(world, session);
            if (encounter == 0 && Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_PREPARE_DIRECTORY") is { Length: > 0 } preparedDirectory)
            {
                Directory.CreateDirectory(preparedDirectory);
                string preparedPath = Path.Combine(preparedDirectory, $"recipient-{recipient:00}-style-{donor:00}{(assignOverride ? "" : "-native")}-encounter-{Formation:000}.ff8save");
                if (!File.Exists(preparedPath))
                {
                    session.Save(preparedPath, compact: true);
                    File.WriteAllText(Path.ChangeExtension(preparedPath, ".json"), System.Text.Json.JsonSerializer.Serialize(new { SourceResource = "weapon-limit-field-source", SourceSha256 = Convert.ToHexString(sourceHash), StandalonePath = Path.GetFullPath(preparedPath), Formation, Recipient = recipient, Donor = donor, WeaponId = weapon, NativeBaseline = !assignOverride, Field = session.State.FieldId, FieldFrame = world.Events.Frame, Boundary = "Standalone prepared field with one pending ordinary native encounter; StartEncounter is the next control command.", Resume = $"Memoria.FF8.exe --state \"{Path.GetFullPath(preparedPath)}\" --game-dir \"{directory}\"" }));
                }
            }
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.IsPlayer);
            Assert.Equal(recipient, actor.ModelId);
            BattleCombatantDefinition definition = battle.GetCombatantDefinition(actor.Id);
            Assert.Equal(weapon, definition.WeaponId);
            Assert.Equal(donor, definition.WeaponProfile!.ResourceOwner);
            Assert.Equal(recipient, definition.NativePlayerStatRules!.CharacterId);
            BattleModel body = resources.LoadPlayer(recipient, definition.Costume);
            BattleModel composed = resources.LoadPlayerForWeapon(recipient, definition.Costume, weapon);
            Assert.Equal(body.Name, composed.Name);
            Assert.Equal(body.Bones, composed.Bones);
            NativeBattleGraphicsResource compiled = resources.ReadNativeGraphicsResource(definition);
            NativeBattleGraphicsResource donorResource = resources.ReadNativeGraphicsResource(definition with { ModelId = donor, Costume = 0 });
            Assert.Equal(donorResource.SequenceSection.ToArray(), compiled.SequenceSection.ToArray());
            Assert.Equal(body.NativePoseSource!.Skeleton.ToArray(), compiled.PoseSource!.Skeleton.ToArray());
            compiled.Validate();
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: actor.Id);
            TickUntil(world, () => battle.NativeEntryReady, $"recipient {recipient}, donor {donor}, weapon {weapon}: native entry");
            actor.Atb = BattleSession.AtbMaximum;
            TickUntil(world, () => battle.CanChoosePlayerCommand(actor.Id), $"recipient {recipient}, donor {donor}, weapon {weapon}: player readiness");
            if (readyProbeOnly)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 8));
                Assert.True(battle.CanChoosePlayerCommand(actor.Id));
                EmitReadyGripProbe(battle, actor, definition, resources);
                Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
                return;
            }
            BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive && !candidate.Untargetable);
            int eventStart = battle.Events.Count;
            long commandFrame = battle.Frame;
            int targetHp = target.CurrentHp;
            world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { ForcePhysical = true });
            TickUntil(world, () => battle.Events.Skip(eventStart).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id) && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, $"recipient {recipient}, donor {donor}, weapon {weapon}: native attack completion");
            BattleEvent[] damage = battle.Events.Skip(eventStart).Where(entry => entry.Kind == "Damage" && entry.ActorId == actor.Id && entry.TargetId == target.Id).ToArray();
            Assert.NotEmpty(damage);
            Assert.Contains(damage, entry => entry.Amount > 0);
            BattleEvent completion = battle.Events.Skip(eventStart).First(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id);
            Assert.All(damage, entry => Assert.InRange(entry.Frame, commandFrame, completion.Frame));
            damageTimings.Add(damage.Select(entry => entry.Frame - commandFrame).ToArray());
            Assert.True(target.CurrentHp < targetHp);
            Assert.Equal(recipient, actor.ModelId);
            Assert.Equal(weapon, battle.GetCombatantDefinition(actor.Id).WeaponId);
            Assert.Equal(assignments?.Get(recipient)?.WeaponId, session.State.BattleLoadouts?.Get(recipient)?.WeaponId);
            // The action above is ordinary native combat. Explicit debug victory only bounds lifecycle cleanup.
            if (!battle.CanReturnToField)
            {
                ExpandedBattleInstalledEntry.CompleteDebugVictory(world);
            }
            TickUntil(world, () => battle.CanReturnToField, $"recipient {recipient}, donor {donor}, weapon {weapon}: battle return");
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(SceneKind.Field, session.State.Scene);
            Assert.Equal(sourceField, session.State.FieldId);
            Assert.Equal(assignments?.Get(recipient)?.WeaponId, session.State.BattleLoadouts?.Get(recipient)?.WeaponId);
        }
        Assert.Equal(damageTimings[0], damageTimings[1]);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static void EmitReadyGripProbe(BattleSession battle, BattleCombatant actor, BattleCombatantDefinition definition, BattleAssets resources)
    {
        NativeBattleGraphicsState graphics = battle.Presentation!.NativeGraphics!;
        BattlePartyActorModel owner = graphics.ActorModels.Owners.OfType<BattlePartyActorModel>().Single(candidate => candidate.Actor.ActorId == actor.Id);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        BattleModel body = resources.LoadPlayerForWeapon(definition.ModelId, definition.Costume, definition.WeaponId);
        BattleModel weapon = resources.ResolvePlayerRenderWeapon(definition.ModelId, definition.WeaponId!.Value, body) ?? throw new InvalidDataException("READY grip probe requires a rendered native weapon model.");
        ImmutableArray<NativeBattleActionMatrix> bodyBones = NativeBattleBodyBoneMatrices.Require(model, body.NativePoseSource!);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("READY grip probe requires the actual native actor world matrix.");
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        ImmutableArray<NativeBattleActionMatrix> weaponBones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, weapon.NativePoseSource!, identity);
        IReadOnlyList<NativeBattleSemanticBonePair> semantics = BattleHumanoidRigProfiles.SemanticPairs(definition.ModelId, body, definition.ModelId, body);
        BattleVertex[] bodyVertices = (body.NativeControlMeshes ?? body.Meshes).SelectMany(mesh => mesh.Vertices).ToArray();
        BattleVertex[] weaponVertices = (weapon.NativeControlMeshes ?? weapon.Meshes).SelectMany(mesh => mesh.Vertices).ToArray();
        System.Numerics.Vector3[] weaponPoints = weaponVertices.Select(vertex => Transform(weaponBones[vertex.Bone], vertex)).ToArray();
        Assert.NotEmpty(weaponPoints);
        string[] chainNames = ["shoulder-a", "shoulder-tip-a", "upper-arm-a", "forearm-a", "hand-a", "shoulder-b", "shoulder-tip-b", "upper-arm-b", "forearm-b", "hand-b"];
        var pivots = chainNames.Select(joint =>
        {
            int bone = semantics.Single(pair => pair.Joint == joint).Recipient;
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, bodyBones[bone]);
            return new { Joint = joint, Bone = bone, matrix.X, matrix.Y, matrix.Z };
        }).ToArray();
        var distances = new[] { "forearm-a", "hand-a", "forearm-b", "hand-b" }.Select(joint =>
        {
            int bone = semantics.Single(pair => pair.Joint == joint).Recipient;
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, bodyBones[bone]);
            System.Numerics.Vector3[] hand = bodyVertices.Where(vertex => vertex.Bone == bone).Select(vertex => Transform(matrix, vertex)).ToArray();
            float? nearest = hand.Length == 0 ? null : hand.Min(point => weaponPoints.Min(weaponPoint => System.Numerics.Vector3.Distance(point, weaponPoint)));
            return new { Joint = joint, Bone = bone, Vertices = hand.Length, MinimumVertexDistance = nearest };
        }).ToArray();
        var observation = new { Character = definition.ModelId, definition.WeaponId, Formation, battle.Frame, model.Sequence, model.SequenceFrame, Pose = model.SequenceState?.Pose, PoseFrame = model.SequenceState?.PoseFrame114, PoseFlags = model.SequenceState?.PoseFlags109, BodyBones = bodyBones.Length, WeaponBones = weaponBones.Length, WeaponVertices = weaponPoints.Length, EffectiveLinkedTransform = model.ReadEffectiveLinkedTransform(), model.LinkedContactOffset, Pivots = pivots, HandMeshDistances = distances };
        string json = System.Text.Json.JsonSerializer.Serialize(observation);
        Console.WriteLine("READY-GRIP " + json);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_READY_PROBE")!;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, $"ready-native-{definition.ModelId:00}.json"), json);

        static System.Numerics.Vector3 Transform(NativeBattleActionMatrix matrix, BattleVertex vertex)
        {
            NativeBattleVector point = vertex.NativePosition ?? throw new InvalidDataException("READY grip probe requires an original native vertex.");
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, point);
            return new(matrix.X + rotated.X, matrix.Y + rotated.Y, matrix.Z + rotated.Z);
        }
    }

    private static void RequestEncounter(GameWorld world, GameSession session)
    {
        string program = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
        session.SetRandomEncounters(new(true, random, new(SceneKind.Field, session.State.FieldId, world.Events.Frame, Formation, program)));
    }

    private static void TickUntil(GameWorld world, Func<bool> complete, string boundary)
    {
        for (int callback = 0; callback < 1800 && !complete(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(complete(), "Timed out at " + boundary + ": " + string.Join(", ", world.Battle?.Events.TakeLast(8).Select(entry => entry.Kind + "/" + entry.ActorId) ?? []));
    }
}
