using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroDrawResult(NativeBattleHeroFrame Frame, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch, int? CursorStart);

public static class NativeBattleHeroDrawing
{
    public static NativeBattleHeroDrawResult DrawAct(NativeBattleHeroFrame frame, NativeBattleHeroAct operation, NativeBattleHeroResources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePackedMatrix32 currentView, NativeBattleFileStaging staging, uint temporary)
    {
        if (NativeBattleHeroGeometry.IsHidden(operation.Node))
        {
            return new(frame, staging, new([], 0), null);
        }
        Require(frame, resources);
        RequireTemporary(staging, temporary, 88);
        NativeBattleGfAnimationSource source = operation.Source;
        NativeBattleGfAnimationSource expected = source.Address == resources.Main.Address ? resources.Main : source.Address == resources.Orbit.Address ? resources.Orbit : throw new NotSupportedException("The Hero draw requires its original main or orbit source.");
        if (source.Data.IsDefault || !source.Data.AsSpan().SequenceEqual(expected.Data.AsSpan()))
        {
            throw new InvalidDataException("The Hero draw source differs from its admitted original resource.");
        }
        NativeBattleHeroVertexSelection selection = NativeBattleHeroGeometry.Select(source, operation.Node);
        if (selection.RequiresMorph || selection.FirstShape != 0 || selection.SecondShape != 0 || selection.Count != 25)
        {
            throw new NotSupportedException("The Hero draw selector leaves its admitted original single-shape geometry.");
        }
        ImmutableArray<byte> vertices = NativeBattleHeroGeometry.SelectVertices(source, selection, default);
        uint mesh = checked(source.Address + (uint)selection.GeometryOffset);
        uint selected = checked(source.Address + (uint)selection.SourceOffset!.Value);
        uint flags = operation.Node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        staging = Store(staging, temporary, 0, mesh);
        staging = Store(staging, temporary, 4, selected);
        staging = Store(staging, temporary, 12, unchecked((uint)(int)operation.Node.Scalar36));
        staging = Store(staging, temporary, 24, unchecked((uint)operation.Context.Depth));
        staging = Store(staging, temporary, 28, flags);
        if (operation.Node.Scalar36 != 0)
        {
            staging = Store(staging, temporary, 8, operation.Node.Color32);
        }
        currentView.Validate();
        NativeBattleActionMatrix view = currentView.ReadMatrix();
        NativeBattleGeometryRegisterStep step = Registers(frame.Memory);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, view, true, false, Padding(currentView)) };
        NativeBattleVector point = NativeBattleHeroGeometry.ViewInput(operation.Node, operation.Context);
        short? word6 = (operation.Node.Header & 0x200) != 0 ? operation.Context.AnchorWord6 : null;
        step = NativeBattleGeometry.LoadVector(step, 0, point, word6);
        step = NativeBattleGeometry.Transform(step, false);
        NativeBattleActionMatrix matrix = NativeBattleHeroGeometry.Compose(operation.Node, operation.Context, view);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, null) };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
        frame = PublishFarColor(frame, staging, temporary + 8);
        NativeBattleModuleMeshTemporaryResult result = NativeBattleModulePackets.EmitRetainedBatch7043B0(staging, temporary, mesh, source.Data.Slice(selection.GeometryOffset, source.Data.Length - selection.GeometryOffset), matrix, projection, new(flags, operation.Node.Color32, operation.Node.Scalar36, null, null, operation.Context.Depth, 17, 2), colors, offset => ReadVertex(vertices, offset), operation.PilotIdentity);
        return Complete(frame, result.Staging, result.Batch);
    }

    public static NativeBattleHeroDrawResult DrawFan(NativeBattleHeroFrame frame, NativeBattleHeroFan operation, NativeBattleHeroResources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePackedMatrix32 currentView, NativeBattleFileStaging staging, uint temporary)
    {
        Require(frame, resources);
        RequireTemporary(staging, temporary, 136);
        if (operation.MeshAddress != frame.Profile.FanSource || operation.Index is < 0 or >= 16)
        {
            throw new InvalidDataException("The Hero fan requires its actual module's original mesh address.");
        }
        currentView.Validate();
        if (operation.Index != 0)
        {
            RequireHeader(0x20, operation.MeshAddress);
            RequireHeader(0x28, 0);
            RequireHeader(0x2C, unchecked((uint)operation.Fade));
            RequireHeader(0x38, unchecked((uint)operation.Depth));
            RequireHeader(0x3C, 0xF0);
        }
        NativeBattleActionMatrix matrix = NativeBattleHeroGeometry.FanMatrix(operation, currentView.ReadMatrix());
        staging = StoreMatrix(staging, temporary, matrix);
        if (operation.Index == 0)
        {
            staging = Store(staging, temporary, 0x20, operation.MeshAddress);
            staging = Store(staging, temporary, 0x28, 0);
            staging = Store(staging, temporary, 0x2C, unchecked((uint)operation.Fade));
            staging = Store(staging, temporary, 0x38, unchecked((uint)operation.Depth));
            staging = Store(staging, temporary, 0x3C, 0xF0);
        }
        staging = Store(staging, temporary, 0x78, unchecked((uint)operation.Radius));
        staging = Store(staging, temporary, 0x7C, unchecked((uint)operation.Radius));
        staging = Store(staging, temporary, 0x80, unchecked((uint)operation.Radius));
        //7043B0 supplies the mesh's own vertex pointer when flag2000 is absent.
        staging = Store(staging, temporary, 0x24, checked(operation.MeshAddress + 8));
        NativeBattleGeometryRegisterStep step = Registers(frame.Memory);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, null) };
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
        frame = PublishFarColor(frame, staging, temporary + 0x28);
        NativeBattleModuleMeshTemporaryResult result = NativeBattleModulePackets.EmitRetainedBatch7043B0(staging, temporary + 0x20, operation.MeshAddress, resources.Fan, matrix, projection, new(0xF0, 0, operation.Fade, null, null, operation.Depth, 17, 2), colors, offset => ReadVertex(resources.Fan, checked(8 + offset)), operation.PilotIdentity);
        return Complete(frame, result.Staging, result.Batch);

        void RequireHeader(uint offset, uint expected)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(checked(temporary + offset), 4)) != expected)
            {
                throw new InvalidDataException("The Hero fan requires its actual first-iteration header in the active shared allocation.");
            }
        }
    }

    private static NativeBattleHeroDrawResult Complete(NativeBattleHeroFrame frame, NativeBattleFileStaging staging, NativeBattleGfGeometryBatch batch)
    {
        int first = frame.State.PacketCursor ?? throw new NotSupportedException("The Hero draw requires its current private packet cursor.");
        int end = checked(first + batch.AllocationBytes);
        if (first < 0 || end > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("The Hero packet allocation leaves its actual effect arena.");
        }
        return new(frame with { State = frame.State with { PacketCursor = end } }, staging, batch, first);
    }

    private static NativeBattleHeroFrame PublishFarColor(NativeBattleHeroFrame frame, NativeBattleFileStaging staging, uint rgb)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("The Hero mesh requires its canonical far-color controls.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        (byte[] bytes, bool[] known) = staging.ReadSnapshot(rgb, 3);
        registers = registers with { FarRed = known[0] ? (uint)bytes[0] << 4 : null, FarGreen = known[1] ? (uint)bytes[1] << 4 : null, FarBlue = known[2] ? (uint)bytes[2] << 4 : null };
        return frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers } } };
    }

    private static NativeBattleFileStaging StoreMatrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        staging = staging.Write(address, rotation).MarkUnknown(address + 18, 2);
        staging = Store(staging, address, 20, unchecked((uint)matrix.X));
        staging = Store(staging, address, 24, unchecked((uint)matrix.Y));
        return Store(staging, address, 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleFileStaging Store(NativeBattleFileStaging staging, uint address, uint offset, uint value) => staging.Write(checked(address + offset), BitConverter.GetBytes(value));

    private static NativeBattleVector ReadVertex(ImmutableArray<byte> bytes, int offset)
    {
        if (bytes.IsDefault || offset < 0 || offset > bytes.Length - 6)
        {
            throw new InvalidDataException("The Hero vertex read leaves its actual selected resource.");
        }
        ReadOnlySpan<byte> xyz = bytes.AsSpan().Slice(offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
    }

    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? throw new NotSupportedException("The Hero draw requires its retained GTE matrix controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("The Hero draw requires its retained GTE input/output lanes."));

    private static void RequireTemporary(NativeBattleFileStaging staging, uint address, uint length)
    {
        uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        if (address < NativeBattleTemporaryMemory.Address || (address & 3) != 0 || (ulong)address + length != cursor)
        {
            throw new InvalidDataException("The Hero draw requires its actual active temporary allocation.");
        }
    }

    private static void Require(NativeBattleHeroFrame frame, NativeBattleHeroResources resources)
    {
        frame.State.Validate();
        resources.Validate();
        if (frame.Effect.Binding.Effect != frame.State.Effect || frame.Effect.Binding.Entry != frame.Profile.Entry || resources.Effect != frame.State.Effect)
        {
            throw new InvalidDataException("The Hero draw requires its actual selected module and original resources.");
        }
    }
}
