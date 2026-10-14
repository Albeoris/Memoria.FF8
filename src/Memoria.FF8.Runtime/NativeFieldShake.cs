using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed class NativeShakeRenderException(string message) : NotSupportedException(message);

public sealed class NativeShakeTables
{
    private readonly byte[] random;
    private readonly short[] cosine;

    public NativeShakeTables(ReadOnlySpan<byte> randomBytes, ReadOnlySpan<byte> cosineBytes)
    {
        if (randomBytes.Length != 256 || cosineBytes.Length != 512 || Convert.ToHexString(SHA256.HashData(randomBytes)) != "FA4AB9025D0922FDAFA6993E1559F96CEB82C5C7DC59C53CAFFD501B56A13B00" || Convert.ToHexString(SHA256.HashData(cosineBytes)) != "88E990EA8EC604DE0BD7F88A5EE98C50E5908B080B53E0403F0AFE42BAC5324C")
        {
            throw new InvalidDataException("Native shake lookup tables do not match the preserved original executable.");
        }
        random = randomBytes.ToArray();
        cosine = new short[256];
        for (int index = 0; index < cosine.Length; index++)
        {
            cosine[index] = BinaryPrimitives.ReadInt16LittleEndian(cosineBytes.Slice(index * 2, 2));
        }
    }

    public byte Random(byte cursor) => random[cursor];
    public short Cosine(byte angle) => cosine[angle];
}

/// <summary>The fourteen physical bytes at1CE4882 or1CE4890, retained by the process across fields.</summary>
public sealed record NativeShakeAxis([property: JsonRequired] bool Enabled, [property: JsonRequired] bool Oscillating, [property: JsonRequired] byte Cursor, [property: JsonRequired] sbyte Output, [property: JsonRequired] short Amplitude, [property: JsonRequired] short Previous, [property: JsonRequired] short Target, [property: JsonRequired] short Duration, [property: JsonRequired] short Counter)
{
    public static NativeShakeAxis FromColdProcess()
    {
        // Both complete blocks lie in the authenticated PE.data zero-filled virtual tail.
        return new NativeShakeAxis(false, false, 0, 0, 0, 0, 0, 0, 0);
    }

    public NativeShakeAxis ResetForLoad()
    {
        // Native4765A0 writes only bytes0/1 and words10/12.
        return this with { Enabled = false, Oscillating = false, Duration = 0, Counter = 0 };
    }

    public NativeShakeAxis Step(NativeShakeTables? tables)
    {
        if (!Enabled && !Oscillating && Duration <= Counter)
        {
            return Output == 0 ? this : this with { Output = 0 };
        }
        if (tables is null)
        {
            throw new NotSupportedException("Native active shake requires authenticated random and cosine tables.");
        }
        if (Enabled && !Oscillating)
        {
            // Native4765C0's first active phase preserves byte3 output.
            short target = unchecked((short)(Amplitude * tables.Random(Cursor)));
            return this with { Oscillating = true, Counter = 0, Cursor = unchecked((byte)(Cursor + 1)), Previous = 0, Target = (short)(target / 256) };
        }
        NativeShakeAxis phase = this;
        if (Duration < Counter && Oscillating)
        {
            short target = Enabled ? (short)(unchecked((short)(Amplitude * tables.Random(Cursor))) / (Target >= 0 ? -256 : 256)) : (short)0;
            phase = phase with { Counter = 0, Previous = Target, Target = target, Cursor = unchecked((byte)(Cursor + 1)), Oscillating = Enabled };
        }
        byte angle = phase.Duration == 0 ? (byte)0 : unchecked((byte)((phase.Counter << 12) / phase.Duration / 32 + 128));
        int weight = tables.Cosine(angle) + 4096;
        sbyte output = unchecked((sbyte)(phase.Previous + (phase.Target - phase.Previous) * weight / 8192));
        return phase with { Output = output, Counter = unchecked((short)(phase.Counter + 1)) };
    }
}

public sealed record NativeFieldShake([property: JsonRequired] int Version, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] NativeShakeAxis X, [property: JsonRequired] NativeShakeAxis Y)
{
    public static NativeFieldShake FromColdProcess()
    {
        return new NativeFieldShake(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, NativeShakeAxis.FromColdProcess(), NativeShakeAxis.FromColdProcess());
    }

    [JsonIgnore]
    public bool IsColdProcess => this == FromColdProcess();

    public void Validate()
    {
        if (Version != 1 || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || X is null || Y is null)
        {
            throw new InvalidDataException("Native camera shake has no original current process provenance.");
        }
    }

    public NativeFieldShake ResetForLoad()
    {
        Validate();
        return this with { X = X.ResetForLoad(), Y = Y.ResetForLoad() };
    }

    public NativeFieldShake Step(NativeShakeTables? tables)
    {
        Validate();
        return this with { X = X.Step(tables), Y = Y.Step(tables) };
    }
}
