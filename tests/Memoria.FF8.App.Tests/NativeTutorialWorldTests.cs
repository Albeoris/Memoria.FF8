using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialWorldTests
{
    [Fact]
    public void OriginalField159RequestRestoresPlaybackAndKeepsLivePartyIsolated()
    {
        (string directory, GameState source) = Source();
        GameSession session = new(source);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Events.Step();
        FocusOriginalRequest(world, 4);
        NativeTutorialRequest request = Assert.IsType<NativeTutorialRequest>(world.Events.Scene.NativeTutorial);
        Assert.Equal((159, 11, 88, 1742, 4, (byte?)4), (request.FieldId, request.ObjectId, request.ScriptId, request.Operation, request.RawArgument, request.TutorialId));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TUTORIAL_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-native-tutorial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        string before = Path.Combine(output, "field159-original-tuto4-request-pending.json");
        session.SetEventState(world.Events.Capture());
        session.Save(before);
        JsonElement live = LiveOwners(session);
        long heldFrame = world.Events.Frame;
        Assert.NotNull(world.Tutorial);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.Equal(heldFrame, world.Events.Frame);
        Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.SetJunctionMagic, 0, Quantity: 1)).Accepted);
        Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
        string pending = Path.Combine(output, "field159-original-tuto4-pending.json");
        session.SetEventState(world.Events.Capture());
        session.Save(pending);
        GameSession independent = new(GameSession.Load(pending));
        using GameWorld restored = new(independent, directory);
        restored.EnsureField();
        Assert.NotNull(restored.Tutorial);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        bool confirmed = false;
        bool cancelled = false;
        for (int step = 0; step < 3000 && world.Events.Scene.NativeTutorial is not null; step++)
        {
            bool gate = world.Tutorial!.WaitingForInput;
            PlayerAction action = gate ? new PlayerAction(confirmed ? PlayerActionKind.Cancel : PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait, Frames: 8);
            Assert.True(world.Apply(action).Accepted);
            Assert.True(restored.Apply(action).Accepted);
            if (gate)
            {
                cancelled |= confirmed;
                confirmed = true;
            }
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
            Assert.True(JsonElement.DeepEquals(live, LiveOwners(independent)));
        }
        Assert.True(confirmed && cancelled);
        Assert.Null(world.Events.Scene.NativeTutorial);
        Assert.Null(restored.Events.Scene.NativeTutorial);
        Assert.Null(world.Events.Scene.NativeTutorialPlayback);
        Assert.Equal(heldFrame + 1, world.Events.Frame);
        string after = Path.Combine(output, "field159-original-tuto4-explicitly-closed.json");
        session.SetEventState(world.Events.Capture());
        session.Save(after);
        using GameWorld afterRestore = new(new GameSession(GameSession.Load(after)), directory);
        afterRestore.EnsureField();
        Assert.True(afterRestore.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(world.Events.Frame + 1, afterRestore.Events.Frame);
        File.WriteAllText(Path.Combine(output, "continuation.json"), JsonSerializer.Serialize(new
        {
            Source = "Fresh diagnostic native field159 allocation with authentic installed initialization resources; original program bytes retained. No gameplay route or historical migration.",
            DiagnosticSetup = "Unrelated original default fibers held until frame100000; requested door slot7/priority0 carries script88 at original TUTO1742 with raw4. Native init/default saved-result owners retained. Original visibility restored before issuing callback; current skin receipts verified.",
            Field = 159,
            PendingFrame = heldFrame,
            AfterFrame = world.Events.Frame,
            Before = before,
            During = pending,
            After = after,
            Boundary = "TUTO4 explicitly cancelled at second native confirmation gate after one Confirm. Event resumed one native callback. Other field scripts remain diagnostically held; unrelated SCROLLMODE2 renderer boundary is outside this tutorial check.",
            Validation = "Pending standalone loaded independently; same tutorial actions produced identical VM snapshots and unchanged live character/junction/economy/card/limit state. After-event standalone loaded and advanced one ordinary callback.",
            ResumeCommand = $"dotnet run --project recreate/product/src/Memoria.FF8.App -- --headless-control --game-dir \"{directory}\" --state \"{after}\" --control-dir \"{Path.Combine(output, "control")}\""
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Original TUTO4 native host pending: {before}; playback: {pending}; explicitly closed: {after}; field159/frame{world.Events.Frame}, visible callback receipts and independent next callback verified.");
    }

    [Theory]
    [InlineData(0x104, 4)]
    [InlineData(0x1FF, 8)]
    public void OriginalRequestRetainsRawArgumentAndUsesNativeLowByteFallback(int raw, int expected)
    {
        (string directory, GameState source) = Source();
        using GameWorld world = new(new GameSession(source), directory);
        world.EnsureField();
        world.Events.Step();
        FocusOriginalRequest(world, raw);
        Assert.Equal(raw, world.Events.Scene.NativeTutorial!.RawArgument);
        Assert.Equal(unchecked((byte)raw), world.Events.Scene.NativeTutorial.TutorialId);
        JsonElement status = JsonSerializer.SerializeToElement(world.Tutorial!.Status);
        Assert.Equal(expected, status.GetProperty("TutorialId").GetInt32());
    }

    [Fact]
    public void HeadlessBrowserRejectsOrdinaryFieldMutationsUntilExplicitCancel()
    {
        (string directory, GameState source) = Source();
        GameSession session = new(source);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        //This bounded module tests host admission independently of field travel.
        byte[] program = BrowserProgram();
        world.Events.LoadField(159, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        world.Events.Step();
        world.Events.Step();
        Assert.Null(world.Events.Scene.NativeTutorial!.TutorialId);
        Assert.Null(world.Tutorial);
        JsonElement held = world.Events.Capture();
        JsonElement live = LiveOwners(session);
        foreach (PlayerActionKind kind in Enum.GetValues<PlayerActionKind>().Where(kind => kind is not (PlayerActionKind.Wait or PlayerActionKind.Cancel)))
        {
            Assert.False(world.Apply(new PlayerAction(kind)).Accepted);
            Assert.True(JsonElement.DeepEquals(held, world.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 300)).Accepted);
        Assert.True(JsonElement.DeepEquals(held, world.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.Null(world.Events.Scene.NativeTutorial);
        Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
    }

    [Fact]
    public void BrowserSelectedLessonRestoresAndReturnsWithoutAFieldCallback()
    {
        (string directory, GameState source) = Source();
        GameSession session = new(source);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Events.Step();
        FocusOriginalRequest(world, 4);
        CloseAtNextGate(world);
        long fieldFrame = world.Events.Frame;
        JsonElement live = LiveOwners(session);
        world.Events.Scene.UnlockedTutorials.Remove(66);
        Assert.Throws<InvalidOperationException>(() => world.BeginMenuTutorial(4));
        world.Events.Scene.UnlockedTutorials.Add(66);
        world.BeginMenuTutorial(4);
        Assert.Null(world.Events.Scene.NativeTutorial);
        Assert.Equal(4, world.Events.Scene.NativeMenuTutorial!.TutorialId);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 1, Quantity: 0)).Accepted);
        GameSession independent = new(session.State with { EventState = world.Events.Capture() });
        using GameWorld restored = new(independent, directory);
        restored.EnsureField();
        Assert.NotNull(restored.Tutorial);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        for (int tick = 0; tick < 3000 && world.Events.Scene.NativeMenuTutorial is not null; tick++)
        {
            PlayerAction action = new(world.Tutorial!.WaitingForInput ? PlayerActionKind.Cancel : PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.True(restored.Apply(action).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.Null(world.Events.Scene.NativeMenuTutorial);
        Assert.Null(restored.Events.Scene.NativeMenuTutorial);
        Assert.Null(world.Events.Scene.NativeTutorialPlayback);
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.True(JsonElement.DeepEquals(live, LiveOwners(session)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x40)]
    public void NativeMenuInputBaselinePreventsStaleHeldButtonsAndInventedReturnEdges(ushort heldAtExit)
    {
        (string directory, GameState source) = Source();
        GameSession session = new(source);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Events.Step();
        FocusOriginalRequest(world, 4);
        long frame = world.Events.Frame;
        world.Events.ObserveNativeMenuButtons(0x40);
        world.Events.ObserveNativeMenuButtons(heldAtExit);
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal(heldAtExit, session.FieldButtonInput!.RawHeld);
        Assert.Equal((ushort)0, session.FieldButtonInput.RawPressed);
        CloseAtNextGate(world);
        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.Equal(heldAtExit, session.FieldButtonInput.RawHeld);
        Assert.Equal((ushort)0, session.FieldButtonInput.RawPressed);
        Assert.Equal(heldAtExit, session.FieldButtonInput.Held);
        Assert.Equal((ushort)0, session.FieldButtonInput.Pressed);
        Assert.Throws<InvalidOperationException>(() => world.Events.ObserveNativeMenuButtons(0));
    }

    private static void CloseAtNextGate(GameWorld world)
    {
        for (int tick = 0; tick < 3000 && world.Tutorial is not null; tick++)
        {
            PlayerAction action = new(world.Tutorial.WaitingForInput ? PlayerActionKind.Cancel : PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Null(world.Tutorial);
    }

    private static void FocusOriginalRequest(GameWorld world, int raw)
    {
        JsmProgram program = JsmProgram.Read(world.Field!.Jsm);
        Assert.Equal(Jsm.Opcode.TUTO, program.Operations[1742].Opcode);
        Assert.Equal(4, program.Operations[1741].Parameter);
        bool[] visibility = world.Events.Actors.Select(actor => actor.Visible).ToArray();
        JsonNode saved = JsonNode.Parse(world.Events.Capture().GetRawText())!;
        //Isolate the requested original operation from unrelated field scripts/body drawing.
        //This explicit diagnostic fixture preserves native allocation and scheduler ownership.
        foreach (JsonNode? actor in saved["Actors"]!.AsArray())
        {
            actor!["Visible"] = false;
        }
        JsonArray fibers = saved["Fibers"]!.AsArray();
        foreach (JsonNode? fiber in fibers.Where(item => !item!["Initialization"]!.GetValue<bool>()))
        {
            fiber!["WaitReason"] = "Frames";
            fiber["WaitUntil"] = 100000;
        }
        JsonNode root = fibers.Single(item => item!["ObjectId"]!.GetValue<int>() == 11 && !item["Initialization"]!.GetValue<bool>())!;
        JsonNode requested = root.DeepClone();
        long sequence = saved["Sequence"]!.GetValue<long>() + 1;
        requested["Sequence"] = sequence;
        requested["ScriptId"] = 88;
        requested["Position"] = 1742;
        requested["Priority"] = 0;
        requested["Started"] = true;
        requested["WaitReason"] = "None";
        requested["WaitUntil"] = 0;
        requested["NativeDoorSlot"] = 7;
        requested["NativeDoorDefaultFrame"] = false;
        requested["NativeResultFrameEntered"] = true;
        requested["NativeDoorSavedResults"] = new JsonArray(Enumerable.Range(0, 8).Select(index => root["Results"]![index.ToString()]?.DeepClone() ?? JsonValue.Create(0)).ToArray());
        requested["Results"] = new JsonObject();
        requested["Stack"] = new JsonArray(raw);
        requested["StackExpressions"] = new JsonArray(raw.ToString(System.Globalization.CultureInfo.InvariantCulture));
        fibers.Add(requested);
        saved["Sequence"] = sequence;
        JsonNode scheduler = saved["Actors"]![11]!["NativeDoorScheduler"]!;
        scheduler["CurrentSlot"] = 7;
        scheduler["CurrentSequence"] = sequence;
        scheduler["RetiredOperation"] = null;
        world.Events.Restore(JsonSerializer.SerializeToElement(saved));
        //The allocation's first frame has no native skin callback. Restore the authentic
        //visibility only after diagnostic setup, before executing the first full callback.
        System.Reflection.PropertyInfo visible = typeof(ActorState).GetProperty(nameof(ActorState.Visible))!;
        for (int index = 0; index < visibility.Length; index++)
        {
            visible.SetValue(world.Events.Actors[index], visibility[index]);
        }
        world.Events.Step();
        Assert.Contains(world.Events.Actors, actor => actor.Visible && actor.ModelId >= 0);
        Assert.All(world.Events.Scene.ModelParts!.Instances.Where(instance => world.Events.Actors[instance.ObjectId].Visible), instance => Assert.Equal(world.Events.Frame, world.Events.Scene.ModelParts.Draws[instance.ObjectId].Frame));
        Assert.Equal(PlayerTriggerKind.Tutorial, world.NextTrigger.Kind);
    }

    private static JsonElement LiveOwners(GameSession session) => JsonSerializer.SerializeToElement(new { session.State.CharacterVitals, session.State.Economy, session.State.KnownMagic, session.State.CardInventory, session.State.LimitProgress });

    private static (string Directory, GameState State) Source()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required.");
        using GameAssets assets = new(directory);
        // A fresh diagnostic allocation, not gameplay reconstruction or an old-save migration.
        GameState state = GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 159,
            FieldName = "bggate_1",
            WorldControls = NativeWorldControlState.CreateNewGame(),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(),
            SeeD = NativeSeeDState.CreateNewGame() with { SalaryTable = assets.ReadNativeSalaryTable() },
            EventRandom = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame(),
            CharacterVitals = assets.ReadNewGameCharacterVitals(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            CardInventory = NativeCardInventory.CreateNewGame(),
            ShopHistory = assets.ReadNewGameShopHistory(),
            WeaponRecipes = assets.ReadNewGameWeaponRecipes(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            ScannedModels = assets.ReadNewGameScannedModels()
        };
        return (directory, state);
    }

    private static byte[] BrowserProgram()
    {
        (Jsm.Opcode Opcode, int Parameter)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUTUTO, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int argument) = operations[index];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), argument == 0 ? (int)opcode : (int)opcode << 24 | argument);
        }
        return bytes;
    }
}
