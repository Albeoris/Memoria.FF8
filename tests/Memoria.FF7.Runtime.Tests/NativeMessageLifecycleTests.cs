using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMessageLifecycleTests
{
    private static readonly Ff7Window Window = new(0, 48, 28, 212, 57);

    public static void Run()
    {
        OpeningAndClosing();
        RevealBandsAndExpansion();
        WaitPausePageAndLegacy();
        HostPollAndWindowClose();
        SavedPresentationValidation();
    }

    private static void OpeningAndClosing()
    {
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read([0x21, 0x22, 0x23, 0x24, 255]);
        Ff7Dialogue state = New();
        state = Tick(state, tokens, pressed: true);
        Equal(new Ff7DialoguePresentation(1, 53, 14), state.Presentation);
        Equal(false, state.TextVisible);
        foreach (var (width, height) in new[] { (106, 28), (159, 42), (212, 56), (212, 57) })
        {
            state = Tick(state, tokens, pressed: true);
            Equal(width, state.Presentation!.AnimatedWidth);
            Equal(height, state.Presentation!.AnimatedHeight);
            Equal(0, state.TokenPosition);
        }
        Equal(2, state.Presentation!.NativeState);
        Equal(true, state.TextVisible);
        state = Tick(state, tokens);
        Equal(1, state.TokenPosition);
        Equal((short)1, state.Presentation!.RevealBudget);
        state = Tick(state, tokens);
        Equal(3, state.TokenPosition);
        state = Tick(state, tokens);
        Equal(true, state.AwaitingInput);
        Equal(6, state.Presentation!.NativeState);
        Ff7Dialogue confirmed = Ff7DialogueCursor.Confirm(state, tokens);
        Equal(false, confirmed.Completed);
        Equal(true, confirmed.Presentation!.ConfirmPending);
        foreach (var (width, height, completed) in new[] { (159, 43, false), (106, 29, false), (53, 15, false), (0, 1, true) })
        {
            confirmed = Tick(confirmed, tokens);
            Equal(width, confirmed.Presentation!.AnimatedWidth);
            Equal(height, confirmed.Presentation!.AnimatedHeight);
            Equal(completed, confirmed.Completed);
        }
        Equal(false, confirmed.TextVisible);
        Ff7DialogueBounds bounds = Ff7DialogueCursor.WindowBounds(New() with { Presentation = new(1, 53, 14) }, Window);
        Equal(new Ff7DialogueBounds(127.5f, 49.5f, 53, 14), bounds);
        Ff7Dialogue wrapped = Tick(New() with { Presentation = new(1, 32764, 32764) }, tokens, window: Window with { Width = 32767, Height = 32767 });
        Equal(new Ff7DialoguePresentation(1, 8, 8), wrapped.Presentation);
    }

    private static void RevealBandsAndExpansion()
    {
        IReadOnlyList<FieldTextToken> glyphs = FieldText.Read([.. Enumerable.Repeat((byte)0x21, 50), 255]);
        foreach (var (speed, visible) in new[] { (0, 5), (127, 1), (128, 1), (159, 1), (160, 0), (255, 0) })
        {
            Ff7Dialogue state = Tick(Open(), glyphs, speed: (byte)speed);
            Equal(visible, state.TokenPosition);
        }
        Ff7Dialogue slow = Open();
        slow = Tick(slow, glyphs, speed: 255);
        slow = Tick(slow, glyphs, speed: 255);
        slow = Tick(slow, glyphs, speed: 255);
        Equal(1, slow.TokenPosition);
        Ff7Dialogue fast = Tick(Open() with { Presentation = new(2, 212, 57, FastCounter: 15) }, glyphs, held: true);
        Equal((short)16, fast.Presentation!.FastCounter);
        Equal(2, fast.TokenPosition);
        fast = Tick(fast, glyphs);
        Equal((short)15, fast.Presentation!.FastCounter);
        Equal(4, fast.TokenPosition);
        fast = Tick(Open() with { Presentation = new(2, 212, 57, FastCounter: 15) }, glyphs, window: Window with { Flags = 2 });
        Equal(50, fast.TokenPosition);
        Equal((short)15, fast.Presentation!.FastCounter);
        Equal(6, fast.Presentation!.NativeState);
        IReadOnlyList<FieldTextToken> compact = FieldText.Read([0xE2, 0xE1, 0xE0, 0x21, 255]);
        Equal(4, Tick(Open(), compact).TokenPosition);

        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "ABCDEFGHIJKLM", 1, 0, 1, 1, new byte[132]), new(1, "Second", 1, 0, 1, 1, new byte[132])], Party = [0, 255, 255], Inventory = [], Materia = [] };
        IReadOnlyList<FieldTextToken> names = FieldText.Read([0xEA, 255]);
        string Expand(FieldTextToken token) => Ff7DialogueCursor.ExpandToken(token, progress, [1, 0, 255]);
        Equal("ABCDEFGHI", Expand(names[0]));
        Equal("Second", Expand(FieldText.Read([0xF3, 255])[0]));
        Ff7Dialogue name = Tick(Open(), names, expand: Expand);
        Equal(0, name.TokenPosition);
        Equal(1, name.Presentation!.ExpansionOffset);
        for (int callback = 0; callback < 8 && !name.AwaitingInput; callback++)
        {
            name = Tick(name, names, expand: Expand);
        }
        Equal(1, name.TokenPosition);
        Equal(true, name.AwaitingInput);
    }

    private static void WaitPausePageAndLegacy()
    {
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read([0x21, 0xFE, 0xDD, 3, 0, 0x22, 0xFE, 0xDC, 0x23, 0xE8, 0x24, 255]);
        Ff7Dialogue state = Tick(Open(), tokens);
        state = Tick(state, tokens);
        Equal(3, state.WaitTicks);
        Equal(3, state.Presentation!.NativeState);
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            state = Tick(state, tokens);
            Equal(remaining, state.WaitTicks);
            Equal(3, state.Presentation!.NativeState);
        }
        state = Tick(state, tokens);
        Equal(2, state.Presentation!.NativeState);
        Equal(2, state.TokenPosition);
        state = Tick(state, tokens);
        Equal(13, state.Presentation!.NativeState);
        state = Ff7DialogueCursor.Confirm(state, tokens);
        state = Tick(state, tokens);
        Equal(false, state.AwaitingInput);
        Equal(4, state.TokenPosition);
        state = Tick(state, tokens);
        state = Tick(state, tokens);
        Equal(14, state.Presentation!.NativeState);
        Equal(0, state.Page);
        state = Tick(Ff7DialogueCursor.Confirm(state, tokens), tokens);
        Equal(1, state.Page);
        Equal(6, state.TokenPosition);
        Equal(2, state.Presentation!.NativeState);

        Ff7Dialogue legacy = new(1, 0, 0, null, null, 0, Page: 1, TokenPosition: tokens.Count, AwaitingInput: true);
        Ff7Dialogue restored = Ff7DialogueCursor.RestorePresentation(legacy, tokens, Window);
        Equal(6, restored.Presentation!.NativeState);
        Equal(legacy with { Presentation = restored.Presentation }, restored);
        restored = Tick(Ff7DialogueCursor.Confirm(restored, tokens), tokens);
        Equal(7, restored.Presentation!.NativeState);
        Equal(false, restored.Completed);
        Equal(legacy with { Completed = true }, Ff7DialogueCursor.RestorePresentation(legacy with { Completed = true }, tokens, Window));
        Ff7Dialogue persistent = Open() with { TokenPosition = 1, AwaitingInput = true, Presentation = new(6, 212, 57) };
        persistent = Tick(Ff7DialogueCursor.Confirm(persistent, FieldText.Read([0x21, 255])), FieldText.Read([0x21, 255]), window: Window with { Flags = 1 });
        Equal(6, persistent.Presentation!.NativeState);
        Equal(false, persistent.Completed);
    }

    private static void HostPollAndWindowClose()
    {
        Ff7FieldResources field = NativeDialogueTests.Field([0x21, 0x22, 0x23, 0x24, 255]);
        byte[] code = [0, 0x40, 0, 0, 0x80, 0x50, 3, 99, 0x5F, 0x12, 0];
        var program = new FieldProgram(code, [Enumerable.Repeat((ushort)0, 32).ToArray()]);
        Ff7FieldHost host = Host(field);
        var vm = new FieldMachine(program, host: host);
        vm.Initialize();
        for (int callback = 1; callback <= 8; callback++)
        {
            vm.AdvanceTick();
            Equal(1, vm.Capture().Entities[0].ProgramCounter);
            Equal((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 3));
            Ff7FieldProcess process = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
            host = new(field, (_, _) => 1, process);
            vm = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(vm.Capture()))!, host);
        }
        Equal(true, host.Process.Dialogues.Single().AwaitingInput);
        host.CompleteDialogue(host.Process.Dialogues.Single().RequestId, 0);
        Equal(false, host.Process.Dialogues.Single().Completed);
        for (int callback = 0; callback < 3; callback++)
        {
            vm.AdvanceTick();
            Equal((byte)0, vm.Variables.ReadByte(FieldBank.MapByte, 3));
        }
        vm.AdvanceTick();
        Equal((byte)99, vm.Variables.ReadByte(FieldBank.MapByte, 3));
        Equal(12L, vm.Tick);

        host = Host(field);
        var message = new FieldHostRequest(1, 0, 7, 0, new FieldDialogueCommand(0, 0));
        var close = new FieldHostRequest(2, 1, 7, 0, new FieldWindowCommand(FieldWindowOperation.Close, 0));
        host.Execute(message);
        Equal(false, host.Execute(close).Completed);
        for (int callback = 0; callback < 4; callback++)
        {
            host.Execute(message);
        }
        Equal(true, host.Execute(close).Completed);
        Equal(7, host.Process.Dialogues.Single().Presentation!.NativeState);
        Equal(false, host.Process.Dialogues.Single().Completed);
        for (int callback = 0; callback < 4; callback++)
        {
            host.Execute(message);
        }
        Equal(true, host.Process.Dialogues.Single().Completed);
    }

    private static Ff7FieldHost Host(Ff7FieldResources field)
    {
        return new(field, (_, _) => 1, new() { Windows = [Window], BlinkStateInitialized = true });
    }

    private static void SavedPresentationValidation()
    {
        Ff7FieldResources field = NativeDialogueTests.Field([0x21, 255]);
        foreach (Ff7DialoguePresentation presentation in new[] { new Ff7DialoguePresentation(15, 212, 57), new(2, 40000, 57), new(2, 212, 57, FastCounter: 0), new(2, 212, 57, ExpansionOffset: 10), new(2, 212, 57, Row: -1) })
        {
            Ff7FieldProcess process = new() { Windows = [Window], Dialogues = [Open() with { Presentation = presentation }], BlinkStateInitialized = true };
            try
            {
                _ = new Ff7FieldHost(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(process))!);
                throw new Exception("Corrupt native message state must fail before presentation.");
            }
            catch (InvalidDataException)
            {
            }
        }
        _ = new Ff7FieldHost(field, (_, _) => 1, new() { Windows = [Window], Dialogues = [Open() with { Presentation = null }], BlinkStateInitialized = true });
    }

    private static Ff7Dialogue New() => new(1, 0, 0, null, null, 0, Presentation: new(0, 0, 0));
    private static Ff7Dialogue Open() => New() with { Presentation = new(2, 212, 57) };

    private static Ff7Dialogue Tick(Ff7Dialogue state, IReadOnlyList<FieldTextToken> tokens, byte speed = 128, bool held = false, bool pressed = false, Func<FieldTextToken, string>? expand = null, Ff7Window? window = null)
    {
        Ff7Dialogue cold = JsonSerializer.Deserialize<Ff7Dialogue>(JsonSerializer.Serialize(state))!;
        state = Ff7DialogueCursor.AdvanceTick(state, tokens, window ?? Window, speed, held, pressed, expand);
        cold = Ff7DialogueCursor.AdvanceTick(cold, tokens, window ?? Window, speed, held, pressed, expand);
        Equal(state, cold);
        return state;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
