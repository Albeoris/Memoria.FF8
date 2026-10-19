using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownPayloadRanges { get; init; }

    internal void RequirePayloadRange(int offset, int length)
    {
        if (Payload.IsDefault || offset < 12 || length < 0 || offset - 12 > Payload.Length - length)
        {
            throw new InvalidDataException("Native task memory access leaves its physical payload.");
        }
    }

    internal void ValidateUnknownPayloadRanges()
    {
        if (UnknownPayloadRanges is not { } ranges)
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
            if (range is null || range.Offset <= end || range.Length <= 0 || range.Offset < 12 || range.Offset - 12 > Payload.Length - range.Length)
            {
                throw new InvalidDataException("Native task unknown-byte ranges must be bounded, sorted, disjoint and merged.");
            }
            end = checked(range.Offset + range.Length);
        }
    }

    internal void RequireKnownPayload(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        ValidateUnknownPayloadRanges();
        if (length != 0 && UnknownPayloadRanges is { } ranges && ranges.Any(range => range.Offset < (long)offset + length && range.Offset + (long)range.Length > offset))
        {
            throw new NotSupportedException($"Native task payload bytes at+0x{offset:X} are unresolved.");
        }
    }

    public byte[] ReadBytes(int offset, int length)
    {
        RequireKnownPayload(offset, length);
        return Payload.AsSpan().Slice(offset - 12, length).ToArray();
    }

    public NativeBattleModulePoolTask WriteBytes(int offset, ReadOnlySpan<byte> data)
    {
        RequirePayloadRange(offset, data.Length);
        byte[] payload = Payload.ToArray();
        data.CopyTo(payload.AsSpan(offset - 12));
        return (this with { Payload = payload.ToImmutableArray() }).WithoutResourcePointers(offset, data.Length).WithoutUnknownPayload(offset, data.Length);
    }

    public NativeBattleModulePoolTask MarkPayloadUnknown(int offset, int length)
    {
        RequirePayloadRange(offset, length);
        ValidateUnknownPayloadRanges();
        if (length == 0)
        {
            return this;
        }
        List<NativeBattleEffectUnknownRegion> all = UnknownPayloadRanges is { } retained ? retained.ToList() : [];
        all.Add(new(offset, length));
        List<NativeBattleEffectUnknownRegion> merged = [];
        foreach (NativeBattleEffectUnknownRegion range in all.OrderBy(range => range.Offset))
        {
            if (merged.Count != 0 && merged[^1].Offset + merged[^1].Length >= range.Offset)
            {
                NativeBattleEffectUnknownRegion previous = merged[^1];
                merged[^1] = new(previous.Offset, Math.Max(previous.Offset + previous.Length, range.Offset + range.Length) - previous.Offset);
            }
            else
            {
                merged.Add(range);
            }
        }
        return (this with { UnknownPayloadRanges = merged.ToImmutableArray() }).WithoutResourcePointers(offset, length);
    }

    internal NativeBattleModulePoolTask WithoutUnknownPayload(int offset, int length)
    {
        ValidateUnknownPayloadRanges();
        if (UnknownPayloadRanges is not { } ranges || length == 0)
        {
            return this;
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder result = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        int end = checked(offset + length);
        foreach (NativeBattleEffectUnknownRegion range in ranges)
        {
            int rangeEnd = range.Offset + range.Length;
            if (rangeEnd <= offset || range.Offset >= end)
            {
                result.Add(range);
            }
            else
            {
                if (range.Offset < offset)
                {
                    result.Add(new(range.Offset, offset - range.Offset));
                }
                if (rangeEnd > end)
                {
                    result.Add(new(end, rangeEnd - end));
                }
            }
        }
        return this with { UnknownPayloadRanges = result.Count == 0 ? null : result.ToImmutable() };
    }
}
