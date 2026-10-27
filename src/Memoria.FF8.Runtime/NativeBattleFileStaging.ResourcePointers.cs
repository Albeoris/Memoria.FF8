using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResourcePointer(NativeBattleModelCpuReference? Cpu, uint? StagingAddress, int? Length = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? CpuRow { get; init; }

    internal bool HasValidOwner() => Length is not < 0 && (Cpu is not null ? 1 : 0) + (StagingAddress is not null ? 1 : 0) + (CpuRow is not null ? 1 : 0) == 1 && CpuRow is not (< 0 or >= 11) && (Cpu is not { } cpu || cpu.Bank <= 4 && cpu.Offset >= 0 && (cpu.Bank >= 3 || cpu.Offset <= NativeBattleModelCpuBank.Length));
}
public sealed record NativeBattleResourceRowReference(int? CpuRow, uint? StagingAddress);

public sealed partial record NativeBattleFileStaging
{
    public ImmutableDictionary<uint, NativeBattleResourcePointer?> ResourcePointers
    {
        get => Transaction is { } transaction ? transaction.CapturePointers() : owner?.CapturePointers() ?? resourcePointers;
        init
        {
            RequireInitializer();
            resourcePointers = value;
        }
    }

    public NativeBattleFileStaging InvalidateResourcePointers(uint address, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        RequireWritableRange(address, length);
        if (Transaction is { } transaction)
        {
            transaction.InvalidatePointers(address, length);
        }
        else
        {
            RequireOwner().InvalidatePointers(address, length);
        }
        return this;
    }

    public NativeBattleResourcePointer? ReadResourcePointer(uint address)
    {
        RequireOffsetRange(address, 4);
        NativeBattleResourcePointer? pointer;
        bool present = Transaction is { } transaction ? transaction.TryGetPointer(address, out pointer) : snapshot ? resourcePointers.TryGetValue(address, out pointer) : RequireOwner().TryGetPointer(address, out pointer);
        return present ? pointer : new(null, ReadUInt32(address));
    }

    public NativeBattleFileStaging WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        RequireWritableRange(address, 4);
        if (pointer is not null && !pointer.HasValidOwner())
        {
            throw new InvalidDataException("Native staging pointer has an invalid source owner.");
        }
        if (pointer?.StagingAddress is uint numeric)
        {
            WriteUInt32(address, numeric);
        }
        else
        {
            MarkUnknown(address, 4);
        }
        if (Transaction is { } transaction)
        {
            transaction.SetPointerMetadata(address, pointer);
        }
        else
        {
            RequireOwner().SetPointerMetadata(address, true, pointer);
        }
        return this;
    }
}
