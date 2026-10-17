using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleVictoryResourceBinding(NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, uint NextRow);

public static class NativeBattleVictoryResources
{
    public static NativeBattleVictoryResourceBinding Bind5022C0(NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu, NativeBattleGraphicsModel model, uint row, uint directory)
    {
        ImmutableArray<byte> bytes = staging.ReadDat(directory);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int sequences = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int linked = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        staging = staging.WriteResourcePointer(checked(row + 8), new(null, checked(directory + (uint)animations), sequences - animations));
        staging = staging.WriteResourcePointer(checked(row + 12), new(null, checked(directory + (uint)sequences), linked - sequences));
        NativeBattleResourceRowReference source = model.ActiveStagedResourceRow100 is uint active ? new(null, active) : new(model.ActiveResourceRow100 ?? throw new NotSupportedException("Native5022C0 requires its actual current resource row."), null);
        Copy(source, row, 12);
        uint next = checked(row + 16);
        uint? linkedRow = model.LinkedStagedResourceRow;
        if (model.LinkedTransform is not null)
        {
            NativeBattleResourceRowReference linkedSource = linkedRow is uint previous ? new(null, previous) : new(model.LinkedTextureResourceIndex ?? throw new NotSupportedException("Native5022C0 requires the actual linked object's resource row."), null);
            staging = staging.WriteResourcePointer(checked(next + 8), new(null, checked(directory + (uint)linked), bytes.Length - linked));
            Copy(linkedSource, next, 4);
            linkedRow = next;
            next = checked(next + 16);
        }
        NativeBattleSequenceState? state = model.SequenceState;
        if ((model.ModelId4 ?? throw new NotSupportedException("Native5022C0 requires its model BYTE4.")) < 16)
        {
            if (state is null)
            {
                throw new NotSupportedException("Native5022C0 requires its existing resource-header owner.");
            }
            state = state with { Locals = state.Locals.SetItem(7, state.Default2), Default2 = 1 };
        }
        model = model with { Flags2 = (ushort)((model.Flags2 ?? throw new NotSupportedException("Native5022C0 requires model WORD2.")) | 8), ActiveResourceRow100 = null, ActiveStagedResourceRow100 = row, LinkedStagedResourceRow = linkedRow, SequenceState = state };
        return new(staging, model, next);

        void Copy(NativeBattleResourceRowReference original, uint destination, byte mask)
        {
            NativeBattleResourceRowBinding.CopyFields502270<NativeBattleResourceRowReference, NativeBattleResourcePointer?>(original, new(null, destination), mask, Read, (target, field, pointer) => staging = staging.WriteResourcePointer(checked(target.StagingAddress!.Value + (uint)field * 4), pointer));
        }

        NativeBattleResourcePointer? Read(NativeBattleResourceRowReference owner, int field)
        {
            if (owner.StagingAddress is uint address)
            {
                return staging.ReadResourcePointer(checked(address + (uint)field * 4));
            }
            int[] offsets = [12, 16, 20, 44];
            NativeBattleModelCpuResourceRow original = cpu.Rows[owner.CpuRow!.Value];
            return original.Fields.TryGetValue(offsets[field], out NativeBattleModelCpuReference? reference) && reference is not null ? new(reference, null) : null;
        }
    }

    public static NativeBattleGraphicsResource Resolve(NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu, NativeBattleGraphicsModel model, NativeBattleGraphicsResource original)
    {
        uint row = model.ActiveStagedResourceRow100 ?? throw new InvalidOperationException("No staged resource row is selected.");
        NativeBattlePoseSource pose = new(Skeleton(row), Block(checked(row + 8)));
        NativeBattlePoseSource? linked = model.LinkedStagedResourceRow is uint linkedRow ? new(Skeleton(linkedRow), Block(checked(linkedRow + 8))) : original.LinkedPoseSource;
        NativeBattleGraphicsResource resource = original with { SequenceSection = Block(checked(row + 12)), PoseSource = pose, AnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()), LinkedPoseSource = linked, LinkedAnimationFrames = linked is null ? [] : NativeBattlePoseDecoder.ReadFrameCounts(linked.Animations.AsSpan()) };
        resource.Validate();
        return resource;

        ImmutableArray<byte> Skeleton(uint pointerRow)
        {
            NativeBattleResourcePointer pointer = staging.ReadResourcePointer(pointerRow) ?? throw new NotSupportedException("Native staged skeleton pointer remains unknown.");
            byte count = Read(pointer, 1)[0];
            return Read(pointer, checked(16 + 48 * count)).ToImmutableArray();
        }

        ImmutableArray<byte> Block(uint address)
        {
            NativeBattleResourcePointer pointer = staging.ReadResourcePointer(address) ?? throw new NotSupportedException("Native staged resource pointer remains unknown.");
            return Read(pointer, pointer.Length ?? throw new NotSupportedException("Native staged animation/sequence extent is not owned after its pointer bytes changed.")).ToImmutableArray();
        }

        byte[] Read(NativeBattleResourcePointer pointer, int length)
        {
            if (pointer.Cpu is { } reference)
            {
                return cpu.Cpu.Banks[reference.Bank].Read(reference.Offset, length);
            }
            return staging.Read(pointer.StagingAddress ?? throw new NotSupportedException("Native resource pointer has no observed address owner."), length);
        }
    }
}
