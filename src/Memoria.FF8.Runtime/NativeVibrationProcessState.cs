using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeVibrationBank([property: JsonRequired] string SourceHash, [property: JsonRequired] ImmutableArray<int> Offsets, [property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        if (string.IsNullOrEmpty(SourceHash) || SourceHash.Length != 64 || Offsets.IsDefaultOrEmpty || Bytes.IsDefaultOrEmpty || Offsets.Length * 4 > Bytes.Length)
        {
            throw new InvalidDataException("Vibration bank requires its complete authenticated pointer table and payload.");
        }
        foreach (int offset in Offsets)
        {
            if (offset != 0 && (offset < Offsets.Length * 4 || offset > Bytes.Length - 4))
            {
                throw new InvalidDataException("Vibration pattern header lies outside its original bank.");
            }
            if (offset != 0 && offset + 4 + ReadWord(offset) + ReadWord(offset + 2) > Bytes.Length)
            {
                throw new InvalidDataException("Vibration stream lies outside its original bank.");
            }
        }
    }

    public ushort ReadWord(int offset) => (ushort)(Bytes[offset] | Bytes[offset + 1] << 8);
}

public sealed record NativeVibrationChannel([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] int Cursor, [property: JsonRequired] ushort Length, [property: JsonRequired] short Counter, [property: JsonRequired] byte Active)
{
    public static NativeVibrationChannel Cold() => new(ImmutableArray<byte>.Empty, 0, 0, 0, 0);

    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length < Length || Bytes.Length > Length + 2 || Cursor < 0 || Cursor > Length + 1)
        {
            throw new InvalidDataException("Vibration channel lost its retained stream, cursor or WORD length.");
        }
    }

    internal (NativeVibrationChannel State, int Output) Step()
    {
        if (Active == 0)
        {
            return (this, -1);
        }
        if (Cursor >= Length)
        {
            return (this with { Active = 0 }, -1);
        }
        if (Cursor + 1 >= Bytes.Length)
        {
            throw new InvalidDataException("Native vibration stream lacks its duration byte.");
        }
        int duration = Bytes[Cursor + 1];
        if (duration == 255)
        {
            //4A2B80 leaves the active BYTE set at the sentinel.
            return (this, -1);
        }
        if (Cursor + 2 >= Bytes.Length)
        {
            throw new InvalidDataException("Native vibration interpolation lacks its following intensity byte.");
        }
        int intensity;
        NativeVibrationChannel next;
        if (Counter < duration)
        {
            int sample = Counter + 1;
            intensity = (sample * Bytes[Cursor + 2] + Bytes[Cursor] * (duration + 1 - sample)) / (duration + 1);
            next = this with { Counter = unchecked((short)(Counter + 1)) };
        }
        else
        {
            intensity = Bytes[Cursor + 2];
            next = this with { Cursor = Cursor + 2, Counter = 0 };
        }
        return (next, Math.Clamp(2 * intensity, 0, 255));
    }
}

public sealed record NativeVibrationSlot([property: JsonRequired] NativeVibrationChannel A, [property: JsonRequired] NativeVibrationChannel B, [property: JsonRequired] ushort Generation, [property: JsonRequired] byte Priority, [property: JsonRequired] byte Id);
public sealed record NativeVibrationPort([property: JsonRequired] byte RequestedA, [property: JsonRequired] byte RequestedB, [property: JsonRequired] bool Pending);
public sealed record NativeVibrationEnqueue(NativeVibrationProcessState State, int Handle);

/// <summary>Single process-owned4A29A0/4A2C30 queue and the actual PC56D980 stub endpoint.</summary>
public sealed record NativeVibrationProcessState
{
    public const string CurrentRule = "ff8-en-vibration-4a2d30-4a29a0-4a2c30-pc56d980-v1";
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required ImmutableArray<NativeVibrationSlot> Slots { get; init; }
    [JsonRequired]
    public required ImmutableArray<NativeVibrationPort> Ports { get; init; }
    [JsonRequired]
    public required int QuarterClockRemainder { get; init; }
    [JsonRequired]
    public required int LastSetReturn { get; init; }
    [JsonRequired]
    public required int GraphicsSpeed20409F0 { get; init; }
    [JsonRequired]
    public required int PreviousCarSpeed2045C84 { get; init; }
    [JsonRequired]
    public required int CarHandleC76788 { get; init; }

