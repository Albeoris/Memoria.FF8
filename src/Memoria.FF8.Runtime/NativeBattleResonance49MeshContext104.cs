using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResonance49MeshContext104Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context104, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleResonance49MeshContext104
{
    public static NativeBattleResonance49MeshContext104Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2, uint? selectedVerticesAddress = null, Func<int, (NativeBattleVector Vertex, short? W)>? readSelectedVertex = null)
    {
        uint flags = U(context, 0x14);
        if (U(context, 0) != mesh.Address || flags is not (0x2030 or 0x20F0) || depthShift > 31 || selectedVerticesAddress is null || readSelectedVertex is null || U(context, 4) != selectedVerticesAddress)
        {
            throw new InvalidDataException("Resonance mesh requires its actual104-byte selected-vertex context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Resonance mesh requires its current geometry owner."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Resonance mesh requires its current arithmetic owner."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Resonance mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Resonance mesh requires its current FAR owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Resonance mesh requires its current FAR controls.");
        (byte[] rgbBytes, bool[] rgbKnown) = context.ReadSnapshot(8, 3);
        uint? rgb = rgbKnown.All(value => value) ? (uint)(rgbBytes[0] | rgbBytes[1] << 8 | rgbBytes[2] << 16) : null;
        far = far with { FarRed = rgbKnown[0] ? (uint)rgbBytes[0] << 4 : null, FarGreen = rgbKnown[1] ? (uint)rgbBytes[1] << 4 : null, FarBlue = rgbKnown[2] ? (uint)rgbBytes[2] << 4 : null };
        int vertexCount = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            context = Put(context, 0x2C, checked(mesh.Address + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (group is not (3 or 4 or 5 or 7))
            {
                throw new NotSupportedException("Resonance mesh primitive group is unsupported.");
            }
            bool quad = (group & 1) != 0;
            bool gouraud = group >= 4;
            bool textured = group is 3 or 7;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, lengths[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    var vertex = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex.Vertex, vertex.W);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                context = Put(context, 0x3C, firstFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = Put(context, 0x30, registers.Geometry.Mac0!.Value);
                }
                bool fourthLoaded = false;
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3 && !fourthLoaded)
                    {
                        var vertex = Vertex(3);
                        registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, vertex.Vertex, vertex.W), projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        fourthLoaded = true;
                    }
                    return projected[corner];
                }
                (byte[] cueBytes, bool[] cueKnown) = context.ReadSnapshot(12, 4);
                int? cue = cueKnown.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(cueBytes) : null;
                ushort u = textured ? W(context, 0x18) : (ushort)0;
                ushort v = textured ? W(context, 0x1A) : (ushort)0;
                if ((u | v) != 0)
                {
                    throw new NotSupportedException("Resonance selected programme requires its zero UV offsets.");
                }
                NativeBattleTextureWindow full = new(0, 0, 256, 256);
                NativeBattleEsunaMeshContext packetContext = new(flags, rgb, cue, unchecked((int)U(context, 16)), u, v, full, full, tableBaseBucket, depthShift);
                NativeBattleGfGeometryBatch batch = group switch
                {
                    3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, ReadCorner, packetContext, constants, allocated),
                    4 => NativeBattleRestorationMeshPackets.EmitG3_88F220(face, ReadCorner, packetContext, constants, allocated),
                    5 => NativeBattleRestorationMeshPackets.EmitG4_88F470(face, ReadCorner, packetContext, constants, allocated),
                    7 => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, ReadCorner, packetContext, constants, allocated),
                    _ => throw new InvalidDataException("Resonance selected mesh group changed during emission.")
                };
                if (batch.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Resonance requires its actual ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Resonance requires its actual ZSF3."));
                    int depth = unchecked((int)registers.Geometry.Otz!.Value);
                    context = Put(context, 0x38, unchecked((uint)depth));
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        uint actualCue = U(context, 12);
                        registers = gouraud ? NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, textured, flags, actualCue, far, constants) : registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(face), actualCue, far, constants).Registers };
                    }
                    depth = Math.Max(0, unchecked(depth + (int)U(context, 16)));
                    context = Put(context, 0x38, unchecked((uint)depth));
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        attempts.Add(attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = unchecked(tableBaseBucket + (depth >> depthShift)) } } : attempt);
                    }
                    allocated = checked(allocated + batch.AllocationBytes);
                }
                else
                {
                    attempts.AddRange(batch.Attempts);
                }
                cursor = checked(cursor + lengths[group]);
                context = Put(context, 0x2C, checked(mesh.Address + (uint)cursor));

                (NativeBattleVector Vertex, short? W) Vertex(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > checked(vertexCount * 8) - 8)
                    {
                        throw new InvalidDataException("Resonance face leaves its selected packed vertex bank.");
                    }
                    return readSelectedVertex(offset);
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context, new(attempts.ToImmutable(), allocated));
    }
    private static ushort W(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
