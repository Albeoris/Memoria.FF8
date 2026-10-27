using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8StripRange(short First, short Last);
public sealed record NativeBattleDoom8StripTables(ImmutableArray<short> Widths, ImmutableArray<short> Heights, ImmutableArray<NativeBattleDoom8StripRange> Ranges)
{
    public void Validate()
    {
        if (Widths.IsDefault || Widths.Length != 4 || Heights.IsDefault || Heights.Length != 18 || Ranges.IsDefault || Ranges.Length != 14 || Ranges[^1] is not { First: 127 })
        {
            throw new InvalidDataException("Doom strips require their complete width, height and range tables.");
        }
        foreach (NativeBattleDoom8StripRange range in Ranges.Take(Ranges.Length - 1))
        {
            if (range is null || range.First < 0 || range.Last < range.First || range.Last > 16)
            {
                throw new InvalidDataException("Doom strip ranges exceed the original seventeen rows.");
            }
        }
    }
}

public sealed record NativeBattleDoom8StripAnchor(byte Actor, uint Destination) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8StripLookBasis(NativeBattleVector Direction, NativeBattleVector Up) : NativeBattleDoom8Operation;

public static partial class NativeBattleDoom8Strips
{
    public const uint TargetAddress = 0x275F510;
    public const uint BaseVertices = 0x275E4A8;
    public const uint NearVertices = 0x275D010;
    public const uint FarVertices = 0x275CDF0;
    public const uint ColorsAddress = 0x275F3F8;

