using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownPayloadRanges { get => RequireSnapshotData().Unknown; init => RejectInitializer(); }

    internal void RequirePayloadRange(int offset, int length)
    {
        if (offset < 12 || length < 0 || offset - 12 > PayloadLength - length)
        {
            throw new InvalidDataException("Native task memory access leaves its physical payload.");
        }
    }

    internal void ValidateUnknownPayloadRanges()
    {
        if (owner is not null)
        {
            owner.RequireActive();
            return;
        }
        if (RequireSnapshotData().Unknown is not { } ranges)
        {
            return;
        }
        if (ranges.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native task unknown-byte ranges require a nonempty physical mask.");
        }
        int end = 11;
        foreach (NativeBattleEffectUnknownRegion range in ranges)
        {
            if (range is null || range.Offset <= end || range.Length <= 0 || range.Offset < 12 || range.Offset - 12 > PayloadLength - range.Length)
            {
                throw new InvalidDataException("Native task unknown-byte ranges must be bounded, sorted, disjoint and merged.");
            }
            end = checked(range.Offset + range.Length);
        }
    }

    internal void RequireKnownPayload(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        if (owner is not null)
        {
            for (int index = 0; index < length; index++)
            {
                _ = owner.Body.ReadByte(Start + offset + index);
            }
            return;
        }
        ValidateUnknownPayloadRanges();
        foreach (NativeBattleEffectUnknownRegion range in RequireSnapshotData().Unknown ?? [])
        {
            if (range.Offset < (long)offset + length && range.Offset + (long)range.Length > offset)
            {
                throw new NotSupportedException($"Native task payload bytes at+0x{offset:X} are unresolved.");
            }
        }
    }

    /// <summary>Borrow raw payload bytes for the immediate owner scope; unknown lanes remain raw and storage growth invalidates the view.</summary>
    public ReadOnlySpan<byte> ReadRawPayloadSpan()
    {
        return owner is null ? RequireSnapshotData().Payload.AsSpan() : owner.RequireActive().Body.BorrowRawReadOnly(Start + 12, PayloadLength);
    }

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequirePayloadRange(offset, destination.Length);
        if (owner is not null)
        {
            owner.Body.ReadInto(Start + offset, destination);
        }
        else
        {
            RequireKnownPayload(offset, destination.Length);
            RequireSnapshotData().Payload.AsSpan().Slice(offset - 12, destination.Length).CopyTo(destination);
        }
    }

    public byte[] ReadBytes(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        byte[] result = new byte[length];
        ReadInto(offset, result);
        return result;
    }

    public void ReadWithKnownness(int offset, Span<byte> bytes, Span<bool> known)
    {
        RequirePayloadRange(offset, bytes.Length);
        if (known.Length != bytes.Length)
        {
            throw new ArgumentException("Native task byte/knownness destinations must match.", nameof(known));
        }
        if (owner is not null)
        {
            owner.Body.ReadWithKnownness(Start + offset, bytes, known);
            return;
        }
        RequireSnapshotData().Payload.AsSpan().Slice(offset - 12, bytes.Length).CopyTo(bytes);
        known.Fill(true);
        foreach (NativeBattleEffectUnknownRegion range in RequireSnapshotData().Unknown ?? [])
        {
            int first = Math.Max(offset, range.Offset);
            int end = (int)Math.Min((long)offset + bytes.Length, range.Offset + (long)range.Length);
            if (end > first)
            {
                known.Slice(first - offset, end - first).Fill(false);
            }
        }
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        byte[] bytes = new byte[length];
        bool[] known = new bool[length];
        ReadWithKnownness(offset, bytes, known);
        return (bytes, known);
    }

    public NativeBattleModulePoolTask WriteBytes(int offset, ReadOnlySpan<byte> data)
    {
        RequirePayloadRange(offset, data.Length);
        RequireOwner().Body.Write(Start + offset, data);
        return this;
    }

    public NativeBattleModulePoolTask WriteWithKnownness(int offset, ReadOnlySpan<byte> data, ReadOnlySpan<bool> known)
    {
        RequirePayloadRange(offset, data.Length);
        RequireOwner().Body.WriteWithKnownness(Start + offset, data, known);
        return this;
    }

    public NativeBattleModulePoolTask WriteRetainedBytes(int offset, ReadOnlySpan<byte> data)
    {
        RequirePayloadRange(offset, data.Length);
        RequireOwner().Body.WriteRetainedBytes(Start + offset, data);
        return this;
    }

    public NativeBattleModulePoolTask ClearPayload(int expectedLength)
    {
        if (expectedLength != PayloadLength)
        {
            throw new InvalidDataException("Native task clear changes its physical payload width.");
        }
        return ClearPayload();
    }

    public NativeBattleModulePoolTask ClearPayload()
    {
        NativeBattleTaskBankOwner current = RequireOwner();
        if (current.HasCheckpoint)
        {
            current.Storage.ReserveCheckpointCapacity(PayloadLength, current.Storage.ResourcePointerCount);
        }
        Span<byte> zero = stackalloc byte[256];
        zero.Clear();
        for (int offset = 0; offset < PayloadLength; offset += zero.Length)
        {
            current.Body.Write(Start + 12 + offset, zero[..Math.Min(zero.Length, PayloadLength - offset)]);
        }
        return this;
    }

    public NativeBattleModulePoolTask MarkPayloadUnknown(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        RequireOwner().Body.MarkUnknown(Start + offset, length);
        return this;
    }

    internal NativeBattleTaskSnapshotData ExportSnapshotData() => owner is null ? RequireSnapshotData() : owner.ReadTask(slot);

    internal void Replace(NativeBattleTaskSnapshotData data)
    {
        RequireOwner();
        if (data.Payload.IsDefault || data.Payload.Length != PayloadLength)
        {
            throw new InvalidDataException("Native replacement task changes its physical payload.");
        }
        NativeBattleTaskBankSnapshot proof = new(0, 0, checked((ushort)(PayloadLength + 12)), [data], null, null, false);
        proof.Tasks[0].ValidateUnknownPayloadRanges();
        proof.Tasks[0].ValidateResourcePointers();
        NativeBattleTaskBankOwner current = RequireOwner();
        if (current.HasCheckpoint)
        {
            current.Storage.ReserveCheckpointCapacity(checked(PayloadLength + 12 + 4 * (data.Pointers?.Count ?? 0)), checked(current.Storage.ResourcePointerCount + 2 * (data.Pointers?.Count ?? 0)));
        }
        UpdateHeader(data.Flags, data.State, data.Next, data.Callback);
        WriteBytes(12, data.Payload.AsSpan());
        foreach (NativeBattleEffectUnknownRegion range in data.Unknown ?? [])
        {
            MarkPayloadUnknown(range.Offset, range.Length);
        }
        foreach ((int offset, NativeBattleResourcePointer pointer) in data.Pointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty)
        {
            WriteResourcePointer(offset, pointer);
        }
    }
}
