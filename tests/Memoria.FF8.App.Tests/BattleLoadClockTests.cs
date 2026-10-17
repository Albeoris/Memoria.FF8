using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleLoadClockTests
{
    [Theory]
    [InlineData(BattleAtbMode.Normal)]
    [InlineData(BattleAtbMode.TurnBased)]
    public void Accelerated_native_callbacks_are_independent_of_sixty_or_ninety_host_updates(BattleAtbMode mode)
    {
        string source = RequireSource();
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession sixtySession = new(GameSession.Load(source));
        GameSession ninetySession = new(GameSession.Load(source));
        using GameWorld sixty = new(sixtySession, TestResources.GameDirectory);
        using GameWorld ninety = new(ninetySession, TestResources.GameDirectory);
        sixty.EnsureField();
        ninety.EnsureField();
        sixty.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: mode));
        ninety.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: mode));
        long initialCallback = sixty.Battle!.NativeHostCallbacks!.Value;
        long initialFieldCallbacks = sixty.Events.Scene.FieldCallbackClock!.BattleCallbacks!.Value;
        long initialFieldFrame = sixty.Events.Frame;

        AdvanceOneSecondAtTripleSpeed(sixty, sixtySession, 60);
        AdvanceOneSecondAtTripleSpeed(ninety, ninetySession, 90);

        Assert.Equal(initialCallback + 90, sixty.Battle.NativeHostCallbacks);
        Assert.Equal(initialCallback + 90, ninety.Battle!.NativeHostCallbacks);
        Assert.Equal(initialFieldCallbacks + 90, sixty.Events.Scene.FieldCallbackClock!.BattleCallbacks);
        Assert.Equal(initialFieldFrame, sixty.Events.Frame);
        Assert.Equal(initialFieldFrame, ninety.Events.Frame);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(sixtySession.State), JsonSerializer.SerializeToElement(ninetySession.State)));
        Assert.True(JsonElement.DeepEquals(sixty.Events.Capture(), ninety.Events.Capture()));
        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(sixty), NativeBattleProductionEffectAdmissionTests.RenderBattle(ninety));
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static void AdvanceOneSecondAtTripleSpeed(GameWorld world, GameSession session, int updates)
    {
        long previousTicks = 0;
        long simulationSubticks = 0;
        for (int update = 1; update <= updates; update++)
        {
            long currentTicks = TimeSpan.TicksPerSecond * update / updates;
            TimeSpan elapsed = TimeSpan.FromTicks((currentTicks - previousTicks) * 3);
            previousTicks = currentTicks;
            world.Battle!.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            simulationSubticks += elapsed.Ticks * BattleSession.FramesPerSecond;
            int frames = checked((int)(simulationSubticks / TimeSpan.TicksPerSecond));
            simulationSubticks %= TimeSpan.TicksPerSecond;
            if (frames > 0)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick, frames));
            }
        }
        Assert.Equal(TimeSpan.TicksPerSecond, previousTicks);
        Assert.Equal(0, simulationSubticks);
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
    }

    [Fact]
    public void Standalone_native_battle_load_preserves_distinct_simulation_and_callback_clocks()
    {
        string source = RequireSource();
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        BattleSnapshot saved = session.State.Battle!;
        Assert.Equal(BattleAtbMode.TurnBased, saved.Mode);
        Assert.True(saved.NativeHostCallbacks > saved.Frame);
        JsonElement before = JsonSerializer.SerializeToElement(session.State);
        using GameWorld world = new(session, TestResources.GameDirectory);

        world.EnsureField();

        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(session.State)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved), JsonSerializer.SerializeToElement(world.Battle!.Capture())));
        Assert.True(JsonElement.DeepEquals(session.State.EventState!.Value, world.Events.Capture()));
        Assert.Equal(saved.NativeHostCallbacks, world.Events.Scene.FieldCallbackClock!.BattleCallbacks);
        Assert.Equal(SHA256.HashData(original), SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Theory]
    [InlineData(BattleAtbMode.Normal)]
    [InlineData(BattleAtbMode.TurnBased)]
    public void Capture_after_presentation_callbacks_publishes_matching_battle_and_field_clocks(BattleAtbMode mode)
    {
        string source = RequireSource();
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: mode));
        BattleSession battle = world.Battle!;
        long fieldFrame = world.Events.Frame;
        long callback = battle.NativeHostCallbacks!.Value;
        long fieldCallbacks = world.Events.Scene.FieldCallbackClock!.BattleCallbacks!.Value;
        long timerCalls = session.ExamTimer!.ClockCalls;
        long mainLoopCalls = session.State.WorldProcess!.MainLoopCallbacks;
        TimeSpan nextCallback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

        battle.AdvanceNativeGraphicsPresentation(nextCallback);
        world.SynchronizeBattlePresentation();
        long advanced = battle.NativeHostCallbacks!.Value - callback;
        Assert.True(advanced > 0);
        Assert.Equal(callback, session.State.Battle!.NativeHostCallbacks);
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.Equal(fieldCallbacks + advanced, world.Events.Scene.FieldCallbackClock!.BattleCallbacks);
        Assert.Equal(timerCalls + advanced * 4, session.ExamTimer!.ClockCalls);
        Assert.Equal(mainLoopCalls + advanced, session.State.WorldProcess!.MainLoopCallbacks);

        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        Assert.Equal(battle.NativeHostCallbacks, session.State.Battle!.NativeHostCallbacks);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(session.State.Battle)));
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-battle-load-clock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "after-presentation.json");
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.Equal(mode, restored.Battle!.Mode);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));

            battle.AdvanceNativeGraphicsPresentation(nextCallback);
            restored.Battle.AdvanceNativeGraphicsPresentation(nextCallback);
            world.CaptureBattleState();
            restored.CaptureBattleState();
            session.SetEventState(world.Events.Capture());
            restoredSession.SetEventState(restored.Events.Capture());
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(source)), originalHash);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_rejects_battle_callback_ledger_mismatch_even_when_total_field_timer_calls_match()
    {
        string source = RequireSource();
        GameSession session = new(CorruptCallbackClassification(GameSession.Load(source)));
        using GameWorld world = new(session, TestResources.GameDirectory);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => world.EnsureField());

        Assert.Contains("executed native callback clock", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Save_rejects_invalid_battle_continuation_before_replacing_valid_or_creating_empty_slot(int slot)
    {
        string source = RequireSource();
        byte[] sourceBytes = File.ReadAllBytes(source);
        GameState saved = GameSession.Load(source);
        GameSession corrupt = new(CorruptCallbackClassification(saved));
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-battle-save-clock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(source, directory);
            int validations = 0;
            void Validate(GameState candidate)
            {
                validations++;
                using GameWorld world = new(new GameSession(candidate), TestResources.GameDirectory);
                world.EnsureField();
            }
            store.Save(slot, new GameSession(saved), Validate);
            Assert.Equal(1, validations);
            byte[] validBytes = File.ReadAllBytes(store.GetPath(slot));

            InvalidDataException existingError = Assert.Throws<InvalidDataException>(() => store.Save(slot, corrupt, Validate));
            Assert.Equal(2, validations);
            Assert.Contains("executed native callback clock", existingError.Message);
            Assert.Equal(validBytes, File.ReadAllBytes(store.GetPath(slot)));
            Assert.False(File.Exists(store.GetPath(slot) + ".tmp"));

            int emptySlot = slot + 2;
            InvalidDataException emptyError = Assert.Throws<InvalidDataException>(() => store.Save(emptySlot, corrupt, Validate));
            Assert.Equal(3, validations);
            Assert.Contains("executed native callback clock", emptyError.Message);
            Assert.False(File.Exists(store.GetPath(emptySlot)));
            Assert.False(File.Exists(store.GetPath(emptySlot) + ".tmp"));
            Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static GameState CorruptCallbackClassification(GameState saved)
    {
        JsonNode events = JsonNode.Parse(saved.EventState!.Value.GetRawText())!;
        JsonNode clock = events["Scene"]!["FieldCallbackClock"]!;
        long callbacks = clock["BattleCallbacks"]!.GetValue<long>();
        long menus = clock["MenuCallbacks"]!.GetValue<long>();
        Assert.True(callbacks > 0);
        // Keep the allocation timer total unchanged so the battle owner must reject the misclassified callback.
        clock["BattleCallbacks"] = callbacks - 1;
        clock["MenuCallbacks"] = menus + 4;
        return saved with { EventState = JsonSerializer.SerializeToElement(events) };
    }

    private static string RequireSource()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_LOAD_CLOCK_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone native battle clock fixture is required.");
        return source!;
    }
}
