using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleTemplateSprite683490
{
    public static NativeBattleModuleSpriteDraw Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleModuleResource resource, NativeBattleCameraProjection projection, int tableBaseBucket = 17)
    {
        int count = unchecked((int)Dword(temporary, 0x30));
        uint source = Dword(temporary, 0x2C);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            long offset = (long)source - resource.Address + index * 20L;
            if (resource.Bytes.IsDefault || offset < 0 || offset > resource.Bytes.Length - 20)
            {
                throw new InvalidDataException("Native683490 requires its actual bounded source quads.");
            }
            byte[] quad = resource.Bytes.AsSpan().Slice((int)offset, 20).ToArray();
            foreach (int zero in new[] { 0x66, 0x68, 0x6A, 0x6E, 0x70, 0x72 })
            {
                temporary = Word(temporary, zero, 0);
            }
            temporary = Word(Word(Word(temporary, 0x74, 4096), 0x64, BinaryPrimitives.ReadUInt16LittleEndian(quad.AsSpan(16))), 0x6C, BinaryPrimitives.ReadUInt16LittleEndian(quad.AsSpan(18)));
            short width = (short)(quad[0] << 3);
            short height = (short)(quad[2] << 3);
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            for (int corner = 0; corner < 4; corner++)
            {
                temporary = Word(Word(temporary, 0x44 + corner * 8, unchecked((ushort)corners[corner].X)), 0x46 + corner * 8, unchecked((ushort)corners[corner].Y));
            }
            temporary = Put(Put(Put(temporary, 0x78, unchecked((uint)(width + (BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(8)) << 4)))), 0x7C, unchecked((uint)(height + (BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(10)) << 4)))), 0x80, 0);
            NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native683490 requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native683490 requires its current color registers."));
            NativeBattleActionMatrix main = Matrix(temporary, 0x84);
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, true, false, Padding(temporary, 0x96)) };
            SkinWrites packet = new(40, allocated);
            for (int column = 0; column < 3; column++)
            {
                step = NativeBattleGeometry.LoadIrColumn(step, ReadWord(temporary, 0x64 + column * 2), ReadWord(temporary, 0x6A + column * 2), ReadWord(temporary, 0x70 + column * 2));
                step = NativeBattleGeometry.Transform(step, false, true);
                if (column == 0)
                {
                    packet.Word(0, 0x09000000);
                    packet.Word(4, BinaryPrimitives.ReadUInt32LittleEndian(quad));
                }
                else if (column == 1)
                {
                    packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(quad.AsSpan(4)));
                }
                else
                {
                    packet.Byte(28, quad[4]);
                }
                for (int row = 0; row < 3; row++)
                {
                    temporary = Word(temporary, 0x64 + row * 6 + column * 2, unchecked((ushort)step.Colors.Ir[row]!.Value));
                }
            }
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, false, true, null) };
            step = NativeBattleGeometry.LoadVector(step, 0, new(unchecked((short)Dword(temporary, 0x78)), unchecked((short)Dword(temporary, 0x7C)), 0), 0);
            step = NativeBattleGeometry.Transform(step);
            packet.Byte(21, quad[5]);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Put(temporary, 0x78 + axis * 4, unchecked((uint)step.Colors.Mac[axis]!.Value));
            }
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, Matrix(temporary, 0x64), true, true, Padding(temporary, 0x76)) };
            for (int corner = 0; corner < 3; corner++)
            {
                step = NativeBattleGeometry.LoadVector(step, corner, Vector(temporary, 0x44 + corner * 8), Padding(temporary, 0x4A + corner * 8));
            }
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native683490 requires its projection controls.");
            step = NativeBattleGeometry.ProjectTriple(step, projection, controls.Dqa, controls.Dqb);
            byte right = (byte)Math.Min(quad[4] + quad[0], 255);
            packet.Byte(36, right);
            packet.Byte(20, right);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad.AsSpan(14));
            int palette = unchecked((sbyte)temporary.Read(0x20 + (flags >> 12), 1)[0]);
            packet.Word16(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad.AsSpan(6)) + (palette << 6))));
            uint projectedFlags = step.Colors.Flags ?? throw new InvalidDataException("Native683490 projection lost FLAG.");
            temporary = Put(temporary, 0x40, projectedFlags);
            if ((projectedFlags & 0x60000) != 0)
            {
                attempts.Add(packet.Attempt(null));
            }
            else
            {
                packet.Word16(22, (ushort)(flags & 0x1FF));
                uint[] screens = new uint[4];
                ushort[] depths = new ushort[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    screens[corner] = step.Geometry.Sxy[corner]!.Value;
                    depths[corner] = unchecked((ushort)step.Geometry.Sz[corner + 1]!.Value);
                    packet.Word(8 + corner * 8, screens[corner]);
                }
                step = NativeBattleGeometry.LoadVector(step, 0, Vector(temporary, 0x5C), Padding(temporary, 0x62));
                step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
                byte bottom = (byte)Math.Min(quad[5] + quad[2], 255);
                packet.Byte(37, bottom);
                packet.Byte(29, bottom);
                screens[3] = step.Geometry.Sxy[2]!.Value;
                depths[3] = unchecked((ushort)step.Geometry.Sz[3]!.Value);
                packet.Word(32, screens[3]);
                uint opcode = quad[3] & 0xFCu;
                ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? [depths[0], depths[1], depths[2], depths[3]] : [depths[1], depths[2], depths[3], 0];
                attempts.Add(packet.Attempt(new(checked(tableBaseBucket + count - index), packet.Words(), payload)));
                allocated = checked(allocated + 40);
            }
            memory = memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
        }
        return new(memory, temporary, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleActionMatrix Matrix(NativeBattleEffectArena arena, int offset) => new(Enumerable.Range(0, 9).Select(index => unchecked((short)ReadWord(arena, offset + index * 2))).ToImmutableArray(), unchecked((int)Dword(arena, offset + 20)), unchecked((int)Dword(arena, offset + 24)), unchecked((int)Dword(arena, offset + 28)), 4096);
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(unchecked((short)ReadWord(arena, offset)), unchecked((short)ReadWord(arena, offset + 2)), unchecked((short)ReadWord(arena, offset + 4)));
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort ReadWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
}
