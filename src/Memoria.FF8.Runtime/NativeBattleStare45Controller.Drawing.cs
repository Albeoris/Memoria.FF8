using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStare45DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal = 0x1D8E054, ushort Flags = 0) : NativeBattleStare45Operation;

public sealed record NativeBattleStare45MeshContext104Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context104, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleStare45MeshContext104
{
    public static NativeBattleStare45MeshContext104Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2, uint? selectedVerticesAddress = null, Func<int, (NativeBattleVector Vertex, short? W)>? readSelectedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = U(context, 0x14);
        if (U(context, 0) != mesh.Address || flags is not (0x2030 or 0x20F0) || depthShift > 31 || selectedVerticesAddress is null || readSelectedVertex is null || U(context, 4) != selectedVerticesAddress)
        {
            throw new InvalidDataException("Stare mesh requires its actual104-byte selected-vertex context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Stare mesh requires its current geometry owner."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Stare mesh requires its current arithmetic owner."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Stare mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Stare mesh requires its current FAR owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Stare mesh requires its current FAR controls.");
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
            if (group is not (3 or 7))
            {
                throw new NotSupportedException("Stare mesh primitive group is unsupported.");
            }
            bool quad = (group & 1) != 0;
            bool gouraud = group >= 4;
            bool textured = group is 3 or 7;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, lengths[group]).ToArray();
                NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[quad ? 4 : 3] : null;
                void Capture(int corner, NativeBattleVector vertex)
                {
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, vertex), projection);
                    }
                }
                for (int corner = 0; corner < 3; corner++)
                {
                    var vertex = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex.Vertex, vertex.W);
                    Capture(corner, vertex.Vertex);
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
                        Capture(3, vertex.Vertex);
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
                    throw new NotSupportedException("Stare selected programme requires its zero UV offsets.");
                }
                NativeBattleTextureWindow full = new(0, 0, 256, 256);
                NativeBattleEsunaMeshContext packetContext = new(flags, rgb, cue, unchecked((int)U(context, 16)), u, v, full, full, tableBaseBucket, depthShift);
                NativeBattleGfGeometryBatch batch = group switch
                {
                    3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, ReadCorner, packetContext, constants, allocated),
                    7 => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, ReadCorner, packetContext, constants, allocated),
                    _ => throw new InvalidDataException("Stare selected mesh group changed during emission.")
                };
                if (batch.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Stare requires its actual ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Stare requires its actual ZSF3."));
                    int depth = unchecked((int)registers.Geometry.Otz!.Value);
                    context = Put(context, 0x38, unchecked((uint)depth));
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        uint actualCue = U(context, 12);
                        registers = gouraud ? NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, textured, flags, actualCue, far, constants) : registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(face), actualCue, far, constants).Registers };
                    }
                    depth = Math.Max(0, unchecked(depth + (int)U(context, 16)));
                    context = Put(context, 0x38, unchecked((uint)depth));
                    int bucket = unchecked(tableBaseBucket + (depth >> depthShift));
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
                    {
                        if (attempt.Primitive is not { } primitive)
                        {
                            attempts.Add(attempt);
                            continue;
                        }
                        attempts.Add(attempt with { Primitive = null });
                        byte[] mode = new byte[12];
                        mode[3] = 2;
                        BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000020);
                        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[12];
                        mask.AsSpan(3).Fill(NativeBattleGfPacketByteState.Known);
                        attempts.Add(new(checked(allocated + batch.AllocationBytes), mode.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, [0x02000000, 0xE1000020, 0], [0, 0, 0, 0])));
                        NativeBattleGeometryPrimitive polygon = primitive with { Bucket = bucket };
                        if (pilotIdentity is { } identity && world is not null && ((polygon.Words[1] >> 24) & 0xE0) == 0x20)
                        {
                            polygon = polygon with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) };
                        }
                        attempts.Add(attempt with { Primitive = polygon, WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, attempt.Data.Length).ToImmutableArray() });
                    }
                    allocated = checked(allocated + batch.AllocationBytes + 12);
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
                        throw new InvalidDataException("Stare face leaves its selected packed vertex bank.");
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
