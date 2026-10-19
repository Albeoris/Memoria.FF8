using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleVictoryResourceBinding(NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, uint NextRow);

public static class NativeBattleVictoryResources
{
    public static NativeBattleVictoryResourceBinding Bind5022C0(NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu, NativeBattleGraphicsModel model, uint row, uint directory)
    {
        (NativeBattleGraphicsModel modelResult, uint next) = Bind5022C0(staging.ReadDat(directory), cpu, model, row, directory, address => staging.ReadResourcePointer(address), (address, pointer) => staging = staging.WriteResourcePointer(address, pointer));
        return new(staging, modelResult, next);
    }

    public static (NativeBattleGraphicsModel Model, uint NextRow) Bind5022C0(ImmutableArray<byte> bytes, NativeBattleModelCpuResources cpu, NativeBattleGraphicsModel model, uint row, uint directory, Func<uint, NativeBattleResourcePointer?> readPointer, Action<uint, NativeBattleResourcePointer?> writePointer)
    {
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int sequences = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int linked = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        writePointer(checked(row + 8), new(null, checked(directory + (uint)animations), sequences - animations));
        writePointer(checked(row + 12), new(null, checked(directory + (uint)sequences), linked - sequences));
        NativeBattleResourceRowReference source = model.ActiveStagedResourceRow100 is uint active ? new(null, active) : new(model.ActiveResourceRow100 ?? throw new NotSupportedException("Native5022C0 requires its actual current resource row."), null);
        Copy(source, row, 12);
        uint next = checked(row + 16);
        uint? linkedRow = model.LinkedStagedResourceRow;
        if (model.LinkedTransform is not null)
        {
            NativeBattleResourceRowReference linkedSource = linkedRow is uint previous ? new(null, previous) : new(model.LinkedTextureResourceIndex ?? throw new NotSupportedException("Native5022C0 requires the actual linked object's resource row."), null);
            writePointer(checked(next + 8), new(null, checked(directory + (uint)linked), bytes.Length - linked));
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
        return (model, next);

        void Copy(NativeBattleResourceRowReference original, uint destination, byte mask)
        {
            NativeBattleResourceRowBinding.CopyFields502270<NativeBattleResourceRowReference, NativeBattleResourcePointer?>(original, new(null, destination), mask, Read, (target, field, pointer) => writePointer(checked(target.StagingAddress!.Value + (uint)field * 4), pointer));
        }

        NativeBattleResourcePointer? Read(NativeBattleResourceRowReference owner, int field)
        {
            if (owner.StagingAddress is uint address)
            {
                return readPointer(checked(address + (uint)field * 4));
            }
            int[] offsets = [12, 16, 20, 44];
            NativeBattleModelCpuResourceRow original = cpu.Rows[owner.CpuRow!.Value];
            if (!original.Fields.TryGetValue(offsets[field], out NativeBattleModelCpuReference? reference))
            {
                return null;
            }
            return reference is null ? new(null, 0) : new(reference, null);
        }
    }

    public static NativeBattleGraphicsResource Resolve(NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu, NativeBattleGraphicsModel model, NativeBattleGraphicsResource original)
    {
        return Resolve(model, original, staging.ReadResourcePointer, Read);

        byte[] Read(NativeBattleResourcePointer pointer, int length)
        {
            return pointer.Cpu is { } reference ? NativeBattleModelEnemyHeap.Read(cpu, staging, reference, length) : staging.Read(pointer.StagingAddress ?? throw new NotSupportedException("Native resource pointer has no observed address owner."), length);
        }
    }

    public static NativeBattleGraphicsResource Resolve(NativeBattleGraphicsModel model, NativeBattleGraphicsResource original, Func<uint, NativeBattleResourcePointer?> readPointer, Func<NativeBattleResourcePointer, int, byte[]> read)
    {
        uint row = model.ActiveStagedResourceRow100 ?? throw new InvalidOperationException("No staged resource row is selected.");
        NativeBattlePoseSource pose = new(Skeleton(row), Block(checked(row + 8)));
        NativeBattlePoseSource? linked = model.LinkedStagedResourceRow is uint linkedRow ? new(Skeleton(linkedRow), Block(checked(linkedRow + 8))) : original.LinkedPoseSource;
        NativeBattleGraphicsResource resource = original with { SequenceSection = Block(checked(row + 12)), PoseSource = pose, AnimationFrames = NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan()), LinkedPoseSource = linked, LinkedAnimationFrames = linked is null ? [] : NativeBattlePoseDecoder.ReadFrameCounts(linked.Animations.AsSpan()) };
        resource.Validate();
        return resource;

        ImmutableArray<byte> Skeleton(uint pointerRow)
        {
            NativeBattleResourcePointer pointer = readPointer(pointerRow) ?? throw new NotSupportedException("Native staged skeleton pointer remains unknown.");
            byte count = Read(pointer, 1)[0];
            return Read(pointer, checked(16 + 48 * count)).ToImmutableArray();
        }

        ImmutableArray<byte> Block(uint address)
        {
            NativeBattleResourcePointer pointer = readPointer(address) ?? throw new NotSupportedException("Native staged resource pointer remains unknown.");
            return Read(pointer, pointer.Length ?? throw new NotSupportedException("Native staged animation/sequence extent is not owned after its pointer bytes changed.")).ToImmutableArray();
        }

        byte[] Read(NativeBattleResourcePointer pointer, int length) => read(pointer, length);
    }
}
