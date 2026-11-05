using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldBattleProfileHandoffTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_world_encounter_returns_with_current_cpu_and_can_start_the_next_encounter_without_resetting_world_progress(bool reverse)
    {
        string original = TestResources.FixturePath("world-profile-return-source");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(original));
        string classic = TestResources.GameDirectory;
        string? remastered = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR") ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_CROSS_EDITION_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(remastered), "Select optional Remastered resources for the actual world return check.");
        string sourceDirectory = reverse ? remastered! : classic;
        string targetDirectory = reverse ? classic : remastered!;
        using GameAssets sourceAssets = new(sourceDirectory);
        GameSession sourceSession = new(PlayerStateLoader.Load(original, sourceDirectory, sharedAssets: sourceAssets));
        using GameWorld source = new(sourceSession, sourceDirectory, sharedAssets: sourceAssets, validateWorldPresentation: false);
        source.EnsureField();
        source.ApplyBattleInput(new(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
        source.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        EnterActualWorldEncounter(source, sourceSession);
        GameState active = sourceSession.PrepareSnapshot();
        Assert.Equal(sourceAssets.NativeRuntimeProfile!.ExecutableHash, active.NativeBattleMemory!.ModelCpuResources!.Cpu.EffectiveLayout.ExecutableHash);
        using GameAssets targetAssets = new(targetDirectory);
        GameSession controlSession = new(active);
        using GameWorld control = new(controlSession, sourceDirectory, sharedAssets: sourceAssets, validateWorldPresentation: false);
        control.EnsureField();
        GameSession targetSession = new(active);
        using GameWorld target = new(targetSession, targetDirectory, sharedAssets: targetAssets, validateWorldPresentation: false);
        target.EnsureField();
        FinishActualBattle(control, controlSession);
        FinishActualBattle(target, targetSession);
        Assert.Equal(JsonSerializer.Serialize(control.WorldScene!.State), JsonSerializer.Serialize(target.WorldScene!.State));
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.SavedWorld), JsonSerializer.Serialize(targetSession.State.SavedWorld));
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.ExamTimer), JsonSerializer.Serialize(targetSession.State.ExamTimer));
        Assert.Equal(controlSession.State.Playtime, targetSession.State.Playtime);
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.WorldProcess), JsonSerializer.Serialize(targetSession.State.WorldProcess));
        Assert.Equal(controlSession.State.CrtRandom, targetSession.State.CrtRandom);
        Assert.Equal(controlSession.State.GlobalRandom, targetSession.State.GlobalRandom);
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.EventRandom), JsonSerializer.Serialize(targetSession.State.EventRandom));
        Assert.Equal(controlSession.State.AppliedBattleResultIds, targetSession.State.AppliedBattleResultIds);
        Assert.Equal(JsonSerializer.Serialize(GameplaySaveCharacters.Capture(controlSession.State.CharacterVitals!)), JsonSerializer.Serialize(GameplaySaveCharacters.Capture(targetSession.State.CharacterVitals!)));
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.Economy), JsonSerializer.Serialize(targetSession.State.Economy));
        Assert.Equal(targetAssets.ReadNewGameCharacterVitals().KernelSha256, targetSession.State.CharacterVitals!.KernelSha256);
        Assert.Equal(targetAssets.ReadNewGameShotAvailability().KernelSha256, targetSession.State.ShotAvailability!.KernelSha256);
        Assert.Equal(controlSession.State.ShotAvailability!.Flags770, targetSession.State.ShotAvailability.Flags770);
        Assert.Equal(JsonSerializer.Serialize(controlSession.State.RandomEncounters), JsonSerializer.Serialize(targetSession.State.RandomEncounters));
        AssertCurrentCpu(targetSession, targetAssets);
        GameSession coldSession = ColdDiskCopy(targetSession);
        using GameWorld cold = new(coldSession, targetDirectory, sharedAssets: targetAssets, validateWorldPresentation: false);
        cold.EnsureField();
        AssertCurrentCpu(coldSession, targetAssets);
        Assert.Equal(JsonSerializer.Serialize(target.WorldScene.State), JsonSerializer.Serialize(cold.WorldScene!.State));
        EnterActualWorldEncounter(cold, coldSession);
        Assert.Equal(targetAssets.NativeRuntimeProfile!.ExecutableHash, cold.Battle!.NativeModelCpuResources!.Cpu.EffectiveLayout.ExecutableHash);
        GameSession nextColdSession = ColdDiskCopy(coldSession);
        using (GameWorld nextCold = new(nextColdSession, targetDirectory, sharedAssets: targetAssets, validateWorldPresentation: false))
        {
            nextCold.EnsureField();
            Assert.Equal(targetAssets.NativeRuntimeProfile.ExecutableHash, nextCold.Battle!.NativeModelCpuResources!.Cpu.EffectiveLayout.ExecutableHash);
        }
        FinishActualBattle(cold, coldSession);
        AssertCurrentCpu(coldSession, targetAssets);
        coldSession.RequestField(165, 0);
        cold.EnsureField();
        Assert.Equal(targetAssets.NativeRuntimeProfile.ExecutableHash, cold.Events.NativeRuntimeProfile!.ExecutableHash);
        AssertCurrentCpu(coldSession, targetAssets);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(original)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Eye_publication_updates_only_an_active_field_owner_and_preserves_world_crt(bool dormant)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState state = dormant
            ? PlayerStateLoader.Load(TestResources.FixturePath("world-profile-return-source"), directory, sharedAssets: assets)
            : GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets);
        GameSession session = new(state);
        using GameWorld world = new(session, directory, sharedAssets: assets, validateWorldPresentation: false);
        world.EnsureField();
        NativeFieldEyeProcess process = Assert.IsType<NativeFieldEyeProcess>(world.Events.Scene.NativeEyeProcess);
        NativeFieldTextureAnimations animations = Assert.IsType<NativeFieldTextureAnimations>(process.Memory.FieldTextureAnimations);
        NativeCrtRandomState current = session.State.CrtRandom!.Draw(out _);
        session.Restore(session.State with { CrtRandom = current });
        GameState before = session.State;
        NativeBattleRetainedMemory memory = session.CurrentNativeBattleMemory!;
        world.Events.PublishNativeFieldTextureAnimations(animations);
        Assert.Equal(current, session.State.CrtRandom);
        if (dormant)
        {
            Assert.Same(before, session.State);
            Assert.Same(memory, session.CurrentNativeBattleMemory);
            Assert.Equal(process.CrtRandom, world.Events.Scene.NativeEyeProcess!.CrtRandom);
        }
        else
        {
            Assert.Equal(current, world.Events.Scene.NativeEyeProcess!.CrtRandom);
            Assert.Same(world.Events.Scene.NativeEyeProcess.Memory, session.CurrentNativeBattleMemory);
        }
    }

    private static void EnterActualWorldEncounter(GameWorld world, GameSession session)
    {
        for (int tick = 0; tick < 1200 && !world.HasPendingBattle; tick++)
        {
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            int direction = tick / 8 % 4;
            float horizontal = direction == 0 ? 1 : direction == 2 ? -1 : 0;
            float vertical = direction == 1 ? 1 : direction == 3 ? -1 : 0;
            PlayerActionResult step = world.Apply(new(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical));
            Assert.True(step.Accepted, step.Detail);
        }
        Assert.NotNull(session.State.RandomEncounters?.Pending);
        Assert.NotNull(session.State.PendingBattleEncounter);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        Assert.NotNull(world.Battle);
    }

    private static void FinishActualBattle(GameWorld world, GameSession session)
    {
        for (int callback = 0; callback < 600 && !world.Battle!.NativeEntryReady; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(world.Battle!.NativeEntryReady);
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        for (int tick = 0; tick < 600 && !world.Battle!.CanReturnToField; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(world.Battle!.CanReturnToField);
        try
        {
            world.ApplyBattleInput(new(BattleInputKind.Return));
        }
        catch (InvalidDataException exception)
        {
            var diagnostic = new
            {
                Occurrence = session.State.PendingBattleEncounter,
                Random = session.State.RandomEncounters?.Pending,
                Receipt = session.State.SavedWorld?.FootReturn,
                AllocationHash = world.WorldScene!.AllocationProgramSha256,
                Result = world.Battle?.NativeResult
            };
            throw new InvalidDataException(exception.Message + " " + JsonSerializer.Serialize(diagnostic), exception);
        }
        Assert.Null(world.Battle);
    }

    private static GameSession ColdDiskCopy(GameSession source)
    {
        string path = Path.Combine(Path.GetTempPath(), "Memoria-world-profile-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            source.Save(path, compact: true);
            return new(GameSession.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertCurrentCpu(GameSession session, GameAssets assets)
    {
        string image = assets.NativeRuntimeProfile!.ExecutableHash;
        Assert.Equal(image, session.State.NativeBattleMemory!.ColdExecutableSha256);
        Assert.Equal(image, session.State.NativeBattleMemory.ModelCpuResources!.Cpu.EffectiveLayout.ExecutableHash);
    }
}
