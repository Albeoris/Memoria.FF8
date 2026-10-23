using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect303BodyBank
{
    ProjectionScratch,
    Common,
    Effect
}

public sealed record NativeBattleEffect303BodyWrite(NativeBattleEffect303BodyBank Bank, NativeBattleGfGeometryAttempt Attempt);
public sealed record NativeBattleEffect303BodyBatch(ImmutableArray<NativeBattleEffect303BodyWrite> Writes, int CommonAllocationBytes, int EffectAllocationBytes);
public sealed record NativeBattleEffect303BodyStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary188, NativeBattleEffect303BodyBatch Batch);

public static partial class NativeBattleEffect303BodyDrawing
{
    public static NativeBattleEffect303BodyStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary188, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, int tableBaseBucket = 17, byte depthShift = 0)
    {
        if (U32(temporary188, 0x24) != scratchAddress || scratchLength < 0 || scratchLength > NativeBattleEffect303Profile.BodyProjectionCapacity || depthShift > 31)
        {
            throw new InvalidDataException("Effect303 body requires its actual bounded projection workspace.");
        }
        int scratchOffset = NativeBattleWaterExecutionFields.ArenaOffset(scratchAddress, scratchLength);
        NativeBattleEffectArena scratch = NativeBattleEffectNodeContextMemory.Copy(memory.Arena ?? throw new NotSupportedException("Effect303 body requires its current projection memory."), scratchOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, scratchLength), 0, scratchLength);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect303 body requires current GTE geometry."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect303 body requires current GTE arithmetic."));
        var writes = ImmutableArray.CreateBuilder<NativeBattleEffect303BodyWrite>();
        int common = 0;
        int effect = 0;
        uint commonStart = U32(temporary188, 0xB4);
        uint effectStart = U32(temporary188, 0xB8);
        int objects = ReadInt(geometryAddress);
        for (int index = 0; index < objects; index++)
        {
            uint cursor = checked(geometryAddress + (uint)ReadInt(checked(geometryAddress + 4 + (uint)(index * 4))));
            if ((U32(temporary188, 0x38) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            temporary188 = Dword(temporary188, 0xAC, cursor);
            temporary188 = Dword(temporary188, 0xB0, scratchAddress);
            int groups = ReadWord(cursor);
            cursor += 2;
            temporary188 = Dword(temporary188, 0xAC, cursor);
            int vertices = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = ReadWord(cursor);
                cursor += 2;
                temporary188 = Dword(temporary188, 0xAC, cursor);
                if ((uint)bone >= (uint)bones.Length)
                {
                    throw new InvalidDataException("Effect303 body selected a bone outside its current recipient rig.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
                int count = ReadWord(cursor);
                cursor += 2;
                if (count < 0)
                {
                    throw new NotSupportedException("Effect303 body negative vertex count reaches an unsupported native loop.");
                }
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 12);
                    if (destination > scratchLength - 12)
                    {
                        throw new InvalidDataException("Effect303 body vertices leave its actual projection capacity.");
                    }
                    byte[] xyz = readGeometry(cursor, 6).Read(0, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect303 body requires current SXY2.");
                    uint depth = unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect303 body requires current SZ3."))) >> 2));
                    temporary188 = Dword(Dword(temporary188, 0x3C, screen), 0x40, depth);
                    if (unchecked((short)depth) <= 0)
                    {
                        scratch = scratch.Write(destination + 6, [0x10]);
                        writes.Add(new(NativeBattleEffect303BodyBank.ProjectionScratch, new(destination + 6, ImmutableArray.Create((byte)0x10), ImmutableArray.Create(NativeBattleGfPacketByteState.Known), null)));
                    }
                    else
                    {
                        int x = unchecked((short)screen);
                        int y = unchecked((short)(screen >> 16));
                        byte clip = temporary188.Read(0x42, 1)[0];
                        if (x < (S16(temporary188, 0x2C) << 3))
                        {
                            clip |= 1;
                        }
                        else if (x >= (S16(temporary188, 0x30) << 3))
                        {
                            clip |= 2;
                        }
                        if (y < (S16(temporary188, 0x2E) << 3))
                        {
                            clip |= 4;
                        }
                        else if (y >= (S16(temporary188, 0x32) << 3))
                        {
                            clip |= 8;
                        }
                        int irX = registers.Colors.Ir[0] ?? throw new NotSupportedException("Effect303 projected U requires current IR1.");
                        int irY = registers.Colors.Ir[1] ?? throw new NotSupportedException("Effect303 projected V requires current IR2.");
                        temporary188 = temporary188.Write(0x42, [clip, unchecked((byte)(irX >> 3)), unchecked((byte)(irY >> 3))]);
                        NativeBattleEffectArena row = NativeBattleEffectNodeContextMemory.Copy(temporary188, 0x3C, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), 0, 12);
                        scratch = NativeBattleEffectNodeContextMemory.Copy(row, 0, scratch, destination, 12);
                        (byte[] data, bool[] known) = row.ReadSnapshot(0, 12);
                        writes.Add(new(NativeBattleEffect303BodyBank.ProjectionScratch, new(destination, data.ToImmutableArray(), known.Select(value => value ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown).ToImmutableArray(), null)));
                    }
                    vertices++;
                    cursor += 6;
                }
                temporary188 = Dword(temporary188, 0xAC, cursor);
                temporary188 = Dword(temporary188, 0xB0, checked(scratchAddress + (uint)(vertices * 12)));
            }
            cursor = (cursor + 3) & ~3u;
            temporary188 = temporary188.Write(0x28, readGeometry(cursor, 4).Read(0, 4));
            cursor += 12;
            temporary188 = Dword(Dword(temporary188, 0xAC, cursor), 0x20, cursor);
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int faces = U16(temporary188, quad ? 0x2A : 0x28);
                for (int face = 0; face < faces; face++)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    NativeBattleEffectArena[] corners = new NativeBattleEffectArena[quad ? 4 : 3];
                    byte clipOr = 0;
                    byte clipAnd = 255;
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (vertex >= vertices)
                        {
                            throw new InvalidDataException("Effect303 body face leaves its current projected object.");
                        }
                        corners[corner] = NativeBattleEffectNodeContextMemory.Copy(scratch, vertex * 12, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), 0, 12);
                        temporary188 = NativeBattleEffectNodeContextMemory.Copy(corners[corner], 0, temporary188, 0x3C + corner * 12, 12);
                        byte clip = corners[corner].Read(6, 1)[0];
                        clipOr |= clip;
                        clipAnd &= clip;
                    }
                    if (clipOr < 0x10 && clipAnd == 0)
                    {
                        registers = registers with { Geometry = registers.Geometry with { Sxy = Enumerable.Range(0, 3).Select(corner => (uint?)U32(corners[corner], 0)).ToImmutableArray() } };
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect303 body requires current NCLIP.");
                        temporary188 = Dword(temporary188, 0x6C, area);
                        if (unchecked((int)area) >= 0)
                        {
                            ImmutableArray<uint?> depths = registers.Geometry.Sz;
                            for (int corner = 0; corner < corners.Length; corner++)
                            {
                                depths = depths.SetItem(quad ? corner : corner + 1, unchecked((uint)(int)S16(corners[corner], 4)));
                            }
                            registers = registers with { Geometry = registers.Geometry with { Sz = depths } };
                            registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect303 body requires current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect303 body requires current ZSF3."));
                            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect303 body requires current averaged depth.");
                            temporary188 = Dword(temporary188, 0x74, depth);
                            int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                            EmitFaces303(source, quad, corners, temporary188, registers.Geometry, bucket, common, effect, writes);
                            common += quad ? 40 : 32;
                            effect += quad ? 64 : 56;
                            temporary188 = Dword(temporary188, 0xB8, checked(effectStart + (uint)effect));
                        }
                    }
                    cursor += quad ? 20u : 16u;
                }
            }
            temporary188 = Dword(temporary188, 0xB4, checked(commonStart + (uint)common));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary188, new(writes.ToImmutable(), common, effect));

        int ReadInt(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }
}
