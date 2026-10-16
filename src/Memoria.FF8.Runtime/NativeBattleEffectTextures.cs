using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectAnimationRegisters([property: JsonRequired] NativeBattleEffectHeapAllocation Source, [property: JsonRequired] int BaseOffset, [property: JsonRequired] int StreamOffset, [property: JsonRequired] int ModelTableOffset);
public sealed record NativeBattleEffectVramCopy(short SourceX, short SourceY, short Width, short Height, short DestinationX, short DestinationY);
public sealed record NativeBattleEffectTimUpload([property: JsonRequired] NativeBattleEffectHeapAllocation? Source, [property: JsonRequired] int Offset)
{
    public byte Kind { get; init; } = 1;
    public NativeBattleEffectVramCopy? Copy { get; init; }
}
public sealed record NativeBattleEffectVramUpload(short X, short Y, short Width, short Height, ImmutableArray<byte> Bytes)
{
    public bool Copied { get; init; }
}
public sealed record NativeBattleEffectTextureDrain(NativeBattleEffectTextureState State, ImmutableArray<NativeBattleEffectVramUpload> Uploads);

/// <summary>505E30/505D20's32 physical16-byte slots and45BD30's retained1024x51216-bit VRAM.</summary>
public sealed record NativeBattleEffectTextureState([property: JsonRequired] ImmutableArray<NativeBattleEffectTimUpload?> Slots, [property: JsonRequired] int Count, [property: JsonRequired] NativeBattleEffectAnimationRegisters? Animation, [property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> VramRegions)
{
    public static NativeBattleEffectTextureState FromColdProcess() => new(ImmutableArray.CreateRange(new NativeBattleEffectTimUpload?[32]), 0, null, []);

    public NativeBattleEffectTextureState Initialize500400()
    {
        Validate();
        //500400 clears1D98420 only. Slots,509970 registers and VRAM survive battle initialization.
        return this with { Count = 0 };
    }

    public void Validate()
    {
        if (Slots.IsDefault || Slots.Length != 32 || Count is < 0 or > 31 || Slots.Take(Count).Any(slot => slot is null))
        {
            throw new InvalidDataException("Native texture upload state requires32 physical slots and its successful0..31 pending count.");
        }
        new NativeBattleEffectArena(VramRegions).Validate();
        foreach (NativeBattleEffectTimUpload? slot in Slots)
        {
            if (slot is { Kind: 1 })
            {
                RequireSource(slot.Source!, slot.Offset, 12);
            }
            else if (slot is not null && (slot.Kind != 3 || slot.Copy is null))
            {
                throw new InvalidDataException("Native texture command requires its original TIM pointer or copy rectangle.");
            }
        }
        if (Animation is { } animation)
        {
            RequireSource(animation.Source, animation.BaseOffset, 6);
            RequireSource(animation.Source, animation.StreamOffset, 1);
            RequireSource(animation.Source, animation.ModelTableOffset, 2);
        }
    }

    public NativeBattleEffectTextureState Register509970(NativeBattleEffectHeapAllocation source, int offset = 0)
    {
        Validate();
        RequireSource(source, offset, 6);
        ReadOnlySpan<byte> data = source.Data.AsSpan().Slice(offset);
        NativeBattleEffectAnimationRegisters registers = new(source, offset, checked(offset + BinaryPrimitives.ReadUInt16LittleEndian(data[2..])), checked(offset + BinaryPrimitives.ReadUInt16LittleEndian(data[4..])));
        NativeBattleEffectTextureState result = this with { Animation = registers };
        result.Validate();
        return result;
    }

    public NativeBattleEffectTextureState Register5099A0(NativeBattleEffectHeapAllocation source, int offset = 0) => Register509970(source, offset);

    public (NativeBattleEffectTextureState State, int NextOffset) EnqueueTim505E30(NativeBattleEffectHeapAllocation source, int offset = 0)
    {
        Validate();
        if (Count == 31)
        {
            throw new NotSupportedException("Native505E30 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        RequireSource(source, offset, 12);
        int imageBlock = checked(offset + 8 + BlockLength(source, offset + 8));
        int next = checked(imageBlock + BlockLength(source, imageBlock));
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(source, offset)) with { Source = source, Offset = offset, Kind = 1 };
        return (this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 }, next);
    }

    public NativeBattleEffectTextureState EnqueueCopy505EB0(short sourceX, short sourceY, short width, short height, short destinationX, short destinationY)
    {
        Validate();
        if (Count == 31)
        {
            throw new NotSupportedException("Native505EB0 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Kind = 3, Copy = new(sourceX, sourceY, width, height, destinationX, destinationY) };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureDrain Drain505D20()
    {
        Validate();
        if (Count == 0)
        {
            return new(this, []);
        }
        NativeBattleEffectArena bank = new(VramRegions);
        ImmutableArray<NativeBattleEffectVramUpload>.Builder uploads = ImmutableArray.CreateBuilder<NativeBattleEffectVramUpload>();
        for (int index = 0; index < Count; index++)
        {
            NativeBattleEffectTimUpload pending = Slots[index]!;
            if (pending.Kind == 3)
            {
                Copy(pending.Copy!);
                continue;
            }
            NativeBattleEffectHeapAllocation source = pending.Source ?? throw new InvalidDataException("Native TIM upload lost its source.");
            int first = pending.Offset + 8;
            Upload(first);
            Upload(checked(first + BlockLength(source, first)));

            void Upload(int block)
            {
                int length = BlockLength(source, block);
                ReadOnlySpan<byte> data = source.Data.AsSpan().Slice(block, length);
                short x = BinaryPrimitives.ReadInt16LittleEndian(data[4..]);
                short y = BinaryPrimitives.ReadInt16LittleEndian(data[6..]);
                short width = BinaryPrimitives.ReadInt16LittleEndian(data[8..]);
                short height = BinaryPrimitives.ReadInt16LittleEndian(data[10..]);
                if (x < 0 || y < 0 || width < 0 || height < 0 || x + width > 1024 || y + height > 512 || 12L + 2L * width * height > length)
                {
                    throw new InvalidDataException("Native45BD30 upload leaves its source block or1024x512 word VRAM.");
                }
                ImmutableArray<byte> bytes = data.Slice(12, checked(2 * width * height)).ToArray().ToImmutableArray();
                for (int row = 0; row < height; row++)
                {
                    bank = bank.Write(checked(2 * ((y + row) * 1024 + x)), bytes.AsSpan().Slice(2 * row * width, 2 * width));
                }
                uploads.Add(new(x, y, width, height, bytes));
            }
        }
        return new(this with { Count = 0, VramRegions = bank.Regions }, uploads.ToImmutable());

        void Copy(NativeBattleEffectVramCopy copy)
        {
            if (copy.SourceX < 0 || copy.SourceY < 0 || copy.DestinationX < 0 || copy.DestinationY < 0 || copy.Width < 0 || copy.Height < 0 || copy.SourceX + copy.Width > 1024 || copy.DestinationX + copy.Width > 1024 || copy.SourceY + copy.Height > 512 || copy.DestinationY + copy.Height > 512)
            {
                throw new InvalidDataException("Native45BDD0 copy leaves its1024x512 word VRAM.");
            }
            byte[] memory = bank.Read(0, NativeBattleEffectArena.Length);
            int bytesPerRow = 2 * copy.Width;
            for (int row = 0; row < copy.Height; row++)
            {
                int read = 2 * ((copy.SourceY + row) * 1024 + copy.SourceX);
                int write = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
                int offset = 0;
                //45BDD0 uses forward REP MOVSD followed by REP MOVSB, including overlapping rectangles.
                for (; offset + 4 <= bytesPerRow; offset += 4)
                {
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(memory.AsSpan(read + offset, 4));
                    BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(write + offset, 4), value);
                }
                for (; offset < bytesPerRow; offset++)
                {
                    memory[write + offset] = memory[read + offset];
                }
            }
            byte[] changed = new byte[bytesPerRow * copy.Height];
            for (int row = 0; row < copy.Height; row++)
            {
                int write = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
                ReadOnlySpan<byte> bytes = memory.AsSpan(write, bytesPerRow);
                bank = bank.Write(write, bytes);
                bytes.CopyTo(changed.AsSpan(row * bytesPerRow));
            }
            uploads.Add(new(copy.DestinationX, copy.DestinationY, copy.Width, copy.Height, changed.ToImmutableArray()) { Copied = true });
        }
    }

    public byte[] ReadVram()
    {
        Validate();
        //45BD30's base1B47818 is PE zero-fill; the shared sparse1MB bank preserves untouched zeroes.
        return new NativeBattleEffectArena(VramRegions).Read(0, 1024 * 512 * 2);
    }

    private static int BlockLength(NativeBattleEffectHeapAllocation source, int offset)
    {
        RequireSource(source, offset, 12);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(offset));
        if (length < 12 || length > int.MaxValue)
        {
            throw new InvalidDataException("Native TIM upload has an invalid block length.");
        }
        RequireSource(source, offset, (int)length);
        return (int)length;
    }

    private static void RequireSource(NativeBattleEffectHeapAllocation source, int offset, int length)
    {
        if (source is null || string.IsNullOrEmpty(source.Identity) || source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
        {
            throw new InvalidDataException("Native effect resource pointer leaves its retained source allocation.");
        }
    }
}
