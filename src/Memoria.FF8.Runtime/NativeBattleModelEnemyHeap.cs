using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModelCpuReference
{
    [System.Text.Json.Serialization.JsonIgnore]
    public uint SharedAddress => Bank switch { 3 => checked(NativeBattleModelCpuResources.EnemyAddress + (uint)Offset), 4 => checked(0x1D999C0u + (uint)Offset), _ => throw new InvalidOperationException("This reference selects a party CPU bank.") };
}

public static class NativeBattleModelEnemyHeap
{
    public static byte[] Read(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length)
    {
        return reference.Bank < 3 ? cpu.Cpu.Banks[reference.Bank].Read(reference.Offset, length) : staging.Read(reference.SharedAddress, length);
    }

    public static (NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Load(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelDatSource source, int destination, int? sharedRow, bool special143)
    {
        int cursor = cpu.EnemyCursor ?? throw new NotSupportedException("Native enemy load requires its actual508260 heap cursor.");
        NativeBattleModelCpuResourceRow row;
        if (special143)
        {
            int selected = sharedRow ?? cpu.LastModel142Row ?? throw new NotSupportedException("Native507F80 requires its actual retained model142 resource row.");
            NativeBattleModelCpuResourceRow original = cpu.Rows[selected];
            row = cpu.Rows[destination] with { PrivateLength4 = 0, SharedLength8 = 0, LinkedPhysicalIndex36 = 255 };
            foreach (int field in new[] { 12, 20, 24, 44 })
            {
                row = row.Set(field, original.Fields.TryGetValue(field, out NativeBattleModelCpuReference? pointer) ? pointer : throw new NotSupportedException("Native507F80 reaches an unknown model142 resource pointer."));
                row = row with { Lengths = original.Lengths.TryGetValue(field, out int length) ? row.Lengths.SetItem(field, length) : row.Lengths.Remove(field) };
            }
            row = row.Set(16, new(4, 0)).Set(48, null).Set(28, At(0)).Set(32, At(1));
            row = row with { Lengths = row.Lengths.SetItem(16, 4).SetItem(28, source.Section(1) - source.Section(0)).SetItem(32, source.Section(2) - source.Section(1)) };
            staging = staging.Write(At(0).SharedAddress, source.Bytes.AsSpan().Slice(source.Section(0), source.Section(2) - source.Section(0)));
            cursor = checked(cursor + source.Section(2) - source.Section(0));
        }
        else if (sharedRow is int existing)
        {
            NativeBattleModelCpuResourceRow original = cpu.Rows[existing];
            int length = original.PrivateLength4 ?? throw new NotSupportedException("Native507740 requires its actual resource DWORD4 copy length.");
            byte[] copied = Read(cpu, staging, original.Require(12), length);
            row = original.Set(12, new(3, cursor));
            staging = staging.Write(At(0).SharedAddress, copied);
            cursor = checked((cursor + length + 3) & ~3);
        }
        else
        {
            int first = source.Section(0);
            int end = source.Section(9);
            staging = staging.Write(At(0).SharedAddress, source.Bytes.AsSpan().Slice(first, end - first));
            row = cpu.Rows[destination] with { PrivateLength4 = source.Section(1) - first, SharedLength8 = end - source.Section(1) };
            foreach ((int field, int section) in new (int, int)[] { (12, 0), (16, 1), (20, 2), (48, 3), (24, 4), (44, 5), (28, 6), (32, 7), (40, 8) })
            {
                row = row.Set(field, field == 44 && source.Section(5) == source.Section(6) ? null : At(section));
                row = row with { Lengths = row.Lengths.SetItem(field, source.Section(section + 1) - source.Section(section)) };
            }
            int scratch = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(source.Section(2) - 4, 4));
            ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native507010 requires its actual scratch maximum.");
            cpu = cpu with { ScratchMaximum = maximum < scratch ? unchecked((ushort)scratch) : maximum };
            cursor = checked(cursor + end - first);
        }
        cpu = cpu with { Rows = cpu.Rows.SetItem(destination, row), EnemyCursor = cursor, LastModel142Row = special143 && sharedRow is int shared ? shared : cpu.LastModel142Row };
        return (cpu, staging);

        NativeBattleModelCpuReference At(int section) => new(3, checked((cpu.EnemyCursor ?? 0) + source.Section(section) - source.Section(0)));
    }

    public static (NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging) Release(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelTextureCache cache, int resource)
    {
        if (cache.Rows[resource].Kind != 3)
        {
            return (cpu, staging);
        }
        NativeBattleModelCpuResourceRow row = cpu.Rows[resource];
        bool inactive = false;
        Compact(row.Require(12), row.PrivateLength4 ?? throw new NotSupportedException("Native5080D0 requires its actual private allocation length."));
        inactive = true;
        if (!cache.Rows.Where((_, index) => index != resource).Any(candidate => candidate.Kind == 3 && candidate.ModelId == cache.Rows[resource].ModelId))
        {
            Compact(cpu.Rows[resource].Require(16), row.SharedLength8 ?? throw new NotSupportedException("Native5080D0 requires its actual shared allocation length."));
            byte slot = row.LinkedPhysicalIndex36 ?? throw new NotSupportedException("Native5080D0 requires its observed linked sound slot.");
            if (slot != 255)
            {
                cpu = cpu with { EnemyLinkedSlots = unchecked((byte)((cpu.EnemyLinkedSlots ?? throw new NotSupportedException("Native5080D0 requires its retained slot mask.")) & ~(1 << slot))) };
            }
        }
        return (cpu, staging);

        void Compact(NativeBattleModelCpuReference address, int length)
        {
            if (length == 0)
            {
                return;
            }
            if (address.Bank != 3)
            {
                throw new NotSupportedException("Native508170 requires its actual enemy heap allocation.");
            }
            int cursor = cpu.EnemyCursor ?? throw new NotSupportedException("Native508170 requires its retained heap cursor.");
            staging = staging.CopyMemory(checked(address.SharedAddress + (uint)length), address.SharedAddress, checked(cursor - address.Offset - length));
            var rows = cpu.Rows.ToBuilder();
            for (int index = 0; index < rows.Count; index++)
            {
                if (cache.Rows[index].Kind != 3 || inactive && index == resource)
                {
                    continue;
                }
                NativeBattleModelCpuResourceRow changed = rows[index];
                foreach ((int field, NativeBattleModelCpuReference? pointer) in changed.Fields)
                {
                    if (pointer is { Bank: 3 } && pointer.Offset > address.Offset)
                    {
                        changed = changed.Set(field, pointer with { Offset = checked(pointer.Offset - (field == 48 ? 2 * (length >> 1) : length)) });
                    }
                }
                rows[index] = changed;
            }
            cpu = cpu with { Rows = rows.ToImmutable(), EnemyCursor = checked(cursor - length) };
        }
    }
}
