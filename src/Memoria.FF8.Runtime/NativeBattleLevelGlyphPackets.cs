using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLevelGlyphPacketsStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleLevelGlyphPackets
{
    public static NativeBattleLevelGlyphPacketsStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context88, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, Func<int, NativeBattleEffectArena> readPackedVertex, bool applyDepthBias = false, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
        NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked(ReadInt(0) + 12);
        context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
        for (int group = 0; group < 2; group++)
        {
            bool gouraud = group != 0;
            int stride = gouraud ? 36 : 24;
            int count = ReadInt(cursor);
            cursor = checked(cursor + 4);
            context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
            if (count > (mesh.Bytes.Length - cursor) / stride)
            {
                throw new InvalidDataException("Level glyph face list leaves its actual resource extent.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = Read(cursor, stride);
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
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
                NativeBattleGfGeometryBatch batch = EmitFace(face, gouraud, Corner, context88, constants, tableBaseBucket, depthShift, allocated, applyDepthBias);
                if (batch.AllocationBytes != 0)
                {
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Level glyph drawing requires the current averaged depth.");
                    int selectedDepth = applyDepthBias ? Math.Max(0, unchecked((int)depth + (int)Dword(context88, 0x18))) : unchecked((int)depth);
                    context88 = Put(context88, 0x40, unchecked((uint)selectedDepth));
                    int bucket = unchecked(tableBaseBucket + (selectedDepth >> depthShift));
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        NativeBattleGfGeometryAttempt captured = attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = bucket } };
                        if (pilotIdentity is { } identity && world is not null && captured.Primitive is { Words.Length: > 2 } primitive && (primitive.Words[1] & 0xFC000000u) is 0x2C000000u or 0x3C000000u)
                        {
                            captured = captured with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, world[0], world[1], world[2], world[3], 4) } };
                        }
                        attempts.Add(captured);
                    }
                    uint cue = Dword(context88, 12);
                    if (cue != 0)
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
                    if (world is not null && pilotMatrix is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, vertex), projection);
                    }
                }
            }
            context88 = Put(context88, 0x34, checked(mesh.Address + (uint)cursor));
            if (group == 0)
            {
                cursor = checked(cursor + 12);
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

    public static NativeBattleGfGeometryBatch EmitFace(ReadOnlySpan<byte> face, bool gouraud, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEffectArena context88, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, int byteOffset = 0, bool applyDepthBias = false)
    {
        uint cue = Dword(context88, 12);
        uint farColor = cue == 0 ? 0 : (uint)(context88.Read(8, 1)[0] | context88.Read(9, 1)[0] << 8 | context88.Read(10, 1)[0] << 16);
        NativeBattleEffectMeshWindow window = new(0, Word(context88, 0x22), Window(context88, 0x24), Window(context88, 0x2C), true, context88.Read(0x32, 1)[0]);
        NativeBattleEffectMeshPacketContext packet = new(cue == 0 ? 0u : gouraud ? 0x80u : 0x40u, farColor, unchecked((int)cue), null, null, 0, false, applyDepthBias ? unchecked((int)Dword(context88, 0x18)) : null, tableBaseBucket, depthShift, window);
        return NativeBattleEffectMeshPackets.EmitTextured(face, readCorner, true, gouraud, packet, constants, byteOffset, NativeBattleEffectMeshClip.OutsideScreen);
    }

    private static NativeBattleTextureWindow Window(NativeBattleEffectArena context, int offset) => new(unchecked((short)Word(context, offset)), unchecked((short)Word(context, offset + 2)), unchecked((short)Word(context, offset + 4)), unchecked((short)Word(context, offset + 6)));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
