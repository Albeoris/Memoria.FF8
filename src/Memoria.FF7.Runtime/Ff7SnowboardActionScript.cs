namespace Memoria.FF7.Runtime;

public sealed record Ff7SnowboardActionLoop(byte Count = 0, byte Start = 0);
public sealed record Ff7SnowboardActionTrigger(bool Enabled = false, bool Pending = false, byte Target = 0);
public sealed record Ff7SnowboardActionScriptState
{
    public ushort ActionWord { get; init; }
    public short ProgramCounter { get; init; }
    public int Remaining { get; init; }
    public int LoopDepth { get; init; }
    public byte CanPush { get; init; }
    public byte FrameModel { get; init; }
    public Ff7SnowboardActionLoop[] Loops { get; init; } = [new(), new(), new(), new()];
    public Ff7SnowboardActionTrigger[] Triggers { get; init; } = [new(), new(), new(), new()];
}

public readonly record struct Ff7SnowboardActionScriptInput(ushort Held, int Speed, byte? BranchSelector = null);
public abstract record Ff7SnowboardActionEffect;
/// <summary>Model is the native pose-table index, not a renderable model allocation.</summary>
public sealed record Ff7SnowboardActionFrame(byte Model, byte Duration, byte Easing) : Ff7SnowboardActionEffect;
public sealed record Ff7SnowboardActionJump(float VelocityY, float Gravity, short AirFrames) : Ff7SnowboardActionEffect;
public sealed record Ff7SnowboardActionScriptStep(Ff7SnowboardActionScriptState State, Ff7SnowboardActionEffect[] Effects, byte? UnsupportedCommand = null);

public static class Ff7SnowboardActionScript
{
    /// <summary>The external action selector may reset first and then set ActionWord's native 0x8000 continuation flag.</summary>
    public static Ff7SnowboardActionScriptState Reset(Ff7SnowboardActionScriptState state)
    {
        Validate(state);
        return state with { ProgramCounter = 0, Remaining = 0, LoopDepth = 0, CanPush = 0, Triggers = state.Triggers.Select(trigger => trigger with { Enabled = false, Pending = false }).ToArray() };
    }

