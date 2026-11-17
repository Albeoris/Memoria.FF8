using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleBreath129Frame Frame, NativeBattleGraphicsState Graphics, NativeBattleBreath129BodySource Source) ReadNativeBreath129Body(NativeBattleBreath129Frame frame, NativeBattleGraphicsState graphics, uint row100, bool publish)
    {
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Breath body access requires its actual current shared staging owner.");
        BattleModelOwner[] actors = graphics.ActorModels.Entries.Where(entry => RowAddress(entry.State) == row100).Select(entry => entry.Owner).ToArray();
        if (actors.Length != 1)
        {
            throw new NotSupportedException("Breath body row requires one actual current resource owner.");
        }
        BattleModelOwner actor = actors[0];
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
        NativeFileStaging = staging;
        NativeEffectMemory = frame.Memory;
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics with { OriginalEffect = frame.Effect }, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Breath body access requires its actual current skeleton source.");
        if (publish)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(model, pose, temporary);
            staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            model = prepared.Model;
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model) };
            frame = frame with { Staging = staging };
            NativeFileStaging = staging;
        }
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleBodyBoneMatrices.Require(model, pose).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, staging, NativeBattleEffectArena.Clear(), 0);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        int alignment = pointer.Cpu is { } cpu ? cpu.Offset & 3 : (int)((pointer.StagingAddress ?? throw new NotSupportedException("Breath body access requires its actual geometry owner.")) & 3);
        long identity = pointer.Cpu is { } logical && (logical.ResourceOnly || logical.EnemyResourceId is not null) ? unchecked(NativeBattlePilotVisualLifetimes.EpochFor(pointer.CpuPublication ?? (NativeModelCpuResources ?? throw new NotSupportedException("Breath body access requires its current resource owner.")).GetResourceBank(logical)) << 32) : unchecked((long)(pointer.Cpu?.TryPhysicalAddress() ?? pointer.StagingAddress ?? throw new NotSupportedException("Breath body access requires its source identity.")) << 32);
        NativeBattleBreath129BodySource source = new(row100, ReadGeometry, bones) { GeometryAlignment = (byte)alignment, GeometryIdentity = identity };
        return (frame, graphics, source);

        NativeBattleEffectArena ReadGeometry(int offset, int length)
        {
            byte[] bytes = ReadNativeDeformedGeometry(bridge, pointer, offset, length);
            return NativeBattleEffectArena.Clear().Write(0, bytes);
        }

        uint? RowAddress(NativeBattleGraphicsModel candidate)
        {
            return candidate.ActiveStagedResourceRow100 is null && candidate.ActiveResourceRow100 is null ? null : NativeModelResourceAddress100(candidate);
        }
    }
}
