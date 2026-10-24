using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect64BodyStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary152, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect64BodyDrawing
{
    public static NativeBattleEffect64BodyStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary152, NativeBattleEffectArena vertexScratch, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleEffectArena offsets, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint scratchAddress, int scratchLength, int tableBaseBucket = 17, byte depthShift = 0)
    {
        if (U32(temporary152, 4) != scratchAddress || scratchLength < 0 || depthShift > 31)
        {
            throw new InvalidDataException("Effect64 offset-body drawing requires its actual bounded projection workspace.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect64 offset-body drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect64 offset-body drawing requires current arithmetic registers."));
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int objects = ReadInt(geometryAddress);
        for (int index = 0; index < objects; index++)
        {
            uint cursor = checked(geometryAddress + (uint)ReadInt(checked(geometryAddress + 4 + (uint)(index * 4))));
            if ((U32(temporary152, 0x18) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            int groups = ReadWord(cursor);
            cursor += 2;
            int vertices = 0;
            for (int group = 0; group < groups; group++)
            {
                int bone = ReadWord(cursor);
                int count = ReadWord(cursor + 2);
                cursor += 4;
                if ((uint)bone >= (uint)bones.Length || count < 0)
                {
                    throw new InvalidDataException("Effect64 offset-body group leaves its actual current bone or vertex bank.");
                }
                bool visible = Word(offsets, bone * 16 + 6) < 128;
                if (visible)
                {
                    NativeBattlePackedMatrix32 matrix = bones[bone];
                    registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, Padding(matrix)) };
                }
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 8);
                    if (destination > scratchLength - 8)
                    {
                        throw new InvalidDataException("Effect64 offset-body vertices leave the current physical scratch capacity.");
                    }
                    if (!visible)
                    {
                        vertexScratch = vertexScratch.Write(destination + 6, [0x10]);
                    }
                    else
                    {
                        byte[] xyz = readGeometry(cursor, 6).Read(0, 6);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect64 offset-body projection requires current screen output.");
                        uint depth = unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect64 offset-body projection requires current depth output."))) >> 2));
                        temporary152 = temporary152.Write(0x1C, BitConverter.GetBytes(screen)).Write(0x20, BitConverter.GetBytes(depth));
                        if (unchecked((short)depth) <= 0)
                        {
                            vertexScratch = vertexScratch.Write(destination + 6, [0x10]);
                        }
                        else
                        {
                            int x = unchecked((short)screen);
                            int y = unchecked((short)(screen >> 16));
                            byte clip = 0;
                            if (x < (Word(temporary152, 0xC) << 3))
                            {
                                clip = 1;
                            }
                            else if (x >= (Word(temporary152, 0x10) << 3))
                            {
                                clip = 2;
                            }
                            if (y < (Word(temporary152, 0xE) << 3))
                            {
                                clip |= 4;
                            }
                            else if (y >= (Word(temporary152, 0x12) << 3))
                            {
                                clip |= 8;
                            }
                            byte selector = offsets.Read(bone * 16 + 6, 1)[0];
                            temporary152 = temporary152.Write(0x22, [clip, selector]);
                            vertexScratch = vertexScratch.Write(destination, temporary152.Read(0x1C, 8));
                        }
                    }
                    vertices++;
                    cursor += 6;
                }
            }
            cursor = (cursor + 3) & ~3u;
            temporary152 = temporary152.Write(8, readGeometry(cursor, 4).Read(0, 4));
            cursor += 12;
            temporary152 = temporary152.Write(0, BitConverter.GetBytes(cursor));
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int faces = U16(temporary152, quad ? 10 : 8);
                temporary152 = temporary152.Write(quad ? 10 : 8, BitConverter.GetBytes(unchecked((ushort)(faces - 1))));
                for (int face = 0; face < faces; face++)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    if ((source[15] & 0x80) != 0)
                    {
                        cursor += quad ? 20u : 16u;
                        temporary152 = temporary152.Write(quad ? 10 : 8, BitConverter.GetBytes(unchecked((ushort)(faces - face - 2))));
                        continue;
                    }
                    NativeBattleEffectArena[] corners = new NativeBattleEffectArena[quad ? 4 : 3];
                    byte clipOr = 0;
                    byte clipAnd = 255;
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (vertex >= vertices)
                        {
                            throw new InvalidDataException("Effect64 offset-body polygon leaves its current projected vertices.");
                        }
                        corners[corner] = NativeBattleEffectNodeContextMemory.Copy(vertexScratch, vertex * 8, NativeBattleEffectArena.Clear(), 0, 8);
                        temporary152 = NativeBattleEffectNodeContextMemory.Copy(corners[corner], 0, temporary152, 0x1C + corner * 8, 8);
                        byte clip = corners[corner].Read(6, 1)[0];
                        clipOr |= clip;
                        clipAnd &= clip;
                    }
                    if (clipOr < 0x10 && clipAnd == 0 && corners.Skip(1).All(corner => corner.Read(7, 1)[0] == corners[0].Read(7, 1)[0]))
                    {
                        registers = registers with { Geometry = registers.Geometry with { Sxy = Enumerable.Range(0, 3).Select(corner => (uint?)U32(corners[corner], 0)).ToImmutableArray() } };
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect64 offset-body NCLIP requires its current result.");
                        temporary152 = temporary152.Write(0x3C, BitConverter.GetBytes(area));
                        if (unchecked((int)area) >= 0)
                        {
                            ImmutableArray<uint?> depths = registers.Geometry.Sz;
                            for (int corner = 0; corner < corners.Length; corner++)
                            {
                                depths = depths.SetItem(quad ? corner : corner + 1, unchecked((uint)(int)Word(corners[corner], 4)));
                            }
                            registers = registers with { Geometry = registers.Geometry with { Sz = depths } };
                            registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect64 offset-body AVSZ4 requires current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect64 offset-body AVSZ3 requires current ZSF3."));
                            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect64 offset-body averaging requires current OTZ.");
                            temporary152 = temporary152.Write(0x44, BitConverter.GetBytes(depth));
                            attempts.Add(EmitProjectedFace64(source, quad, corners, U32(temporary152, 0x14), registers.Geometry, unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift)), allocated));
                            allocated += quad ? 40 : 32;
                        }
                    }
                    cursor += quad ? 20u : 16u;
                    temporary152 = temporary152.Write(quad ? 10 : 8, BitConverter.GetBytes(unchecked((ushort)(faces - face - 2))));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary152, vertexScratch, new(attempts.ToImmutable(), allocated));

        int ReadInt(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static NativeBattleGfGeometryAttempt EmitProjectedFace64(byte[] source, bool quad, NativeBattleEffectArena[] corners, uint color, NativeBattleGeometryRegisters geometry, int bucket, int offset)
    {
        byte[] data = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[data.Length];
        void Store(int at, ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(data.AsSpan(at));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, bytes.Length);
        }
        Store(0, BitConverter.GetBytes(quad ? 0x09000000u : 0x07000000u));
        uint opcode = (uint)(((BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(14)) & 0x200) >> 8) | (quad ? 0x2C : 0x24));
        Store(4, BitConverter.GetBytes(color | (opcode << 24)));
        for (int corner = 0; corner < corners.Length; corner++)
        {
            Store(8 + corner * 8, corners[corner].Read(0, 4));
        }
        Store(12, source.AsSpan(8, 4));
        Store(20, source.AsSpan(12, 4));
        ushort page = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(22)) & 0xFDFF));
        Store(22, BitConverter.GetBytes(page));
        Store(28, source.AsSpan(quad ? 16 : 6, 2));
        if (quad)
        {
            Store(36, source.AsSpan(18, 2));
        }
        ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<int> payload = quad ? geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect64 offset-body quad insertion requires its current depth FIFO.")))).ToImmutableArray() : [unchecked((int)geometry.Sz[1]!.Value), unchecked((int)geometry.Sz[2]!.Value), unchecked((int)geometry.Sz[3]!.Value), 0];
        return new(offset, data.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, words, payload));
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short? Padding(NativeBattlePackedMatrix32 packed) => packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan().Slice(18, 2)) : null;
}
