using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectFileRequest(short FileId, uint DestinationAddress, uint CompletionCallback);
public sealed record NativeBattleEffectIoAdvance(NativeBattleEffectMemory Memory, NativeBattleEffectFileRequest? Request);
public sealed record NativeBattleEffectIoPoll(NativeBattleEffectMemory Memory, bool Busy, NativeBattleEffectFileRequest? Request);
public sealed record NativeBattleSummonStreamRequest(uint SourceAddress, int Number);
public sealed record NativeBattleEffectIoCompletion(NativeBattleEffectMemory Memory, NativeBattleSummonStreamRequest? Stream);

/// <summary>534110/5341D0/534210's original mutable effect-arena descriptor stream, separate from482590's completion scheduler.</summary>
public static class NativeBattleEffectIo
{
    public static NativeBattleEffectMemory Initialize534110(NativeBattleEffectMemory memory, int offset)
    {
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native534110 requires the retained effect IO callback owner.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native534110 requires the actual571870 effect arena.");
        arena = arena.Write(offset, [0, 0, 1, 0, 0]);
        return memory with { Arena = arena, EffectIo = io with { ControllerOffset = offset, BaseOffset = offset } };
    }

    public static NativeBattleEffectMemory Queue5341D0(NativeBattleEffectMemory memory, short fileId, uint destinationAddress, short type)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native5341D0 requires its actual effect descriptor bytes.");
        int controller = memory.EffectIo?.BaseOffset ?? throw new NotSupportedException("Native5341D0 requires its actual534110 base pointer.");
        byte count = arena.Read(controller + 3, 1)[0];
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(descriptor, type);
        BinaryPrimitives.WriteInt16LittleEndian(descriptor.AsSpan(2), fileId);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), destinationAddress);
        arena = arena.Write(controller + 8 + count * 8, descriptor).Write(controller + 3, [unchecked((byte)(count + 1))]);
        return memory with { Arena = arena };
    }

    public static NativeBattleEffectIoAdvance Advance534210(NativeBattleEffectMemory memory)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native534210 requires its actual effect descriptor bytes.");
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native534210 requires its retained completion callback.");
        int controller = io.BaseOffset ?? throw new NotSupportedException("Native534210 requires its actual534110 base pointer.");
        byte[] header = arena.Read(controller, 5);
        if (header[0] != 0)
        {
            return new(memory, null);
        }
        if (header[4] < header[3] && header[1] == 0)
        {
            byte[] descriptor = arena.Read(controller + 8 + header[4] * 8, 8);
            NativeBattleEffectFileRequest request = new(BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(2)), BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4)), io.Callback);
            return new(memory with { Arena = arena.Write(controller, [1]) }, request);
        }
        if (header[1] != 0)
        {
            memory = memory with { Arena = arena.Write(controller + 1, [unchecked((byte)(header[1] - 1))]) };
        }
        return new(memory, null);
    }

    public static NativeBattleEffectIoPoll Poll534270(NativeBattleEffectMemory memory)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native534270 requires its actual effect descriptor bytes.");
        int controller = memory.EffectIo?.BaseOffset ?? throw new NotSupportedException("Native534270 requires its actual534110 base pointer.");
        byte[] header = arena.Read(controller, 5);
        if (header[3] == header[4] && header[0] == 0 && header[2] != 0 && header[1] == 0)
        {
            arena = arena.Write(controller + 2, [1, 0, 0]);
            return new(memory with { Arena = arena }, false, null);
        }
        NativeBattleEffectIoAdvance advance = Advance534210(memory);
        return new(advance.Memory, true, advance.Request);
    }

    public static NativeBattleEffectIoCompletion Complete534150(NativeBattleEffectMemory memory, Func<uint, int?, NativeBattleEffectHeapAllocation>? resolveSource = null)
    {
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native534150 requires its retained callback/controller pointers.");
        if (io.Callback != 0x534150)
        {
            throw new InvalidDataException("Native534150 was not selected by the actual effect initializer.");
        }
        return CompleteDescriptor(memory, resolveSource ?? ReadArena, false);

        NativeBattleEffectHeapAllocation ReadArena(uint address, int? length)
        {
            if (address < NativeBattleEffectNodeMemory.ArenaAddress || address - NativeBattleEffectNodeMemory.ArenaAddress >= NativeBattleEffectArena.Length || length is not int count)
            {
                throw new NotSupportedException("Native534150 source requires its actual bounded current allocation.");
            }
            int offset = checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
            return new($"20DFAB8+{offset:X}", (memory.Arena ?? throw new NotSupportedException("Native534150 requires its current effect arena.")).Read(offset, count).ToImmutableArray());
        }
    }

    public static NativeBattleEffectIoCompletion Complete6C3580(NativeBattleEffectMemory memory, Func<uint, int?, NativeBattleEffectHeapAllocation> resolveSource)
    {
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native6C3580 requires its retained callback/controller pointers.");
        if (io.Callback != 0x6C3580)
        {
            throw new InvalidDataException("Native6C3580 was not selected by the actual6C3560 initializer.");
        }
        return CompleteDescriptor(memory, resolveSource, true);
    }

    private static NativeBattleEffectIoCompletion CompleteDescriptor(NativeBattleEffectMemory memory, Func<uint, int?, NativeBattleEffectHeapAllocation> resolveSource, bool quezacotl)
    {
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native effect completion requires its retained descriptor callback owner.");
        int controller = io.ControllerOffset ?? throw new NotSupportedException("Native6C3580 requires its actual534110 current controller.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C3580 requires its original effect arena.");
        NativeBattleEffectTextureState? textures = memory.Textures;
        byte[] header = arena.Read(controller, 5);
        byte[] descriptor = arena.Read(controller + 8 + header[4] * 8, 8);
        short type = BinaryPrimitives.ReadInt16LittleEndian(descriptor);
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4));
        NativeBattleSummonStreamRequest? stream = null;
        byte delay;
        if (type <= 2)
        {
            if (textures is null)
            {
                throw new NotSupportedException("Native effect completion requires the actual505E30 upload queue.");
            }
            if (pointer is >= 0x20DFAB8 and < 0x21DFAB8)
            {
                (textures, _) = textures.EnqueueArenaTim505E30(arena, checked((int)(pointer - 0x20DFAB8)));
            }
            else
            {
                (textures, _) = textures.EnqueueTim505E30(resolveSource(pointer, null));
            }
            delay = unchecked((byte)type);
        }
        else if (type <= 4)
        {
            //501860 acknowledges immediately after publishing pointer1D96DC0 and number1D96DB0.
            NativeBattleEffectHeapAllocation source = resolveSource(pointer, 4);
            if (source.Data.Length < 4)
            {
                throw new InvalidDataException("Native501860 stream request lacks its original leading DWORD.");
            }
            stream = new(pointer, unchecked(BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()) + 690000));
            arena = arena.Write(controller + 2, [1]);
            delay = unchecked((byte)(type - 3));
        }
        else
        {
            if (quezacotl && type == 0x10)
            {
                if (textures is null)
                {
                    throw new NotSupportedException("Native6C3580 requires its actual505E30 upload queue.");
                }
                foreach (int offset in new[] { 0x2C6A4, 0x348C4, 0x38AE4 })
                {
                    (textures, _) = textures.EnqueueArenaTim505E30(arena, offset);
                }
            }
            delay = 0;
        }
        arena = arena.Write(controller, [0, delay]).Write(controller + 4, [unchecked((byte)(header[4] + 1))]);
        memory = memory with { Arena = arena, Textures = textures };
        if (stream is not null)
        {
            memory = memory with { SummonStream = new(stream.Number, stream.SourceAddress) };
        }
        return new(memory, stream);
    }

}
