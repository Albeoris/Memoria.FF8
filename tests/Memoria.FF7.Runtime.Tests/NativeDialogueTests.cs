using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDialogueTests
{
    private static void CursorTimingAndRestore()
    {
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(new byte[] { 0x21, 0xFE, 0xDD, 3, 0, 0x22, 0xE8, 0x23, 0xFE, 0xDC, 0x24, 0xFF });
        Ff7Dialogue state = new(1, 0, 0, null, null, 0);
        state = Ff7DialogueCursor.AdvanceTick(state, tokens);
        Equal(3, state.WaitTicks);
        Equal(false, state.AwaitingInput);
        Equal(state, Ff7DialogueCursor.Confirm(state, tokens));
        Ff7Dialogue restored = System.Text.Json.JsonSerializer.Deserialize<Ff7Dialogue>(System.Text.Json.JsonSerializer.Serialize(state))!;
        for (int tick = 0; tick < 3; tick++)
        {
            state = Ff7DialogueCursor.AdvanceTick(state, tokens);
            restored = Ff7DialogueCursor.AdvanceTick(restored, tokens);
            Equal(state, restored);
            Equal(false, state.AwaitingInput);
        }
        state = Ff7DialogueCursor.AdvanceTick(state, tokens);
        Equal(true, state.AwaitingInput);
        Equal(0, state.Page);
        state = Ff7DialogueCursor.Confirm(state, tokens);
        Equal(1, state.Page);
        Equal(false, state.Completed);
        state = Ff7DialogueCursor.AdvanceTick(state, tokens);
        Equal(true, state.AwaitingInput);
        state = Ff7DialogueCursor.Confirm(state, tokens);
        Equal(1, state.Page);
        state = Ff7DialogueCursor.AdvanceTick(state, tokens);
        Equal(true, Ff7DialogueCursor.Confirm(state, tokens).Completed);
        IReadOnlyList<FieldTextToken> scroll = FieldText.Read(new byte[] { 0xFE, 0xE0, 0xFF });
        try
        {
            Ff7DialogueCursor.AdvanceTick(new(1, 0, 0, null, null, 0), scroll);
            throw new Exception("Unsupported scrolling text was accepted.");
        }
        catch (NotSupportedException)
        {
        }
        Ff7Dialogue question = new(2, 0, 0, 2, 3, 2, AwaitingInput: true);
        Ff7Dialogue selected = Ff7DialogueCursor.Select(question, 3);
        Equal(2, question.Selected);
        Equal(3, selected.Selected);
        Equal(true, Ff7DialogueCursor.Confirm(selected, []).Completed);
        Equal(3, Ff7DialogueCursor.Confirm(selected, []).Selected);
        try
        {
            Ff7DialogueCursor.Select(question, 1);
            throw new Exception("A reply outside the original question bounds was accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    public static void Run()
    {
        CursorTimingAndRestore();
        Ff7FieldResources field = Field();
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { Tick = 42, Windows = [new Ff7Window(0, 10, 20, 200, 70)] });
        FieldHostRequest request = new(1, 0, 1, 104, new FieldDialogueCommand(0, 0, 0, 1, 0));
        Equal(false, host.Execute(request).Completed);
        host.UpdateDialogue(1, 0, 0, 2, 3, false);
        Ff7Dialogue waiting = host.Capture().Dialogues.Single();
        Equal(3, waiting.WaitTicks);
        Equal(2, waiting.TokenPosition);
        Equal(false, waiting.Completed);

        FieldMachine machine = new(FieldProgram.FromPcScripts(field.Scripts));
        machine.Initialize();
        Ff7SessionSave ff7 = new()
        {
            ResourceProfile = "authored-dialogue",
            Language = "en",
            FieldName = field.Name,
            Progress = new Ff7Progress { InitialKernelData = [1], Characters = [new Ff7CharacterState(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [], Materia = [], Gil = 240 },
            Vm = machine.Capture(),
            Process = host.Capture()
        };
        CombinedSessionSave envelope = new() { ActiveGame = SessionGame.Ff7, Ff8Payload = [8, 0, 255], Ff8PayloadFormat = "opaque-ff8", Ff7 = ff7 };
        CombinedSessionSave restored = RoundTrip(envelope);
        Equal(true, restored.Ff8Payload!.SequenceEqual(envelope.Ff8Payload));
        Equal(42L, restored.Ff7!.Process.Tick);
        Ff7FieldHost cold = new(field, (_, _) => 1, restored.Ff7.Process);
        Equal(3, cold.Process.Dialogues.Single().WaitTicks);
        Equal(false, cold.Execute(request).Completed);
        cold.UpdateDialogue(1, 0, 0, 2, 2, false);
        Equal(2, cold.Process.Dialogues.Single().WaitTicks);
        cold.UpdateDialogue(1, 1, 1, 6, 0, true);
        CombinedSessionSave paused = RoundTrip(envelope with { Ff7 = ff7 with { Process = cold.Capture() } });
        Ff7Dialogue page = paused.Ff7!.Process.Dialogues.Single();
        Equal(1, page.Page);
        Equal(1, page.Selected);
        Equal(6, page.TokenPosition);
        Equal(true, page.AwaitingInput);
        Ff7FieldHost resumed = new(field, (_, _) => 1, paused.Ff7.Process);
        resumed.UpdateDialogue(1, 1, 1, 7, 0, true);
        resumed.CompleteDialogue(1, 1);
        FieldHostReply result = resumed.Execute(request);
        Equal(true, result.Completed);
        Equal(1, result.Value);
        Equal(42L, resumed.Process.Tick);

        Ff7FieldHost invalid = new(field, (_, _) => 1);
        invalid.Execute(new FieldHostRequest(1, 0, 1, 104, new FieldWindowCommand(FieldWindowOperation.Size, 0, 0, 0, 100, 50)));
        invalid.Execute(request with { Id = 2 });
        Reject(() => invalid.UpdateDialogue(2, 2, 0, 0, 0, false));
        Reject(() => invalid.UpdateDialogue(2, 0, 0, 8, 0, false));
        Reject(() => invalid.UpdateDialogue(2, 0, 0, 0, 65536, false));
        Reject(() => invalid.UpdateDialogue(2, 0, 0, 0, 1, true));
        Reject(() => invalid.UpdateDialogue(2, 0, 2, 0, 0, false));
    }

    private static CombinedSessionSave RoundTrip(CombinedSessionSave save)
    {
        using MemoryStream bytes = new();
        CombinedSessionCodec.Write(bytes, save);
        bytes.Position = 0;
        return CombinedSessionCodec.Read(bytes);
    }

    private static Ff7FieldResources Field()
    {
        byte[] text = [0x21, 0xFE, 0xDD, 3, 0, 0x22, 0xE8, 0xEA, 0xFE, 0xDC, 0x23, 0xFF];
        byte[] section = new byte[110 + text.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 0x0502);
        section[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(4), 106);
        for (int i = 0; i < 32; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(40 + i * 2), 104);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(106), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(108), 4);
        text.CopyTo(section, 110);
        return new Ff7FieldResources("dialogue-fixture", null!, PcFieldScripts.Read(section), null!, [], [], null!, null!, null!);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Dialogue expected {expected}, got {actual}.");
        }
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new Exception("Invalid dialogue continuation was accepted.");
    }
}
