using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLevelPlainPacketsStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleLevelPlainPackets
{
    public static NativeBattleLevelPlainPacketsStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context88, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, NativeBattleEffectArena> readPackedVertex)
    {
        if (mesh.Bytes.IsDefaultOrEmpty || Dword(context88, 0) != mesh.Address || depthShift > 31)
        {
            throw new InvalidDataException("Level glyph drawing requires its actual addressed mesh and ordering shift.");
        }
        _ = Dword(context88, 4);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Level glyph drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Level glyph drawing requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Level glyph drawing requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Level glyph drawing requires current primitive controls.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Level glyph drawing requires current far-color controls.");
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked(ReadInt(0) + 12);
        context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
        for (int group = 0; group < 2; group++)
        {
            bool gouraud = group != 0;
            int stride = 24;
            int count = ReadInt(cursor);
            cursor = checked(cursor + 4);
            context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
            if (count < 0 || count > (mesh.Bytes.Length - cursor) / stride)
            {
                throw new InvalidDataException("Level glyph face list leaves its actual resource extent.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = Read(cursor, stride);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Level glyph triple projection requires current flags.");
                context88 = Put(context88, 0x44, firstFlags);
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                NativeBattleModuleProjectedVertex Corner(int corner)
                {
                    if (corner == 3)
                    {
                        Load(3, 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Level glyph averaging requires current ZSF4."));
                    }
                    return projected[corner];
                }
                NativeBattleGfGeometryBatch batch = EmitFace(face, gouraud, Corner, context88, constants, tableBaseBucket, depthShift, allocated);
                if (batch.AllocationBytes != 0)
                {
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Level glyph drawing requires the current averaged depth.");
                    int selectedDepth = Math.Max(0, unchecked((int)depth + (int)Dword(context88, 0x18)));
                    context88 = Put(context88, 0x40, unchecked((uint)selectedDepth));
                    int bucket = unchecked(tableBaseBucket + (selectedDepth >> depthShift));
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        attempts.Add(attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = bucket } });
                    }
                    uint cue = Dword(context88, 12);
                    if (cue != 0 && !gouraud)
                    {
                        byte[] rgb = context88.Read(8, 3);
                        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
                        registers = gouraud ? NativeBattleGouraudMeshRegisters.Shade(registers, face, true, true, 0x80, cue, far, constants) : registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(face), cue, far, constants).Registers };
                    }
                }
                else
                {
                    attempts.AddRange(batch.Attempts);
                }
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + stride);

                void Load(int corner, int slot)
                {
                    int offset = checked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    NativeBattleEffectArena packed = readPackedVertex(offset);
                    (byte[] bytes, bool[] known) = packed.ReadSnapshot(0, 8);
                    NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(packed.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(packed.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(packed.Read(4, 2)));
                    short? padding = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
                    registers = NativeBattleGeometry.LoadVector(registers, slot, vertex, padding);
                }
            }
            context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
            if (group == 0)
            {
                cursor = checked(cursor + 4);
                context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context88, new(attempts.ToImmutable(), allocated));

        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        byte[] Read(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Level glyph source read leaves its original bounded mesh.");
            }
            return mesh.Bytes.AsSpan(offset, length).ToArray();
        }
    }

    public static NativeBattleGfGeometryBatch EmitFace(ReadOnlySpan<byte> face, bool gouraud, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEffectArena context88, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, int byteOffset = 0)
    {
        uint cue = Dword(context88, 12);
        uint farColor = cue == 0 ? 0 : (uint)(context88.Read(8, 1)[0] | context88.Read(9, 1)[0] << 8 | context88.Read(10, 1)[0] << 16);
        NativeBattleEffectMeshPacketContext packet = new(gouraud ? 0x20u : cue == 0 ? 0u : 0x40u, farColor, unchecked((int)cue), null, null, 0, false, unchecked((int)Dword(context88, 0x18)), tableBaseBucket, depthShift);
        NativeBattleGfGeometryBatch drawn = gouraud ? NativeBattleEffectMeshPackets.EmitGouraud(face, readCorner, true, packet, constants, byteOffset) : NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, false, packet, constants, byteOffset, NativeBattleEffectMeshClip.OutsideScreen);
        if (drawn.AllocationBytes == 0)
        {
            return drawn;
        }
        NativeBattleGfGeometryAttempt body = drawn.Attempts.Single();
        int bucket = body.Primitive!.Bucket;
        NativeBattleGfGeometryAttempt before = Mode(checked(byteOffset + drawn.AllocationBytes), 0xE1000220, bucket);
        SkinWrites linkOnly = new(body.PacketLength, body.ByteOffsetRelative);
        NativeBattleGfGeometryAttempt link = linkOnly.Attempt(body.Primitive);
        if (!gouraud)
        {
            return new([body with { Primitive = null }, before, link], checked(drawn.AllocationBytes + 8));
        }
        NativeBattleGfGeometryAttempt after = Mode(checked(byteOffset + drawn.AllocationBytes + 8), 0xE1000240, bucket);
        return new([body with { Primitive = null }, before, link, after], checked(drawn.AllocationBytes + 16));
    }

    private static NativeBattleGfGeometryAttempt Mode(int offset, uint command, int bucket)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(bucket, [0x01000000, command], [0, 0, 0, 0]));
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
