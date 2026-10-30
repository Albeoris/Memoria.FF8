using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect66Frame Frame) DrawNativeEffect66Body(NativeBattleGraphicsState graphics, NativeBattleEffect66Frame frame, uint temporaryAddress, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        int actor = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect66 body requires its current temporary memory.");
        NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect66 body requires its current physical skeleton.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect66 body requires its current world matrix."));
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect66 body requires its actual ordinary vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleEffectArena temporary = NativeDeformedBodyView(frame.Staging, temporaryAddress, 176);
        temporary = temporary.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216));
        temporary = NativeBattleEffectNodeContextMemory.Copy(NativeDeformedBodyView(frame.Staging, 0x1D969A8, 4), 0, temporary, 0x10, 4);
        temporary = temporary.Write(0x1C, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect66 body requires its actual model color."))).Write(0x20, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect66 body requires its current object mask.")).MeshMask124));
        temporary = model.Alpha7 is byte alpha ? temporary.Write(0x28, [alpha, alpha, alpha]) : temporary.MarkUnknown(0x28, 3);
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging, temporary, temporaryAddress);
        NativeBattleResourcePointer geometry = NativeDeformedGeometryPointer(bridge, false);
        uint geometryAddress = geometry.Cpu?.PhysicalAddress ?? geometry.StagingAddress ?? throw new NotSupportedException("Effect66 body requires its current addressed geometry.");
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleBodyBoneMatrices.Require(model, pose).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        NativeBattleEffect66BodyStep drawn = NativeBattleEffect66BodyDrawing.Draw(frame, geometryAddress, ReadGeometry, bones, (NativeCamera ?? throw new NotSupportedException("Effect66 body requires its current camera.")).CapturePackedView1D97778(), temporary, NativeDeformedBodyView(frame.Staging, scratchAddress, scratchLength), scratchLength, resources, inputs, pilotIdentity is { } identity ? identity with { ResourceGeneration = geometryAddress } : null);
        frame = drawn.Frame with { Staging = NativeBattleTemporaryMemory.PublishView(drawn.Frame.Staging ?? throw new InvalidDataException("Effect66 body lost its temporary memory."), scratchAddress, drawn.Scratch, scratchLength) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging, temporaryAddress, drawn.Temporary176, 176) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        return (graphics with { Models = graphics.Models.SetItem(actor, restored.Model) }, frame);

        NativeBattleEffectArena ReadGeometry(uint address, int length)
        {
            long relative = (long)address - geometryAddress;
            if (relative < 0 || relative > int.MaxValue)
            {
                throw new InvalidDataException("Effect66 body read leaves its current geometry.");
            }
            return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, geometry, checked((int)relative), length));
        }
    }
}
