using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public enum Ff7SnowboardObjectProducerKind
{
    None,
    Balloon,
    Obstacle,
    ImpactObstacle,
    BendingObstacle,
    StaticDecoration,
    TopplingObstacle,
    RetiringObstacle,
    BouncingObstacle,
    Unsupported
}

public sealed record Ff7SnowboardBounceInitialization(int X, int Y, int Z, int FloorY, int HorizontalVelocity, short ActiveTicks);
public sealed record Ff7SnowboardObjectProducer(Ff7SnowboardObjectProducerKind Kind, byte[] Template, int X, int Y, int Z, short Heading, short Variant)
{
    public IReadOnlyList<Ff7SnowboardObjectProducer> Additional { get; init; } = Array.Empty<Ff7SnowboardObjectProducer>();
    public bool ChecksPlayerClearance { get; init; }
    public short? TotalScoreThreshold { get; init; }
    public Ff7SnowboardBounceInitialization? Bounce { get; init; }
    public int PlaneTriangleBase { get; init; } = -1;
    public IReadOnlyList<short> PlaneThresholds { get; init; } = Array.Empty<short>();
}
public sealed record Ff7SnowboardObjectData(IReadOnlyList<Ff7SnowboardObjectProducer> Producers, IReadOnlyList<short> BalloonScores);

