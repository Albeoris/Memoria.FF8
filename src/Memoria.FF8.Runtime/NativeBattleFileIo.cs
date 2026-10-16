using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The separate callback DWORD and12-byte482610 row; null denotes unobserved retained memory.</summary>
public sealed record NativeBattleFileIoRow([property: JsonRequired] uint? SchedulerCallback, [property: JsonRequired] short? Status, [property: JsonRequired] short? Countdown, [property: JsonRequired] int? FileResult, [property: JsonRequired] uint? CompletionCallback);

public sealed record NativeBattleFileIoCompletion(int Slot, uint Callback, int Result);

/// <summary>482560/482590's global sixteen-row completion scheduler; file bytes are written synchronously before queueing.</summary>
public sealed record NativeBattleFileIoState([property: JsonRequired] ImmutableArray<NativeBattleFileIoRow> Rows, [property: JsonRequired] short? ActiveCount, [property: JsonRequired] short? CurrentSlot, [property: JsonRequired] short? DelayOverride)
{
    public static NativeBattleFileIoState FromColdProcess() => new(Enumerable.Range(0, 16).Select(_ => new NativeBattleFileIoRow(0, 0, 0, 0, 0)).ToImmutableArray(), 0, 0, 0);

    public static NativeBattleFileIoState Unknown() => new(Enumerable.Range(0, 16).Select(_ => new NativeBattleFileIoRow(null, null, null, null, null)).ToImmutableArray(), null, null, null);

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 16 || Rows.Any(row => row is null) || CurrentSlot is < 0 or >= 16)
        {
            throw new InvalidDataException("Native482590 requires sixteen physical IO rows and its bounded current-slot WORD.");
        }
    }

    public NativeBattleFileIoState Reset482560()
    {
        Validate();
        return this with { Rows = Rows.Select(row => row with { SchedulerCallback = 0, Status = 0 }).ToImmutableArray(), ActiveCount = 0 };
    }

    public NativeBattleFileIoState Queue482610(int fileResult, uint completionCallback, int? outerBattlePhase)
    {
        Validate();
        if (completionCallback == 0)
        {
            return this;
        }
        int slot = 0;
        bool empty = false;
        for (int index = 0; index < 16; index++)
        {
            uint callback = Rows[index].SchedulerCallback ?? throw new NotSupportedException("Native482610 requires each scanned callback slot's actual retained DWORD.");
            if (callback == 0)
            {
                slot = index;
                empty = true;
                break;
            }
        }
        short delay = CalculateCountdown482610(fileResult, DelayOverride ?? throw new NotSupportedException("Native482610 requires the actual retained delay WORD1D2A1FA."), outerBattlePhase);
        NativeBattleFileIoRow row = Rows[slot] with { SchedulerCallback = 0x482870, Countdown = delay, FileResult = fileResult, CompletionCallback = completionCallback };
        short? count = empty ? unchecked((short)((ActiveCount ?? throw new NotSupportedException("Native482610 requires its actual active-count WORD.")) + 1)) : ActiveCount;
        return this with { Rows = Rows.SetItem(slot, row), ActiveCount = count };
    }

    public static short CalculateCountdown482610(int fileResult, short delayOverride, int? outerBattlePhase)
    {
        if (delayOverride != 0 && outerBattlePhase is null)
        {
            throw new NotSupportedException("Native482610's nonzero delay override requires the actual outer battle phase DWORD1D27B04.");
        }
        if (delayOverride != 0 && outerBattlePhase == 4)
        {
            return delayOverride;
        }
        int high = unchecked((int)((0x66666667L * fileResult) >> 32));
        int quotient = high >> (delayOverride == 0 ? 13 : 12);
        quotient = unchecked(quotient + (int)((uint)quotient >> 31));
        return unchecked((short)quotient);
    }

    public NativeBattleFileIoState Advance482590(Func<NativeBattleFileIoState, NativeBattleFileIoCompletion, NativeBattleFileIoState> dispatch)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(dispatch);
        NativeBattleFileIoState state = this with { CurrentSlot = 0 };
        int selected = -1;
        for (int index = 0; index < 16; index++)
        {
            uint callback = state.Rows[index].SchedulerCallback ?? throw new NotSupportedException("Native482590 requires each scanned callback slot's actual retained DWORD.");
            if (callback != 0)
            {
                if (callback != 0x482870)
                {
                    throw new NotSupportedException($"Native482590 scheduler callback{callback:X8} is unsupported.");
                }
                selected = index;
                break;
            }
        }
        if (selected < 0)
        {
            return state;
        }
        state = state with { CurrentSlot = checked((short)selected) };
        NativeBattleFileIoRow row = state.Rows[selected];
        short countdown = row.Countdown ?? throw new NotSupportedException("Native482870 requires its actual signed countdown WORD.");
        if (countdown == 0)
        {
            state = state with { Rows = state.Rows.SetItem(selected, row with { Status = -1 }) };
            uint completion = row.CompletionCallback ?? throw new NotSupportedException("Native482870 requires its actual completion-function DWORD.");
            if (completion != 0)
            {
                int result = row.FileResult ?? throw new NotSupportedException("Native482870 requires its actual synchronous file-read result DWORD.");
                state = dispatch(state, new(selected, completion, result)) ?? throw new InvalidDataException("Native482870 completion lost its IO owner.");
                state.Validate();
            }
        }
        //482870 holds the physical row address across the completion callback, then decrements that row's current WORD.
        row = state.Rows[selected];
        countdown = row.Countdown ?? throw new NotSupportedException("Native482870 completion lost its physical countdown WORD.");
        state = state with { Rows = state.Rows.SetItem(selected, row with { Countdown = unchecked((short)(countdown - 1)) }) };
        //482590 reloads the global current index after its callback; ordinary enqueue leaves it unchanged.
        int current = state.CurrentSlot ?? throw new NotSupportedException("Native482590 completion lost its current-slot WORD.");
        row = state.Rows[current];
        short status = row.Status ?? throw new NotSupportedException("Native482590 requires its actual completion status WORD.");
        if (status == -1)
        {
            short count = state.ActiveCount ?? throw new NotSupportedException("Native482590 requires its actual active-count WORD.");
            state = state with { Rows = state.Rows.SetItem(current, row with { Status = 0, SchedulerCallback = 0 }), ActiveCount = unchecked((short)(count - 1)) };
        }
        return state with { CurrentSlot = 0 };
    }
}
