using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Caller-owned current bytes at a native model resource address; mutable EXE banks must be supplied from their actual owner.</summary>
public record NativeBattleGfAnimationSource
{
    private ImmutableArray<byte> data;
    private NativeBattleEffectArena? arena;
    private NativeBattleFileStaging? staging;
    private uint stagingAddress;
    private int offset;
    private int length;
    internal NativeBattleGfSourceReader? ActiveReader;

    public NativeBattleGfAnimationSource(uint Address, ImmutableArray<byte> Data)
    {
        this.Address = Address;
        data = Data;
        length = Data.IsDefault ? 0 : Data.Length;
    }

    protected NativeBattleGfAnimationSource(uint address, NativeBattleEffectArena arena)
    {
        Address = address;
        this.arena = arena;
        length = arena.LogicalLength;
    }

    internal virtual NativeBattleEffectArena? FrozenArena => null;

    [JsonRequired]
    public uint Address { get; init; }
    [JsonRequired]
    public virtual ImmutableArray<byte> Data
    {
        get => arena is null && staging is null ? data : throw new InvalidOperationException("Borrowed animation sources require bounded readers, not a payload export.");
        init
        {
            data = value;
            arena = null;
            staging = null;
            stagingAddress = 0;
            offset = 0;
            length = value.IsDefault ? 0 : value.Length;
            ActiveReader = null;
        }
    }
    [JsonIgnore]
    public int Length => length;
    [JsonIgnore]
    internal bool IsDefault => arena is null && staging is null && data.IsDefault;
    internal NativeBattleEffectArena? Arena => arena;
    internal int Offset => offset;
    internal NativeBattleFileStaging? Staging => staging;
    internal uint StagingAddress => stagingAddress;
    internal ImmutableArray<byte> CatalogData => data;

    internal static NativeBattleGfAnimationSource Borrow(uint address, NativeBattleEffectArena arena, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > arena.LogicalLength - length)
        {
            throw new InvalidDataException("Native borrowed animation source leaves its actual physical owner.");
        }
        return new(address, default(ImmutableArray<byte>)) { arena = arena, offset = offset, length = length };
    }

    internal static NativeBattleGfAnimationSource Borrow(uint address, NativeBattleFileStaging staging, int length)
    {
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
        return new(address, default(ImmutableArray<byte>)) { staging = staging, stagingAddress = address, length = length };
    }

    internal NativeBattleGfSourceReader OpenReader() => new(this);
}

/// <summary>701970's explicitly cleared 44-byte callback record, plus decoded source provenance.</summary>
public sealed record NativeBattleGfAnimationNode(int ResourceOffset, uint AnimationFlags, ImmutableArray<byte> Data)
{
    public ushort NodeIndex => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan());
    public ushort GeometryIndex => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan().Slice(2));
    public uint Header => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(4));
    public NativeBattleVector Vector8 => Vector(8);
    public NativeBattleVector Vector16 => Vector(16);
    public NativeBattleVector Vector24 => Vector(24);
    public uint Color32 => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(32));
    public short Scalar36 => Word(36);
    public short Scalar38 => Word(38);
    public short Word40 => Word(40);
    public short Word42 => Word(42);

    private short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan().Slice(offset));
    private NativeBattleVector Vector(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4));
}

public sealed record NativeBattleGfAnimationAdvance(NativeBattleGfAnimationWorkspace Workspace, int Remaining, int EmittedNodes);

/// <summary>7016B0/701970's native pointer/frame header and variable-sized derivative caches. Existing bytes are never assumed zero.</summary>
public sealed partial record NativeBattleGfAnimationWorkspace
{
    [JsonIgnore] public uint ResourceAddress => arena.ReadUInt32(0);
    [JsonIgnore] public int Frame => arena.ReadInt32(4);

    public NativeBattleGfAnimationWorkspace Initialize7016B0(NativeBattleGfAnimationSource source, int sizeBytes)
    {
        return Initialize(source, sizeBytes);
    }

