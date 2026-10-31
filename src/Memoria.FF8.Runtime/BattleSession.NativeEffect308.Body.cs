using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect308Frame DrawNativeEffect308Body(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, NativeBattleEffect308DrawOffsetBody669070 operation, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleGraphicsState currentGraphics = graphics;
        int actor = NativeEffect308Actor(graphics, operation.ModelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect308 requires its current addressed memory.");
        NativeBattlePoseSource source = inputs.ReadPoseSource(operation.ModelAddress);
        NativeBattleEffectArena offsets = ReadBytes(frame, operation.OffsetRowsAddress, checked(source.BoneCount * 16));
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect308 requires its current projection capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 152);
        NativeBattleEffect298BodyFrame body = new(frame.Memory, allocation.Staging, graphics.Models[actor], NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 152));
        body = NativeBattleEffect298BodyDrawing.DrawOuter(body, inputs.ReadView(frame), operation.OffsetRowsAddress, offsets, scratchAddress, new(Shadow, Prepare, DrawSource, Restore));
        graphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, body.Model) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(body.Staging, allocation.Address, body.Temporary152, 152);
        return frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 152) };

        NativeBattleEffect298BodyFrame Shadow(NativeBattleEffect298BodyFrame selected)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(selected.Staging, new(selected.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect308 shadow requires its current GTE matrix."), selected.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect308 shadow requires its current arithmetic registers.")), selected.Model, inputs.ReadProjection(frame), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect308 shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect308 shadow requires its original seeds."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect308 shadow requires its actual primitive capabilities."), 4112, 16, operation.PilotIdentity);
            NativeBattleEffect308Frame delivered = DeliverNativeEffect308Batch(ref currentGraphics, frame with { Memory = selected.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) }, shadow.Batch, 0x1D8E054);
            frame = frame with { Effect = delivered.Effect };
            return selected with { Memory = delivered.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(delivered.Staging!, 0x78) };
        }

        NativeBattleEffect298BodyFrame Prepare(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect308Frame prepared = PrepareNativeEffect308Pose(ref currentGraphics, frame with { Memory = selected.Memory, Staging = selected.Staging }, actor, source, offsets);
            return selected with { Memory = prepared.Memory, Staging = prepared.Staging!, Model = currentGraphics.Models[actor] };
        }

        NativeBattleEffect298BodyFrame DrawSource(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect308Frame current = frame with { Memory = selected.Memory, Staging = selected.Staging };
            (uint address, Func<int, int, byte[]> read) = ReadNativeEffect308Geometry(currentGraphics, current, actor);
            ImmutableArray<NativeBattlePackedMatrix32> bones = ReadNativeEffect308PackedBones(currentGraphics, current, actor, source);
            NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(selected.Staging, scratchAddress, scratchLength);
            NativeBattleEffect298BodyStep drawn = NativeBattleEffect298BodyDrawing.DrawBody(selected.Memory, selected.Temporary152, scratch, ReadGeometry, address, bones, offsets, inputs.ReadProjection(current), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect308 requires its current projection controls."), scratchAddress, scratchLength, 17, 0, operation.PilotIdentity);
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(selected.Staging, scratchAddress, drawn.VertexScratch, scratchLength);
            staged = NativeBattleTemporaryMemory.PublishView(staged, allocation.Address, drawn.Temporary152, 152);
            NativeBattleEffect308Frame delivered = DeliverNativeEffect308Batch(ref currentGraphics, current with { Memory = drawn.Memory, Staging = staged }, drawn.Batch, 0x1D8E054);
            frame = frame with { Effect = delivered.Effect };
            return selected with { Memory = delivered.Memory, Staging = delivered.Staging!, Temporary152 = drawn.Temporary152 };

            NativeBattleEffectArena ReadGeometry(uint selectedAddress, int length)
            {
                return NativeBattleEffectArena.Clear().Write(0, read(checked((int)(selectedAddress - address)), length));
            }
        }

        NativeBattleEffect298BodyFrame Restore(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect308Frame restored = RestoreNativeEffect308Pose(ref currentGraphics, frame with { Memory = selected.Memory, Staging = selected.Staging }, actor, source);
            return selected with { Memory = restored.Memory, Staging = restored.Staging!, Model = currentGraphics.Models[actor] };
        }
    }

}
