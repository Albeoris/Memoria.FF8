using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect69CasterDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary96, NativeBattleEffectArena Scratch, NativeBattleGfGeometryBatch Batch);

/// <summary>Current-bone world projection and the caster's retained eight-byte half-plane mesh stream.</summary>
public static class NativeBattleEffect69CasterDrawing
{
    public static NativeBattleEffect69CasterDrawingStep Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary96, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> currentComposedBones, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena scratch, int scratchLength, int tableBaseBucket = 17)
    {
        if (scratchLength < 0 || scratchLength > NativeBattleEffectArena.Length || (scratchLength & 7) != 0 || currentComposedBones.IsDefault || currentComposedBones.IsEmpty)
        {
            throw new InvalidDataException("Caster drawing requires its current bounded eight-byte workspace and packed bones.");
        }
        int shift = unchecked((int)Dword(temporary96, 0x58)) & 31;
        uint scratchAddress = Dword(temporary96, 4);
        uint mask = Dword(temporary96, 0x18);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Caster drawing requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Caster drawing requires its current arithmetic registers."));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int objects = Integer(geometryAddress);
        if (objects < 0)
        {
            throw new InvalidDataException("Caster drawing requires its current object directory.");
        }
        for (int item = 0; item < objects; item++)
        {
            uint cursor = checked(geometryAddress + (uint)Integer(checked(geometryAddress + 4u + (uint)(item * 4))));
            temporary96 = Put(temporary96, 0x50, cursor);
            if ((mask & (1u << (item & 31))) == 0)
            {
                continue;
            }
            temporary96 = Put(temporary96, 0x5C, scratchAddress);
            int destination = 0;
            int groups = Short(cursor);
            if (groups < 0)
            {
                throw new InvalidDataException("Caster drawing requires nonnegative current bone group counts.");
            }
            cursor += 2;
            temporary96 = Put(temporary96, 0x50, cursor);
            for (int group = 0; group < groups; group++)
            {
                int bone = Short(cursor);
                cursor += 2;
                temporary96 = Put(temporary96, 0x50, cursor);
                if ((uint)bone >= (uint)currentComposedBones.Length)
                {
                    throw new InvalidDataException("Caster drawing leaves its current packed bone owner.");
                }
                registers = LoadMatrix(registers, currentComposedBones[bone]);
                int vertices = Short(cursor);
                if (vertices < 0 || destination > scratchLength - checked(vertices * 8))
                {
                    throw new InvalidDataException("Caster drawing leaves its current renderer scratch capacity.");
                }
                cursor += 2;
                uint end = checked(cursor + (uint)(vertices * 6));
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    NativeBattleEffectArena source = readGeometry(cursor, 6);
                    NativeBattleVector xyz = Vector(source, 0);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, xyz, 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    scratch = StoreVector(scratch, destination + vertex * 8, new(unchecked((short)Required(registers.Colors.Ir[0])), unchecked((short)Required(registers.Colors.Ir[1])), unchecked((short)Required(registers.Colors.Ir[2]))));
                    cursor += 6;
                }
                registers = LoadMatrix(registers, view);
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    int row = destination + vertex * 8;
                    int worldY = Signed(scratch, row + 2);
                    (byte[] bytes, bool[] known) = scratch.ReadSnapshot(row, 8);
                    registers = registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    temporary96 = Put(temporary96, 0x1C, Required(registers.Geometry.Sxy[2]));
                    temporary96 = Put(temporary96, 0x20, unchecked((uint)((int)Required(registers.Geometry.Sz[3]) >> 2)));
                    if (Word(temporary96, 0x20) == 0)
                    {
                        scratch = scratch.Write(row + 6, [0x10]);
                        continue;
                    }
                    byte high = worldY > unchecked((int)Dword(temporary96, 0x4C)) ? (byte)2 : (byte)1;
                    temporary96 = temporary96.Write(0x23, [high]);
                    int x = Signed(temporary96, 0x1C);
                    int y = Signed(temporary96, 0x1E);
                    byte low = temporary96.Read(0x22, 1)[0];
                    if (x < Signed(temporary96, 0x0C) * 8)
                    {
                        low |= 1;
                    }
                    else if (x >= Signed(temporary96, 0x10) * 8)
                    {
                        low |= 2;
                    }
                    if (y < Signed(temporary96, 0x0E) * 8)
                    {
                        low |= 4;
                    }
                    else if (y >= Signed(temporary96, 0x12) * 8)
                    {
                        low |= 8;
                    }
                    temporary96 = temporary96.Write(0x22, [low]);
                    scratch = Copy(temporary96, 0x1C, scratch, row, 8);
                }
                cursor = end;
                destination += vertices * 8;
                temporary96 = Put(Put(temporary96, 0x50, cursor), 0x5C, checked(scratchAddress + (uint)destination));
            }
            cursor = checked((cursor + 3) & ~3u);
            ushort triangles = UnsignedShort(cursor);
            ushort quads = UnsignedShort(cursor + 2);
            cursor += 12;
            temporary96 = Put(Put(temporary96, 0x50, cursor), 0, cursor).Write(8, BitConverter.GetBytes(triangles)).Write(10, BitConverter.GetBytes(quads));
            DrawFaces(triangles, false);
            DrawFaces(quads, true);

            void DrawFaces(ushort count, bool quad)
            {
                int counter = quad ? 10 : 8;
                temporary96 = temporary96.Write(counter, BitConverter.GetBytes(unchecked((ushort)(count - 1))));
                for (int face = 0; face < count; face++)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    int corners = quad ? 4 : 3;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int selected = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        int row = selected * 8;
                        if (row > scratchLength - 8)
                        {
                            throw new InvalidDataException("Caster face leaves its current projected scratch owner.");
                        }
                        temporary96 = Copy(scratch, row, temporary96, 0x1C + corner * 8, 8);
                    }
                    SkinWrites packet = new(quad ? 40 : 32, allocated);
                    byte lowOr = 0;
                    byte lowAnd = 255;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        byte value = temporary96.Read(0x22 + corner * 8, 1)[0];
                        lowOr |= value;
                        lowAnd &= value;
                    }
                    if (lowOr < 0x10 && lowAnd == 0)
                    {
                        byte high = 0;
                        for (int corner = 0; corner < corners; corner++)
                        {
                            high |= temporary96.Read(0x23 + corner * 8, 1)[0];
                        }
                        if (high == 1)
                        {
                            uint[] screens = Enumerable.Range(0, corners).Select(corner => Dword(temporary96, 0x1C + corner * 8)).ToArray();
                            registers = registers with { Geometry = registers.Geometry with { Sxy = [screens[0], screens[1], screens[2]] } };
                            registers = NativeBattleGeometry.Clip45EE10(registers);
                            uint area = Required(registers.Geometry.Mac0);
                            temporary96 = Put(temporary96, 0x3C, area);
                            if (unchecked((int)area) >= 0)
                            {
                                uint[] depths = Enumerable.Range(0, corners).Select(corner => (uint)Word(temporary96, 0x20 + corner * 8)).ToArray();
                                registers = registers with { Geometry = registers.Geometry with { Sz = quad ? [depths[0], depths[1], depths[2], depths[3]] : [registers.Geometry.Sz[0], depths[0], depths[1], depths[2]] } };
                                registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Caster quads require their current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Caster triangles require their current ZSF3."));
                                uint command = quad ? 0x2C000000u : 0x24000000u;
                                uint color = Dword(temporary96, 0x14) | command | ((source[15] & 2) != 0 ? 0x02000000u : 0);
                                packet.Word(0, quad ? 0x09000000u : 0x07000000u);
                                packet.Word(4, color);
                                for (int corner = 0; corner < corners; corner++)
                                {
                                    packet.Word(8 + corner * 8, screens[corner]);
                                }
                                packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
                                packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)) & 0xFDFFFFFF);
                                packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
                                if (quad)
                                {
                                    packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
                                }
                                uint depth = Required(registers.Geometry.Otz);
                                temporary96 = Put(temporary96, 0x44, depth);
                                NativeBattleGeometryPrimitive primitive = new(unchecked(tableBaseBucket + ((int)depth >> shift)), packet.Words(), Sidecars(registers.Geometry, color));
                                attempts.Add(packet.Attempt(primitive));
                                allocated += quad ? 40 : 32;
                            }
                            else
                            {
                                attempts.Add(packet.Attempt(null));
                            }
                        }
                        else
                        {
                            attempts.Add(packet.Attempt(null));
                        }
                    }
                    else
                    {
                        attempts.Add(packet.Attempt(null));
                    }
                    cursor += quad ? 20u : 16u;
                    temporary96 = temporary96.Write(counter, BitConverter.GetBytes(unchecked((ushort)(count - face - 2))));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary96, scratch, new(attempts.ToImmutable(), allocated));

        int Integer(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
        ushort UnsignedShort(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix)
    {
        NativeBattleActionMatrix values = matrix.ReadMatrix();
        return registers with { Geometry = registers.Geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray(), Translation = [values.X, values.Y, values.Z] } };
    }

    private static ImmutableArray<int> Sidecars(NativeBattleGeometryRegisters geometry, uint color)
    {
        uint command = (color >> 24) & 0xFC;
        if (command is < 0x20 or > 0x3C)
        {
            return [0, 0, 0, 0];
        }
        return (command & 8) != 0 ? [unchecked((int)Required(geometry.Sz[0])), unchecked((int)Required(geometry.Sz[1])), unchecked((int)Required(geometry.Sz[2])), unchecked((int)Required(geometry.Sz[3]))] : [unchecked((int)Required(geometry.Sz[1])), unchecked((int)Required(geometry.Sz[2])), unchecked((int)Required(geometry.Sz[3])), 0];
    }

    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, destination, offset, length);
    private static NativeBattleVector Vector(NativeBattleEffectArena source, int offset) => new(Signed(source, offset), Signed(source, offset + 2), Signed(source, offset + 4));
    private static short Signed(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static ushort Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena destination, int offset, uint value) => destination.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena StoreVector(NativeBattleEffectArena destination, int offset, NativeBattleVector vector) => destination.Write(offset, BitConverter.GetBytes(vector.X)).Write(offset + 2, BitConverter.GetBytes(vector.Y)).Write(offset + 4, BitConverter.GetBytes(vector.Z));
    private static int Required(int? value) => value ?? throw new NotSupportedException("Caster drawing requires its current arithmetic result.");
    private static uint Required(uint? value) => value ?? throw new NotSupportedException("Caster drawing requires its current geometry result.");
}
