using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldBattleReturnPresentationTests
{
    [Fact]
    public void RandomFieldVictoryPublishesItsFirstResumedCallbackBeforeRenderingOrSaving()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("field-battle-return-entry");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.True(session.State.FullBattlePlaythrough);
        Assert.False(world.HasPendingBattle);
        Assert.Null(world.Battle);
        int fieldId = world.Events.FieldId;
        long fieldFrame = world.Events.Frame;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_RETURN_EVIDENCE");
        string output = string.IsNullOrWhiteSpace(evidence)
            ? Path.Combine(TestResources.ArtifactDirectory, "field-battle-return-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(Path.GetFullPath(evidence), "field-battle-return-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        session.SetEventState(world.Events.Capture());
        string before = Path.Combine(output, "before-battle.json");
        session.Save(before, compact: true);

        ushort formation = world.Field!.RandomEncounters!.Formations[0];
        string programHash = Convert.ToHexString(SHA256.HashData(world.Field.Jsm));
        RandomEncounterSessionState encounters = session.State.RandomEncounters ?? new(false, NativeRandomEncounterState.FromColdProcess(), null);
        // Controlled encounter scheduling selects the field's first original formation; it does not claim a naturally reached encounter roll.
        RandomEncounterRequest request = new(SceneKind.Field, fieldId, fieldFrame, formation, programHash);
        session.SetRandomEncounters(encounters with { Pending = request });
        world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, NativeSeed: 0));
        Assert.NotNull(world.Battle);
        Assert.NotNull(world.Battle.NativeGraphics);
        Assert.Equal(fieldFrame, world.Events.Frame);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.InstantVictory));
        int callbacks = 0;
        for (; callbacks < 2400 && !world.Battle.CanReturnToField; callbacks++)
        {
            world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        }
        Assert.True(world.Battle.CanReturnToField, "Native victory must finish within the bounded combat callbacks.");
        Assert.Equal(fieldFrame, world.Events.Frame);

        long returnStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        long returnAllocated = GC.GetTotalAllocatedBytes();
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Return));
        double returnMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(returnStarted).TotalMilliseconds;
        long returnBytes = GC.GetTotalAllocatedBytes() - returnAllocated;

        Assert.Null(world.Battle);
        Assert.False(world.HasPendingBattle);
        Assert.Null(session.State.RandomEncounters!.Pending);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(fieldId, world.Events.FieldId);
        Assert.Equal(fieldFrame + 1, world.Events.Frame);
        Assert.False(world.Events.IsNativeModelDrawPending);
        AssertCurrentVisibleModelSamples(world);
        JsonElement returned = world.Events.Capture();
        byte[] pixels = world.Render(TimeSpan.Zero).ToArray();
        Assert.NotEmpty(pixels);
        Assert.Equal(pixels, world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(returned, world.Events.Capture()), "Rendering must not execute the missing field callback.");

        string after = Path.Combine(output, "after-return.json");
        session.SetEventState(returned);
        session.Save(after, compact: true);
        GameSession restoredSession = new(GameSession.Load(after));
        using GameWorld restored = new(restoredSession, game);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(returned, restored.Events.Capture()));
        AssertCurrentVisibleModelSamples(restored);
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.Null(world.Events.LastFault);
        Assert.Null(restored.Events.LastFault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(original, File.ReadAllBytes(source));

        if (!string.IsNullOrWhiteSpace(evidence))
        {
            File.WriteAllText(Path.Combine(output, "proof.json"), JsonSerializer.Serialize(new
            {
                FieldId = fieldId,
                Formation = formation,
                EntryFrame = fieldFrame,
                ReturnFrame = returned.GetProperty("Frame").GetInt64(),
                ContinuedFrame = world.Events.Frame,
                VictoryCallbacks = callbacks,
                ReturnMilliseconds = returnMilliseconds,
                ReturnBytes = returnBytes,
                SourceSha256 = Convert.ToHexString(SHA256.HashData(original)),
                BeforeSave = Path.GetFileName(before),
                AfterSave = Path.GetFileName(after),
                ImmediateRestoreMatches = true,
                RenderIsPure = true,
                NextWaitMatches = true
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Field battle return evidence: {output}");
        }
    }

    private static void AssertCurrentVisibleModelSamples(GameWorld world)
    {
        ActorState[] visible = world.Events.Actors.Where(actor => actor.Active && actor.Visible && actor.RenderModelId >= 0).ToArray();
        Assert.NotEmpty(visible);
        foreach (ActorState actor in visible)
        {
            Assert.NotNull(world.Events.GetNativeModelPartTranslations(actor.ObjectId));
            NativeModelPartDraw draw = world.Events.Scene.ModelParts!.Draws[actor.ObjectId];
            Assert.Equal(world.Events.Frame, draw.Frame);
            Assert.Equal(actor.RenderModelId, draw.ModelId);
        }
    }
}
