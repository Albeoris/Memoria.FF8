using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelCpuResourceRow(ImmutableDictionary<int, NativeBattleModelCpuReference?> Fields)
{
    public int? PrivateLength4 { get; init; }
    public int? SharedLength8 { get; init; }
    public ImmutableDictionary<int, int> Lengths { get; init; } = ImmutableDictionary<int, int>.Empty;
    public byte? LinkedPhysicalIndex36 { get; init; }
    public static NativeBattleModelCpuResourceRow Unknown() => new(ImmutableDictionary<int, NativeBattleModelCpuReference?>.Empty);
    public NativeBattleModelCpuReference Require(int field) => Fields.TryGetValue(field, out NativeBattleModelCpuReference? value) && value is not null ? value : throw new NotSupportedException($"Native resource row DWORD{field} has no observed CPU-bank reference.");
    public NativeBattleModelCpuResourceRow Set(int field, NativeBattleModelCpuReference? value) => this with { Fields = Fields.SetItem(field, value) };
}
public sealed record NativeBattleModelAnimationCopy(int DestinationOffset, ImmutableArray<byte> Bytes, int HeaderPointerField);
public sealed record NativeBattleModelCpuResources(NativeBattleModelCpuBanks Cpu, ImmutableArray<NativeBattleModelCpuResourceRow> Rows)
{
    public const uint EnemyAddress = 0x1939018;
    public int? EnemyCursor { get; init; }
    public byte? EnemyLinkedSlots { get; init; }
    public int? LastModel142Row { get; init; }
    public NativeBattleModelCpuBank Palettes { get; init; } = NativeBattleModelCpuBank.Unknown();
    public NativeBattleModelCpuBank AnimationFrames { get; init; } = NativeBattleModelCpuBank.Unknown();
    public ImmutableDictionary<int, int?> AnimationPointers { get; init; } = ImmutableDictionary<int, int?>.Empty;
    public ushort? ScratchMaximum { get; init; }
    public static NativeBattleModelCpuResources Unknown() => new(NativeBattleModelCpuBanks.Unknown(), Enumerable.Range(0, 11).Select(_ => NativeBattleModelCpuResourceRow.Unknown()).ToImmutableArray());
    public static NativeBattleModelCpuResources FromColdProcess()
    {
        ImmutableDictionary<int, NativeBattleModelCpuReference?> pointers = new[] { 8, 12, 16, 20, 24, 28, 32, 40, 44, 48 }.ToImmutableDictionary(offset => offset, _ => (NativeBattleModelCpuReference?)null);
        return Unknown() with { Palettes = NativeBattleModelCpuBank.Unknown().Write(0, new byte[0xC00]), Rows = Enumerable.Range(0, 11).Select(_ => new NativeBattleModelCpuResourceRow(pointers)).ToImmutableArray() };
    }

    public void Validate()
    {
        if (Cpu.Banks.IsDefault || Cpu.Banks.Length != 3 || Rows.IsDefault || Rows.Length != 11 || Rows.Any(row => row is null) || AnimationPointers.Any(item => item.Key < 0 || item.Key >= 96 || item.Value is < 0 or >= NativeBattleModelCpuBank.Length))
        {
            throw new InvalidDataException("Native model CPU resources require their three banks and eleven physical row aliases.");
        }
        foreach (NativeBattleModelCpuBank bank in Cpu.Banks.Append(Palettes).Append(AnimationFrames))
        {
            _ = bank.Read(0, 0);
        }
        if (EnemyCursor is < 0 || EnemyLinkedSlots > 7 || LastModel142Row is < 0 or >= 11 || Rows.Any(row => row.PrivateLength4 < 0 || row.SharedLength8 < 0 || row.Lengths.Any(item => item.Value < 0) || row.Fields.Any(field => field.Value is { } reference && (reference.Bank > 4 || reference.Offset < 0 || reference.Bank < 3 && reference.Offset > NativeBattleModelCpuBank.Length))))
        {
            throw new InvalidDataException("Native model resource row points outside its known logical bank.");
        }
    }

    public NativeBattleModelCpuResources SavePalettes507400(NativeBattleModelTextureLoad load)
    {
        NativeBattleModelCpuBank palettes = Palettes;
        foreach (int offset in load.TimOffsets)
        {
            ReadOnlySpan<byte> source = load.Source.Data.AsSpan();
            short x = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset + 12));
            short y = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset + 14));
            int slot = y - 224;
            if (x < 256 && slot is >= 0 and < 6)
            {
                if (offset > source.Length - 20 - 512)
                {
                    throw new InvalidDataException("Native507400 saved palette copy leaves its actual TIM source.");
                }
                palettes = palettes.Write(slot * 512, source.Slice(offset + 20, 512));
            }
        }
        return this with { Palettes = palettes };
    }
}
public sealed record NativeBattleModelResourceCompletion(NativeBattleModelCpuBanks Cpu, ImmutableArray<NativeBattleModelCpuResourceRow> Rows, int ActiveBodyRow, int? ActiveLinkedRow, NativeBattleModelAnimationCopy? AnimationCopy, int? ScratchCandidate);

