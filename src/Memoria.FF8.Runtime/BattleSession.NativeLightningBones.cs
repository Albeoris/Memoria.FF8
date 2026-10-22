using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleFileStaging Staging, NativeBattleGraphicsState Graphics) PrepareNativeLightningBones(NativeBattleFileStaging staging, NativeBattleGraphicsState graphics, byte actor, bool compose)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Lightning requires its current caster skeleton.");
        if (compose)
        {
            _ = NativeBattleBodyBoneMatrices.Require(model, source);
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Lightning requires its current caster world matrix."));
            return (staging, graphics with { Models = graphics.Models.SetItem(actor, model) });
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, temporary);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        return (staging, graphics with { Models = graphics.Models.SetItem(actor, restored.Model) });
    }

    private NativeBattleEffectMemory ProjectNativeLightningBoneTrail(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeBattleGraphicsState graphics, byte actor, uint head, uint bankAddress, int bankLength)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Lightning bone projection requires its current caster skeleton.");
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(model, source);
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Lightning bone projection requires its current CPU resource owner.");
        NativeBattleResourcePointer pointer;
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            uint at = checked(staged + 4);
            if (NativeBattleEffectTaskMemory.TryReadPointer(effect.Pools, at, out NativeBattleResourcePointer? selected))
            {
                pointer = selected ?? throw new NotSupportedException("Lightning bone projection geometry pointer is NULL.");
            }
            else
            {
                pointer = (IsNativeEffectArenaAddress(at) ? memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(at, 4)) : staging.ReadResourcePointer(at)) ?? throw new NotSupportedException("Lightning bone projection geometry pointer is unobserved.");
            }
        }
        else
        {
            NativeBattleModelCpuResourceRow row = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Lightning bone projection requires its actual CPU row.")];
            pointer = new(row.Require(16), null, row.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Lightning bone projection requires its current camera.");
        memory = NativeBattleElectrocute275BoneProjection.ProjectChain(memory, head, bankAddress, bankLength, ReadBone, ReadGeometry, camera.CapturePackedView1D97778(), camera.Projection ?? throw new NotSupportedException("Lightning bone projection requires its current projection."), memory.ScriptedGraphics ?? throw new NotSupportedException("Lightning bone projection requires its current projection controls."));
        return memory;

        NativeBattlePackedMatrix32 ReadBone(int index)
        {
            if ((uint)index >= (uint)matrices.Length)
            {
                throw new InvalidDataException("Lightning selected bone leaves its current physical matrix bank.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[index], null);
        }

        byte[] ReadGeometry(int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int available && offset > available - length)
            {
                throw new InvalidDataException("Lightning geometry read leaves its current resource extent.");
            }
            if (pointer.Cpu is { } reference)
            {
                return NativeBattleModelEnemyHeap.Read(cpu, staging, reference with { Offset = checked(reference.Offset + offset) }, length);
            }
            uint address = checked((pointer.StagingAddress ?? throw new NotSupportedException("Lightning requires its addressed geometry source.")) + (uint)offset);
            if (NativeBattleEffectTaskMemory.TryRead(effect.Pools, address, length, out byte[] bytes))
            {
                return bytes;
            }
            return IsNativeEffectArenaAddress(address) ? memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : staging.Read(address, length);
        }
    }
}
