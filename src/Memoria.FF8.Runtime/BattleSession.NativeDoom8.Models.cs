using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleDoom8Frame ExecuteDoom8Model(NativeBattleDoom8Frame frame, NativeBattleDoom8Operation operation, NativeBattleDoom8Resources resources, NativeBattleCameraState camera, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, bool, NativeBattleDoom8Frame> deliver)
    {
        switch (operation)
        {
            case NativeBattleDoom8ReaperInitializeModel initialize:
                if (initialize.ResourceAddress != resources.ReaperModel.Address)
                {
                    throw new NotSupportedException("Doom model initialization requires its actual selected resource.");
                }
                return NativeBattleDoom8Models.Initialize(frame, initialize.TaskAddress, resources.ReaperModel, initialize.Argument3, initialize.DefaultColor44);
            case NativeBattleDoom8ReaperSelect select:
                return NativeBattleDoom8Models.Select(frame, select.TaskAddress, resources.ReaperModel, select.Animation);
            case NativeBattleDoom8ReaperAdvanceLoop advance:
                return NativeBattleDoom8Models.Advance(frame, advance.TaskAddress, resources.ReaperModel, true).Frame;
            case NativeBattleDoom8ReaperPause5084E0:
                return frame;
            case NativeBattleDoom8ReaperPrepareAppearance appearance:
                if (appearance.ScratchAddress != NativeBattleDoom8ReaperAppearance.ScratchAddress || appearance.VertexScratchAddress != 0x275ABF0 || appearance.DescriptorAddress != 0x275F9E0)
                {
                    throw new NotSupportedException("Doom appearance requires its actual retained scratch owners.");
                }
                return NativeBattleDoom8ReaperAppearance.Initialize8CD020(frame, appearance.ModelAddress);
            case NativeBattleDoom8ReaperObserveBone502170 anchor:
                uint address = checked(anchor.ModelAddress - 0x30);
                NativeBattleEffectOwnedModelState model = NativeBattleDoom8Models.Read(frame, address);
                NativeBattleModulePoolTask anchoredTask = NativeBattleDoom8Controller.Read(frame, address);
                NativeBattleActionMatrix anchoredWorld = ReadDoom8TaskMatrix(anchoredTask, 0x70).ReadMatrix();
                NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveEffectOwnedBoneAnchor502170(resources.ReaperModel.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Doom anchor requires its current effect-owned pose."), anchoredWorld, anchor.Bone, anchor.Distance);
                return ObserveDoom8Anchor(frame, producer);
            case NativeBattleDoom8ReaperDrawModel draw:
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Doom model drawing requires its current projection.");
                NativeBattleGraphicsDefinition graphicsDefinition = definition.NativeGraphics ?? throw new NotSupportedException("Doom model drawing requires its original graphics rules.");
                NativeBattleDoom8ModelDrawInputs inputs = new(camera.CapturePackedView1D97778(), projection, graphicsDefinition.Rules.ShadowSource ?? throw new NotSupportedException("Doom model drawing requires its original shadow template."), graphicsDefinition.PrimitiveCapabilities ?? throw new NotSupportedException("Doom model drawing requires its actual primitive capabilities."), graphicsDefinition.Rules.SummonColors ?? throw new NotSupportedException("Doom model drawing requires its interpolation constants."));
                NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, draw.TaskAddress);
                bool appearanceDraw = (task.ReadByte(0x26) & 8) != 0;
                if (appearanceDraw)
                {
                    NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(unchecked((short)task.ReadWord(0x3C)), unchecked((short)task.ReadWord(0x3E)), unchecked((short)task.ReadWord(0x40))));
                    world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = unchecked((short)task.ReadWord(0x4C)), Y = unchecked((short)task.ReadWord(0x4E)), Z = unchecked((short)task.ReadWord(0x50)) };
                    frame = NativeBattleDoom8Controller.Write(frame, draw.TaskAddress, task.WriteBytes(0x70, NativeBattlePackedMatrix32.FromMatrix(world, null).Bytes.AsSpan()).MarkPayloadUnknown(0x82, 2));
                }
                uint first = frame.State.ReadDword(0x2758504);
                NativeBattleEffectOwnedModelState owned = NativeBattleDoom8Models.Read(frame, draw.TaskAddress);
                NativeBattleDoom8ModelDrawStep drawn = appearanceDraw ? NativeBattleDoom8ModelDrawing.Draw8CC6B0(frame, RequireDoom8Staging(frame), draw.TaskAddress, owned, resources.ReaperModel, inputs) : NativeBattleDoom8ModelDrawing.Draw8CC4D0(frame, RequireDoom8Staging(frame), draw.TaskAddress, owned, resources.ReaperModel, inputs);
                frame = drawn.Frame with { Staging = drawn.Staging };
                uint end = frame.State.ReadDword(0x2758504);
                frame = deliver(frame with { State = frame.State.WriteDword(0x2758504, first) }, drawn.Batch, false);
                if (frame.State.ReadDword(0x2758504) != end)
                {
                    throw new InvalidDataException("Doom model packet publication differs from its actual private cursor advancement.");
                }
                return frame;
            default:
                throw new NotSupportedException("Doom operation requires its concrete native service.");
        }
    }

    private NativeBattleDoom8Frame ObserveDoom8Anchor(NativeBattleDoom8Frame frame, NativeBattleBoneAnchorProducer producer)
    {
        NativeFileStaging = RequireDoom8Staging(frame);
        NativeBattleEffectMemory memory = ObserveNativeBoneAnchor(frame.Memory, producer);
        return frame with { Memory = memory, Staging = NativeFileStaging };
    }

    private NativeBattleDoom8Frame ObserveDoom8Basis(NativeBattleDoom8Frame frame, NativeBattleVector direction, NativeBattleVector up)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireDoom8Staging(frame), 32);
        NativeBattleGeometryRegisterStep cross = NativeBattleGeometry.LoadDiagonal45DC70(new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Doom basis requires its geometry controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Doom basis requires its data registers.")), up);
        cross = NativeBattleGeometry.LoadSignedIr45DC40(cross, direction);
        cross = NativeBattleGeometry.OuterProduct45E820(cross);
        byte[] vectors = new byte[18];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(index * 2), unchecked((short)cross.Colors.Ir[index]!.Value));
        }
        short[] source = [up.X, up.Y, up.Z, direction.X, direction.Y, direction.Z];
        for (int index = 0; index < source.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vectors.AsSpan(6 + index * 2), source[index]);
        }
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Release5082D0(allocation.Staging.Write(allocation.Address, vectors), 32);
        return frame with { Memory = frame.Memory with { GeometryRegisters = cross.Geometry, ScriptedColorRegisters = cross.Colors }, Staging = staging };
    }

    private static NativeBattlePackedMatrix32 ReadDoom8TaskMatrix(NativeBattleModulePoolTask task, int offset)
    {
        task.RequirePayloadRange(offset, 32);
        task.ValidateUnknownPayloadRanges();
        ImmutableArray<byte> bytes = task.ReadRawPayloadSpan().Slice(offset - 12, 32).ToArray().ToImmutableArray();
        ImmutableArray<bool> known = Enumerable.Range(0, 32).Select(index => task.ReadUnknownPayloadRanges() is not { } ranges || !ranges.Any(range => offset + index >= range.Offset && offset + index < range.Offset + range.Length)).ToImmutableArray();
        return new(bytes, known);
    }
}
