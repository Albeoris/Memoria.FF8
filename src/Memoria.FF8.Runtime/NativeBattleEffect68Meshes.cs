using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    internal static NativeBattleEffect68Frame BindMeshes(NativeBattleEffect68Frame frame, NativeBattleEffect68Resources resources)
    {
        (NativeBattleEffect68State state, NativeBattleEffectMemory memory) = frame.State.BindMeshSources(frame.Memory, resources);
        return frame with { State = state, Memory = memory };
    }

    internal static (NativeBattleEffect68Frame Frame, NativeBattlePackedMatrix32 Matrix) MeshMatrix(NativeBattleEffect68Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0);
        foreach ((char axis, int offset) in new (char, int)[] { ('Y', 0x46), ('X', 0x44), ('Z', 0x48) })
        {
            NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, frame.Staging ?? throw new NotSupportedException("Effect68 matrix rotation requires its current temporary memory."), packed, axis, S(task, offset));
            frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
            packed = rotated.Matrix;
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(packed.ReadMatrix(), unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))) with { X = S(task, 0x1C), Y = S(task, 0x1E), Z = S(task, 0x20) };
        ushort? padding = packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadUInt16LittleEndian(packed.Bytes.AsSpan().Slice(18)) : null;
        return (frame, NativeBattlePackedMatrix32.FromMatrix(matrix, padding));
    }

    internal static NativeBattleEffect68Frame DrawMesh(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs, bool spinner = false)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        if (task.ReadDword(0x4C) is 0x1591C44 or 0x159202C)
        {
            frame = BindMeshes(frame, resources);
        }
        (frame, NativeBattlePackedMatrix32 matrix) = MeshMatrix(frame, task);
        frame = LoadMatrix(frame, matrix);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(task.ReadDword(0x4C))).Write(8, BitConverter.GetBytes(task.ReadDword(0x40))).Write(12, BitConverter.GetBytes((int)S(task, 0x50))).Write(0x1C, BitConverter.GetBytes(0xF0u));
        NativeBattleStaticMeshResource mesh = resources.ReadCurrentMesh(frame.Memory, task.ReadDword(0x4C));
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(68, task) with { ResourceGeneration = mesh.Address };
        NativeBattleStaticMeshContext88Step drawn = spinner ? DrawSpinner(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, identity) : NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, pilotIdentity: identity);
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect68DrawBatch(drawn.Batch, Cursor));
        return Release(frame, 88);
    }

    public static NativeBattleStaticMeshContext88Step DrawSpinner(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (mesh.Address != 0x15934CC || mesh.Bytes.Length != 136 || BinaryPrimitives.ReadInt32LittleEndian(context.Read(0x1C, 4)) != 0xF0)
        {
            throw new NotSupportedException("Effect68 spinner requires its selected two-face F0 mesh.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect68 spinner requires its current geometry registers."), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect68 spinner requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect68 spinner requires its current renderer.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect68 spinner requires its current far color.")).PublishFarColor45DDA0(context.Read(8, 1)[0], context.Read(9, 1)[0], context.Read(10, 1)[0]);
        context = CWord(CWord(context, 4, mesh.Address + 8), 0x18, 0);
        uint rgb = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(8, 4));
        int fade = BinaryPrimitives.ReadInt32LittleEndian(context.Read(12, 4));
        short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Effect68 spinner requires its current ZSF4.");
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor += 4;
            context = CWord(context, 0x20, mesh.Address + (uint)cursor);
            if (count != (group == 3 ? 2 : 0))
            {
                throw new InvalidDataException("Effect68 spinner leaves its admitted primitive groups.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, 24).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags!.Value;
                context = CWord(context, 0x30, flags);
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), flags);
                }
                if ((flags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = CWord(context, 0x24, registers.Geometry.Mac0!.Value);
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector vertex, short w) = Vertex(3);
                        registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, vertex, w), projection, controls.Dqa, controls.Dqb);
                        corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, zsf4);
                    }
                    return corners[corner];
                }
                NativeBattleGfGeometryAttempt attempt = NativeBattleScanAltMeshPackets.EmitFace8DF200(face, ReadCorner, new(0xF0, rgb, fade, null, null, 0, 17, 2), zsf4, constants, allocated);
                if (attempt.Primitive is { } primitive)
                {
                    context = CWord(context, 0x2C, registers.Geometry.Otz!.Value);
                    registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(face), unchecked((uint)fade), far, constants).Registers };
                    attempt = attempt with { Primitive = primitive with { Bucket = unchecked(17 + ((int)registers.Geometry.Otz.Value >> 2)) } };
                    if (pilotIdentity is { } identity && world is not null)
                    {
                        attempt = attempt with { Primitive = attempt.Primitive! with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + faceIndex) }, world[0], world[1], world[2], world[3], 4) } };
                    }
                    allocated += 44;
                }
                attempts.Add(attempt);
                cursor += 24;
                context = CWord(context, 0x20, mesh.Address + (uint)cursor);

                (NativeBattleVector Vertex, short W) Vertex(int corner)
                {
                    int offset = 8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 4)));
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, vertex), projection);
                    }
                    return (vertex, BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 6)));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far, DrawMode2795BD0 = 0xE1000220 } }, context, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleEffectArena CWord(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));

    public static NativeBattleEffect68Frame MorphVertices(NativeBattleEffect68Frame frame, uint firstAddress, uint secondAddress, uint destination, short factor, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        frame = BindMeshes(frame, resources);
        ImmutableArray<byte> first = resources.ReadVertexBank(firstAddress);
        ImmutableArray<byte> second = resources.ReadVertexBank(secondAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan().Slice(4));
        NativeBattleScanProcess scan = frame.Memory.Scan ?? throw new NotSupportedException("Effect68 morph requires its current shared mutable mesh owner.");
        NativeBattleScanByteOwner mesh = scan.MutableMeshes[destination];
        (frame, uint temporary) = Allocate(frame, 8);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 8).Write(0, BitConverter.GetBytes(4096 - factor)).Write(4, BitConverter.GetBytes((int)factor)), 8);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int index = 0; index < count; index++)
        {
            int offset = 8 + index * 8;
            registers = Weighted(first, 4096 - factor, false);
            registers = Weighted(second, factor, true);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect68 morph requires its current saturated IR.")));
                mesh = mesh.WriteWord(offset + axis * 2, unchecked((ushort)value));
            }

            NativeBattleGeometryRegisterStep Weighted(ImmutableArray<byte> source, int weight, bool accumulate)
            {
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)weight) } };
                registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset))), unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset + 2))), unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(offset + 4))));
                return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
            }
        }
        frame = Publish(frame, registers);
        scan = frame.Memory.Scan ?? throw new NotSupportedException("Effect68 morph lost its current shared process.");
        frame = frame with { Memory = frame.Memory with { Scan = scan with { MutableMeshes = scan.MutableMeshes.SetItem(destination, mesh) } } };
        return Release(frame, 8);
    }
}