    /// <summary>An address AND-transform applied after bank binding; null retains the unmodified binding without inventing a physical heap address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? ResourcePointerAndMask { get; init; }

    public static NativeVibrationProcessState FromColdProcess()
    {
        //BSS motor/clock words0; executed4A2D30 priority0, generation1, IDs0..3.
        //Authenticated PE.data C76788 isFFFFFFFF, not a BSS handle.
        return new NativeVibrationProcessState { Rule = CurrentRule, Slots = Enumerable.Range(0, 4).Select(index => new NativeVibrationSlot(NativeVibrationChannel.Cold(), NativeVibrationChannel.Cold(), 1, 0, (byte)index)).ToImmutableArray(), Ports = ImmutableArray.Create(new NativeVibrationPort(0, 0, false), new NativeVibrationPort(0, 0, false)), QuarterClockRemainder = 0, LastSetReturn = 0, GraphicsSpeed20409F0 = 0, PreviousCarSpeed2045C84 = 0, CarHandleC76788 = -1 };
    }

    public void Validate()
    {
        if (Rule != CurrentRule || Slots.IsDefault || Slots.Length != 4 || Ports.IsDefault || Ports.Length != 2 || QuarterClockRemainder is < 0 or > 3 || GraphicsSpeed20409F0 < 0 || PreviousCarSpeed2045C84 < 0)
        {
            throw new InvalidDataException("Vibration process requires its four physical slots, two controller ports and retained world words.");
        }
        for (int index = 0; index < Slots.Length; index++)
        {
            NativeVibrationSlot slot = Slots[index] ?? throw new InvalidDataException("Missing physical vibration slot.");
            if (slot.Id != index || slot.A is null || slot.B is null)
            {
                throw new InvalidDataException("Vibration slot ID/channel allocation is invalid.");
            }
            slot.A.Validate();
            slot.B.Validate();
        }
        if (Ports.Any(port => port is null))
        {
            throw new InvalidDataException("Missing native controller port.");
        }
    }

    public NativeVibrationEnqueue Enqueue(NativeVibrationBank bank, int pattern, int priority)
    {
        Validate();
        int selected = 255;
        for (int index = 0; index < 4; index++)
        {
            int oldPriority = unchecked((sbyte)Slots[index].Priority);
            if (oldPriority == 0)
            {
                selected = index;
                break;
            }
            if (oldPriority <= priority && selected > oldPriority)
            {
                //Original EDI becomes the selected SLOT INDEX, not the prior priority.
                selected = index;
            }
        }
        if (selected == 255)
        {
            return new NativeVibrationEnqueue(this, 0);
        }
        if (ResourcePointerAndMask is uint mask && mask != uint.MaxValue)
        {
            throw new NotSupportedException("The vibration resource pointer has an address transform whose result is not proved to retain the authenticated bank binding.");
        }
        bank.Validate();
        if ((uint)pattern >= bank.Offsets.Length)
        {
            throw new NotSupportedException("Vibration pattern DWORD lies outside its original pointer table.");
        }
        int offset = bank.Offsets[pattern];
        if (offset == 0)
        {
            return new NativeVibrationEnqueue(this, -1);
        }
        NativeVibrationSlot old = Slots[selected];
        ushort aLength = bank.ReadWord(offset);
        ushort bLength = bank.ReadWord(offset + 2);
        //The native end pointer bounds cursor admission, not interpolation's
        //two-byte lookahead. Retain actual adjacent bytes, never pad them.
        NativeVibrationChannel a = aLength == 0 ? old.A with { Active = 0 } : new NativeVibrationChannel(bank.Bytes.Skip(offset + 4).Take(aLength + 2).ToImmutableArray(), 0, aLength, -1, 1);
        NativeVibrationChannel b = bLength == 0 ? old.B with { Active = 0 } : new NativeVibrationChannel(bank.Bytes.Skip(offset + 4 + aLength).Take(bLength + 2).ToImmutableArray(), 0, bLength, -1, 1);
        ushort generation = unchecked((ushort)(old.Generation + 1));
        if (generation > 1023)
        {
            generation = 1;
        }
        NativeVibrationSlot replacement = old with { A = a, B = b, Generation = generation, Priority = unchecked((byte)priority) };
        return new NativeVibrationEnqueue(this with { Slots = Slots.SetItem(selected, replacement) }, generation * 16 | old.Id);
    }