    public static Ff7SnowboardActionScriptState SignalEvent(Ff7SnowboardActionScriptState state, int slot)
    {
        Validate(state);
        if ((uint)slot >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        Ff7SnowboardActionTrigger[] triggers = state.Triggers.ToArray();
        triggers[slot] = triggers[slot] with { Pending = true };
        return state with { Triggers = triggers };
    }

    public static Ff7SnowboardActionScriptStep Advance(Ff7SnowboardActionScriptState state, IReadOnlyList<byte> scripts, IReadOnlyList<ushort> masks, Ff7SnowboardActionScriptInput input)
    {
        Validate(state);
        if (scripts.Count != 64 * 256 || masks.Count != 8)
        {
            throw new InvalidDataException("Snowboard action data requires 64 native script blocks and eight input masks.");
        }
        byte branchSelector = input.BranchSelector ?? (unchecked((short)input.Speed) < 256 ? (byte)1 : (byte)0);
        if ((state.ActionWord & 0x8000) == 0)
        {
            state = Reset(state);
            branchSelector = 0;
        }
        Ff7SnowboardActionTrigger[] triggers = state.Triggers.ToArray();
        Ff7SnowboardActionLoop[] loops = state.Loops.ToArray();
        state = state with { Triggers = triggers, Loops = loops };
        for (int slot = 0; slot < triggers.Length; slot++)
        {
            if (triggers[slot] is { Enabled: true, Pending: true } trigger)
            {
                state = state with { ProgramCounter = trigger.Target, Remaining = 0 };
                triggers[slot] = trigger with { Enabled = false, Pending = false };
                break;
            }
        }
        List<Ff7SnowboardActionEffect> effects = [];
        HashSet<string> seen = [];
        byte? unsupported = null;
        bool yielded = state.Remaining != 0;
        while (!yielded)
        {
            string key = $"{state.ActionWord}:{state.ProgramCounter}:{state.LoopDepth}:{state.CanPush}:{state.FrameModel}:" + string.Join(';', loops.Select(loop => $"{loop.Count},{loop.Start}")) + ":" + string.Join(';', triggers.Select(trigger => $"{trigger.Enabled},{trigger.Pending},{trigger.Target}"));
            if (!seen.Add(key))
            {
                throw new NotSupportedException("A native snowboard action cycle does not yield.");
            }
            long offset = (state.ActionWord & 255) * 256L + state.ProgramCounter * 4L;
            if (state.ProgramCounter < 0 || offset < 0 || offset > scripts.Count - 4)
            {
                throw new InvalidDataException("Snowboard action command is outside the supplied script allocation.");
            }
            byte opcode = scripts[(int)offset];
            byte first = scripts[(int)offset + 1];
            byte second = scripts[(int)offset + 2];
            byte third = scripts[(int)offset + 3];
            short next = unchecked((short)(state.ProgramCounter + 1));
            switch (opcode)
            {
                case 0:
                case 6:
                    yielded = true;
                    break;
                case 1:
                    if (third >= 4)
                    {
                        throw new NotSupportedException("The native snowboard action easing selector is unsupported.");
                    }
                    effects.Add(new Ff7SnowboardActionFrame(first, second, third));
                    state = state with { ProgramCounter = next, Remaining = second, FrameModel = first };
                    yielded = true;
                    break;
                case 2:
                    if (state.LoopDepth >= 3)
                    {
                        yielded = true;
                        break;
                    }
                    state = state with { LoopDepth = state.LoopDepth + 1, ProgramCounter = next };
                    loops[state.LoopDepth] = new(first, unchecked((byte)next));
                    break;
                case 3:
                    if (state.LoopDepth <= 0)
                    {
                        yielded = true;
                        break;
                    }
                    Ff7SnowboardActionLoop loop = loops[state.LoopDepth];
                    if (loop.Count != 0)
                    {
                        loops[state.LoopDepth] = loop with { Count = unchecked((byte)(loop.Count - 1)) };
                        state = state with { ProgramCounter = loop.Start };
                    }
                    else
                    {
                        state = state with { ProgramCounter = next, LoopDepth = state.LoopDepth - 1 };
                    }
                    break;
                case 4:
                    state = state with { ProgramCounter = first };
                    break;
                case 5:
                case 11:
                    if (first >= 4)
                    {
                        throw new InvalidDataException("Snowboard action event slot is outside the native four-slot array.");
                    }
                    triggers[first] = opcode == 5 ? new(true, false, second) : triggers[first] with { Enabled = false };
                    state = state with { ProgramCounter = next };
                    break;
                case 7:
                    state = state with { ActionWord = first };
                    yielded = true;
                    break;
                case 8:
                    effects.Add(new Ff7SnowboardActionJump(-first * 512f, 4096f, 1));
                    state = state with { ProgramCounter = next };
                    break;
                case 9:
                    if (first >= 8)
                    {
                        throw new InvalidDataException("Snowboard action input mask selector is outside its native table.");
                    }
                    ushort held = second == 0 ? input.Held : unchecked((ushort)~input.Held);
                    if ((held & masks[first]) == 0)
                    {
                        yielded = true;
                    }
                    else
                    {
                        state = state with { ProgramCounter = next };
                    }
                    break;
                case 10:
                    if (branchSelector > 3)
                    {
                        throw new NotSupportedException("The native snowboard action branch selector is unsupported.");
                    }
                    state = state with { ProgramCounter = branchSelector switch { 0 => next, 1 => first, 2 => second, _ => third } };
                    break;
                case 12:
                    state = state with { CanPush = first, ProgramCounter = next };
                    break;
                case 13:
                    state = state with { ProgramCounter = next };
                    break;
                default:
                    unsupported = opcode;
                    yielded = true;
                    break;
            }
        }
        state = state with { Remaining = Math.Max(0, state.Remaining - 1) };
        return new(state, effects.ToArray(), unsupported);
    }

    private static void Validate(Ff7SnowboardActionScriptState state)
    {
        if (state.Loops is not { Length: 4 } || state.Triggers is not { Length: 4 } || state.Loops.Any(loop => loop is null) || state.Triggers.Any(trigger => trigger is null) || state.LoopDepth is < 0 or > 3 || state.Remaining is < 0 or > 255)
        {
            throw new InvalidDataException("Invalid native snowboard action observation state.");
        }
    }
}
