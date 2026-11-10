namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleMessageEntry(string Text, byte Delay, byte Remaining);

public sealed record Ff7BattleMessageState(Ff7BattleMessageEntry[] Pending, string? Visible = null, bool BlocksQueue = false)
{
    public static Ff7BattleMessageState Empty { get; } = new([]);
}

public static class Ff7NativeBattleMessage
{
    public const byte DefaultSpeed = 128;

    public static Ff7BattleMessageState Enqueue(Ff7BattleMessageState state, string text, byte speed = DefaultSpeed)
    {
        if (state.Pending.Length >= 64)
        {
            return state;
        }

        Ff7BattleMessageEntry entry = new(text, 1, (byte)((speed >> 2) + 4));
        return state with { Pending = [..state.Pending, entry] };
    }

    public static Ff7BattleMessageState AdvancePresentation(Ff7BattleMessageState state, bool liveFrame, bool paused, bool drawEnabled = true)
    {
        if (state.Pending.Length == 0)
        {
            return state with { Visible = null, BlocksQueue = false };
        }

        Ff7BattleMessageEntry entry = state.Pending[0];
        bool advance = liveFrame && !paused;
        if (entry.Delay != 0)
        {
            return state with { Pending = advance ? ReplaceHead(state.Pending, entry with { Delay = (byte)(entry.Delay - 1) }) : state.Pending, Visible = null, BlocksQueue = false };
        }

        if (entry.Remaining == 0)
        {
            return state with { Pending = state.Pending[1..], BlocksQueue = true };
        }

        return state with
        {
            Pending = advance ? ReplaceHead(state.Pending, entry with { Remaining = (byte)(entry.Remaining - 1) }) : state.Pending,
            Visible = drawEnabled ? entry.Text : state.Visible,
            BlocksQueue = true
        };
    }

    private static Ff7BattleMessageEntry[] ReplaceHead(Ff7BattleMessageEntry[] entries, Ff7BattleMessageEntry entry)
    {
        Ff7BattleMessageEntry[] result = (Ff7BattleMessageEntry[])entries.Clone();
        result[0] = entry;
        return result;
    }
}
