using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable catalog reads, or one scoped original-at-open observation of a working resource.</summary>
internal sealed class NativeBattleGfSourceReader : IDisposable
{
    private readonly ImmutableArray<byte> data;
    private readonly NativeBattleReadWindow window;
    private readonly NativeBattleGfAnimationSource? source;
    private readonly NativeBattleGfSourceReader? previous;
    private readonly bool borrowed;
    private readonly bool ownsWindow;
    private bool disposed;

    internal NativeBattleGfSourceReader(ImmutableArray<byte> data)
    {
        this.data = data;
        Length = data.IsDefault ? 0 : data.Length;
        IsDefault = data.IsDefault;
    }

    internal NativeBattleGfSourceReader(NativeBattleGfAnimationSource source)
    {
        this.source = source;
        data = source.CatalogData;
        Length = source.Length;
        IsDefault = source.IsDefault;
        if (source.Arena is { } arena)
        {
            borrowed = true;
            previous = source.ActiveReader;
            if (previous is not null)
            {
                window = previous.window;
            }
            else
            {
                window = arena.OpenReadWindow(source.Offset, source.Length);
                ownsWindow = true;
            }
            source.ActiveReader = this;
        }
    }

    public int Length { get; }
    public bool IsDefault { get; }
    public byte Byte(int offset)
    {
        RequireRange(offset, 1);
        return borrowed ? window.ReadByte(offset) : data[offset];
    }
    public ushort UShort(int offset)
    {
        RequireRange(offset, 2);
        return borrowed ? window.ReadUInt16(offset) : BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset, 2));
    }
    public short Short(int offset) => unchecked((short)UShort(offset));
    public uint UInt(int offset)
    {
        RequireRange(offset, 4);
        return borrowed ? window.ReadUInt32(offset) : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));
    }
    public int Int(int offset) => unchecked((int)UInt(offset));

    public void ReadInto(int offset, Span<byte> destination)
    {
        RequireRange(offset, destination.Length);
        if (borrowed)
        {
            window.ReadInto(offset, destination);
        }
        else
        {
            data.AsSpan().Slice(offset, destination.Length).CopyTo(destination);
            NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.CpuCopyPayload, destination.Length);
        }
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        RequireRange(offset, destination.Length);
        if (known.Length != destination.Length)
        {
            throw new ArgumentException("Native source bytes and knownness destinations must have matching widths.", nameof(known));
        }
        if (borrowed)
        {
            window.ReadWithKnownness(offset, destination, known);
        }
        else
        {
            data.AsSpan().Slice(offset, destination.Length).CopyTo(destination);
            known.Fill(true);
            NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.CpuCopyPayload, destination.Length);
            NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.Fill, known.Length);
        }
    }

    public ReadOnlySpan<byte> Read(int offset, int length)
    {
        RequireRange(offset, length);
        if (!borrowed)
        {
            return data.AsSpan().Slice(offset, length);
        }
        byte[] bytes = new byte[length];
        window.ReadInto(offset, bytes);
        return bytes;
    }

    private void RequireRange(int offset, int length)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Native GF animation access exceeds its observed resource/workspace bytes.");
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            if (borrowed)
            {
                source!.ActiveReader = previous;
                if (ownsWindow)
                {
                    window.Dispose();
                }
            }
        }
    }
}
