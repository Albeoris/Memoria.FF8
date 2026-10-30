using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect69Frame Frame) DrawNativeEffect69Caster(NativeBattleGraphicsState graphics, NativeBattleEffect69Frame frame, NativeBattleEffect69DrawCaster draw, Func<NativeBattleEffect69Frame, NativeBattleGfGeometryBatch, NativeBattleEffect69Frame> deliver)
    {
        uint delta = unchecked(draw.ModelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect69 caster requires an actual physical model row.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect69 caster requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect69 caster requires its current camera.");
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect69 caster requires its current projected capacity.")) * 8);
        if (draw.ScratchAddress != checked(0x196B018u - (uint)capacity))
        {
            throw new InvalidDataException("Effect69 caster changed its current physical scratch pointer.");
        }
        (frame, uint temporaryAddress) = NativeBattleEffect69Controller.Allocate(frame, 96);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect69 caster requires its current world matrix."));
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        NativeBattleEffectArena temporary = NativeBattleEffect69Controller.ReadBytes(frame, temporaryAddress, 96);
        temporary = Put(temporary, 4, draw.ScratchAddress);
        temporary = temporary.Write(12, BitConverter.GetBytes((short)0)).Write(14, BitConverter.GetBytes((short)0)).Write(16, BitConverter.GetBytes((short)320)).Write(18, BitConverter.GetBytes((short)216));
        temporary = Put(temporary, 0x14, model.Color40 ?? throw new NotSupportedException("Effect69 caster requires its full current model color."));
        temporary = Put(temporary, 0x18, model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect69 caster requires its current object mask."));
        temporary = Put(Put(temporary, 0x4C, unchecked((uint)draw.Threshold)), 0x58, 0).MarkUnknown(0x54, 4);
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging!, temporary, temporaryAddress);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        uint geometryAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect69 caster requires addressed current geometry.");
        NativeBattleEffect69CasterDrawingStep drawn = NativeBattleEffect69CasterDrawing.Draw(frame.Memory, temporary, ReadGeometry, geometryAddress, NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), camera.CapturePackedView1D97778(), camera.Projection ?? throw new NotSupportedException("Effect69 caster requires its current projection."), frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect69 caster requires its current GTE controls."), NativeBattleTemporaryMemory.ReadView(frame.Staging!, draw.ScratchAddress, capacity), capacity, pilotIdentity: draw.PilotIdentity is { } identity ? identity with { Primitive = actor, ResourceGeneration = geometryAddress } : null);
        frame = NativeBattleEffect69Controller.WriteBytes(frame with { Memory = drawn.Memory }, temporaryAddress, drawn.Temporary96, 96);
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, draw.ScratchAddress, drawn.Scratch, capacity) };
        frame = deliver(frame, drawn.Batch);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
        graphics = graphics with { Models = graphics.Models.SetItem(actor, restored.Model) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88), 88) };
        return (graphics, NativeBattleEffect69Controller.Release(frame, 96));

        NativeBattleEffectArena ReadGeometry(uint address, int length)
        {
            long offset = (long)address - geometryAddress;
            if (offset < 0 || offset > int.MaxValue)
            {
                throw new InvalidDataException("Effect69 caster geometry read leaves its current source.");
            }
            return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, pointer, checked((int)offset), length));
        }

        static NativeBattleEffectArena Put(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    }
}
