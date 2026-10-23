using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePcSaveConverterTests
{
    [Fact]
    public void Original_export_rejects_guardian_forms_preference_instead_of_losing_it()
    {
        GameState source = Defaults() with { GameplayPreferences = new GameplayPreferences { GuardianFormsEnabled = true } };
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(source));
        Assert.Contains("Choose a Memoria save slot", exception.Message);
    }

    [Fact]
    public void Original_import_defaults_guardian_forms_to_off_even_with_enabled_installation_defaults()
    {
        byte[] original = NativePcSaveConverter.Export(Defaults());
        GameState enabledDefaults = Defaults() with { GameplayPreferences = new GameplayPreferences { GuardianFormsEnabled = true } };
        GameState imported = NativePcSaveConverter.Import(original, enabledDefaults, _ => "fixture");
        Assert.False(imported.GameplayPreferences.GuardianFormsEnabled);
    }

    [Fact]
    public void Transfer_uses_saved_progress_and_native_pose_instead_of_new_game_progress()
    {
        GameState source = Defaults() with { FieldId = 165, FieldName = "fixture", PreviousFieldId = 177, FieldArrival = new FieldTransition(165, -723, -8555, 12, 96), EventMemory = NativeEventMemory.Empty.Write(401, 1, NativeScalarValue.Exact(91)) };
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        vitals.Records[0] = vitals.Records[0] with { Experience = 5200, HitPoints = new NativeHpWord(432, 432), DeathCount = 8, GuardianForceCompatibility = Enumerable.Repeat((ushort)678, 16).ToArray(), Junction = new NativeCharacterJunction(3, 0x04030201, 0x08070605, Enumerable.Repeat((byte)1, 20).ToArray()) };
        vitals.Records[0].MagicInventory[0] = new NativeMagicStock(1, 77);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Experience = 12000, LearnedBank = new NativeGuardianForceLearnedBank(0x123456789, 0x76543210), KillCount = 19, DeathCount = 2, Learning = 4, ForgottenAbilities = 0x123456 };
        NativeEconomy economy = source.Economy!.DeepCopy();
        economy.Items[13] = new NativeItemStock(82, 67);
        source = source with { CharacterVitals = vitals, Economy = economy with { Gil = 1234567, PastGil = 87654 }, Playtime = new NativePlaytimeState(54321, 0, 0), CardInventory = source.CardInventory! with { CollectionFlags = 1, TripleTriadStatistics = new TripleTriadStatistics(8, 4, 2) } };
        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), Defaults(), id => $"field{id}");
        Assert.Equal(165, imported.FieldId);
        Assert.Equal("field165", imported.FieldName);
        Assert.Equal(source.FieldArrival, imported.FieldArrival);
        Assert.Equal((uint)1234567, imported.Economy!.Gil);
        Assert.Equal(new NativeItemStock(82, 67), imported.Economy.Items[13]);
        Assert.Equal((uint)5200, imported.CharacterVitals!.Records[0].Experience);
        Assert.Equal((ushort)432, imported.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(new NativeMagicStock(1, 77), imported.CharacterVitals.Records[0].MagicInventory[0]);
        Assert.Equal(vitals.Records[0].Junction, imported.CharacterVitals.Records[0].Junction);
        Assert.Equal((ushort)678, imported.CharacterVitals.Records[0].GuardianForceCompatibility![0]);
        Assert.Equal(vitals.GuardianForces[0].LearnedBank, imported.CharacterVitals.GuardianForces[0].LearnedBank);
        Assert.Equal((uint)0x123456, imported.CharacterVitals.GuardianForces[0].ForgottenAbilities);
        Assert.Equal((uint)54321, imported.Playtime!.Seconds);
        Assert.Equal(91, imported.EventMemory.Read(401, 1).Values[0]);
        Assert.Equal(new TripleTriadStatistics(8, 4, 2), imported.CardInventory!.TripleTriadStatistics);
        Assert.Null(imported.EventState);
        Assert.Equal(NativeFieldLoadOrigin.OriginalSave, imported.FieldLoadContext!.Origin);
        Assert.Equal(NativeSavedWorldOriginKind.OriginalSave, imported.SavedWorld!.Origin.Kind);
        _ = new GameSession(imported);
    }

    [Fact]
    public void Subsequent_export_updates_owners_and_preserves_unmodeled_native_bytes_and_names()
    {
        byte[] file = NativePcSaveConverter.Export(Defaults());
        NativePcSaveData original = NativePcSaveCodec.Decode(file);
        original.Block[NativePcSaveData.MainOffset + 16] = 197;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture");
        JsonElement snapshot = JsonSerializer.SerializeToElement(new { Scene = new { Names = new Dictionary<int, string> { [0] = "Leon", [16] = "Storm" } } });
        imported = imported with { EventState = snapshot, Economy = imported.Economy! with { Gil = 7654321 } };
        GameState transferred = NativePcSaveConverter.Import(NativePcSaveConverter.Export(imported), Defaults(), _ => "fixture");
        Assert.Equal((byte)197, transferred.OriginalSave!.Block[NativePcSaveData.MainOffset + 16]);
        Assert.Equal((uint)7654321, transferred.Economy!.Gil);
        Assert.Equal("Leon", transferred.OriginalSave.Names[0]);
        Assert.Equal("Storm", transferred.OriginalSave.Names[16]);
    }

    [Fact]
    public void Allocated_or_inactive_actors_do_not_replace_the_safe_saved_field_pose()
    {
        GameState source = Defaults() with { FieldId = 165, FieldArrival = new FieldTransition(165, -723, -8555, 12, 96), EventState = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 0, 255, 255 }, NativeFieldInitialization = new { Stage = "Allocated" } }, Actors = new[] { new { CharacterId = 0, Active = true, X = 0, Y = 0, WalkmeshTriangle = 65535, Direction = 0 } } }) };
        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), Defaults(), _ => "fixture");
        Assert.Equal(source.FieldArrival, imported.FieldArrival);
    }

    [Fact]
    public void Foot_world_pose_and_saved_vehicle_state_transfer_without_field_reconstruction()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { Slot4 = new NativeSavedWorldVehicleSlot(73, new NativeSavedWorldPose(100, 200, 3, 400)), FuelDistance112 = 1234, WorldEventFlags116 = new NativeWorldEventFlags(0x123456789ABCDEF0, ulong.MaxValue) };
        GameState source = Defaults() with { Scene = SceneKind.WorldMap, FieldId = 165, PreviousFieldId = 165, FieldLoadContext = null, SavedWorld = saved, WorldArrival = new WorldTransition(62, 0, -1), World = new WorldState { X = -1234, Z = 5678, NativeHeight = 99, BodyYaw = 512, CameraYaw = 1024 } };
        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), Defaults(), _ => "fixture");
        Assert.Equal(SceneKind.WorldMap, imported.Scene);
        Assert.Equal(new WorldTransition(62, 0, -1), imported.WorldArrival);
        Assert.Null(imported.EventState);
        Assert.Equal(new NativeSavedWorldPose(-1234, 5678, 99, 512), imported.SavedWorld!.FootReturn!.Pose0);
        Assert.Equal(saved.Slot4, imported.SavedWorld.Slot4);
        Assert.Equal((uint)1234, imported.SavedWorld.FuelDistance112);
        Assert.Equal(saved.WorldEventFlags116, imported.SavedWorld.WorldEventFlags116);
    }

    [Fact]
    public void Unsupported_world_entity_normalizes_to_a_default_foot_loader_with_saved_progress()
    {
        GameState source = Defaults() with { Scene = SceneKind.WorldMap, FieldId = 165, PreviousFieldId = 165, FieldLoadContext = null, WorldArrival = new WorldTransition(62, 0, -1), World = new WorldState { X = 100, Z = 200, NativeHeight = 3, BodyYaw = 4, CameraYaw = 5 } };
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(source));
        data.Block[NativePcSaveData.MainOffset + 4624 + 105] = 33;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture");
        Assert.Equal(new WorldTransition(62, 0, -1), imported.WorldArrival);
        Assert.Equal(new NativeSavedWorldPose(100, 200, 3, 4), imported.SavedWorld!.FootReturn!.Pose0);
        Assert.Equal(source.Economy!.Gil, imported.Economy!.Gil);
    }

    [Fact]
    public void Empty_title_state_cannot_export_an_all_dead_placeholder()
    {
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(new GameState()));
    }

    [Fact]
    public void Imported_field_later_entry62_uses_its_saved_native_foot_and_parked_garden_poses()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeSavedWorldPose parked = new(30000, -7000, -120, 64);
        WorldState foot = new() { Frame = 71, X = 31500, Z = -6200, NativeHeight = -32, BodyYaw = 165, CameraYaw = 199 };
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame().WithFootReturn(NativeSavedWorldFootReturn.Create(foot, 257, 750, 3, 0, 0)) with { GardenPose3 = parked };
        GameState field = Defaults() with { FieldId = 257, FieldArrival = new FieldTransition(257, 10, 20, 0, 0), SavedWorld = saved, WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750) };
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(field));
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(388), 500);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture");
        Assert.Equal(SceneKind.Field, imported.Scene);
        Assert.Equal(NativeSavedWorldFootReturn.OriginalSaveRule, imported.SavedWorld!.FootReturn!.Rule);
        Assert.Equal((short)0, imported.SavedWorld.FootReturn.MapLocationId);
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(new WorldTransition(62, 0, -1), imported.WorldEntryMemory!, imported.EventMemory, imported.SavedWorld, imported.CharacterVitals!);
        Assert.Equal(parked, allocation.GardenPose);
        Assert.Equal(new NativeWorldPathPosition(30000, -120, 7000), allocation.Objects[allocation.Garden64Index].Position);
        Assert.Equal(new NativeWorldPathPosition(31500, -32, 6200), allocation.Objects[allocation.FootObjectIndex].Position);
        Assert.Equal((ushort)199, allocation.CameraYaw);
        Assert.Equal(new NativeSavedWorldPose(31500, -6200, -32, 165), imported.SavedWorld.FootReturn.Pose0);
    }

    [Theory]
    [InlineData(48)]
    [InlineData(50)]
    public void Imported_field_retains_vehicle_saved_reload_auxiliaries(int entity)
    {
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        int world = NativePcSaveData.MainOffset + 4624;
        int pose = world + (entity == 48 ? 36 : 24);
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(pose), 12345);
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(pose + 4), 23456);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(pose + 8), 77);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(pose + 10), 123);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 96), 321);
        data.Block[world + 105] = (byte)entity;
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(388), 500);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture");
        NativeSavedWorldState saved = imported.SavedWorld!;
        if (entity == 48)
        {
            Assert.Equal(NativeSavedWorldGardenReturn.OriginalSaveRule, saved.GardenReturn!.Rule);
            Assert.Equal(new NativeSavedWorldPose(12345, 23456, 77, 123), saved.GardenReturn.Pose3);
            Assert.Equal((ushort)321, saved.GardenReturn.CameraYaw96);
        }
        else
        {
            Assert.Equal(NativeSavedWorldRagnarokReturn.OriginalSaveRule, saved.RagnarokReturn!.Rule);
            Assert.Equal(new NativeSavedWorldPose(12345, 23456, 77, 123), saved.RagnarokReturn.Pose2);
            Assert.Equal((ushort)321, saved.RagnarokReturn.CameraYaw96);
        }
    }

    [Fact]
    public void Imported_field_defaults_an_unrepresentable_saved_camera_without_changing_its_valid_world_pose()
    {
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        int world = NativePcSaveData.MainOffset + 4624;
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(world), 2855);
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(world + 4), unchecked((uint)-23150));
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 8), 683);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 10), 3113);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 96), 0xF9C5);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture");
        Assert.Equal(new NativeSavedWorldPose(2855, -23150, 683, 3113), imported.SavedWorld!.FootReturn!.Pose0);
        Assert.Equal((ushort)0, imported.SavedWorld.FootReturn.CameraYaw96);
        Assert.Equal((ushort)0xF9C5, BinaryPrimitives.ReadUInt16LittleEndian(imported.OriginalSave!.Block.AsSpan(world + 96)));
    }

    [Fact]
    public void Installed_original_fixture_imports_and_exports_current_initialized_owners()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture), "Set MEMORIA_FF8_PC_SAVE_FIXTURE to an original PC save.");
        using GameAssets assets = new(TestResources.GameDirectory);
        GameState imported = NativePcSaveConverter.Import(File.ReadAllBytes(fixture!), assets);
        GameState transferred = NativePcSaveConverter.Import(NativePcSaveConverter.Export(imported), assets);
        Assert.Equal(imported.FieldId, transferred.FieldId);
        Assert.Equal(imported.FieldArrival, transferred.FieldArrival);
        Assert.Equal(imported.Economy!.Gil, transferred.Economy!.Gil);
        Assert.Equal(imported.CharacterVitals!.Records.Select(row => row.Experience), transferred.CharacterVitals!.Records.Select(row => row.Experience));
        Assert.Equal(imported.CardInventory!.Cards, transferred.CardInventory!.Cards);
        Assert.Equal(imported.OriginalSave!.Names.OrderBy(pair => pair.Key), transferred.OriginalSave!.Names.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void LegacyGuardianForceProjectionMapsProgressByIdentityAndPreservesLiveState()
    {
        GameState target = Defaults();
        NativeCharacterVitals source = target.CharacterVitals!;
        ImmutableArray<NativeGuardianForceLearningRule> rules = source.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 1)).SetItem(1, new(1, 255, 2));
        source = source with { MenuDefinitions = source.MenuDefinitions with { Learning = source.MenuDefinitions.Learning.SetItem(0, rules) } };
        NativeCharacterVitals edited = source.DeepCopy() with { MenuDefinitions = source.MenuDefinitions with { Learning = source.MenuDefinitions.Learning.SetItem(0, rules.SetItem(0, rules[1]).SetItem(1, rules[0])) } };
        edited.GuardianForces[0] = edited.GuardianForces[0] with { AbilityProgress = edited.GuardianForces[0].AbilityProgress.SetItem(0, 17).SetItem(1, 29), ForgottenAbilities = 1, Learning = 1 };
        GameState active = target with { CharacterVitals = edited };
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(active, source);
        Assert.True(plan.RequiresConfirmation);
        Assert.Equal((byte)29, plan.ProjectedState.CharacterVitals!.GuardianForces[0].AbilityProgress[0]);
        Assert.Equal((byte)17, plan.ProjectedState.CharacterVitals.GuardianForces[0].AbilityProgress[1]);
        Assert.Equal(2u, plan.ProjectedState.CharacterVitals.GuardianForces[0].ForgottenAbilities);
        Assert.Equal(1u, active.CharacterVitals!.GuardianForces[0].ForgottenAbilities);
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(active, source));
        byte[] bytes = NativePcSaveConverter.Export(active, source, true);
        GameState imported = NativePcSaveConverter.Import(bytes, target with { CharacterVitals = source }, _ => "fixture");
        Assert.Equal((byte)29, imported.CharacterVitals!.GuardianForces[0].AbilityProgress[0]);
    }

    [Fact]
    public void LegacyGuardianForceProjectionReviewsExpandedDefaultsAndDropsOnlyUnrepresentableState()
    {
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!;
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = source.ResolveGuardianForceTemplates().SetItem(0, Enumerable.Range(1, 50).Select(ability => new GuardianForceAbilityTemplate(ability, true, 10, LearningPriority: ability)).ToImmutableArray());
        NativeCharacterVitals expanded = source.ApplyGuardianForceTemplates(templates);
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(state with { CharacterVitals = expanded }, source);
        Assert.True(plan.RequiresConfirmation);
        Assert.Equal(28, plan.Losses.Count(loss => loss.Kind == "Capacity"));
        Assert.True(plan.ProjectedState.CharacterVitals!.GuardianForces[0].LearnedBank.Has(22));
        Assert.False(plan.ProjectedState.CharacterVitals.GuardianForces[0].LearnedBank.Has(23));
        Assert.True(expanded.GuardianForces[0].LearnedBank.Has(50));
        Assert.Null(plan.ProjectedState.CharacterVitals.GuardianForces[0].Progression);
        Assert.Null(plan.ProjectedState.CharacterVitals.GuardianForces[0].LastProjection);
        Assert.True(plan.ProjectedState.CharacterVitals.MenuDefinitions.Templates.IsDefault);
        Assert.NotEmpty(NativePcSaveConverter.Export(state with { CharacterVitals = expanded }, source, true));
    }

    [Fact]
    public void LegacyGuardianForceProjectionPreservesRepresentableItemTaughtAbility()
    {
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!;
        NativeCharacterVitals taught = source.TeachGuardianForceAbility(0, 42);
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(state with { CharacterVitals = taught }, source);
        Assert.Empty(plan.Losses);
        Assert.True(plan.ProjectedState.CharacterVitals!.GuardianForces[0].LearnedBank.Has(42));
    }

    [Fact]
    public void LegacyGuardianForceProjectionReviewsLossOfAttributionEvenWhenValuesMatchSource()
    {
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!;
        GuardianForceAbilityProfile profile = new(source.ResolveGuardianForceTemplates(), "source", [], [], []);
        GameState attributed = state with { CharacterVitals = source with { GuardianForceProfile = profile } };
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(attributed, source);
        Assert.Contains(plan.Losses, loss => loss.Kind == "Attribution");
        Assert.Null(plan.ProjectedState.CharacterVitals!.GuardianForceProfile);
        Assert.Same(profile, attributed.CharacterVitals!.GuardianForceProfile);
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(attributed));
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(attributed, source));
        GameState recovered = state with { CharacterVitals = source with { GuardianForceRecoveryProfile = profile, GuardianForceOpaqueBaseline = true, GuardianForceCompatibilityDiagnostics = ["Recovered attribution"] } };
        NativeGuardianForceLegacyExportPlan recoveryPlan = NativeGuardianForceLegacyCompatibility.Analyze(recovered, source);
        Assert.Contains(recoveryPlan.Losses, loss => loss.Kind == "Attribution");
        Assert.Null(recoveryPlan.ProjectedState.CharacterVitals!.GuardianForceRecoveryProfile);
        Assert.False(recoveryPlan.ProjectedState.CharacterVitals.GuardianForceOpaqueBaseline);
        Assert.Empty(recoveryPlan.ProjectedState.CharacterVitals.GuardianForceCompatibilityDiagnostics);
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(recovered));
    }

    [Fact]
    public void LegacyGuardianForceProjectionReviewsDefaultOnlyEditAndKeepsOtherFeatureBlockers()
    {
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!;
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> defaults = source.ResolveGuardianForceTemplates().SetItem(0, [new(1, false, 10)]);
        source = source with { MenuDefinitions = source.MenuDefinitions with { Templates = defaults, SourceTemplates = defaults } };
        NativeCharacterVitals edited = source.ApplyGuardianForceTemplates(defaults.SetItem(0, [new(1, true, 10)]));
        GameState active = state with { CharacterVitals = edited, GameplayPreferences = new() { GuardianFormsEnabled = true } };
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(active, source);
        Assert.Contains(plan.Losses, loss => loss.Kind == "Configuration");
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(active, source, true));
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(active with { GameplayPreferences = new() }));
    }

    [Fact]
    public void LegacyGuardianForceProjectionRestoresUnacquiredTargetDefaultsWithoutGrantingDraftAbilities()
    {
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!.DeepCopy();
        source.GuardianForces[0] = source.GuardianForces[0] with { Flags = 0, LearnedBank = new(4, 0), Learning = 1 };
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> defaults = source.ResolveGuardianForceTemplates();
        source = source with { MenuDefinitions = source.MenuDefinitions with { Templates = defaults, SourceTemplates = defaults, InitialStates = source.GuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray() } };
        NativeCharacterVitals edited = source.ApplyGuardianForceTemplates(defaults.SetItem(0, defaults[0].Add(new(3, true, 10))));
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(state with { CharacterVitals = edited }, source);
        NativeGuardianForceVital gf = plan.ProjectedState.CharacterVitals!.GuardianForces[0];
        Assert.Equal((byte)0, gf.Flags);
        Assert.True(gf.LearnedBank.Has(2));
        Assert.False(gf.LearnedBank.Has(3));
        Assert.Equal((byte)1, gf.Learning);
    }

    private static GameState Defaults()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        string hash = NativeInputConfiguration.OriginalInitHash;
        NativeStatCurve curve = new(100, 0, 100, 255, 200);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = hash,
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(100, 100), 0, new NativeCharacterJunction(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 100, new NativeGuardianForceLearnedBank(0, 0), null, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(new byte[57]), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(rank => (byte)rank).ToArray(), Gil = 5000, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        return session.State with { CharacterVitals = vitals, Economy = economy, FieldArrival = new FieldTransition(74, 10, 20, 0, 0) };
    }
}
