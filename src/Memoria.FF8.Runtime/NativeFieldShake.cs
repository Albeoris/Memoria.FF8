using System.Buffers.Binary;
using System.Security.Cryptography;

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

public sealed record NativeShakeAlternative(byte Cursor, short? Previous, short? Target, sbyte? Output);

public sealed record NativeShakeAxis(bool Enabled, bool Oscillating, short? Amplitude, short Duration, short Counter, NativeShakeAlternative[] Alternatives)
{
    public static NativeShakeAxis UnknownProcessHistory()
    {
        return new NativeShakeAxis(false, false, null, 0, 0, Enumerable.Range(0, 256).Select(index => new NativeShakeAlternative((byte)index, null, null, null)).ToArray());
    }

    public NativeShakeAxis ResetForLoad()
    {
        return this with { Enabled = false, Oscillating = false, Duration = 0, Counter = 0 };
    }

    public void Validate()
    {
        if (Duration < 0 || Duration == short.MaxValue || Counter < 0 || Alternatives is null || Alternatives.Length != 256 || Alternatives.Any(item => item is null) || Alternatives.Select(item => item.Cursor).Distinct().Count() != 256 || (Enabled && Amplitude is null) || (Oscillating || !Enabled && Duration > Counter) && Alternatives.Any(item => item.Previous is null || item.Target is null))
        {
            throw new InvalidDataException("Native shake axis lost its supported signed-word phase or complete unknown cursor domain.");
        }
    }

    public NativeShakeAxis Step(NativeShakeTables tables)
    {
        Validate();
        if (!Enabled && !Oscillating && Duration <= Counter)
        {
            return Alternatives.All(item => item.Output == 0) ? this : this with { Alternatives = Alternatives.Select(item => item with { Output = 0 }).ToArray() };
        }
        if (Enabled && !Oscillating)
        {
            // 4765C0's first active phase does not write output+3.
            return this with { Oscillating = true, Counter = 0, Alternatives = Alternatives.Select(item => item with { Cursor = unchecked((byte)(item.Cursor + 1)), Previous = 0, Target = (short)(unchecked((short)(Amplitude!.Value * tables.Random(item.Cursor))) / 256) }).ToArray() };
        }
        NativeShakeAlternative[] next = Alternatives;
        bool oscillating = Oscillating;
        short counter = Counter;
        if (Duration < Counter)
        {
            counter = 0;
            if (Enabled)
            {
                next = next.Select(item => item with { Cursor = unchecked((byte)(item.Cursor + 1)), Previous = item.Target, Target = (short)(unchecked((short)(Amplitude!.Value * tables.Random(item.Cursor))) / (item.Target >= 0 ? -256 : 256)) }).ToArray();
            }
            else
            {
                oscillating = false;
                next = next.Select(item => item with { Cursor = unchecked((byte)(item.Cursor + 1)), Previous = item.Target, Target = 0 }).ToArray();
            }
        }
        byte angle = Duration == 0 ? (byte)0 : unchecked((byte)((counter << 12) / Duration / 32 + 128));
        int weight = tables.Cosine(angle) + 4096;
        next = next.Select(item => item with { Output = unchecked((sbyte)(item.Previous!.Value + (item.Target!.Value - item.Previous.Value) * weight / 8192)) }).ToArray();
        return this with { Oscillating = oscillating, Counter = checked((short)(counter + 1)), Alternatives = next };
    }

    public sbyte RequireUniqueOutput()
    {
        Validate();
        sbyte? value = Alternatives[0].Output;
        if (value is null || Alternatives.Any(item => item.Output != value))
        {
            throw new NativeShakeRenderException("Native camera shake has multiple possible historical cursor outputs; no guessed pixel displacement is rendered.");
        }
        return value.Value;
    }
}

public sealed record NativeFieldShake(int Version, string ExecutableHash, NativeShakeAxis X, NativeShakeAxis Y)
{
    public static NativeFieldShake UnknownProcessHistory()
    {
        return new NativeFieldShake(1, NativeFieldRuntimeProfile.FF8EnExecutableHash, NativeShakeAxis.UnknownProcessHistory(), NativeShakeAxis.UnknownProcessHistory());
    }

    public void Validate()
    {
        if (Version != 1 || ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || X is null || Y is null)
        {
            throw new InvalidDataException("Native camera shake has no original runtime provenance.");
        }
        X.Validate();
        Y.Validate();
    }

    public NativeFieldShake ResetForLoad() => this with { X = X.ResetForLoad(), Y = Y.ResetForLoad() };
}
