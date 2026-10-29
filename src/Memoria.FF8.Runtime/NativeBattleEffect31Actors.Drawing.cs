using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Actors
{
    private static NativeBattleEffect31Frame DrawSprite(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena point = ReadBytes(frame, address + 0x1C, 8);
        (byte[] padding, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, view, Point(point), w, 4096, unchecked((short)task.ReadWord(0x54))) };
        NativeBattleEffectArena temporary = Temporary(frame, allocation.Address, 180);
        temporary = Word(Dword(temporary, 0, task.ReadDword(0x4C)), 4, unchecked((short)task.ReadWord(0x50)));
        temporary = Word(temporary, 0x24, 0);
        frame = WriteBytes(frame, allocation.Address, temporary, 180);
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, temporary, resources.ReadSprite, projection with { View = projection.View ?? view.ReadMatrix() }, inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(31, task));
        frame = WriteBytes(frame with { Memory = drawn.Memory }, allocation.Address, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect31DrawBatch(drawn.Batch, CursorGlobal));
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
    }

    public static NativeBattleEffect31Frame DrawNode(NativeBattleEffect31Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 context, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 88);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena temporary = Temporary(frame, allocation.Address, 88);
        int geometry = SourceInt(source.Data, checked(8 + unchecked((short)node.GeometryIndex) * 4));
        uint meshAddress = checked(source.Address + (uint)geometry);
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(meshAddress);
        int count = SourceInt(source.Data, checked(geometry + 4));
        if (count < 0 || count > MorphScratchLength / 8)
        {
            throw new InvalidDataException("Effect31 node vertices exceed their actual interpolation owner.");
        }
        temporary = Dword(temporary, 0, meshAddress);
        int first = checked(geometry + 12 + count * 8 * node.Word40);
        int second = checked(geometry + 12 + count * 8 * node.Word42);
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        uint selectedAddress;
        if (interpolate)
        {
            NativeBattleEffectArena morph = ReadBytes(frame, MorphScratch, count * 8);
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = SourcePoint(source.Data, checked(first + vertex * 8));
                NativeBattleVector b = SourcePoint(source.Data, checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [(int)unchecked((ushort)a.X), (int)unchecked((ushort)a.Y), (int)unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)node.Scalar38), Ir = [(int)unchecked((ushort)b.X), (int)unchecked((ushort)b.Y), (int)unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, inputs.Colors);
                for (int axis = 0; axis < 3; axis++)
                {
                    morph = Word(morph, vertex * 8 + axis * 2, unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect31 morph requires its actual weighted IR result."))));
                }
            }
            frame = WriteBytes(frame, MorphScratch, morph, count * 8);
            selectedAddress = MorphScratch;
        }
        else
        {
            _ = SourceSlice(source.Data, selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        temporary = Dword(temporary, 4, selectedAddress);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix parent = context.ReadMatrix();
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            x = node.Vector8.X;
            y = node.Vector8.Y;
            z = node.Vector8.Z;
        }
        else
        {
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, parent, true, false, Padding(context) is ushort pad ? unchecked((short)pad) : null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null);
            registers = NativeBattleGeometry.Transform(registers, false);
            x = registers.Colors.Mac[0] ?? throw new NotSupportedException("Effect31 node requires its full X MAC.");
            y = registers.Colors.Mac[1] ?? throw new NotSupportedException("Effect31 node requires its full Y MAC.");
            z = registers.Colors.Mac[2] ?? throw new NotSupportedException("Effect31 node requires its full Z MAC.");
            local = NativeBattleGfMatrices.Multiply56C090(parent, local);
        }
        local = local with { X = unchecked(x + parent.X), Y = unchecked(y + parent.Y), Z = unchecked(z + parent.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale.X, scale.Y, scale.Z);
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, local, true, true, null) };
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        temporary = Dword(Dword(temporary, 0x1C, flags), 0x0C, unchecked((uint)(int)node.Scalar36));
        if (node.Scalar36 != 0)
        {
            temporary = Dword(temporary, 8, node.Color32);
        }
        frame = Publish(frame, registers);
        frame = WriteBytes(frame, allocation.Address, temporary, 88);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect31 mesh requires its current primitive process.");
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect31 mesh requires its current projection controls.");
        NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(mesh.Bytes, meshAddress, local, inputs.ReadProjection(frame), temporary, registers, process.ModelRendererRegisters ?? throw new NotSupportedException("Effect31 mesh requires its current far-color registers."), controls.Dqa, controls.Dqb, controls.Zsf4, inputs.Colors, zsf3: controls.Zsf3, selectedVerticesAddress: selectedAddress, readSelectedVertex: ReadVertex, pilotIdentity: pilotIdentity);
        frame = Publish(frame, drawn.Registers);
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } } };
        frame = WriteBytes(frame, allocation.Address, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect31DrawBatch(drawn.Batch, CursorGlobal));
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > checked(count * 8 - 8))
            {
                throw new InvalidDataException("Effect31 mesh index leaves its actual selected vertex bank.");
            }
            NativeBattleEffectArena point = interpolate ? ReadBytes(frame, checked(MorphScratch + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, SourceSlice(source.Data, checked(selected + offset), 8));
            (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
            return (Point(point), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect31Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect31 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect31 requires its current arithmetic registers."));
    private static NativeBattleEffectArena Temporary(NativeBattleEffect31Frame frame, uint address, int length) => CopyBytes(ReadBytes(frame, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattleEffect31Frame Publish(NativeBattleEffect31Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static int SourceInt(ImmutableArray<byte> source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(SourceSlice(source, offset, 4));
    private static NativeBattleVector SourcePoint(ImmutableArray<byte> source, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(SourceSlice(source, offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(SourceSlice(source, offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(SourceSlice(source, offset + 4, 2)));
    private static ReadOnlySpan<byte> SourceSlice(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Effect31 node read leaves its current programme source.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
