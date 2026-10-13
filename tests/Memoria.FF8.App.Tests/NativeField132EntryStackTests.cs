using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField132EntryStackTests
{
    [Fact]
    public void ActualFreshArrivalCarriesOnlySuccessfulLookupResidueAndRestoresFullVmAndPixels()
    {
        (string directory, string source) = Corpus();
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Null(world.Events.Scene.NativeFieldEntryStack);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(1, world.Events.Frame);
        Assert.Equal(new NativeFieldEntryStackReceipt(2, NativeFieldEntryStackContract.WalkmeshHash, new FieldTransition(132, 2927, -556, 56, 100), 63, 53, 58), world.Events.Scene.NativeFieldEntryStack);
        PartyTraceState trace = world.Events.Scene.PartyTrace!;
        Assert.Equal(new PartyTraceSample(2866, -629, 0, 58, 100), trace.FirstTrail[54]);
        (int X, int Y)[] carried = [(2860, -637), (2853, -645), (2846, -653), (2840, -661), (2833, -669)];
        for (int offset = 0; offset < carried.Length; offset++)
        {
            int record = 53 - offset;
            Assert.Equal(new PartyTraceSample(carried[offset].X, carried[offset].Y, 0, 58, 100), trace.FirstTrail[record]);
            Assert.Equal(trace.FirstTrail[record], trace.SecondTrail[record]);
        }
        int followerId = world.Events.Scene.NativeActorObjectIds[world.Events.Scene.FieldPartyNativeActorIds[1]];
        ActorState follower = world.Events.Actors.Single(actor => actor.ObjectId == followerId);
        Assert.Equal((2833f, -669f, 0f, 58), (follower.X, follower.Y, follower.Z, follower.WalkmeshTriangle));
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-entry132-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(save);
            using var resumed = new GameWorld(new GameSession(GameSession.Load(save)), directory);
            resumed.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            world.SetRenderSize(320, 240);
            resumed.SetRenderSize(320, 240);
            for (int tick = 0; tick < 60; tick++)
            {
                var wait = new PlayerAction(PlayerActionKind.Wait);
                Assert.Equal(world.Apply(wait), resumed.Apply(wait));
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            }
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            File.Delete(save);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void UnknownFirstWinnerRefusesWithoutSeedingArrivalTriangleOrPublishingEntry()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Func<int, int, int, int?> original = world.Events.ResolvePartyTraceTriangle!;
        // Synthetic missing first lookup tests the contract boundary; it does
        // not alter the retained natural checkpoint or an actor placement.
        world.Events.ResolvePartyTraceTriangle = (x, y, z) => x == 2927 && y == -556 ? null : original(x, y, z);
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Contains("record63", failure.Message);
        Assert.Equal(0, world.Events.Scene.NativeActorLayoutVersion);
        Assert.Equal(0, world.Events.Scene.NativeFieldLoadVersion);
        Assert.Null(world.Events.Scene.NativeFieldEntryStack);
        Assert.Null(world.Events.Scene.PartyTrace);
        Assert.Equal(JsonValueKind.Object, world.Events.Capture().GetProperty("PendingArrival").ValueKind);
    }

    [Fact]
    public void MalformedEntryReceiptsRefuseBeforeMutationAndHistoricalAbsenceStaysUnknown()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        JsonElement before = world.Events.Capture();
        Action<JsonObject>[] corruptions =
        [
            receipt => receipt["Version"] = 3,
            receipt => receipt["WalkmeshHash"] = "unproved",
            receipt => receipt["Arrival"]!["FieldId"] = 131,
            receipt => receipt["FirstWinnerRecord"] = 62,
            receipt => receipt["FirstCarriedRecord"] = 52,
            receipt => receipt["CarriedTriangle"] = 57
        ];
        foreach (Action<JsonObject> corrupt in corruptions)
        {
            JsonNode altered = JsonNode.Parse(before.GetRawText())!;
            corrupt(altered["Scene"]!["NativeFieldEntryStack"]!.AsObject());
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(altered)));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        JsonNode historical = JsonNode.Parse(before.GetRawText())!;
        historical["Scene"]!.AsObject().Remove("NativeFieldEntryStack");
        JsonElement unknown = JsonSerializer.SerializeToElement(historical);
        world.Events.Restore(unknown);
        Assert.Null(world.Events.Scene.NativeFieldEntryStack);
        Assert.True(JsonElement.DeepEquals(unknown, world.Events.Capture()));
    }

    [Fact]
    public void SharedStackContractRequiresNativeRuntimeAndActualWalkmeshIdentity()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        var field = world.Field!;
        var arrival = new FieldTransition(132, 2927, -556, 56, 100);
        Assert.True(NativeFieldEntryStackAdapter.Supports(field, arrival));
        byte[] alteredJsm = (byte[])field.Jsm.Clone();
        alteredJsm[^1] ^= 1;
        Assert.True(NativeFieldEntryStackAdapter.Supports(field with { Jsm = alteredJsm }, arrival));
        Assert.False(NativeFieldEntryStackAdapter.Supports(field with { WalkmeshHash = "altered" }, arrival));
        Assert.False(NativeFieldEntryStackAdapter.Supports(field with { WalkmeshHash = null }, arrival));
        Assert.False(NativeFieldEntryStackAdapter.Supports(field with { NativeExecutableHash = null }, arrival));
        Assert.False(NativeFieldEntryStackAdapter.Supports(field, arrival with { FieldId = 131 }));
        Assert.True(NativeFieldEntryStackAdapter.Supports(field, arrival with { Direction = 99 }));
        Assert.True(NativeFieldEntryStackAdapter.Supports(field, arrival with { NativeExtraWord = 0 }));
        world.Events.CanUseNativeFieldEntryStack = _ => false;
        Assert.Contains("record53", Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.Wait))).Message);
        Assert.Null(world.Events.Scene.NativeFieldEntryStack);
        Assert.Equal(0, world.Events.Scene.NativeFieldLoadVersion);
    }

    private static (string Directory, string Source) Corpus()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return (directory, Path.Combine(repository!.FullName, "../fixtures/external-fixture.json"));
    }
}
