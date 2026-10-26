using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationFieldQuestionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(255)]
    [InlineData(1)]
    public void EvaluatedNativeQuestionKeepsVmAndResultsAcrossGenerationAndRestore(int cancel)
    {
        (Jsm.Opcode, int)[] commands = [(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_W, 1300), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.POPM_W, 1302), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHM_W, 1300), (Jsm.Opcode.PSHM_W, 1302), (Jsm.Opcode.PSHM_W, 1302), (Jsm.Opcode.PSHN_L, cancel), (Jsm.Opcode.ASK, 0), (Jsm.Opcode.HALT, 0)];
        EventEngine live = Create(commands);
        for (int callback = 0; callback < 20 && live.CurrentDialogue?.NativeWindow?.Progress != 4096; callback++)
        {
            live.Apply(new(PlayerActionKind.Wait));
        }
        DialogueState native = live.CurrentDialogue!;
        Assert.NotNull(native);
        Assert.Equal(1, native.FirstChoice);
        Assert.Equal(2, native.LastChoice);
        Assert.Equal(2, native.InitialChoice);
        Assert.Equal(cancel, native.CancelChoice);
        JsonElement active = live.Capture();
        EventEngine restored = Create(commands);
        restored.Restore(active);
        Assert.True(JsonElement.DeepEquals(active, restored.Capture()));
        string hash = Convert.ToHexString(SHA256.HashData(Program(commands)));
        LocalizationFieldText field = new("Localized heading{Line}Help{Line}Options{Line}Yes{Line}No", hash, 20, 30, 300, 120, null, null, null, null, [new(native.Operation, 0, true, null, null, null, null) { IsChoice = true }]) { ChoiceLineMap = [new(1, 3), new(2, 4)] };
        LocalizationCatalog Catalog(LocalizationFieldText value) => new(new Dictionary<string, LocalizationValue> { ["$en_fixture_0000"] = new(value.Body, "fixture", "fixture") { Field = value } });
        DialogueState Display(EventEngine engine, LocalizationCatalog catalog)
        {
            DialogueState original = engine.CurrentDialogue!;
            return RecreateGame.ProjectLocalizedDialogue(original, catalog.ResolveField("$en_fixture_0000", original.Operation, hash, original.FirstChoice, original.LastChoice, original.InitialChoice, original.CancelChoice));
        }
        DialogueState display = Display(live, Catalog(field));
        Assert.Equal(3, display.FirstChoice);
        Assert.Equal(4, display.LastChoice);
        Assert.Equal(4, display.InitialChoice);
        Assert.Equal(cancel == 1 ? 3 : cancel, display.CancelChoice);
        Assert.Equal(display, Display(restored, Catalog(field)));
        Assert.StartsWith("Updated", Display(restored, Catalog(field with { Body = field.Body.Replace("Localized", "Updated", StringComparison.Ordinal) })).Text);
        Assert.True(JsonElement.DeepEquals(active, live.Capture()));
        Assert.True(JsonElement.DeepEquals(active, restored.Capture()));
        Assert.Equal(native.Text, RecreateGame.ProjectLocalizedDialogue(native, null).Text);
        int nativeSelection = display.LastChoice!.Value - display.FirstChoice!.Value + native.FirstChoice!.Value;
        Assert.True(live.Apply(new(PlayerActionKind.Choose, nativeSelection)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Choose, nativeSelection)).Accepted);
        for (int callback = 0; callback < 10; callback++)
        {
            live.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(live.Capture(), restored.Capture()));
        }
        Assert.Empty(live.Dialogues);
        JsonElement fiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1);
        Assert.Equal(1, fiber.GetProperty("Results").GetProperty("0").GetInt32());
        Assert.Null(live.LastFault);
    }

    [Fact]
    public void OptionalStandaloneActiveQuestionSavesAndRestoresWithoutOwningTranslatedText()
    {
        string original = TestResources.FixturePath("App.Tests.LocalizationFieldQuestionTests.Standalone");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(original));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        DialogueState native = world.Events.CurrentDialogue!;
        Assert.NotNull(native);
        Assert.NotNull(native.FirstChoice);
        Assert.NotNull(native.LastChoice);
        JsonElement before = world.Events.Capture();
        string hash = Convert.ToHexString(SHA256.HashData(world.Field!.Jsm));
        LocalizationFieldText field = new("Localized heading{Line}Unicode translation{Line}" + native.Text, hash, null, null, null, null, null, null, null, null, [new(native.Operation, native.MessageId, true, null, null, null, null) { IsChoice = true }])
        {
            ChoiceLineMap = Enumerable.Range(native.FirstChoice!.Value, native.LastChoice!.Value - native.FirstChoice.Value + 1).Select(line => new LocalizationChoiceLineMapEntry(line, line + 2)).ToImmutableArray()
        };
        LocalizationCatalog catalog = new(new Dictionary<string, LocalizationValue> { ["$en_fixture_0000"] = new(field.Body, "fixture", "fixture") { Field = field } });
        DialogueState Display(DialogueState dialogue) => RecreateGame.ProjectLocalizedDialogue(dialogue, catalog.ResolveField("$en_fixture_0000", dialogue.Operation, hash, dialogue.FirstChoice, dialogue.LastChoice, dialogue.InitialChoice, dialogue.CancelChoice));
        DialogueState display = Display(native);
        Assert.Equal(native.FirstChoice + 2, display.FirstChoice);
        Assert.Equal(native.LastChoice + 2, display.LastChoice);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        session.SetEventState(before);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_CHECKPOINT_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        string active = Path.Combine(output, "localized-question-active.json");
        session.Save(active);
        using GameWorld restored = new(new GameSession(GameSession.Load(active)), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(display, Display(restored.Events.CurrentDialogue!));
        Assert.Equal(native.Text, restored.Events.CurrentDialogue!.Text);
        Assert.True(world.Apply(new(PlayerActionKind.Choose, native.LastChoice.Value)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Choose, native.LastChoice.Value)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        session.SetEventState(world.Events.Capture());
        string selected = Path.Combine(output, "localized-question-selected.json");
        session.Save(selected);
        File.WriteAllText(Path.Combine(output, "localized-question-validation.json"), JsonSerializer.Serialize(new { Source = original, Active = active, Selected = selected, Field = world.Events.FieldId, Frame = world.Events.Frame, native.Operation, native.MessageId, native.FirstChoice, native.LastChoice, native.InitialChoice, native.CancelChoice, NativeTextUnchanged = true, ActiveRestoreEqual = true, ChoiceRestoreEqual = true }));
    }

    private static EventEngine Create((Jsm.Opcode Opcode, int Parameter)[] commands, bool requirePlayable = false)
    {
        NativeInputConfiguration configuration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray());
        GameSession coldProcess = GameSession.CreateColdProcess();
        coldProcess.SetEventRandom(coldProcess.EventRandom!.ClearSavedStateForNewGame());
        GameSession session = new(coldProcess.State with { Scene = SceneKind.Field, FieldId = 600, WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), InputConfiguration = configuration });
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (_, _, _) => 0
        };
        engine.LoadField(600, Program(commands), ["A native scene message."], new FieldTransition(600, 0, 0, 0, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Scene.Party[0] = 0;
        engine.Scene.Party[1] = 255;
        engine.Scene.Party[2] = 255;
        for (int callback = 0; callback < 8; callback++)
        {
            engine.Step();
            JsonElement saved = engine.Capture();
            if (saved.GetProperty("DefaultsStarted").GetBoolean() && saved.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1 && fiber.GetProperty("Started").GetBoolean()))
            {
                break;
            }
        }
        Assert.Null(engine.LastFault);
        
        return engine;
    }

    private static byte[] Program((Jsm.Opcode Opcode, int Parameter)[] commands)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1)];
        operations.AddRange(commands);
        operations.Add((Jsm.Opcode.RET, 8));
        byte[] bytes = new byte[16 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Count));
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode is Jsm.Opcode.LBL or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_W ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }
}
