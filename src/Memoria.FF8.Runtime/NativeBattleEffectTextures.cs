using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectAnimationRegisters([property: JsonRequired] NativeBattleEffectHeapAllocation Source, [property: JsonRequired] int BaseOffset, [property: JsonRequired] int StreamOffset, [property: JsonRequired] int ModelTableOffset);
public sealed record NativeBattleEffectVramCopy(short SourceX, short SourceY, short Width, short Height, short DestinationX, short DestinationY);
public sealed record NativeBattleEffectTextureRectangle(short X, short Y, short Width, short Height);
public sealed record NativeBattleEffectTimUpload([property: JsonRequired] NativeBattleEffectHeapAllocation? Source, [property: JsonRequired] int Offset)
{
    public byte Kind { get; init; } = 1;
    public NativeBattleEffectVramCopy? Copy { get; init; }
    public NativeBattleEffectTextureRectangle? Rectangle { get; init; }
    public int? ArenaOffset { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? StagingAddress { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? ProcessAddress { get; init; }
}
public sealed record NativeBattleEffectVramUpload(short X, short Y, short Width, short Height, ImmutableArray<byte> Bytes)
{
    public bool Copied { get; init; }
}
public sealed record NativeBattleEffectTextureDrain(NativeBattleEffectTextureState State, ImmutableArray<NativeBattleEffectVramUpload> Uploads)
{
    public NativeBattleEffectArena? Arena { get; init; }
}

/// <summary>505E30/505D20's32 physical16-byte slots and45BD30's retained1024x51216-bit VRAM.</summary>
public sealed record NativeBattleEffectTextureState([property: JsonRequired] ImmutableArray<NativeBattleEffectTimUpload?> Slots, [property: JsonRequired] int Count, [property: JsonRequired] NativeBattleEffectAnimationRegisters? Animation, [property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> VramRegions)
{
    public const int PcPageReadTailLength = 384;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? VramReadTail { get; init; }

    public NativeBattleEffectTextureState Upload45BD30(short x, short y, short width, short height, ReadOnlySpan<byte> bytes)
    {
        Validate();
        if (x < 0 || y < 0 || width < 0 || height < 0 || x + width > 1024 || y + height > 512 || bytes.Length != 2 * width * height)
        {
            throw new InvalidDataException("Native45BD30 upload requires its exact bounded VRAM rectangle and source words.");
        }
        NativeBattleEffectArena memory = new(VramRegions);
        for (int row = 0; row < height; row++)
        {
            memory = memory.Write(2 * ((y + row) * 1024 + x), bytes.Slice(2 * row * width, 2 * width));
        }
        return this with { VramRegions = memory.Regions };
    }

    public static NativeBattleEffectTextureState FromColdProcess() => new(ImmutableArray.CreateRange(new NativeBattleEffectTimUpload?[32]), 0, null, []) { VramReadTail = new byte[PcPageReadTailLength].ToImmutableArray() };

    public NativeBattleEffectTextureState Initialize500400()
    {
        Validate();
        //500400 clears1D98420 only. Slots,509970 registers and VRAM survive battle initialization.
        return this with { Count = 0 };
    }

    public void Validate()
    {
        if (VramReadTail is { } tail && (tail.IsDefault || tail.Length != PcPageReadTailLength))
        {
            throw new InvalidDataException("Native PC texture captures require their complete retained trailing read window.");
        }
        if (Slots.IsDefault || Slots.Length != 32 || Count is < 0 or > 31 || Slots.Take(Count).Any(slot => slot is null))
        {
            throw new InvalidDataException("Native texture upload state requires32 physical slots and its successful0..31 pending count.");
        }
        new NativeBattleEffectArena(VramRegions).Validate();
        foreach (NativeBattleEffectTimUpload? slot in Slots)
        {
            if (slot is { Kind: 1, ProcessAddress: uint processAddress })
            {
                if (processAddress != 0x16146E8 || slot.StagingAddress is not null || slot.ArenaOffset is not null || slot.Source is not null || slot.Offset != 0)
                {
                    throw new InvalidDataException("Native process TIM requires its current retained physical pointer union.");
                }
            }
            else if (slot is { Kind: 1, StagingAddress: uint stagingAddress })
            {
                if (stagingAddress < NativeBattleFileStaging.Address || slot.ArenaOffset is not null || slot.Source is not null || slot.Offset != 0)
                {
                    throw new InvalidDataException("Native staging TIM requires its current shared199B018 pointer union.");
                }
            }
            else if (slot is { Kind: 1, ArenaOffset: int arenaOffset })
            {
                if (arenaOffset is < 0 or > NativeBattleEffectArena.Length - 12 || slot.Source is not null || slot.Offset != 0)
                {
                    throw new InvalidDataException("Native TIM pointer leaves the original effect arena.");
                }
            }
            else if (slot is { Kind: 1 })
            {
                RequireSource(slot.Source!, slot.Offset, 12);
            }
            else if (slot is { Kind: 0, Rectangle: { } rectangle })
            {
                RequireRectangle(rectangle);
                int length = checked(2 * rectangle.Width * rectangle.Height);
                if (slot.StagingAddress is not null)
                {
                    throw new InvalidDataException("Native rectangle upload cannot use a staging TIM pointer.");
                }
                if (slot.ProcessAddress is uint rectangleAddress)
                {
                    RequireScriptedRange(rectangleAddress, length);
                    if (slot.ArenaOffset is not null || slot.Source is not null || slot.Offset != 0)
                    {
                        throw new InvalidDataException("Native scripted rectangle upload requires one source pointer union arm.");
                    }
                }
                else if (slot.ArenaOffset is int rectangleOffset)
                {
                    RequireArenaRange(rectangleOffset, length);
                    if (slot.Source is not null || slot.Offset != 0)
                    {
                        throw new InvalidDataException("Native rectangle upload requires one source pointer union arm.");
                    }
                }
                else
                {
                    RequireSource(slot.Source!, slot.Offset, length);
                }
            }
            else if (slot is { Kind: 2, Rectangle: { } readback })
            {
                RequireRectangle(readback);
                RequireArenaRange(slot.ArenaOffset ?? throw new InvalidDataException("Native readback requires its actual arena destination."), checked(2 * readback.Width * readback.Height));
                if (slot.Source is not null || slot.Offset != 0 || slot.StagingAddress is not null || slot.ProcessAddress is not null)
                {
                    throw new InvalidDataException("Native readback requires one destination pointer union arm.");
                }
            }
            else if (slot is not null && (slot.Kind != 3 || slot.Copy is null || slot.ArenaOffset is not null || slot.StagingAddress is not null || slot.ProcessAddress is not null))
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
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(source, offset)) with { Source = source, Offset = offset, Kind = 1, ArenaOffset = null, StagingAddress = null, ProcessAddress = null };
        return (this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 }, next);
    }

    public (NativeBattleEffectTextureState State, int NextOffset) EnqueueArenaTim505E30(NativeBattleEffectArena arena, int offset)
    {
        Validate();
        if (Count == 31)
        {
            throw new NotSupportedException("Native505E30 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectHeapAllocation current = MaterializeArenaTim(arena, offset);
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Source = null, Offset = 0, Kind = 1, ArenaOffset = offset, StagingAddress = null, ProcessAddress = null };
        return (this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 }, checked(offset + current.Data.Length));
    }

    public (NativeBattleEffectTextureState State, uint NextAddress) EnqueueStagingTim505E30(NativeBattleFileStaging staging, uint address)
    {
        Validate();
        if (Count == 31)
        {
            throw new NotSupportedException("Native505E30 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectHeapAllocation current = MaterializeStagingTim(staging, address);
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Source = null, Offset = 0, Kind = 1, ArenaOffset = null, StagingAddress = address, ProcessAddress = null };
        return (this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 }, unchecked(address + (uint)current.Data.Length));
    }

    public (NativeBattleEffectTextureState State, uint NextAddress) EnqueueProcessTim505E30(uint address, NativeBattleEffectHeapAllocation current)
    {
        Validate();
        if (address != 0x16146E8)
        {
            throw new NotSupportedException("Native process TIM pointer requires its supported retained resource owner.");
        }
        if (Count == 31)
        {
            throw new NotSupportedException("Native505E30 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        RequireSource(current, 0, 12);
        int imageBlock = checked(8 + BlockLength(current, 8));
        int next = checked(imageBlock + BlockLength(current, imageBlock));
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Source = null, Offset = 0, Kind = 1, ArenaOffset = null, StagingAddress = null, ProcessAddress = address };
        return (this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 }, checked(address + (uint)next));
    }

    public NativeBattleEffectTextureState EnqueueCopy505EB0(short sourceX, short sourceY, short width, short height, short destinationX, short destinationY)
    {
        Validate();
        if (Count == 31)
        {
            throw new NotSupportedException("Native505EB0 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Kind = 3, Copy = new(sourceX, sourceY, width, height, destinationX, destinationY), Source = null, Offset = 0, ArenaOffset = null, StagingAddress = null, ProcessAddress = null };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureState EnqueueArenaRectangle505DF0(NativeBattleEffectTextureRectangle rectangle, NativeBattleEffectArena arena, int offset)
    {
        Validate();
        RequireRectangle(rectangle);
        arena.Validate();
        RequireArenaRange(offset, checked(2 * rectangle.Width * rectangle.Height));
        if (Count == 31)
        {
            throw new NotSupportedException("Native505DF0 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Kind = 0, Source = null, Offset = 0, Rectangle = rectangle, ArenaOffset = offset, StagingAddress = null, ProcessAddress = null };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureState EnqueueRectangle505DF0(NativeBattleEffectTextureRectangle rectangle, NativeBattleEffectHeapAllocation source, int offset = 0)
    {
        Validate();
        RequireRectangle(rectangle);
        RequireSource(source, offset, checked(2 * rectangle.Width * rectangle.Height));
        if (Count == 31)
        {
            throw new NotSupportedException("Native505DF0 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(source, offset)) with { Kind = 0, Source = source, Offset = offset, Rectangle = rectangle, ArenaOffset = null, StagingAddress = null, ProcessAddress = null };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureState EnqueueScriptedRectangle505DF0(NativeBattleEffectTextureRectangle rectangle, uint address)
    {
        Validate();
        RequireRectangle(rectangle);
        RequireScriptedRange(address, checked(2 * rectangle.Width * rectangle.Height));
        if (Count == 31)
        {
            throw new NotSupportedException("Native505DF0 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Kind = 0, Source = null, Offset = 0, Rectangle = rectangle, ArenaOffset = null, StagingAddress = null, ProcessAddress = address };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureState EnqueueArenaReadback505E70(NativeBattleEffectTextureRectangle rectangle, int destinationOffset)
    {
        Validate();
        RequireRectangle(rectangle);
        RequireArenaRange(destinationOffset, checked(2 * rectangle.Width * rectangle.Height));
        if (Count == 31)
        {
            throw new NotSupportedException("Native505E70 increments its count to32 without writing the final slot; the resulting stale-slot drain is unsupported.");
        }
        NativeBattleEffectTimUpload slot = (Slots[Count] ?? new(null, 0)) with { Kind = 2, Source = null, Offset = 0, Rectangle = rectangle, ArenaOffset = destinationOffset, StagingAddress = null, ProcessAddress = null };
        return this with { Slots = Slots.SetItem(Count, slot), Count = Count + 1 };
    }

    public NativeBattleEffectTextureDrain Drain505D20(NativeBattleEffectArena? arenaSource = null, NativeBattleFileStaging? stagingSource = null, Func<uint, NativeBattleEffectHeapAllocation>? resolveProcessTim = null, NativeBattleScriptedEffectMemory? scriptedSource = null)
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
            if (pending.Kind == 2)
            {
                NativeBattleEffectTextureRectangle rectangle = pending.Rectangle!;
                NativeBattleEffectArena destination = arenaSource ?? throw new NotSupportedException("Native505D20 readback requires its latest effect arena destination.");
                int bytesPerRow = checked(2 * rectangle.Width);
                for (int row = 0; row < rectangle.Height; row++)
                {
                    byte[] bytes = bank.Read(checked(2 * ((rectangle.Y + row) * 1024 + rectangle.X)), bytesPerRow);
                    destination = destination.Write(checked(pending.ArenaOffset!.Value + row * bytesPerRow), bytes);
                }
                arenaSource = destination;
                continue;
            }
            if (pending.Kind == 3)
            {
                Copy(pending.Copy!);
                continue;
            }
            if (pending.Kind == 0)
            {
                NativeBattleEffectTextureRectangle rectangle = pending.Rectangle!;
                int length = checked(2 * rectangle.Width * rectangle.Height);
                if (pending.ProcessAddress is uint rectangleAddress)
                {
                    NativeBattleScriptedEffectMemory current = scriptedSource ?? throw new NotSupportedException("Native505D20 requires the latest scripted process memory for its retained mutable rectangle pointer.");
                    Write(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, current.Read(rectangleAddress, length));
                }
                else if (pending.ArenaOffset is int rectangleOffset)
                {
                    NativeBattleEffectArena current = arenaSource ?? throw new NotSupportedException("Native505D20 requires the latest effect arena for its retained mutable rectangle pointer.");
                    Write(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, current.Read(rectangleOffset, length));
                }
                else
                {
                    Write(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, pending.Source!.Data.AsSpan().Slice(pending.Offset, length));
                }
                continue;
            }
            NativeBattleEffectHeapAllocation source = pending.ProcessAddress is uint processAddress ? (resolveProcessTim ?? throw new NotSupportedException("Native505D20 requires the current retained process TIM owner."))(processAddress) : pending.StagingAddress is uint stagingAddress ? MaterializeStagingTim(stagingSource ?? throw new NotSupportedException("Native505D20 requires the latest shared199B018 staging for its retained mutable TIM pointer."), stagingAddress) : pending.ArenaOffset is int arenaOffset ? MaterializeArenaTim(arenaSource ?? throw new NotSupportedException("Native505D20 requires the latest effect arena for its retained mutable TIM pointer."), arenaOffset) : pending.Source ?? throw new InvalidDataException("Native TIM upload lost its source.");
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
                Write(x, y, width, height, data.Slice(12, checked(2 * width * height)));
            }

            void Write(short x, short y, short width, short height, ReadOnlySpan<byte> data)
            {
                ImmutableArray<byte> bytes = data.ToArray().ToImmutableArray();
                NativeBattleEffectArenaWriter writer = new(bank);
                for (int row = 0; row < height; row++)
                {
                    writer.Write(checked(2 * ((y + row) * 1024 + x)), bytes.AsSpan().Slice(2 * row * width, 2 * width));
                }
                bank = writer.Freeze();
                uploads.Add(new(x, y, width, height, bytes));
            }
        }
        return new(this with { Count = 0, VramRegions = bank.Regions }, uploads.ToImmutable()) { Arena = arenaSource };

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
            NativeBattleEffectArenaWriter writer = new(bank);
            for (int row = 0; row < copy.Height; row++)
            {
                int write = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
                ReadOnlySpan<byte> bytes = memory.AsSpan(write, bytesPerRow);
                writer.Write(write, bytes);
                bytes.CopyTo(changed.AsSpan(row * bytesPerRow));
            }
            bank = writer.Freeze();
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

    private static NativeBattleEffectHeapAllocation MaterializeArenaTim(NativeBattleEffectArena arena, int offset)
    {
        return MaterializeMutableTim($"20DFAB8+{offset:X}", (relative, count) => arena.Read(checked(offset + relative), count));
    }

    private static NativeBattleEffectHeapAllocation MaterializeStagingTim(NativeBattleFileStaging staging, uint address)
    {
        return MaterializeMutableTim($"199B018:{address:X8}", (relative, count) => staging.Read(unchecked(address + (uint)relative), count));
    }

    private static NativeBattleEffectHeapAllocation MaterializeMutableTim(string name, Func<int, int, byte[]> read)
    {
        int first = BinaryPrimitives.ReadInt32LittleEndian(read(8, 4));
        if (first < 12)
        {
            throw new InvalidDataException("Native mutable TIM has no actual CLUT block.");
        }
        int second = BinaryPrimitives.ReadInt32LittleEndian(read(checked(8 + first), 4));
        if (second < 12)
        {
            throw new InvalidDataException("Native mutable TIM has no actual image block.");
        }
        return new(name, read(0, checked(8 + first + second)).ToImmutableArray());
    }

    private static void RequireRectangle(NativeBattleEffectTextureRectangle rectangle)
    {
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.Width < 0 || rectangle.Height < 0 || rectangle.X + rectangle.Width > 1024 || rectangle.Y + rectangle.Height > 512)
        {
            throw new InvalidDataException("Native505DF0 rectangle leaves its1024x512 word VRAM.");
        }
    }

    private static void RequireArenaRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Native rectangle pointer leaves the original effect arena.");
        }
    }

    private static void RequireScriptedRange(uint address, int length)
    {
        long offset = (long)address - NativeBattleScriptedEffectMemory.Address;
        if (offset < 0 || length < 0 || offset > NativeBattleScriptedEffectMemory.Length - length)
        {
            throw new InvalidDataException("Native rectangle pointer leaves the current scripted process owner.");
        }
    }

    private static void RequireSource(NativeBattleEffectHeapAllocation source, int offset, int length)
    {
        if (source is null || string.IsNullOrEmpty(source.Identity) || source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
        {
            throw new InvalidDataException("Native effect resource pointer leaves its retained source allocation.");
        }
    }
}
