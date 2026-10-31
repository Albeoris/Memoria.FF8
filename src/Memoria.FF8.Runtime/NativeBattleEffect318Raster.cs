using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect318RasterStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary180, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect318Raster
{
    public static NativeBattleEffect318RasterStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleModuleResource source, NativeBattleCameraProjection projection) => DrawLoaded(memory, temporary, source, projection, null);

    public static NativeBattleEffect318RasterStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleModuleResource source, NativeBattleCameraProjection projection, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        int count = unchecked((int)U32(temporary, 0x30));
        if (count <= 0)
        {
            return new(memory, temporary, new([], 0));
        }
        uint address = U32(temporary, 0x2C);
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Projected quads require their current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            int offset = checked((int)(address - source.Address) + index * 20);
            byte[] quad = source.Bytes.AsSpan().Slice(offset, 20).ToArray();
            short width = (short)(quad[0] << 3);
            short height = (short)(quad[2] << 3);
            NativeBattleActionMatrix local = new([I16(quad, 16), 0, 0, 0, I16(quad, 18), 0, 0, 0, 4096], 0, 0, 0, 4096);
            for (int item = 0; item < 9; item++)
            {
                temporary = Word(temporary, 0x64 + item * 2, local.Rotation[item]);
            }
            temporary = Put(Put(Put(temporary, 0x78, unchecked((uint)(width + 16 * I16(quad, 8)))), 0x7C, unchecked((uint)(height + 16 * I16(quad, 10)))), 0x80, 0);
            NativeBattleActionMatrix main = Matrix(temporary, 0x84);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, true, false, Padding(temporary, 0x96)) };
            for (int column = 0; column < 3; column++)
            {
                registers = NativeBattleGeometry.LoadIrColumn(registers, U16(temporary, 0x64 + column * 2), U16(temporary, 0x6A + column * 2), U16(temporary, 0x70 + column * 2));
                registers = NativeBattleGeometry.Transform(registers, false, true);
                for (int row = 0; row < 3; row++)
                {
                    temporary = Word(temporary, 0x64 + row * 6 + column * 2, unchecked((short)registers.Colors.Ir[row]!.Value));
                }
            }
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, false, true, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)U32(temporary, 0x78)), unchecked((short)U32(temporary, 0x7C)), 0), 0);
            registers = NativeBattleGeometry.Transform(registers);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Put(temporary, 0x78 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            NativeBattleActionMatrix matrix = Matrix(temporary, 0x64);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, Padding(temporary, 0x76)) };
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), I16(temporary, 0x48)), new(width, unchecked((short)-height), I16(temporary, 0x50)), new(unchecked((short)-width), height, I16(temporary, 0x58)), new(width, height, 0)];
            for (int corner = 0; corner < 4; corner++)
            {
                temporary = Word(Word(temporary, 0x44 + corner * 8, corners[corner].X), 0x46 + corner * 8, corners[corner].Y);
            }
            for (int corner = 0; corner < 3; corner++)
            {
                registers = NativeBattleGeometry.LoadVector(registers, corner, corners[corner], Padding(temporary, 0x4A + corner * 8));
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags!.Value;
            temporary = Put(temporary, 0x40, flags);
            uint[] screens = [registers.Geometry.Sxy[0]!.Value, registers.Geometry.Sxy[1]!.Value, registers.Geometry.Sxy[2]!.Value, 0];
            int[] depths = [unchecked((ushort)registers.Geometry.Sz[1]!.Value), unchecked((ushort)registers.Geometry.Sz[2]!.Value), unchecked((ushort)registers.Geometry.Sz[3]!.Value), 0];
            byte[] packet = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            Store32(0, 0x09000000);
            Store32(4, (uint)quad[3] << 24 | (uint)quad[1] * 0x010101);
            Store32(12, BinaryPrimitives.ReadUInt32LittleEndian(quad.AsSpan(4)));
            Store8(28, quad[4]);
            byte right = unchecked((byte)Math.Min(quad[4] + quad[0], 255));
            Store8(20, right);
            Store8(21, quad[5]);
            Store8(36, right);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(Attempt(null));
                continue;
            }
            Store16(22, (ushort)(I16(quad, 14) & 0x1FF));
            for (int corner = 0; corner < 3; corner++)
            {
                Store32(8 + corner * 8, screens[corner]);
            }
            corners[3] = corners[3] with { Z = I16(temporary, 0x60) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, corners[3], Padding(temporary, 0x62));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            screens[3] = registers.Geometry.Sxy[2]!.Value;
            depths[3] = unchecked((ushort)registers.Geometry.Sz[3]!.Value);
            byte bottom = unchecked((byte)Math.Min(quad[5] + quad[2], 255));
            Store8(29, bottom);
            Store8(37, bottom);
            Store32(32, screens[3]);
            uint opcode = quad[3] & 0xFCu;
            ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? depths.ToImmutableArray() : [depths[1], depths[2], depths[3], 0];
            NativeBattleGeometryPrimitive primitive = new(19, Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray(), payload);
            if (pilotIdentity is { } identity)
            {
                System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, corners[corner]), projection);
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + offset) }, World(0), World(1), World(2), World(3), 4) { SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
            }
            attempts.Add(Attempt(primitive));
            allocated = checked(allocated + 40);
            NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? linked) => new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), linked);
            void Store8(int at, byte value)
            {
                packet[at] = value;
                mask[at] = NativeBattleGfPacketByteState.Known;
            }
            void Store16(int at, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(at), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, 2);
            }
            void Store32(int at, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(at), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, 4);
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? throw new NotSupportedException("Projected quads require current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Projected quads require current arithmetic registers."));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short I16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static short I16(byte[] value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.AsSpan(offset));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena value, int offset, short number) => value.Write(offset, BitConverter.GetBytes(number));
    private static short? Padding(NativeBattleEffectArena value, int offset)
    {
        (byte[] bytes, bool[] known) = value.ReadSnapshot(offset, 2);
        return known.All(item => item) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static NativeBattleActionMatrix Matrix(NativeBattleEffectArena value, int offset) => new(Enumerable.Range(0, 9).Select(index => I16(value, offset + index * 2)).ToImmutableArray(), unchecked((int)U32(value, offset + 20)), unchecked((int)U32(value, offset + 24)), unchecked((int)U32(value, offset + 28)), 4096);
}
