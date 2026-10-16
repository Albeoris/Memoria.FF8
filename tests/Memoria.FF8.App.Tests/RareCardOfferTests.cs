using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RareCardOfferTests
{
    [Fact]
    public void ExactNativeLocationChanceDeckAndCurrentActorGatesControlOffersWithoutMutation()
    {
        var (session, engine) = Create(Challenge(121, 50));
        JsonElement before = engine.Capture();
        NativeCardInventory cards = session.CardInventory!;
        Assert.Equal([new RareCardOffer(0, 84)], engine.GetRareCardOffers());
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.Same(cards, session.CardInventory);
        Assert.All(cards.ObtainedRareCards, value => Assert.Equal(0, value));

        SetActor(engine, nameof(ActorState.Visible), false);
        Assert.Empty(engine.GetRareCardOffers());
        SetActor(engine, nameof(ActorState.Visible), true);
        SetActor(engine, nameof(ActorState.TalkEnabled), false);
        Assert.Empty(engine.GetRareCardOffers());
        SetActor(engine, nameof(ActorState.TalkEnabled), true);
        SetActor(engine, nameof(ActorState.Active), false);
        Assert.Empty(engine.GetRareCardOffers());
        Assert.Empty(Create(Challenge(121, 0)).Engine.GetRareCardOffers());
        Assert.Empty(Create(Challenge(122, 50)).Engine.GetRareCardOffers());
        Assert.Empty(Create(Challenge(121, 50), quantity: 4).Engine.GetRareCardOffers());
        var busy = Create(Challenge(121, 50));
        typeof(EventEngine).GetMethod("Queue", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(busy.Engine, [0, 2, 7, false]);
        Assert.Empty(busy.Engine.GetRareCardOffers());
    }

    [Fact]
    public void CurrentScriptGateAndOverlappingScratchWritesAreEvaluatedExactly()
    {
        var gated = new List<(Jsm.Opcode, int)> { (Jsm.Opcode.PSHM_B, 512), (Jsm.Opcode.JPF, 9) };
        gated.AddRange(Challenge(121, 50));
        Assert.Empty(Create(gated.ToArray()).Engine.GetRareCardOffers());
        var enabled = Create(gated.ToArray(), gate: 1);
        Assert.Single(enabled.Engine.GetRareCardOffers());

        var overlap = new List<(Jsm.Opcode, int)> { (Jsm.Opcode.PSHN_L, 121), (Jsm.Opcode.POPM_W, 520), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 521), (Jsm.Opcode.PSHM_W, 520), (Jsm.Opcode.PSHN_L, 121), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 9) };
        overlap.AddRange(Challenge(121, 50));
        Assert.Empty(Create(overlap.ToArray()).Engine.GetRareCardOffers());

        var unknown = new List<(Jsm.Opcode, int)> { (Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.JPF, 9) };
        unknown.AddRange(Challenge(121, 50));
        Assert.Empty(Create(unknown.ToArray()).Engine.GetRareCardOffers());
    }

    [Theory]
    [InlineData(621, 6, 121, 145)]
    [InlineData(165, 18, 31, 1132)]
    public void ActualNativeChallengeUsesItsSavedOwnerWithoutMutation(int field, int actor, int owner, int operation)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native field corpus is required.");
        using GameAssets assets = new(directory);
        byte[] native = assets.LoadField(field).Jsm;
        JsmProgram source = JsmProgram.Read(native);
        var fixture = Create(Challenge(121, 50));
        fixture.Session.CardInventory!.Cards[84] = (byte)owner;
        fixture.Engine.LoadField(field, native, []);
        typeof(EventEngine).GetField("defaultsStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Engine, true);
        SetActor(fixture.Engine, nameof(ActorState.ModelId), 6, actor);
        JsonElement before = fixture.Engine.Capture();
        Assert.Contains(new RareCardOffer(actor, 84), fixture.Engine.GetRareCardOffers());
        Assert.True(JsonElement.DeepEquals(before, fixture.Engine.Capture()));
        Assert.Equal(Jsm.Opcode.CARDGAME, source.Operations[operation].Opcode);
    }

    [Fact]
    public void ForcedRareInsertionCapsAtFiveAndPlayerOwnerNeverQualifies()
    {
        var fixture = Create(Challenge(121, 200));
        for (int card = 77; card <= 84; card++)
        {
            fixture.Session.CardInventory!.Cards[card] = 121;
        }
        Assert.Equal(Enumerable.Range(77, 5), fixture.Engine.GetRareCardOffers().Select(offer => offer.CardId));
        var player = Create(Challenge(240, 50));
        player.Session.CardInventory!.Cards[84] = 240;
        Assert.Empty(player.Engine.GetRareCardOffers());
    }

    [Fact]
    public void ActualHiddenTwinProxyMarksTheVisibleRequestedCardOwners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native field corpus is required.");
        using GameAssets assets = new(directory);
        byte[] native = assets.LoadField(156).Jsm;
        JsmProgram source = JsmProgram.Read(native);
        var fixture = Create(Challenge(121, 50));
        fixture.Session.CardInventory!.Cards[84] = 226;
        fixture.Session.CardInventory!.Cards[85] = 45;
        fixture.Engine.LoadField(156, native, []);
        typeof(EventEngine).GetField("defaultsStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Engine, true);
        typeof(EventEngine).GetProperty(nameof(EventEngine.NativeRuntimeProfile))!.SetValue(fixture.Engine, NativeFieldRuntimeProfile.FF8En);
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.NativeActorLayoutVersion))!.SetValue(fixture.Engine.Scene, 1);
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.PartyBindingMemory))!.SetValue(fixture.Engine.Scene, new NativePartyBindingMemory(1, 0, 1, 2));
        fixture.Engine.Scene.NativeActorObjectIds.AddRange(source.NativeObjects.Take(source.NativeActorCount));
        SetActor(fixture.Engine, nameof(ActorState.ModelId), 17, 18);
        SetActor(fixture.Engine, nameof(ActorState.Visible), false, 18);
        SetActor(fixture.Engine, nameof(ActorState.ModelId), 18, 19);
        SetActor(fixture.Engine, nameof(ActorState.ModelId), 19, 20);
        var expected = new[] { new RareCardOffer(19, 84), new RareCardOffer(19, 103), new RareCardOffer(20, 85) };
        JsonElement before = fixture.Engine.Capture();
        Assert.Equal(expected, fixture.Engine.GetRareCardOffers().OrderBy(offer => offer.ObjectId));
        Assert.True(JsonElement.DeepEquals(before, fixture.Engine.Capture()));
        SetActor(fixture.Engine, nameof(ActorState.TalkEnabled), false, 18);
        Assert.Empty(fixture.Engine.GetRareCardOffers());
    }

    private static (Jsm.Opcode, int)[] Challenge(int owner, int chance)
    {
        return [(Jsm.Opcode.PSHN_L, owner), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, chance), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.CARDGAME, 0)];
    }

    private static (GameSession Session, EventEngine Engine) Create((Jsm.Opcode, int)[] talk, int quantity = 5, int gate = 0)
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        cards.Cards[0] = (byte)(128 | quantity);
        cards.Cards[84] = 121;
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 100, CardInventory = cards });
        EventEngine engine = new(session, NativeEventMemory.FromDWords(new Dictionary<int, int> { [512] = gate }));
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 2)];
        operations.AddRange(talk);
        operations.Add((Jsm.Opcode.RET, 0));
        byte[] bytes = new byte[18 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 5);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), (ushort)operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode == Jsm.Opcode.PSHN_L ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18 + index * 4), encoded);
        }
        engine.LoadField(100, bytes, []);
        typeof(EventEngine).GetField("defaultsStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, true);
        SetActor(engine, nameof(ActorState.ModelId), 6);
        return (session, engine);
    }

    private static void SetActor(EventEngine engine, string property, object value, int objectId = 0)
    {
        typeof(ActorState).GetProperty(property)!.SetValue(engine.Actors[objectId], value);
    }
}