    public NativeVibrationProcessState AndResourcePointer(uint mask) => this with { ResourcePointerAndMask = (ResourcePointerAndMask ?? uint.MaxValue) & mask };

    public bool IsPlaying(int handle) => handle != 0 && Slots[handle & 3].Priority != 0 && Slots[handle & 3].Generation == handle >> 4;

    public NativeVibrationProcessState Stop4A2940(int handle)
    {
        Validate();
        if (handle == -1)
        {
            return StopAll();
        }
        if (handle == 0)
        {
            return this;
        }
        int index = handle & 3;
        NativeVibrationSlot slot = Slots[index];
        return slot.Priority != 0 && slot.Generation == handle >> 4 ? this with { Slots = Slots.SetItem(index, slot with { Priority = 0 }) } : this;
    }

    public NativeVibrationProcessState StopAll() => this with { Slots = Slots.Select(slot => slot with { Priority = 0 }).ToImmutableArray(), Ports = Ports.SetItem(0, new NativeVibrationPort(0, 0, true)) };
    public NativeVibrationProcessState ClearControllerRequests() => this with { Ports = ImmutableArray.Create(new NativeVibrationPort(0, 0, true), new NativeVibrationPort(0, 0, true)) };
    public NativeVibrationProcessState MarkControllerPending(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 1);
        return this with { Ports = Ports.SetItem(port, Ports[port] with { Pending = true }) };
    }
    public NativeVibrationProcessState EndFieldCallback() => StopAll().ClearControllerRequests();

    public NativeVibrationProcessState SampleCarStart(int entity, NativeVibrationBank bank)
    {
        Validate();
        if (entity is not (>= 32 and <= 40) and not 132)
        {
            return this;
        }
        NativeVibrationProcessState next = this;
        if (PreviousCarSpeed2045C84 == 0 && GraphicsSpeed20409F0 != 0 && (CarHandleC76788 < 0 || !IsPlaying(CarHandleC76788)))
        {
            NativeVibrationEnqueue result = Enqueue(bank, 1, 255);
            next = result.State with { CarHandleC76788 = result.Handle };
        }
        return next with { PreviousCarSpeed2045C84 = GraphicsSpeed20409F0 };
    }

    public NativeVibrationProcessState Advance(int ticks)
    {
        //One actual49A970 callback: menu1, field/world2, world559240
        //entity50 cadence3, movie4;0 represents a skipped UI callback.
        //Batched histories must execute the same callback repeatedly.
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ticks, 4);
        Validate();
        int remaining = checked(QuarterClockRemainder + ticks);
        NativeVibrationProcessState result = this;
        while (remaining >= 4)
        {
            result = result.StepQueue();
            remaining -= 4;
        }
        return result with { QuarterClockRemainder = remaining };
    }

    private NativeVibrationProcessState StepQueue()
    {
        ImmutableArray<NativeVibrationSlot>.Builder slots = Slots.ToBuilder();
        int aOutput = 0;
        int bOutput = 0;
        for (int index = 0; index < slots.Count; index++)
        {
            NativeVibrationSlot slot = slots[index];
            if (slot.Priority == 0)
            {
                continue;
            }
            (NativeVibrationChannel a, int aValue) = slot.A.Step();
            (NativeVibrationChannel b, int bValue) = slot.B.Step();
            slots[index] = slot with { A = a, B = b, Priority = aValue == -1 && bValue == -1 ? (byte)0 : slot.Priority };
            aOutput = Math.Max(aOutput, aValue);
            bOutput = Math.Max(bOutput, bValue);
        }
        return this with { Slots = slots.ToImmutable(), Ports = Ports.SetItem(0, new NativeVibrationPort((byte)aOutput, (byte)bOutput, true)) };
    }
}
