using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original camera group/track directory addressed by503520, including shared track tails.</summary>
public sealed record NativeBattleCameraBank(ImmutableArray<byte> Data)
{
    public int? TrackOffset(byte selector)
    {
        ushort ReadWord(int offset)
        {
            if (Data.IsDefault || offset < 0 || offset > Data.Length - 2)
            {
                throw new InvalidDataException("Native camera directory exceeds its original resource.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan().Slice(offset));
        }
        int group = selector >> 4 & 15;
        int groups = ReadWord(0);
        if (group >= groups)
        {
            return null;
        }
        int directory = ReadWord(2 + 2 * group);
        int relative = unchecked((short)ReadWord(directory + 2 * (selector & 7)));
        int track = checked(directory + 2 * relative);
        _ = ReadWord(track);
        return track;
    }
}

public sealed record NativeBattleCameraTask(byte Channel, NativeBattleCameraBank Bank, int ProgramOffset, ushort Elapsed, ushort Duration);

/// <summary>5041E0/503520/5035E0 two-slot camera callback owner; mask includes the separately written high bits.</summary>
public sealed record NativeBattleCameraState(ushort Mask, ImmutableArray<NativeBattleCameraTask?> Tasks, ImmutableArray<int> Order)
{
    public ImmutableArray<short> Variables { get; init; } = [0, 0, 0, 0, 0, 0, 0, 0];
    public static NativeBattleCameraState Initialize() => new(0, [null, null], []);

    public void Validate()
    {
        if (Variables.IsDefault || Variables.Length != 8 || Tasks.IsDefault || Tasks.Length != 2 || Order.IsDefault || Order.Any(slot => slot is < 0 or >= 2 || Tasks[slot] is null) || Order.Distinct().Count() != Order.Length || Order.Length != Tasks.Count(task => task is not null) || Tasks.Any(task => task is not null && (task.Channel >= 8 || task.Bank.Data.IsDefault || task.ProgramOffset < 0 || task.ProgramOffset > task.Bank.Data.Length - 2)))
        {
            throw new InvalidDataException("Native camera task owner requires its two original slots and bounded resource cursors.");
        }
    }

    public NativeBattleCameraState Start(NativeBattleCameraBank bank, byte selector)
    {
        Validate();
        int? track = bank.TrackOffset(selector);
        int slot = Tasks.IndexOf(null);
        if (track is null || slot < 0)
        {
            return this;
        }
        byte channel = (byte)(selector & 7);
        return this with { Mask = (ushort)(Mask | 1 << channel), Tasks = Tasks.SetItem(slot, new(channel, bank, track.Value, 0, 0)), Order = Order.Add(slot) };
    }

    public NativeBattleCameraState Tick(uint graphicsFlags)
    {
        Validate();
        NativeBattleCameraState result = this;
        foreach (int slot in Order)
        {
            if (Tasks[slot] is not { } task)
            {
                continue;
            }
            while (task.Elapsed >= task.Duration)
            {
                (int next, ushort? duration) = ReadSegment(task.Bank, task.ProgramOffset);
                if (duration is null)
                {
                    result = result with { Mask = (ushort)(result.Mask & ~(1 << task.Channel)), Tasks = result.Tasks.SetItem(slot, null), Order = result.Order.Remove(slot) };
                    break;
                }
                task = task with { ProgramOffset = next, Elapsed = 0, Duration = duration.Value };
            }
            if (result.Tasks[slot] is not null)
            {
                //5035E0 freezes only elapsed increments under graphics bits101; the callback still polls segments.
                task = task with { Elapsed = (graphicsFlags & 0x101) == 0 ? unchecked((ushort)(task.Elapsed + 16)) : task.Elapsed };
                result = result with { Tasks = result.Tasks.SetItem(slot, task) };
            }
        }
        return result;
    }

    private static (int Next, ushort? Duration) ReadSegment(NativeBattleCameraBank bank, int offset)
    {
        ushort ReadWord()
        {
            if (offset < 0 || offset > bank.Data.Length - 2)
            {
                throw new InvalidDataException("Native camera segment exceeds its original track resource.");
            }
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(bank.Data.AsSpan().Slice(offset));
            offset += 2;
            return value;
        }
        ushort flags = ReadWord();
        if (flags == ushort.MaxValue)
        {
            return (offset, null);
        }
        int lens = flags >> 6 & 3;
        int roll = flags >> 8 & 3;
        int parameters = Math.Max(0, lens - 1) + Math.Max(0, roll - 1);
        for (int parameter = 0; parameter < parameters; parameter++)
        {
            _ = ReadWord();
        }
        ushort duration = 0;
        for (int knot = 0; ; knot++)
        {
            short frames = unchecked((short)ReadWord());
            if (frames < 0)
            {
                return (offset, duration);
            }
            if (knot >= 32)
            {
                throw new InvalidDataException("Native camera segment exceeds its thirty-two-knot storage.");
            }
            duration = unchecked((ushort)(duration + 16 * frames));
            for (int coordinate = 0; coordinate < 8; coordinate++)
            {
                _ = ReadWord();
            }
        }
    }
}