/// <summary>Resource-only5077B0/5079B0/507BF0/507E20 completion. This does not initialize a model or consume RNG.</summary>
public static class NativeBattleModelResourceCopies
{
    public static NativeBattleModelResourceCompletion Complete(NativeBattleModelResourceBind bind, NativeBattleModelCpuBanks cpu, ImmutableArray<NativeBattleModelCpuResourceRow> rows, int bodyRow, int? linkedRow)
    {
        if (rows.IsDefault || rows.Length != 11 || (uint)bodyRow >= 11 || cpu.Banks.IsDefault || cpu.Banks.Length != 3 || linkedRow is < 0 or >= 11)
        {
            throw new InvalidDataException("Native resource completion requires the actual eleven resource rows and three party CPU banks.");
        }
        NativeBattleModelLoadTask task = bind.Task;
        NativeBattleModelDatSource source = bind.PreparedDat;
        byte bank = task.PhysicalIndex;
        if (bank >= 3)
        {
            throw new InvalidDataException("Native party resource copy leaves its three physical banks.");
        }
        NativeBattleModelCpuResourceRow body = rows[bodyRow];
        int origin;
        int firstSection;
        int endSection;
        int? scratch = null;
        NativeBattleModelAnimationCopy? animation = null;
        if (task.Callback is 0x5077B0 or 0x5079B0)
        {
            origin = 0;
            firstSection = 0;
            bool special = task.Callback == 0x5079B0;
            endSection = special ? 7 : 5;
            body = body.Set(12, At(0)).Set(16, At(1)).Set(20, At(2)).Set(48, source.Section(3) == source.Section(4) ? null : At(3)).Set(44, At(4)).Set(8, At(endSection));
            if (special)
            {
                body = body.Set(24, At(5)).Set(40, At(6));
            }
            int animationSection = special ? 9 : 6;
            int start = source.Section(animationSection);
            int end = source.Section(animationSection + 1);
            animation = new(task.PhysicalIndex * 0x3020 + 0x20, source.Bytes.Slice(start, end - start), 8);
            scratch = ReadScratch();
            rows = rows.SetItem(bodyRow, body);
        }
        else if (task.Callback == 0x507BF0)
        {
            int row = linkedRow ?? throw new NotSupportedException("Native507BF0 requires its selected type2 resource-row alias.");
            NativeBattleModelCpuReference tail = body.Require(8);
            bank = tail.Bank;
            origin = tail.Offset;
            firstSection = 0;
            endSection = 5;
            NativeBattleModelCpuResourceRow linked = rows[row].Set(12, At(0)).Set(16, At(1)).Set(20, At(2));
            body = body.Set(24, At(3)).Set(40, At(4)) with { LinkedPhysicalIndex36 = task.PhysicalIndex };
            rows = rows.SetItem(row, linked).SetItem(bodyRow, body);
            int start = source.Section(7);
            int end = source.Section(8);
            animation = new(task.PhysicalIndex * 0x3020 + 0x2020, source.Bytes.Slice(start, end - start), 24);
            scratch = ReadScratch();
        }
        else if (task.Callback == 0x507E20)
        {
            NativeBattleModelCpuReference tail = body.Require(8);
            bank = tail.Bank;
            origin = tail.Offset;
            firstSection = 0;
            endSection = 3;
            body = body.Set(24, At(1)).Set(40, At(2)) with { LinkedPhysicalIndex36 = task.PhysicalIndex };
            rows = rows.SetItem(bodyRow, body);
        }
        else
        {
            throw new NotSupportedException("Native resource completion requires its original lower-level copy callback.");
        }
        int first = source.Section(firstSection);
        int length = source.Section(endSection) - first;
        if (bank >= 3)
        {
            throw new InvalidDataException("Native resource row aliases an absent party CPU bank.");
        }
        NativeBattleModelCpuBank changed = cpu.Banks[bank].Write(origin, source.Bytes.AsSpan().Slice(first, length));
        if (task.Callback == 0x507E20)
        {
            NativeBattleModelCpuReference geometry = body.Require(16);
            if (geometry.Bank != bank)
            {
                throw new NotSupportedException("Native507E20 relative geometry patch requires the body's actual shared CPU bank.");
            }
            _ = changed.Read(geometry.Offset + 8, 4);
            byte[] relative = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(relative, checked(origin - geometry.Offset + 8));
            changed = changed.Write(geometry.Offset + 8, relative);
        }
        cpu = cpu with { Banks = cpu.Banks.SetItem(bank, changed) };
        return new(cpu, rows, bodyRow, task.Callback == 0x507BF0 ? linkedRow : null, animation, scratch);

        NativeBattleModelCpuReference At(int section) => new(bank, checked(origin + source.Section(section) - source.Section(0)));
        int ReadScratch()
        {
            int position = source.Section(2) - 4;
            if (position < 0 || position > source.Bytes.Length - 4)
            {
                throw new InvalidDataException("Native507010 scratch candidate leaves its copied resource.");
            }
            return BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(position));
        }
    }
}
