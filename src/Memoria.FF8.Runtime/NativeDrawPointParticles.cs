using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeSparkPoint(short X, short Y, short Z)
{
    [JsonIgnore]
    public Vector3 Vector => new(X, Y, Z);
    public static NativeSparkPoint From(int x, int y, int z) => new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
}

public sealed class NativeDrawPointParticleTables
{
    private readonly byte[] random;
    private readonly short[] sine;
    private readonly short[] cosine;
    private readonly byte[] colors;

    public NativeDrawPointParticleTables(string executableHash, ReadOnlySpan<byte> random, ReadOnlySpan<byte> sine, ReadOnlySpan<byte> cosine, ReadOnlySpan<byte> colors)
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(executableHash) || random.Length != 256 || sine.Length != 512 || cosine.Length != 512 || colors.Length != 64)
        {
            throw new InvalidDataException("Draw-point particle tables require the authenticated original executable ranges.");
        }
        this.random = random.ToArray();
        this.sine = new short[256];
        this.cosine = new short[256];
        for (int index = 0; index < 256; index++)
        {
            this.sine[index] = BinaryPrimitives.ReadInt16LittleEndian(sine.Slice(index * 2, 2));
            this.cosine[index] = BinaryPrimitives.ReadInt16LittleEndian(cosine.Slice(index * 2, 2));
        }
        this.colors = colors.ToArray();
    }

    public byte Random(int phase) => random[unchecked((byte)phase)];
    public short Sine(int angle) => sine[unchecked((byte)angle)];
    public short Cosine(int angle) => cosine[unchecked((byte)angle)];
    public Vector3 Color(byte palette, int age)
    {
        if (palette > 3 || age is < 1 or > 5)
        {
            throw new InvalidDataException("Native spark color index is outside the four palettes and five trail colors.");
        }
        int offset = palette * 16 + (age - 1) * 3;
        return new Vector3(colors[offset], colors[offset + 1], colors[offset + 2]);
    }
}

public sealed record NativeDrawPointSpark([property: JsonRequired] byte Status, [property: JsonRequired] NativeSparkPoint Anchor, [property: JsonRequired] NativeSparkPoint Control, [property: JsonRequired] NativeSparkPoint Target, [property: JsonRequired] short Duration, [property: JsonRequired] short Counter, [property: JsonRequired] byte Angle, [property: JsonRequired] byte TargetAngle, [property: JsonRequired] ImmutableArray<NativeSparkPoint> FirstEdge, [property: JsonRequired] ImmutableArray<NativeSparkPoint> SecondEdge, [property: JsonRequired] byte RenderStatus, [property: JsonRequired] short RenderCounter);

/// <summary>Native1CE0750/1CDC620/1CE4C88 particle records, independently of the depletion bank and script RNG.</summary>
public sealed record NativeDrawPointParticles([property: JsonRequired] int Version, [property: JsonRequired] ImmutableArray<NativeDrawPointSpark> Sparks, [property: JsonRequired] byte Palette)
{
    [JsonRequired]
    public ImmutableArray<NativeDrawPointSpark> Drawings { get; init; } = [];
    [JsonIgnore]
    public bool AcquisitionActive => Sparks.Any(spark => spark.Status == 2);

    public static byte Phase(NativeExamTimerState timer)
    {
        timer.Validate();
        NativeTimerAlternative first = timer.Alternatives[0];
        byte phase = unchecked((byte)first.FirstPhase);
        if (timer.Alternatives.Any(item => item.FirstPhase != item.LastPhase || unchecked((byte)item.FirstPhase) != phase))
        {
            throw new NotSupportedException("Native spark phase requires the exact low byte shared by4701B0's two process fractions.");
        }
        return phase;
    }

    public void Validate()
    {
        if (Version != 1 || Palette > 3 || Sparks.IsDefault || Sparks.Length != 8 || Drawings.IsDefault || Drawings.Length > 8 || Sparks.Concat(Drawings).Any(spark => spark is null || spark.Status > 2 || spark.RenderStatus > 2 || spark.Duration is < 16 or > 38 || spark.Counter < 0 || spark.Counter > spark.Duration + 5 || spark.RenderCounter < 0 || spark.RenderCounter > spark.Duration + 4 || spark.FirstEdge.IsDefault || spark.FirstEdge.Length != 8 || spark.SecondEdge.IsDefault || spark.SecondEdge.Length != 8))
        {
            throw new InvalidDataException("Saved draw-point particles lack their eight bounded native records and history rings.");
        }
    }

    /// <summary>Creates an inactive pool; previous anchors are process memory, cold process anchors are zero.</summary>
    public static NativeDrawPointParticles Initialize(NativeDrawPointParticles? previous, byte phase, NativeDrawPointParticleTables tables)
    {
        previous?.Validate();
        return new NativeDrawPointParticles(1, Enumerable.Range(0, 8).Select(index => Spawn(index, previous?.Sparks[index].Anchor ?? default, phase, tables) with { Status = 0 }).ToImmutableArray(), previous?.Palette ?? 0);
    }

    public NativeDrawPointParticles ClearForFieldLoad()
    {
        Validate();
        return this with { Sparks = Sparks.Select(spark => spark with { Status = 0, RenderStatus = 0 }).ToImmutableArray(), Drawings = [] };
    }