    /// <summary>88A7D0 is instruction-equivalent to7016B0 except its DWORD-clear function address.</summary>
    public NativeBattleGfAnimationWorkspace Initialize88A7D0(NativeBattleGfAnimationSource source, int sizeBytes)
    {
        return Initialize(source, sizeBytes);
    }

    private NativeBattleGfAnimationWorkspace Initialize(NativeBattleGfAnimationSource source, int sizeBytes)
    {
        Validate(source);
        if (sizeBytes < 0 || (sizeBytes >> 2) > Length / 4)
        {
            throw new InvalidDataException("Native7016B0 DWORD clear exceeds its observed workspace bytes.");
        }
        using NativeBattleGfSourceReader resource = source.OpenReader();
        using MutationScope mutation = new(this);
        CacheBuffer workspace = new(mutation.View);
        workspace.Clear((sizeBytes >> 2) * 4);
        workspace.UInt(0, source.Address);
        int table = resource.Int(4);
        int count = resource.Int(table);
        int cache = 8;
        for (int index = 0; index < count; index++)
        {
            int node = checked(table + resource.Int(checked(table + 8 + index * 4)));
            uint flags = resource.UInt(checked(node + 4));
            cache = checked(cache + VectorSize(flags) + VectorSize(flags >> 3));
            if ((flags & 0x1C0) != 0)
            {
                workspace.Int(cache, 0x10000000);
                workspace.Int(cache + 4, 0x10000000);
                workspace.Int(cache + 8, 0x10000000);
            }
            cache = checked(cache + VectorSize(flags >> 6));
            cache = checked(cache + ((flags & 0x800) != 0 ? 24 : (flags & 0x400) != 0 ? 16 : (flags & 0x200) != 0 ? 8 : 0));
            cache = checked(cache + ((flags & 0x1000) != 0 ? 12 : 0) + ((flags & 0x2000) != 0 ? 16 : 0));
        }
        mutation.PublishPrefix();
        return this;
    }

    public NativeBattleGfAnimationAdvance Advance701970(NativeBattleGfAnimationSource source, int frozen, Func<NativeBattleGfAnimationWorkspace, NativeBattleGfAnimationNode, NativeBattleGfAnimationWorkspace> emit)
    {
        return Advance(source, frozen, false, emit);
    }

    /// <summary>88A980 always integrates and advances; its exact-frame morph keys use DWORD-spaced frame selectors.</summary>
    public NativeBattleGfAnimationAdvance Advance88A980(NativeBattleGfAnimationSource source, Func<NativeBattleGfAnimationWorkspace, NativeBattleGfAnimationNode, NativeBattleGfAnimationWorkspace> emit)
    {
        return Advance(source, 0, true, emit);
    }