    public static NativeBattleDoom8Frame InitializeTarget8C8120(NativeBattleDoom8Frame frame, uint address, byte actor, NativeBattleDoom8StripTables tables, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> execute)
    {
        tables.Validate();
        RequireTask(frame, address);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (actor >= 7 || actor != task.ReadByte(0x2D))
        {
            throw new InvalidDataException("Doom strips require the actual selected target actor.");
        }
        frame = frame with { State = frame.State.Write(TargetAddress, new byte[88]) };
        NativeBattleDoom8State state = frame.State.Write(TargetAddress + 0x48, [96, 0, 32]).Write(TargetAddress + 0x44, [48, 0, 16]).Write(TargetAddress + 0x40, [0, 0, 0]);
        task.RequirePayloadRange(0x1C, 8);
        task.ValidateUnknownPayloadRanges();
        state = state.Write(TargetAddress + 0x30, task.ReadRawPayloadSpan().Slice(0x10, 8));
        if (task.ReadUnknownPayloadRanges() is { } unknown)
        {
            foreach (NativeBattleEffectUnknownRegion range in unknown)
            {
                int first = Math.Max(range.Offset, 0x1C);
                int last = Math.Min(range.Offset + range.Length, 0x24);
                if (first < last)
                {
                    state = state with { Globals = state.Globals.MarkUnknown(checked((int)(TargetAddress + 0x30 - NativeBattleDoom8State.BaseAddress) + first - 0x1C), last - first) };
                }
            }
        }
        frame = execute(frame with { State = state }, new NativeBattleDoom8StripAnchor(actor, TargetAddress + 0x38));
        state = frame.State;
        int x = Signed(state, TargetAddress + 0x38) - Signed(state, TargetAddress + 0x30);
        int y = Signed(state, TargetAddress + 0x3A) - Signed(state, TargetAddress + 0x32);
        int z = Signed(state, TargetAddress + 0x3C) - Signed(state, TargetAddress + 0x34);
        int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(x * x + y * y + z * z));
        state = state.WriteDword(TargetAddress + 0x4C, unchecked((uint)distance)).WriteDword(TargetAddress + 0x20, 4096).WriteDword(TargetAddress + 0x24, 4096).WriteDword(TargetAddress + 0x28, unchecked((uint)distance));
        NativeBattleVector direction = new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
        frame = execute(frame with { State = state }, new NativeBattleDoom8StripLookBasis(direction, new(0, 4096, 0)));
        NativeBattleActionMatrix basis = NativeBattleDrawStock15Particles.LookBasis8DDA50(direction);
        state = frame.State;
        for (int index = 0; index < 9; index++)
        {
            state = state.WriteWord(TargetAddress + (uint)(index * 2), unchecked((ushort)basis.Rotation[index]));
        }
        for (int axis = 0; axis < 3; axis++)
        {
            state = state.WriteDword(TargetAddress + 0x14 + (uint)(axis * 4), unchecked((uint)(int)Signed(state, TargetAddress + 0x30 + (uint)(axis * 2))));
        }
        for (int row = 0; row < 17; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                state = PutVector(state, BaseVertices + (uint)((row * 4 + column) * 8), new(tables.Widths[column], 0, unchecked((short)(row * 240))));
            }
        }
        frame = frame with { State = state };
        foreach ((uint destination, short angle, bool scaledHeight) in new (uint, short, bool)[] { (NearVertices, 0, false), (FarVertices, 1024, true) })
        {
            NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            var rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), identity, 'Z', angle);
            frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
            NativeBattleActionMatrix matrix = rotated.Matrix.ReadMatrix();
            state = frame.State;
            for (int row = 0; row < 17; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, Vector(state, BaseVertices + (uint)((row * 4 + column) * 8)));
                    int height = scaledHeight ? unchecked((int)state.ReadDword(TargetAddress + 0x28) * tables.Heights[row]) / 4096 : tables.Heights[row];
                    point = point with { Y = unchecked((short)(point.Y + height)) };
                    state = PutVector(state, destination + (uint)((row * 4 + column) * 8), point);
                }
            }
            frame = frame with { State = state };
        }
        return frame;
    }

    public static NativeBattleDoom8Frame Place8C8350(NativeBattleDoom8Frame frame, uint address, short selector, NativeBattleDoom8StripTables tables)
    {
        RequireTask(frame, address);
        tables.Validate();
        if ((uint)selector >= tables.Heights.Length)
        {
            throw new NotSupportedException("Doom placement requires its actual bounded height row.");
        }
        NativeBattleDoom8State state = frame.State;
        short height = unchecked((short)(unchecked((int)state.ReadDword(TargetAddress + 0x28) * tables.Heights[selector]) / 4096));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(ReadTargetMatrix(state).ReadMatrix(), unchecked((int)state.ReadDword(TargetAddress + 0x20)), unchecked((int)state.ReadDword(TargetAddress + 0x24)), unchecked((int)state.ReadDword(TargetAddress + 0x28)));
        NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, height, unchecked((short)(selector * 240))));
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        task = task.WriteWord(0x1C, unchecked((ushort)(point.X + Signed(state, TargetAddress + 0x30)))).WriteWord(0x1E, unchecked((ushort)(point.Y + Signed(state, TargetAddress + 0x32)))).WriteWord(0x20, unchecked((ushort)(point.Z + Signed(state, TargetAddress + 0x34))));
        return NativeBattleDoom8Controller.Write(frame, address, task);
    }

    public static NativeBattleDoom8Frame CopyTargetEndpoint(NativeBattleDoom8Frame frame, uint child)
    {
        (byte[] bytes, bool[] known) = frame.State.Globals.ReadSnapshot(checked((int)(TargetAddress + 0x38 - NativeBattleDoom8State.BaseAddress)), 8);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, child).WriteBytes(0x1C, bytes);
        for (int index = 0; index < 8; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(0x1C + index, 1);
            }
        }
        return NativeBattleDoom8Controller.Write(frame, child, task);
    }

    private static NativeBattlePackedMatrix32 ReadTargetMatrix(NativeBattleDoom8State state)
    {
        (byte[] bytes, bool[] known) = state.Globals.ReadSnapshot(checked((int)(TargetAddress - NativeBattleDoom8State.BaseAddress)), 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleDoom8State PutVector(NativeBattleDoom8State state, uint address, NativeBattleVector vector) => state.WriteWord(address, unchecked((ushort)vector.X)).WriteWord(address + 2, unchecked((ushort)vector.Y)).WriteWord(address + 4, unchecked((ushort)vector.Z));
    private static NativeBattleVector Vector(NativeBattleDoom8State state, uint address) => new(Signed(state, address), Signed(state, address + 2), Signed(state, address + 4));
    private static short Signed(NativeBattleDoom8State state, uint address) => unchecked((short)state.ReadWord(address));
    private static NativeBattleFileStaging RequireStaging(NativeBattleDoom8Frame frame) => frame.Staging ?? throw new NotSupportedException("Doom strips require their current physical temporary staging.");

    private static void RequireTask(NativeBattleDoom8Frame frame, uint address)
    {
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || NativeBattleDoom8Controller.Read(frame, address).Callback != 0x8C8050)
        {
            throw new InvalidDataException("Doom strip placement requires its actual ACT callback.");
        }
        frame.State.Validate();
    }
}
