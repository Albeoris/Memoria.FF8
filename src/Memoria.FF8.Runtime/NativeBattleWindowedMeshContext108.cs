using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindowedMeshContext108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleWindowedMeshContext108
{
    public static NativeBattleWindowedMeshContext108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, int maximumVertices, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, Action<NativeBattleGfGeometryAttempt, int>? observeAttempt = null, Func<int, NativeBattleEffectArena>? readSelectedPackedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        using NativeBattleGfSourceReader reader = new(mesh.Bytes);
        return DrawLoaded(memory, mesh.Address, reader, projection, context, constants, tableBaseBucket, depthShift, maximumVertices, readSelectedVertex, observeAttempt, readSelectedPackedVertex, pilotIdentity);
    }

    internal static NativeBattleWindowedMeshContext108Step DrawLoaded(NativeBattleEffectMemory memory, uint meshAddress, NativeBattleGfSourceReader mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket, byte depthShift, int maximumVertices, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, Action<NativeBattleGfGeometryAttempt, int>? observeAttempt = null, Func<int, NativeBattleEffectArena>? readSelectedPackedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        context.Validate();
        uint flags = Dword(context, 0x1C);
        if (Dword(context, 0) != meshAddress || flags is not (0 or 0xC or 0x30 or 0xF0 or 0x2000 or 0x200C or 0x20C0 or 0x20CC or 0x2030 or 0x203C or 0x20F0 or 0x20FC or 0x800C or 0x8030) || depthShift > 31 || context.ExceedsExtent(108))
        {
            throw new InvalidDataException("Windowed mesh requires its actual bounded windowed context.");
        }
        if ((flags & 0x2000) == 0)
        {
            context = Put(context, 4, checked(meshAddress + 8));
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(4, 4));
        if (maximumVertices < 1 || count < 1 || count > maximumVertices)
        {
            throw new InvalidDataException("Windowed mesh selected rows leave its actual geometry extent.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        int validation = cursor;
        for (int group = 0; group < 8; group++)
        {
            int faces = BinaryPrimitives.ReadInt32LittleEndian(Read(validation, 4));
            if (faces < 0)
            {
                throw new NotSupportedException("Windowed mesh requires nonnegative original polygon counts.");
            }
            validation = checked(validation + 4 + faces * lengths[group]);
            _ = Read(validation, 0);
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Windowed mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Windowed mesh requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Windowed mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Windowed mesh requires its current primitive process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Windowed mesh requires its current far-color owner.");
        (byte[] rgb, bool[] known) = context.ReadSnapshot(8, 3);
        far = far with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int index) => known[index] ? (uint)rgb[index] << 4 : null;
        uint? farColor = known.All(value => value) ? (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16) : null;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int faces = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context = Put(context, 0x34, checked(meshAddress + (uint)cursor));
            for (int index = 0; index < faces; index++)
            {
                byte[] face = Read(cursor, lengths[group]).ToArray();
                bool quad = (group & 1) != 0;
                bool gouraud = group >= 4;
                bool textured = group is 2 or 3 or 6 or 7;
                System.Numerics.Vector3[]? world = pilotIdentity is null ? null : new System.Numerics.Vector3[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short? w) = Vertex(corner);
                    registers = LoadSelected(registers, corner, corner, vertex, w);
                    if (world is not null)
                    {
                        world[corner] = World(registers, corner);
                    }
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                context = Put(context, 0x44, firstFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context = Put(context, 0x38, unchecked((uint)area));
                    if (!quad && (area >= 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Windowed mesh triangle requires its current ZSF3."));
                    }
                }
                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector vertex, short? w) = Vertex(corner);
                        registers = LoadSelected(registers, 0, corner, vertex, w);
                        if (world is not null)
                        {
                            world[3] = World(registers, 0);
                        }
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Windowed mesh quad requires its current ZSF4."));
                    }
                    return projected[corner];
                }
                ushort u = OptionalWord(context, 0x20) ?? 0;
                ushort v = OptionalWord(context, 0x22) ?? 0;
                NativeBattleTextureWindow before = new(0, 0, 0, 0);
                NativeBattleTextureWindow after = new(0, 0, 0, 0);
                if (textured && (u | v) != 0)
                {
                    before = Window(0x24);
                    after = Window(0x2C);
                }
                NativeBattleEsunaMeshContext operands = new(flags, farColor, OptionalInt(context, 12), OptionalInt(context, 24) ?? 0, u, v, before, after, tableBaseBucket, depthShift);
                NativeBattleGfGeometryBatch leaf = group switch
                {
                    0 => NativeBattleWindowedFlat701DD0.EmitF3_701F00(face, Project, operands, constants, allocated),
                    1 => NativeBattleWindowedFlat701DD0.EmitF4_702130(face, Project, operands, constants, allocated),
                    2 => NativeBattleWindowedMesh701DD0.EmitFt3_7023B0(face, Project, operands, constants, allocated),
                    3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, Project, operands, constants, allocated),
                    4 => NativeBattleRestorationMeshPackets.EmitG3_88F220(face, Project, operands, constants, allocated),
                    5 => NativeBattleRestorationMeshPackets.EmitG4_88F470(face, Project, operands, constants, allocated),
                    6 => NativeBattleWindowedMesh701DD0.EmitGt3_703340(face, Project, operands, constants, allocated),
                    _ => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, Project, operands, constants, allocated)
                };
                if (leaf.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    if (textured)
                    {
                        _ = Word(context, 0x20);
                        _ = Word(context, 0x22);
                    }
                    int depth = Math.Max(0, unchecked((int)registers.Geometry.Otz!.Value + (int)Dword(context, 24)));
                    context = Put(context, 0x40, unchecked((uint)depth));
                    int bucket = unchecked(tableBaseBucket + (depth >> depthShift));
                    leaf = leaf with { Attempts = leaf.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        uint cue = Dword(context, 12);
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, textured, flags, cue, far, constants);
                        }
                        else
                        {
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(face), cue, far, constants).Registers };
                        }
                    }
                }
                if (pilotIdentity is { } identity && world is not null)
                {
                    leaf = leaf with { Attempts = leaf.Attempts.Select(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] & 0xE0000000) == 0x20000000 ? attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, world[0], world[1], world[2], quad ? world[3] : default, (byte)world.Length) { SourceDepths = primitive.VertexPayload4.Length == 4 ? new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : System.Numerics.Vector4.Zero, UseSourceDepths = true } } } : attempt).ToImmutableArray() };
                }
                foreach (NativeBattleGfGeometryAttempt attempt in leaf.Attempts)
                {
                    attempts.Add(attempt);
                    observeAttempt?.Invoke(attempt, attempt.Primitive is null ? 0 : attempt.PacketLength);
                }
                allocated = checked(allocated + leaf.AllocationBytes);
                cursor = checked(cursor + lengths[group]);
                context = Put(context, 0x34, checked(meshAddress + (uint)cursor));

                System.Numerics.Vector3 World(NativeBattleGeometryRegisterStep loaded, int slot)
                {
                    ImmutableArray<byte?> bytes = slot switch
                    {
                        0 => loaded.Colors.NormalInput!.Value.Select(value => (byte?)value).ToImmutableArray(),
                        1 => loaded.Geometry.V1,
                        _ => loaded.Geometry.V2
                    };
                    short Component(int axis) => unchecked((short)((bytes[axis * 2] ?? 0) | (bytes[axis * 2 + 1] ?? 0) << 8));
                    NativeBattleActionMatrix matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(loaded.Geometry).ReadMatrix();
                    return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, new(Component(0), Component(1), Component(2))), projection);
                }

                NativeBattleGeometryRegisterStep LoadSelected(NativeBattleGeometryRegisterStep loaded, int slot, int corner, NativeBattleVector vertex, short? w)
                {
                    loaded = NativeBattleGeometry.LoadVector(loaded, slot, vertex, w);
                    if (readSelectedPackedVertex is null || (flags & 0x2000) == 0)
                    {
                        return loaded;
                    }
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    (byte[] bytes, bool[] known) = readSelectedPackedVertex(offset).ReadSnapshot(0, 8);
                    if (!known.Take(6).All(value => value))
                    {
                        throw new NotSupportedException("Windowed mesh requires its selected packed XYZ bytes.");
                    }
                    ImmutableArray<byte?> packed = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
                    return slot switch
                    {
                        0 => loaded with { Colors = loaded.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } },
                        1 => loaded with { Geometry = loaded.Geometry with { V1 = packed } },
                        _ => loaded with { Geometry = loaded.Geometry with { V2 = packed } }
                    };
                }

                (NativeBattleVector Vertex, short? W) Vertex(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
                    {
                        throw new InvalidDataException("Windowed mesh selected vertex leaves its current packed rows.");
                    }
                    _ = Dword(context, 4);
                    if ((flags & 0x2000) == 0)
                    {
                        ReadOnlySpan<byte> row = Read(checked(8 + offset), 8);
                        return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..])), BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
                    }
                    return readSelectedVertex(offset);
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (mesh.IsDefault || offset < 0 || length < 0 || offset > mesh.Length - length)
            {
                throw new InvalidDataException("Windowed mesh mesh leaves its original geometry.");
            }
            return mesh.Read(offset, length);
        }
        NativeBattleTextureWindow Window(int offset) => new(unchecked((short)Word(context, offset)), unchecked((short)Word(context, offset + 2)), unchecked((short)Word(context, offset + 4)), unchecked((short)Word(context, offset + 6)));
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
    private static int? OptionalInt(NativeBattleEffectArena context, int offset)
    {
        (byte[] data, bool[] known) = context.ReadSnapshot(offset, 4);
        return known.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(data) : null;
    }
    private static ushort? OptionalWord(NativeBattleEffectArena context, int offset)
    {
        (byte[] data, bool[] known) = context.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadUInt16LittleEndian(data) : null;
    }
}
