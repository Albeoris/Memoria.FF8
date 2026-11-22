using System.Text.Json;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.App.Tests;

public sealed class NativeInitialEyeRegistrationTests
{
    [Fact]
    public void Logical_save_with_legacy_zero_eye_modes_restores_automatic_blinking_without_rewriting_source()
    {
        string source = TestResources.FixturePath("App.Tests.NativeInitialEyeRegistrationTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameplaySaveSessionLoader.Load(source, TestResources.GameDirectory));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        NativeFieldRegistrationState registration = Assert.IsType<NativeFieldRegistrationState>(world.Events.Scene.NativeFieldRegistration);
        NativeFieldEyeProcess process = Assert.IsType<NativeFieldEyeProcess>(world.Events.Scene.NativeEyeProcess);
        NativeFieldRegisteredInstance[] blinking = registration.Instances.Where(instance => instance.ModelId >= 0 && world.Field!.Models[instance.ModelId].NativeEyes is { Bank.Length: > 0 }).ToArray();
        Assert.NotEmpty(blinking);
        foreach (NativeFieldRegisteredInstance instance in blinking)
        {
            Assert.Equal(0x80000u, world.Events.Actors[instance.ObjectId].NativeEyes!.Mode);
            Assert.Equal(0x10, process.Memory.FileStaging!.Read(instance.Address + 114, 1)[0] & 0xF0);
        }
        NativeBattleRetainedMemory memory = process.Memory;
        Dictionary<int, Rgba32[]> initialPixels = blinking.Select(instance => (instance.ObjectId, Visual: world.CreateVisual(world.Events.Actors[instance.ObjectId]))).Where(item => item.Visual.RegisteredTextures is { Count: > 0 }).ToDictionary(item => item.ObjectId, item => item.Visual.RegisteredTextures![0].Pixels.ToArray());
        Assert.NotEmpty(initialPixels);
        bool copied = false;
        bool pixelsChanged = false;
        for (int callback = 0; callback < 64; callback++)
        {
            world.Events.Step();
            NativeBattleRetainedMemory next = session.CurrentNativeBattleMemory!;
            copied |= !ReferenceEquals(memory.EffectMemory!.Textures, next.EffectMemory!.Textures);
            memory = next;
            foreach ((int objectId, Rgba32[] pixels) in initialPixels)
            {
                pixelsChanged |= !pixels.SequenceEqual(world.CreateVisual(world.Events.Actors[objectId]).RegisteredTextures![0].Pixels);
            }
        }
        Assert.True(copied, "Restored automatic eyes must execute their native texture copies.");
        Assert.True(pixelsChanged, "Model drawing must receive the changing eye texture pixels.");
        session.SetEventState(world.Events.Capture());
        using GameWorld restored = new(new GameSession(session.PrepareSnapshot()), TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void FieldEyeCallbacksKeepTheSharedVramPublicationDuringMovie()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        for (int action = 0; action < 6000; action++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            PlayerActionResult result = world.Apply(OpeningInputHarness.SelectAction(session.IsMoviePlaying, world.CurrentDialogue));
            Assert.True(result.Accepted, result.Detail);
            OpeningInputHarness.AdvanceUnchangedInput(world, result);
        }
        Assert.Equal(229, world.Events.FieldId);
        object initial = session.CurrentNativeBattleMemory!.EffectMemory!.Textures!.VramPublicationIdentity;

        // Exercise the field callbacks without a decoder or a CAM-controlled footstep camera.
        var captureFootstepOrigin = world.Events.CaptureFootstepModelOrigin;
        world.Events.CaptureFootstepModelOrigin = null;
        session.RequestMovie(session.MovieSource?.MovieIndex ?? 0);
        for (int callback = 0; callback < 64; callback++)
        {
            world.Events.StepMovieFrame(0, 0);
        }

        Assert.True(session.IsMoviePlaying);
        NativeFieldEyeProcess process = Assert.IsType<NativeFieldEyeProcess>(world.Events.Scene.NativeEyeProcess);
        object published = process.Memory.EffectMemory!.Textures!.VramPublicationIdentity;
        object sharedPublication = session.CurrentNativeBattleMemory!.EffectMemory!.Textures!.VramPublicationIdentity;
        NativeCrtRandomState? sharedRandom = session.State.CrtRandom;
        session.CompleteMovie();
        world.Events.CaptureFootstepModelOrigin = captureFootstepOrigin;
        for (int callback = 0; callback < 64; callback++)
        {
            world.Events.Step();
        }
        Assert.NotSame(initial, published);
        Assert.Same(published, sharedPublication);
        Assert.Equal(process.CrtRandom, sharedRandom);
        Assert.NotSame(published, session.CurrentNativeBattleMemory!.EffectMemory!.Textures!.VramPublicationIdentity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Battle_save_validation_preserves_current_memory_and_crt_while_restoring_frozen_field_eyes(int slot)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        JsonElement frozen = world.Events.Capture();
        NativeFieldEyeProcess cached = world.Events.Scene.NativeEyeProcess!;
        var field = world.Field!;
        NativeCrtRandomState current = session.State.CrtRandom!.Draw(out _);
        session.SetNativeFieldEyeOwners(session.State.NativeBattleMemory!, current);
        // Isolate persistence ownership with a component battle, without executing effects.
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = cached.Memory with { EffectIoResult = -1 },
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.Normal);
        session.SetBattle(battle.Capture());
        session.SetEventState(world.Events.Capture());
        Assert.NotEqual(cached.Memory.EffectIoResult, session.State.NativeBattleMemory!.EffectIoResult);
        Assert.NotEqual(cached.CrtRandom, current);

        EventEngine RestoreField(GameSession owner)
        {
            EventEngine engine = new(owner)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ReadNativeEyes = model => field.Models[model].NativeEyes,
                ReadNativeModelParts = model => field.Models[model].NativeParts,
                InitInputAxisDirection = field.Information!.ControlDirection
            };
            engine.LoadField(field.Id, field.Jsm, field.Messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, restoringCheckpoint: true);
            engine.Restore(owner.State.EventState!.Value);
            return engine;
        }

        void Validate(GameState state)
        {
            GameSession candidate = new(state);
            NativeBattleRetainedMemory memory = candidate.State.NativeBattleMemory!;
            EventEngine restored = RestoreField(candidate);
            Assert.Same(memory, candidate.State.NativeBattleMemory);
            Assert.Equal(current, candidate.State.CrtRandom);
            Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Scene").GetProperty("NativeEyeProcess"), restored.Capture().GetProperty("Scene").GetProperty("NativeEyeProcess")));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.Battle), JsonSerializer.SerializeToElement(candidate.State.Battle)));
        }

        string directory = TestResources.ArtifactFile("battle-eye-save-" + slot);
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
        store.Save(slot, session, Validate);
        GameState loaded = store.Load(slot);
        Validate(loaded);
        BattleSession resumed = BattleSession.Restore(loaded.Battle!);
        battle.Tick();
        resumed.Tick();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(resumed.Capture())));
    }

    [Fact]
    public void Original_new_game_field_registers_actual_models_and_restores_eye_owners_without_advancing_rng()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        NativeFieldEyeProcess process = Assert.IsType<NativeFieldEyeProcess>(world.Events.Scene.NativeEyeProcess);
        NativeFieldRegistrationState registration = Assert.IsType<NativeFieldRegistrationState>(world.Events.Scene.NativeFieldRegistration);
        Assert.Equal(74, registration.FieldId);
        Assert.All(registration.Instances, instance => Assert.Equal(0x80000u, world.Events.Actors[instance.ObjectId].NativeEyes!.Mode));
        process.Validate(registration);
        // Scene.NativeEyeProcess is cached registration context. Active-field capture refreshes
        // its shared physical memory after subsequent texture publications.
        JsonElement currentCheckpoint = world.Events.Capture();
        NativeFieldEyeProcess currentProcess = JsonSerializer.Deserialize<NativeFieldEyeProcess>(currentCheckpoint.GetProperty("Scene").GetProperty("NativeEyeProcess"))!;
        currentProcess.Validate(registration);
        Assert.Equal(JsonSerializer.Serialize(session.CaptureNativeFieldMemory()), JsonSerializer.Serialize(currentProcess.Memory));
        Assert.Equal(process.CrtRandom, currentProcess.CrtRandom);
        Assert.Equal(currentProcess.CrtRandom, session.State.CrtRandom);
        session.SetEventState(currentCheckpoint);
        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using GameWorld restored = new(new GameSession(snapshot), TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        NativeCrtRandomState? before = session.State.CrtRandom;
        world.Events.Step();
        restored.Events.Step();
        Assert.Equal(before, session.State.CrtRandom);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }
    [Fact]
    public void Dormant_original_field_retains_cached_registration_without_replacing_current_field_crt()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.Events.Step();
        JsonElement frozen = world.Events.Capture();
        NativeFieldEyeProcess cached = world.Events.Scene.NativeEyeProcess!;
        var field = world.Field!;
        // The ordinary field request changes the active owner; restoring the
        // outgoing field is inspection context, without an allocation or callback.
        session.RequestField(75, 0);
        NativeCrtRandomState current = session.State.CrtRandom!.Draw(out _);
        session.SetNativeFieldEyeOwners(session.State.NativeBattleMemory!, current);
        GameState before = session.State;
        EventEngine dormant = new(session)
        {
            AnimationFrameCount = field.GetAnimationFrameCount,
            ReadNativeEyes = model => field.Models[model].NativeEyes,
            ReadNativeModelParts = model => field.Models[model].NativeParts,
            InitInputAxisDirection = field.Information!.ControlDirection
        };
        dormant.LoadField(74, field.Jsm, field.Messages, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, restoringCheckpoint: true);
        dormant.RestoreFieldState(frozen);
        Assert.Same(before, session.State);
        Assert.Equal(current, session.State.CrtRandom);
        Assert.Equal(cached.CrtRandom, dormant.Scene.NativeEyeProcess!.CrtRandom);
        Assert.True(JsonElement.DeepEquals(frozen.GetProperty("Scene").GetProperty("NativeEyeProcess"), dormant.Capture().GetProperty("Scene").GetProperty("NativeEyeProcess")));
        Assert.Same(before, session.State);
    }

}
