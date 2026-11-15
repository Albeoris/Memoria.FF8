using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    private BattleExecutionReference()
    {
    }

    internal sealed record Model : BattleExecutionReference
    {
        internal Model(BattleModelOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            Owner = owner;
        }

        internal BattleModelOwner Owner { get; }
    }

    internal sealed record Targets : BattleExecutionReference
    {
        internal Targets(BattleModelTargets selection)
        {
            ArgumentNullException.ThrowIfNull(selection);
            Selection = selection;
        }

        internal BattleModelTargets Selection { get; }
    }

    internal sealed record Header(BattleActionHeaderReference Reference) : BattleExecutionReference;
    internal sealed record Cursor(BattleContactCursor Reference) : BattleExecutionReference;
    internal sealed record Occurrence(BattleContactOccurrence Reference) : BattleExecutionReference;

    internal sealed record ContactCount : BattleExecutionReference
    {
        internal ContactCount(int value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Value = value;
        }

        internal int Value { get; }

        internal ImmutableArray<byte>? EncodeOriginalWord()
        {
            return Value <= ushort.MaxValue ? [(byte)(Value & 0xFF), (byte)(Value >> 8)] : null;
        }
    }

    internal sealed record TraversalPosition : BattleExecutionReference
    {
        internal TraversalPosition(int value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Value = value;
        }

        internal int Value { get; }

        internal ImmutableArray<byte>? EncodeOriginalByte()
        {
            return Value <= byte.MaxValue ? [(byte)Value] : null;
        }
    }

    internal sealed record ContactOffset : BattleExecutionReference
    {
        internal ContactOffset(int? offset)
        {
            if (offset is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            Offset = offset;
        }

        internal int? Offset { get; }

        internal ImmutableArray<byte>? EncodeOriginalWord()
        {
            if (Offset is > short.MaxValue)
            {
                return null;
            }
            ushort value = Offset is { } offset ? (ushort)offset : ushort.MaxValue;
            return [(byte)(value & 0xFF), (byte)(value >> 8)];
        }
    }
}

internal sealed record BattleExecutionReferenceEntry
{
    internal BattleExecutionReferenceEntry(int width, BattleExecutionReference reference)
    {
        if (width is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        ArgumentNullException.ThrowIfNull(reference);
        Width = width;
        Reference = reference;
    }

    internal int Width { get; }
    internal BattleExecutionReference Reference { get; }
}
internal static class ExecutionReferenceEncoding
{
    internal static void RequireWidth(int width)
    {
        if (width is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
    }

    internal static void Validate(int width, ImmutableArray<byte>? nativeBytes)
    {
        RequireWidth(width);
        if (nativeBytes is { } supplied && (supplied.IsDefault || supplied.Length != width))
        {
            throw new ArgumentException("The native byte encoding must match the reference width.", nameof(nativeBytes));
        }
    }

    internal static ImmutableArray<byte>? Capture(ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        if (!known.Contains(false))
        {
            return ImmutableArray.CreateRange(bytes.ToArray());
        }
        if (known.Contains(true))
        {
            throw new InvalidDataException("A live execution reference has inconsistent backing-byte knownness.");
        }
        return null;
    }
}