    private NativeBattleGfAnimationAdvance Advance(NativeBattleGfAnimationSource source, int frozen, bool curaMorphKeys, Func<NativeBattleGfAnimationWorkspace, NativeBattleGfAnimationNode, NativeBattleGfAnimationWorkspace> emit)
    {
        Validate(source);
        ArgumentNullException.ThrowIfNull(emit);
        if (ResourceAddress != source.Address)
        {
            throw new InvalidDataException("Native701970 source bytes do not identify the workspace's retained native resource pointer.");
        }
        using NativeBattleGfSourceReader resource = source.OpenReader();
        using MutationScope mutation = new(this);
        CacheBuffer workspace = new(mutation.View);
        int table = resource.Int(4);
        int duration = resource.Int(checked(table + 4));
        if (workspace.Int(4) >= duration)
        {
            return new(this, 0, 0);
        }
        int count = resource.Int(table);
        int cache = 8;
        int emitted = 0;
        for (int index = 0; index < count; index++)
        {
            int node = checked(table + resource.Int(checked(table + 8 + index * 4)));
            uint header = resource.UInt(node);
            uint flags = resource.UInt(checked(node + 4));
            int cursor = checked(node + 8);
            Buffer output = new(new byte[44]); // 701970 explicitly calls 701200(output, 0, 11).
            output.UShort(0, unchecked((ushort)index));
            output.UShort(2, (byte)header);
            output.UInt(4, header);
            output.Short(24, 4096);
            output.Short(26, 4096);
            output.Short(28, 4096);
            cache = Vector701820(flags, node, ref cursor, cache, 8);
            cache = Vector701820(flags >> 3, node, ref cursor, cache, 16);
            cache = Vector701820(flags >> 6, node, ref cursor, cache, 24);
            if (frozen == 0)
            {
                if ((flags & 0x800) != 0)
                {
                    for (int component = 0; component < 3; component++)
                    {
                        int offset = component * 2;
                        workspace.Short(cache + 8 + offset, unchecked((short)(workspace.Short(cache + 8 + offset) + workspace.Short(cache + 16 + offset))));
                    }
                }
                if ((flags & 0xC00) != 0)
                {
                    for (int component = 0; component < 3; component++)
                    {
                        int offset = component * 2;
                        workspace.UShort(cache + offset, (ushort)Math.Clamp(workspace.UShort(cache + offset) + workspace.Short(cache + 8 + offset), 0, 65535));
                    }
                }
            }
            if ((flags & 0xE00) != 0)
            {
                Color();
            }
            if ((flags & 0x200) != 0)
            {
                TripleWords701790(NextStream(), cache);
                Color();
            }
            if ((flags & 0xE00) != 0)
            {
                cache += 8;
            }
            if ((flags & 0x400) != 0)
            {
                TripleWords701790(NextStream(), cache);
            }
            if ((flags & 0xC00) != 0)
            {
                cache += 8;
            }
            if ((flags & 0x800) != 0)
            {
                TripleWords701790(NextStream(), cache);
                cache += 8;
            }
            if ((flags & 0x1000) != 0)
            {
                IntegrateScalar(cache);
                int key = KeyFrame(NextStream(), 16);
                if (key >= 0)
                {
                    workspace.Int(cache, resource.Int(key + 4));
                    workspace.Int(cache + 4, resource.Int(key + 8));
                    workspace.Int(cache + 8, resource.Int(key + 12));
                }
                output.Short(36, unchecked((short)(workspace.Int(cache) >> 16)));
                cache += 12;
            }
            if ((flags & 0x2000) != 0)
            {
                IntegrateScalar(cache + 4);
                int key = KeyFrame(NextStream(), 20);
                if (key >= 0)
                {
                    workspace.Short(cache, resource.Short(key + 4));
                    workspace.Short(cache + 2, resource.Short(key + (curaMorphKeys ? 8 : 6)));
                    int values = key + (curaMorphKeys ? 12 : 8);
                    workspace.Int(cache + 4, resource.Int(values));
                    workspace.Int(cache + 8, resource.Int(values + 4));
                    workspace.Int(cache + 12, resource.Int(values + 8));
                }
                output.Short(40, workspace.Short(cache));
                output.Short(42, workspace.Short(cache + 2));
                output.Short(38, unchecked((short)(workspace.Int(cache + 4) >> 16)));
                cache += 16;
            }
            NativeBattleGfAnimationWorkspace beforeCallback = mutation.View;
            NativeBattleGfAnimationWorkspace afterCallback = emit(beforeCallback, new(node, flags, NativeBattleTelemetry.Immutable(1238, output.Bytes))) ?? throw new InvalidDataException("Native701970 callback lost its workspace owner.");
            if (afterCallback.Length != Length)
            {
                throw new InvalidDataException("Native701970 callback changed its physical workspace extent.");
            }
            mutation.View.CopyStateFrom(afterCallback);
            mutation.PublishPrefix();
            emitted++;

            int NextStream()
            {
                int stream = checked(node + resource.Int(cursor));
                cursor += 4;
                return stream;
            }

            int KeyFrame(int stream, int stride)
            {
                int frame = workspace.Int(4);
                int time = resource.Int(stream);
                while (time < frame)
                {
                    if (time == -1)
                    {
                        return -1;
                    }
                    stream = checked(stream + stride);
                    time = resource.Int(stream);
                }
                return time == frame ? stream : -1;
            }

            void TripleWords701790(int stream, int destination)
            {
                int key = KeyFrame(stream, 12);
                if (key >= 0)
                {
                    for (int component = 0; component < 3; component++)
                    {
                        workspace.Short(destination + component * 2, resource.Short(key + 4 + component * 2));
                    }
                }
            }

            int Vector701820(uint vectorFlags, int baseOffset, ref int streamCursor, int destination, int outputOffset)
            {
                vectorFlags &= 7;
                if (frozen == 0)
                {
                    if ((vectorFlags & 4) != 0)
                    {
                        for (int component = 0; component < 3; component++)
                        {
                            int offset = component * 4;
                            workspace.Int(destination + 12 + offset, unchecked(workspace.Int(destination + 12 + offset) + workspace.Int(destination + 24 + offset)));
                        }
                    }
                    if ((vectorFlags & 6) != 0)
                    {
                        for (int component = 0; component < 3; component++)
                        {
                            int offset = component * 4;
                            workspace.Int(destination + offset, unchecked(workspace.Int(destination + offset) + workspace.Int(destination + 12 + offset)));
                        }
                    }
                }
                if (vectorFlags != 0)
                {
                    VectorOutput();
                }
                for (int derivative = 0; derivative < 3; derivative++)
                {
                    if ((vectorFlags & 1u << derivative) != 0)
                    {
                        int stream = checked(baseOffset + resource.Int(streamCursor));
                        streamCursor += 4;
                        int key = KeyFrame(stream, 16);
                        if (key >= 0)
                        {
                            for (int component = 0; component < 3; component++)
                            {
                                workspace.Int(destination + derivative * 12 + component * 4, resource.Int(key + 4 + component * 4));
                            }
                        }
                        if (derivative == 0)
                        {
                            VectorOutput();
                        }
                    }
                }
                return checked(destination + VectorSize(vectorFlags));

                void VectorOutput()
                {
                    for (int component = 0; component < 3; component++)
                    {
                        output.Short(outputOffset + component * 2, unchecked((short)(workspace.Int(destination + component * 4) >> 16)));
                    }
                }
            }

            void Color()
            {
                for (int component = 0; component < 3; component++)
                {
                    output.Byte(32 + component, (byte)(workspace.UShort(cache + component * 2) >> 8));
                }
                output.Byte(35, 0);
            }

            void IntegrateScalar(int offset)
            {
                if (frozen != 0)
                {
                    return;
                }
                int velocity = unchecked(workspace.Int(offset + 4) + workspace.Int(offset + 8));
                workspace.Int(offset + 4, velocity);
                workspace.Int(offset, Math.Clamp(unchecked(workspace.Int(offset) + velocity), 0, 0x10000000));
            }
        }
        if (frozen == 0)
        {
            workspace.Int(4, unchecked(workspace.Int(4) + 1));
        }
        mutation.PublishPrefix();
        return new(this, unchecked(duration - workspace.Int(4)), emitted);
    }

    private void Validate(NativeBattleGfAnimationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Length < 8 || source.IsDefault || source.Length < 8)
        {
            throw new InvalidDataException("Native GF animation requires the actual source and retained pointer/frame workspace bytes.");
        }
    }

    private static int VectorSize(uint flags) => (flags & 4) != 0 ? 36 : (flags & 2) != 0 ? 24 : (flags & 1) != 0 ? 12 : 0;

    private sealed class Buffer(byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;
        public int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(At(offset, 4));
        public uint UInt(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(At(offset, 4));
        public short Short(int offset) => BinaryPrimitives.ReadInt16LittleEndian(At(offset, 2));
        public ushort UShort(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(At(offset, 2));
        public void Int(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(At(offset, 4), value);
        public void UInt(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(At(offset, 4), value);
        public void Short(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(At(offset, 2), value);
        public void UShort(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(At(offset, 2), value);
        public void Byte(int offset, byte value) => At(offset, 1)[0] = value;

        private Span<byte> At(int offset, int length)
        {
            if (offset < 0 || offset > Bytes.Length - length)
            {
                throw new InvalidDataException("Native GF animation access exceeds its observed resource/workspace bytes.");
            }
            return Bytes.AsSpan(offset, length);
        }
    }
}