    public NativeDrawPointParticles InitializeCurves(byte phase, NativeDrawPointParticleTables tables)
    {
        Validate();
        // 471F70 initializes curves after 52BC00 has executed the field init scripts.
        // 4745D0 retains the statuses and anchors written by SETDRAWPOINT.
        return this with { Sparks = Sparks.Select((spark, index) => Spawn(index, spark.Anchor, phase, tables) with { Status = spark.Status }).ToImmutableArray(), Drawings = [] };
    }

    public NativeDrawPointParticles ResetAnchors(int x, int y, int z)
    {
        Validate();
        NativeSparkPoint anchor = NativeSparkPoint.From(x, y, z);
        return this with { Sparks = Sparks.Select(spark => spark with { Status = 1, Anchor = anchor }).ToImmutableArray() };
    }

    public NativeDrawPointParticles Launch(int x, int y, int z, byte direction, byte phase, NativeDrawPointParticleTables tables)
    {
        Validate();
        return this with
        {
            Sparks = Sparks.Select((spark, index) => spark with
            {
                Status = 2,
                Control = NativeSparkPoint.From(spark.Anchor.X + 4 * tables.Random(phase + index) - 512, spark.Anchor.Y + 4 * tables.Random(phase + index + 8) - 512, spark.Anchor.Z + (index << 7) + 1000),
                Target = NativeSparkPoint.From(x, y, z + 180),
                Duration = (short)(24 + index * 2),
                Counter = 0,
                Angle = unchecked((byte)(direction + 64)),
                TargetAngle = unchecked((byte)(direction - 64)),
                FirstEdge = Enumerable.Repeat(spark.Anchor, 8).ToImmutableArray(),
                SecondEdge = Enumerable.Repeat(spark.Anchor, 8).ToImmutableArray()
            }).ToImmutableArray()
        };
    }

    public NativeDrawPointParticles Step(byte phase, NativeDrawPointParticleTables tables)
    {
        Validate();
        NativeDrawPointSpark[] next = Sparks.Select(spark => spark with { RenderStatus = 0 }).ToArray();
        ImmutableArray<NativeDrawPointSpark>.Builder drawings = ImmutableArray.CreateBuilder<NativeDrawPointSpark>();
        for (int index = 0; index < next.Length; index++)
        {
            if (next[index].Status == 0)
            {
                continue;
            }
            //474AC0 invokes474970 before EACH active record: inactive earlier
            //indices can respawn here but are not advanced until the next callback.
            for (int spawn = 0; spawn < next.Length; spawn++)
            {
                if (next[spawn].Status == 0)
                {
                    next[spawn] = Spawn(spawn, next[spawn].Anchor, phase, tables);
                }
            }
            NativeDrawPointSpark spark = next[index];
            int counter = spark.Counter;
            NativeSparkPoint center = counter <= spark.Duration ? Curve(spark.Anchor, spark.Control, spark.Target, counter, spark.Duration) : spark.Target;
            int wave = tables.Sine(counter * 8);
            int dx = ((tables.Sine(spark.Angle) >> 8) * wave) >> 12;
            int dy = ((tables.Cosine(spark.Angle) >> 8) * wave) >> 12;
            int slot = counter & 7;
            next[index] = spark with
            {
                FirstEdge = spark.FirstEdge.SetItem(slot, NativeSparkPoint.From(center.X + dx, center.Y - dy, center.Z)),
                SecondEdge = spark.SecondEdge.SetItem(slot, NativeSparkPoint.From(center.X - dx, center.Y + dy, center.Z)),
                RenderStatus = spark.Status,
                RenderCounter = spark.Counter,
                Counter = (short)(counter + 1),
                Status = counter + 1 > spark.Duration + 4 ? (byte)0 : spark.Status
            };
            drawings.Add(next[index]);
        }
        return this with { Sparks = next.ToImmutableArray(), Drawings = drawings.ToImmutable() };
    }

    private static NativeDrawPointSpark Spawn(int index, NativeSparkPoint anchor, byte phase, NativeDrawPointParticleTables tables)
    {
        byte angle = tables.Random(phase + index);
        NativeSparkPoint control = NativeSparkPoint.From(anchor.X + angle - 128, anchor.Y + tables.Random(phase + index + 8) - 128, anchor.Z + 64);
        NativeSparkPoint target = NativeSparkPoint.From(anchor.X, anchor.Y, anchor.Z + 128);
        ImmutableArray<NativeSparkPoint> history = Enumerable.Repeat(anchor, 8).ToImmutableArray();
        return new NativeDrawPointSpark(1, anchor, control, target, (short)(16 + index * 2), 0, angle, angle, history, history, 0, 0);
    }

    private static NativeSparkPoint Curve(NativeSparkPoint start, NativeSparkPoint control, NativeSparkPoint end, int counter, int duration)
    {
        int Axis(short a, short b, short c)
        {
            short first = unchecked((short)(a + counter * (b - a) / duration));
            short second = unchecked((short)(b + counter * (c - b) / duration));
            return unchecked((short)(first + counter * (second - first) / duration));
        }
        return NativeSparkPoint.From(Axis(start.X, control.X, end.X), Axis(start.Y, control.Y, end.Y), Axis(start.Z, control.Z, end.Z));
    }
}