public sealed partial class Ff7PcExecutableData
{
    public static Ff7SnowboardObjectData LoadSnowboardObjectData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] table = ReadVirtual(stream, pe, 0x9661B0, 601 * 4);
        Ff7SnowboardObjectProducer[] producers = new Ff7SnowboardObjectProducer[604];
        for (int index = 0; index < producers.Length; index++)
        {
            if (index >= 601)
            {
                producers[index] = Empty(Ff7SnowboardObjectProducerKind.Unsupported);
                continue;
            }
            uint callback = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(index * 4));
            producers[index] = callback == 0 ? Empty(Ff7SnowboardObjectProducerKind.None) : Decode(callback, index);
        }
        byte[] scores = ReadVirtual(stream, pe, 0x964F70, 10);
        short[] values = new short[5];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = BinaryPrimitives.ReadInt16LittleEndian(scores.AsSpan(index * 2));
        }
        return new(Array.AsReadOnly(producers), Array.AsReadOnly(values));

        Ff7SnowboardObjectProducer Decode(uint address, int logicalDistance)
        {
            byte[] code = ReadVirtual(stream, pe, address, 512);
            if (address is 0x73FEEF or 0x73FF73)
            {
                byte[] rawThresholds = ReadVirtual(stream, pe, 0x965EA8, 24);
                short[] thresholds = new short[12];
                for (int index = 0; index < thresholds.Length; index++)
                {
                    thresholds[index] = BinaryPrimitives.ReadInt16LittleEndian(rawThresholds.AsSpan(index * 2));
                }
                return Empty(Ff7SnowboardObjectProducerKind.None) with { PlaneTriangleBase = address == 0x73FEEF ? 60 : 48, PlaneThresholds = Array.AsReadOnly(thresholds) };
            }
            if (address is 0x73E86D or 0x73F321 or 0x74002A)
            {
                uint position = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(24));
                short heading = unchecked((short)BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(31)));
                short variant = unchecked((sbyte)code[29]);
                short threshold = unchecked((sbyte)code[20]);
                return Read(Ff7SnowboardObjectProducerKind.Balloon, 0x964F28, position, heading, variant) with { TotalScoreThreshold = threshold };
            }
            (Ff7SnowboardObjectProducerKind relativeKind, int deltaX, int deltaY, int deltaZ, int relativeVariant) = address switch
            {
                0x73E0B0 => (Ff7SnowboardObjectProducerKind.StaticDecoration, -602, 2, -442, 0),
                0x73E128 => (Ff7SnowboardObjectProducerKind.StaticDecoration, 1729, 2, -2779, 0),
                0x73E1A0 => (Ff7SnowboardObjectProducerKind.Balloon, 15, -317, -712, 0),
                0x73E217 => (Ff7SnowboardObjectProducerKind.Balloon, 1463, -317, -2160, 0),
                0x73E291 => (Ff7SnowboardObjectProducerKind.Balloon, -161, -378, -887, 1),
                0x73E30B => (Ff7SnowboardObjectProducerKind.Balloon, 1287, -378, -2335, 1),
                0x73E385 => (Ff7SnowboardObjectProducerKind.Balloon, 208, -307, -1964, 1),
                0x73E431 => (Ff7SnowboardObjectProducerKind.Balloon, 208, -371, -1964, 2),
                _ => (Ff7SnowboardObjectProducerKind.Unsupported, 0, 0, 0, 0)
            };
            if (relativeKind != Ff7SnowboardObjectProducerKind.Unsupported)
            {
                uint template = relativeKind == Ff7SnowboardObjectProducerKind.StaticDecoration ? 0x9651D8u : 0x964F28u;
                Ff7SnowboardObjectProducer relative = Relative(relativeKind, template, logicalDistance, deltaX, deltaY, deltaZ, (short)relativeVariant);
                if (address is 0x73E385 or 0x73E431)
                {
                    relative = relative with { Additional = new[] { Relative(Ff7SnowboardObjectProducerKind.BendingObstacle, 0x964D30, logicalDistance, deltaX, 333, deltaZ, 1) } };
                }
                return relative;
            }
            if (address is 0x73E4E7 or 0x73E659 or 0x73E689 or 0x73E6B9 or 0x73E6E9 or 0x73E719 or 0x73E749)
            {
                byte[] offsets = ReadVirtual(stream, pe, 0x965468u + ((uint)logicalDistance & 3) * 24, 24);
                byte[] jitters = ReadVirtual(stream, pe, 0x9654C8, 8);
                List<Ff7SnowboardObjectProducer> group = [];
                if (address != 0x73E4E7)
                {
                    uint position = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(10));
                    group.Add(Read(Ff7SnowboardObjectProducerKind.Balloon, 0x964F28, position, 0x600, 0));
                }
                for (int index = 0; index < 4; index++)
                {
                    int jitter = BinaryPrimitives.ReadInt16LittleEndian(jitters.AsSpan(index * 2));
                    int x = BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 6)) + jitter;
                    int y = BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 6 + 2));
                    int z = BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 6 + 4)) + jitter;
                    short heading = (short)((index & 1) == 0 ? 0xD80 : 0xE80);
                    group.Add(Relative(Ff7SnowboardObjectProducerKind.TopplingObstacle, 0x964F80, logicalDistance, x, y, z, 1) with { Heading = heading });
                }
                return group[0] with { Additional = group.Skip(1).ToArray() };
            }
            if (address == 0x73DFA4)
            {
                uint position = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(13));
                short heading = unchecked((short)BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(20)));
                short variant = unchecked((sbyte)code[18]);
                return Read(Ff7SnowboardObjectProducerKind.Balloon, 0x964F28, position, heading, variant) with { ChecksPlayerClearance = true };
            }
            if (code.AsSpan(0, 5).SequenceEqual(new byte[] { 0x55, 0x8B, 0xEC, 0x5D, 0xC3 }))
            {
                return Empty(Ff7SnowboardObjectProducerKind.None);
            }
            int offset = 0;
            if (!Match(code, ref offset, [0x55, 0x8B, 0xEC, 0x83, 0x7D, 0x0C, 0]))
            {
                return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
            }
            int footer;
            if (code[offset] == 0x74)
            {
                footer = offset + 2 + unchecked((sbyte)code[offset + 1]);
                offset += 2;
            }
            else if (code[offset] == 0x0F && code[offset + 1] == 0x84)
            {
                footer = offset + 6 + BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset + 2));
                offset += 6;
            }
            else
            {
                return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
            }
            if (footer < offset || footer > code.Length - 2 || code[footer] != 0x5D || code[footer + 1] != 0xC3)
            {
                return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
            }
            List<Ff7SnowboardObjectProducer> result = [];
            while (offset < footer)
            {
                List<uint> arguments = [];
                while (Push(code, ref offset, out uint argument))
                {
                    arguments.Add(argument);
                }
                if (arguments.Count is < 1 or > 3 || offset > footer - 10)
                {
                    return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
                }
                byte register = code[offset + 2];
                if (code[offset] != 0x66 || code[offset + 1] != 0x8B || (register != 0x45 && register != 0x4D && register != 0x55) || code[offset + 3] != 8 || code[offset + 4] != 0x50 + ((register - 0x45) >> 3) || code[offset + 5] != 0xE8)
                {
                    return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
                }
                uint factory = unchecked(address + (uint)offset + 10 + (uint)BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset + 6)));
                offset += 10;
                if (!Match(code, ref offset, [0x83, 0xC4, (byte)((arguments.Count + 1) * 4)]))
                {
                    return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
                }
                (Ff7SnowboardObjectProducerKind kind, uint template) = factory switch
                {
                    0x73D6DB when arguments.Count == 3 => (Ff7SnowboardObjectProducerKind.Balloon, 0x964F28u),
                    0x73D764 when arguments.Count == 3 => (Ff7SnowboardObjectProducerKind.Obstacle, 0x964D78u),
                    0x73D860 when arguments.Count == 3 => (Ff7SnowboardObjectProducerKind.ImpactObstacle, 0x964CE8u),
                    0x73DC12 when arguments.Count == 3 => (Ff7SnowboardObjectProducerKind.BendingObstacle, 0x964D30u),
                    0x73DF4D when arguments.Count == 3 && arguments[1] == 0 => (Ff7SnowboardObjectProducerKind.StaticDecoration, 0x9651D8u),
                    0x73FA5E when arguments.Count == 2 => (Ff7SnowboardObjectProducerKind.RetiringObstacle, 0x964E98u),
                    0x73FB1F when arguments.Count == 3 => (Ff7SnowboardObjectProducerKind.BouncingObstacle, 0x9650F8u),
                    _ => (Ff7SnowboardObjectProducerKind.Unsupported, 0u)
                };
                if (kind == Ff7SnowboardObjectProducerKind.Unsupported)
                {
                    return Empty(kind);
                }
                uint position = arguments[0];
                short variant = unchecked((short)arguments[1]);
                short heading = arguments.Count == 3 ? unchecked((short)arguments[2]) : (short)0xE00;
                Ff7SnowboardObjectProducer producer = Read(kind, template, position, heading, variant);
                if (kind == Ff7SnowboardObjectProducerKind.BouncingObstacle)
                {
                    if ((uint)variant >= 4)
                    {
                        return Empty(Ff7SnowboardObjectProducerKind.Unsupported);
                    }
                    int displacement = (variant & 1) == 0 ? 192 << 12 : -192 << 12;
                    byte[] velocity = ReadVirtual(stream, pe, 0x965140u + (uint)variant * 2, 2);
                    int horizontal = BinaryPrimitives.ReadInt16LittleEndian(velocity) << 12;
                    Ff7SnowboardBounceInitialization bounce = new(unchecked(producer.X + displacement), unchecked(producer.Y - 0x400000), unchecked(producer.Z + displacement), unchecked(producer.Y - 0x80000), horizontal, (short)((variant & 2) == 0 ? 144 : 104));
                    producer = producer with { X = 0, Y = 0, Z = 0, Bounce = bounce };
                }
                result.Add(producer);
            }
            return offset == footer && result.Count != 0 ? result[0] with { Additional = result.Skip(1).ToArray() } : Empty(Ff7SnowboardObjectProducerKind.Unsupported);
        }

        Ff7SnowboardObjectProducer Read(Ff7SnowboardObjectProducerKind kind, uint templateAddress, uint position, short heading, short variant)
        {
            byte[] xyz = ReadVirtual(stream, pe, position, 12);
            byte[] template = ReadVirtual(stream, pe, templateAddress, 72);
            if (kind == Ff7SnowboardObjectProducerKind.StaticDecoration)
            {
                byte[] model = ReadVirtual(stream, pe, 0x965220, 2);
                model.CopyTo(template, 0);
            }
            return new(kind, template, BinaryPrimitives.ReadInt32LittleEndian(xyz), BinaryPrimitives.ReadInt32LittleEndian(xyz.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(xyz.AsSpan(8)), heading, variant);
        }

        Ff7SnowboardObjectProducer Relative(Ff7SnowboardObjectProducerKind kind, uint templateAddress, int logicalDistance, int deltaX, int deltaY, int deltaZ, short variant)
        {
            Ff7SnowboardObjectProducer source = Read(kind, templateAddress, 0x937738u + (uint)logicalDistance * 12, 0xA00, variant);
            return source with { X = unchecked((source.X + deltaX) << 12), Y = unchecked((source.Y + deltaY) << 12), Z = unchecked((source.Z + deltaZ) << 12) };
        }
    }

    private static Ff7SnowboardObjectProducer Empty(Ff7SnowboardObjectProducerKind kind) => new(kind, [], 0, 0, 0, 0, 0);

    private static bool Match(byte[] code, ref int offset, ReadOnlySpan<byte> expected)
    {
        if (offset < 0 || offset > code.Length - expected.Length || !code.AsSpan(offset, expected.Length).SequenceEqual(expected))
        {
            return false;
        }
        offset += expected.Length;
        return true;
    }

    private static bool Push(byte[] code, ref int offset, out uint value)
    {
        value = 0;
        if ((uint)offset >= code.Length)
        {
            return false;
        }
        if (code[offset] == 0x6A && offset <= code.Length - 2)
        {
            value = unchecked((uint)(sbyte)code[offset + 1]);
            offset += 2;
            return true;
        }
        if (code[offset] == 0x68 && offset <= code.Length - 5)
        {
            value = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(offset + 1));
            offset += 5;
            return true;
        }
        return false;
    }
}
