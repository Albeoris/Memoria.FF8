using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect314ProjectionServices<TFrame>(Func<TFrame, uint, int, NativeBattleEffectArena> ReadArena, Func<TFrame, uint, NativeBattleEffectArena, int, TFrame> WriteArena, Func<TFrame, NativeBattleEffectMemory> ReadMemory, Func<TFrame, NativeBattleEffectMemory, TFrame> WriteMemory, Func<TFrame, NativeBattlePackedMatrix32> ReadView, Func<TFrame, NativeBattleCameraProjection> ReadProjection);

public static class NativeBattleEffect314Projection663760
{
    public static TFrame ProjectGroup<TFrame>(TFrame frame, NativeBattleEffect314ProjectionServices<TFrame> services, uint address, bool expanded)
    {
        NativeBattleGeometryRegisterStep Registers(TFrame current)
        {
            NativeBattleEffectMemory memory = services.ReadMemory(current);
            return new(memory.GeometryRegisters ?? throw new NotSupportedException("Body projection requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Body projection requires its current arithmetic registers."));
        }
        TFrame Publish(TFrame current, NativeBattleGeometryRegisterStep registers)
        {
            NativeBattleEffectMemory memory = services.ReadMemory(current);
            return services.WriteMemory(current, memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors });
        }
        TFrame LoadMatrix(TFrame current, NativeBattlePackedMatrix32 matrix)
        {
            matrix.Validate();
            NativeBattleGeometryRegisterStep registers = Registers(current);
            return Publish(current, registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan(18, 2)) : null) });
        }
        int length = expanded ? 0x90 : 0x64;
        int cursorOffset = expanded ? 0x74 : 0x54;
        NativeBattleEffectArena context = services.ReadArena(frame, address, length);
        uint raw = Dword(context, cursorOffset);
        uint output = Dword(context, cursorOffset + 4);
        int count = Word(services.ReadArena(frame, raw, 2), 0);
        if (count < 0)
        {
            throw new NotSupportedException("The current geometry requires a finite nonnegative vertex count.");
        }
        raw += 2;
        int stride = expanded ? 16 : 8;
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int index = 0; index < count; index++)
        {
            NativeBattleVector point = Vector(services.ReadArena(frame, raw, 6));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, 0));
            uint destination = output + unchecked((uint)(index * stride + (expanded ? 8 : 0)));
            NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 6);
            for (int axis = 0; axis < 3; axis++)
            {
                xyz = PutWord(xyz, axis * 2, registers.Colors.Ir[axis] ?? throw new NotSupportedException("The vertex requires its current transformed IR value."));
            }
            frame = services.WriteArena(Publish(frame, registers), destination, xyz, 6);
            raw += 6;
        }
        frame = LoadMatrix(Publish(frame, registers), services.ReadView(frame));
        registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = services.ReadMemory(frame).ScriptedGraphics ?? throw new NotSupportedException("The body requires its current projection controls.");
        NativeBattleCameraProjection projection = services.ReadProjection(frame);
        for (int index = 0; index < count; index++)
        {
            uint rowAddress = output + unchecked((uint)(index * stride));
            NativeBattleEffectArena row = services.ReadArena(frame, rowAddress, stride);
            int pointOffset = expanded ? 8 : 0;
            NativeBattleVector point = Vector(row, pointOffset);
            registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, point, Padding(row, pointOffset + 6)), projection, controls.Dqa, controls.Dqb);
            context = PutDword(context, 0x1C, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("The vertex requires its projected screen coordinates."));
            uint depth = unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("The vertex requires its projected depth."))) >> 2));
            context = PutDword(context, 0x20, depth);
            if (unchecked((short)depth) > 0)
            {
                byte gray = Gray(point.Y, Word(context, expanded ? 0x6C : 0x4C));
                row = PutDword(row, 0, Dword(context, 0x1C));
                if (expanded)
                {
                    row = PutWord(row, 4, unchecked((int)depth)).Write(6, [gray]);
                }
                else
                {
                    context = context.Write(0x22, [gray]);
                    row = Copy(context, 0x20, row, 4, 4);
                }
            }
            else
            {
                row = PutWord(row, 4, 0);
            }
            frame = services.WriteArena(Publish(frame, registers), rowAddress, row, stride);
        }
        context = PutDword(PutDword(context, cursorOffset, raw), cursorOffset + 4, output + unchecked((uint)(count * stride)));
        return services.WriteArena(Publish(frame, registers), address, context, length);
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset = 0) => new(Word(arena, offset), Word(arena, offset + 2), Word(arena, offset + 4));
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int from, NativeBattleEffectArena target, int to, int length) => NativeBattleEffectNodeContextMemory.Copy(source, from, target, to, length);
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    public static byte Gray(int coordinate, int threshold)
    {
        short value = unchecked((short)(((coordinate - threshold) >> 2) + 64));
        return value < 0 ? (byte)0 : value > 128 ? (byte)128 : unchecked((byte)value);
    }
}
