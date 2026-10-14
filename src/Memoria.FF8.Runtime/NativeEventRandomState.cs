using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native52D210 process state plus the separately initialized saved draw-point bank.</summary>
public sealed record NativeEventRandomState([property: JsonRequired] uint State, [property: JsonRequired] byte FieldCallbackFlagsCF10, [property: JsonRequired] NativeDrawPointMemory? DrawPoints)
{
    public static NativeEventRandomState FromColdProcess() => new(1, 0, null);

    [JsonIgnore]
    public bool IsColdProcess => State == 1 && FieldCallbackFlagsCF10 == 0;

    public void Validate()
    {
        DrawPoints?.Validate();
    }

    public NativeEventRandomState ClearSavedStateForNewGame()
    {
        Validate();
        // Executed52CA90(1)->530710(eventBank,248) clears this saved bank, not B8EEA0.
        return this with { DrawPoints = NativeDrawPointMemory.FromExecutedNewGame() };
    }

    public NativeEventRandomState Draw(out int result)
    {
        Validate();
        uint next = unchecked(State * 1103515245u + 12345u);
        result = (int)((next >> 16) & 0x7FFF);
        return this with { State = next };
    }

    public NativeEventRandomState AdvanceMovement(uint units)
    {
        Validate();
        NativeDrawPointMemory bank = DrawPoints ?? throw new NotSupportedException("Native draw-point aging requires the executed saved-bank initialization.");
        ushort counter = unchecked((ushort)(bank.RegenerationCounter180 + units));
        if (counter < 0x2800)
        {
            return this with { DrawPoints = bank with { RegenerationCounter180 = counter } };
        }
        byte[] packed = bank.Depletion.ToArray();
        NativeEventRandomState random = this;
        for (int index = 0; index < 256; index++)
        {
            int shift = 2 * (index & 3);
            int value = (packed[index / 4] >> shift) & 3;
            if (value is 1 or 2)
            {
                random = random.Draw(out int draw);
                if ((draw & 1) != 0)
                {
                    packed[index / 4] = (byte)((packed[index / 4] & ~(3 << shift)) | ((value - 1) << shift));
                }
            }
        }
        return random with { DrawPoints = new NativeDrawPointMemory(packed.ToImmutableArray(), 0) };
    }
}

/// <summary>Actual event-bank bytes116..179 and WORD180, with no dictionary mirror.</summary>
public sealed record NativeDrawPointMemory([property: JsonRequired] ImmutableArray<byte> Depletion, [property: JsonRequired] ushort RegenerationCounter180)
{
    public static NativeDrawPointMemory FromExecutedNewGame() => new(ImmutableArray.CreateRange(new byte[64]), 0);

    public bool Equals(NativeDrawPointMemory? other)
    {
        return other is not null && RegenerationCounter180 == other.RegenerationCounter180 && Depletion.AsSpan().SequenceEqual(other.Depletion.AsSpan());
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(RegenerationCounter180);
        foreach (byte value in Depletion)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    public void Validate()
    {
        if (Depletion.IsDefault || Depletion.Length != 64)
        {
            throw new InvalidDataException("Native draw-point depletion requires its exact64-byte packed bank.");
        }
    }

    public int Read(int address, int width)
    {
        ValidateAccess(address, width);
        uint result = 0;
        for (int index = 0; index < width; index++)
        {
            int offset = address + index - 116;
            byte value = offset < 64 ? Depletion[offset] : (byte)(RegenerationCounter180 >> (8 * (offset - 64)));
            result |= (uint)value << (8 * index);
        }
        return unchecked((int)result);
    }

    public NativeDrawPointMemory Write(int address, int width, int value)
    {
        ValidateAccess(address, width);
        byte[] packed = Depletion.ToArray();
        ushort counter = RegenerationCounter180;
        for (int index = 0; index < width; index++)
        {
            int offset = address + index - 116;
            byte next = (byte)(unchecked((uint)value) >> (8 * index));
            if (offset < 64)
            {
                packed[offset] = next;
            }
            else
            {
                int shift = 8 * (offset - 64);
                counter = (ushort)((counter & ~(255 << shift)) | (next << shift));
            }
        }
        return new NativeDrawPointMemory(packed.ToImmutableArray(), counter);
    }

    private void ValidateAccess(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < 116 || (long)address + width > 182)
        {
            throw new NotSupportedException("Native draw-point memory cannot consume unknown surrounding bytes.");
        }
    }
}
