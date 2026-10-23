using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315BodyDrawing
{
    public static NativeBattleEffect315BodyDrawingStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary156, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena scratch, uint scratchAddress, int scratchLength, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (U32(temporary156, 4) != scratchAddress || scratchLength < 0 || scratchLength > NativeBattleEffectArena.Length || scratchLength % 8 != 0 || depthShift > 31)
        {
            throw new InvalidDataException("Effect315 body requires its actual bounded renderer scratch.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 body requires current GTE geometry."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 body requires current GTE arithmetic."));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int objects = ReadInt(geometryAddress);
        for (int index = 0; index < objects; index++)
        {
            uint cursor = unchecked(geometryAddress + (uint)ReadInt(checked(geometryAddress + 4 + (uint)(index * 4))));
            if ((U32(temporary156, 0x20) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            int groups = ReadWord(cursor);
            cursor += 2;
            int vertices = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = ReadWord(cursor);
                cursor += 2;
                if ((uint)bone >= (uint)bones.Length)
                {
                    throw new InvalidDataException("Effect315 body selected a bone outside its current rig.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
                int count = ReadWord(cursor);
                cursor += 2;
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 8);
                    if (destination > scratchLength - 8)
                    {
                        throw new InvalidDataException("Effect315 body vertices leave their current renderer capacity.");
                    }
                    byte[] xyz = readGeometry(cursor, 6).Read(0, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    NativeBattleVector point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
                    scratch = Word(Word(Word(Word(scratch, destination, unchecked((ushort)point.X)), destination + 2, unchecked((ushort)point.Y)), destination + 4, unchecked((ushort)point.Z)), destination + 6, 0);
                    short threshold = S16(temporary156, 0x76);
                    if (point.Y > threshold)
                    {
                        short dx = unchecked((short)(point.X - S16(temporary156, 0x74)));
                        short dz = unchecked((short)(point.Z - S16(temporary156, 0x78)));
                        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                        short nx = unchecked((short)normalized.X);
                        short nz = unchecked((short)normalized.Z);
                        temporary156 = Word(Word(Word(temporary156, 0x54, unchecked((ushort)nx)), 0x56, 0), 0x58, unchecked((ushort)nz));
                        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(dx, 0, dz));
                        int radius = S32(temporary156, 0x34);
                        short x;
                        short z;
                        if (length < radius)
                        {
                            int quotient = unchecked(S32(temporary156, 0x30) - point.Y) / S32(temporary156, 0x3C);
                            int factor = unchecked(unchecked(quotient * S32(temporary156, 0x38)) * S32(temporary156, 0x2C)) >> 12;
                            x = unchecked((short)(point.X + (unchecked(nx * factor) >> 12)));
                            z = unchecked((short)(point.Z + (unchecked(nz * factor) >> 12)));
                        }
                        else
                        {
                            x = unchecked((short)(S16(temporary156, 0x74) + (unchecked(nx * radius) >> 12)));
                            z = unchecked((short)(S16(temporary156, 0x78) + (unchecked(nz * radius) >> 12)));
                        }
                        scratch = Word(Word(Word(scratch, destination, unchecked((ushort)x)), destination + 2, 0), destination + 4, unchecked((ushort)z));
                    }
                    else
                    {
                        scratch = Word(scratch, destination + 2, unchecked((ushort)(point.Y - threshold)));
                    }
                    cursor += 6;
                    vertices++;
                }
            }
            cursor = (cursor + 3) & ~3u;
            temporary156 = temporary156.Write(8, readGeometry(cursor, 8).Read(0, 8));
            cursor += 12;
            temporary156 = Dword(temporary156, 0, cursor);
            short? cameraPadding = camera.Known[18] && camera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(camera.Bytes.AsSpan().Slice(18, 2)) : null;
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, cameraPadding) };
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int count = U16(temporary156, quad ? 0xA : 8);
                for (int face = 0; face < count; face++)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    NativeBattleEffectArena[] corners = new NativeBattleEffectArena[quad ? 4 : 3];
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        int selected = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (selected >= vertices)
                        {
                            throw new InvalidDataException("Effect315 face leaves its current deformed object.");
                        }
                        corners[corner] = NativeBattleEffectNodeContextMemory.Copy(scratch, selected * 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                        temporary156 = NativeBattleEffectNodeContextMemory.Copy(corners[corner], 0, temporary156, 0x54 + corner * 8, 8);
                        if (corner < 3)
                        {
                            registers = NativeBattleGeometry.LoadVector(registers, corner, new(S16(corners[corner], 0), S16(corners[corner], 2), S16(corners[corner], 4)), S16(corners[corner], 6));
                        }
                    }
                    registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                    SkinWrites packet = StartPacket(source, quad, corners, temporary156, allocated);
                    uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Effect315 body requires its projection flags.");
                    temporary156 = Dword(temporary156, 0x50, flags);
                    bool accepted = false;
                    if ((flags & 0x60000) == 0)
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect315 body requires its current NCLIP.");
                        temporary156 = Dword(temporary156, 0x44, area);
                        if (unchecked((int)area) > 0)
                        {
                            for (int corner = 0; corner < 3; corner++)
                            {
                                packet.Word(8 + corner * 8, registers.Geometry.Sxy[corner]!.Value);
                            }
                            if (quad)
                            {
                                registers = NativeBattleGeometry.LoadVector(registers, 0, new(S16(corners[3], 0), S16(corners[3], 2), S16(corners[3], 4)), S16(corners[3], 6));
                                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                                uint uv = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(16));
                                packet.Word(28, uv);
                                packet.Word(36, unchecked((uint)(unchecked((int)uv) >> 16)));
                                packet.Word(32, registers.Geometry.Sxy[2]!.Value);
                                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect315 body requires its current ZSF4."));
                            }
                            else
                            {
                                packet.Word(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(4)) >> 16)));
                                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect315 body requires its current ZSF3."));
                            }
                            uint depth = registers.Geometry.Otz!.Value;
                            temporary156 = Dword(temporary156, 0x4C, depth);
                            int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                            uint opcode = packet.Words()[1] >> 24 & 0xFC;
                            ImmutableArray<int> payload = opcode is >= 0x20 and <= 0x3C ? (opcode & 8) != 0 ? registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0] : [0, 0, 0, 0];
                            attempts.Add(packet.Attempt(new(bucket, packet.Words(), payload)));
                            allocated += quad ? 40 : 32;
                            accepted = true;
                        }
                    }
                    if (!accepted)
                    {
                        attempts.Add(packet.Attempt(null));
                    }
                    cursor += quad ? 20u : 16u;
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary156, scratch, new(attempts.ToImmutable(), allocated));

        int ReadInt(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }
}
