using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A callback's current addressed executable view, published back to the same typed retained bank after its exact stores.</summary>
internal sealed class NativeBattleEffectNodeBank : IDisposable
{
    private readonly Func<uint, int, byte[]>? readExternal;
    private readonly Func<uint, ImmutableArray<byte>>? readResource;
    internal uint Address { get; }
    private ImmutableArray<byte> data;
    private readonly NativeBattleNodeBankOperation? operation;
    internal ImmutableArray<byte> Data => operation is null ? data : throw new InvalidOperationException("Working node storage requires explicit detached capture.");
    internal NativeBattleNodeSource Source => operation?.Source ?? throw new InvalidOperationException("Catalog node bytes have no retained working authority.");
    internal bool IsWorking => operation is not null;
    internal int Length => operation?.Length ?? data.Length;

    internal NativeBattleEffectNodeBank(uint address, ImmutableArray<byte> data, Func<uint, int, byte[]>? readExternal = null, Func<uint, ImmutableArray<byte>>? readResource = null)
    {
        if (data.IsDefaultOrEmpty || (ulong)address + (uint)data.Length > uint.MaxValue)
        {
            throw new InvalidDataException("Native node executable view requires its actual current addressed bytes.");
        }
        Address = address;
        this.data = data;
        this.readExternal = readExternal;
        this.readResource = readResource;
    }

    internal NativeBattleEffectNodeBank(uint address, NativeBattleNodeSource source, Func<uint, int, byte[]>? readExternal = null, Func<uint, ImmutableArray<byte>>? readResource = null)
    {
        if ((ulong)address + (uint)source.Length > uint.MaxValue)
        {
            throw new InvalidDataException("Native node executable view requires its actual current addressed bytes.");
        }
        Address = address;
        operation = new(source);
        this.readExternal = readExternal;
        this.readResource = readResource;
    }

    internal static NativeBattleNodeSource BorrowSource(NativeBattleChoco97IoBanks banks, uint address, int length)
    {
        banks.Validate();
        long offset = (long)address - banks.BaseAddress;
        if (length < 0 || offset < 0 || offset > banks.Length - length)
        {
            throw new NotSupportedException("Native node source leaves its actual process file bank.");
        }
        return NativeBattleNodeSource.Borrow(banks.Bytes, checked((int)offset), length);
    }

    internal void Publish() => operation?.Publish();
    public void Dispose() => operation?.Dispose();

    internal byte ReadByte(uint address)
    {
        return operation is not null && address >= Address && (ulong)address < (ulong)Address + (uint)Length ? operation.ReadByte(checked((int)(address - Address))) : Read(address, 1)[0];
    }

    internal ushort ReadUInt16(uint address)
    {
        return operation is not null && address >= Address && (ulong)address + 2 <= (ulong)Address + (uint)Length ? operation.ReadUInt16(checked((int)(address - Address))) : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    }

    internal uint ReadUInt32(uint address)
    {
        return operation is not null && address >= Address && (ulong)address + 4 <= (ulong)Address + (uint)Length ? operation.ReadUInt32(checked((int)(address - Address))) : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    }

    internal void WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        (operation ?? throw new InvalidOperationException("Native stores require their working node authority.")).Write(offset, bytes);
    }

    internal void WriteUInt16(int offset, ushort value)
    {
        (operation ?? throw new InvalidOperationException("Native stores require their working node authority.")).WriteUInt16(offset, value);
    }

    internal NativeBattleGfSourceReader OpenWholeReader() => operation is not null ? operation.OpenReader(0, Length) : new(data);

    internal NativeBattleGfSourceReader OpenResourceReader(uint address)
    {
        if (readResource is not null)
        {
            return new(ReadResource(address));
        }
        if (operation is not null)
        {
            int offset = checked((int)(address - Address));
            return operation.OpenReader(offset, checked(Length - offset));
        }
        return new(ReadResource(address));
    }

    internal byte[] Read(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (1UL << 32))
        {
            throw new InvalidDataException("Native node executable read leaves its own current resource bank.");
        }
        ulong end = (ulong)address + (uint)length;
        ulong bankEnd = (ulong)Address + (uint)Length;
        if (address >= Address && end <= bankEnd)
        {
            if (operation is not null)
            {
                byte[] bytes = new byte[length];
                operation.ReadInto(checked((int)(address - Address)), bytes);
                return bytes;
            }
            return data.AsSpan().Slice(checked((int)(address - Address)), length).ToArray();
        }
        if (readExternal is null || address < bankEnd && end > Address)
        {
            throw new InvalidDataException("Native node executable read leaves its own current resource bank.");
        }
        byte[] result = readExternal(address, length);
        if (result is null || result.Length != length)
        {
            throw new InvalidDataException("Native node external owner must supply exactly the requested addressed bytes.");
        }
        return result;
    }

    internal ImmutableArray<byte> ReadResource(uint address)
    {
        if (readResource is null)
        {
            if (operation is not null)
            {
                throw new InvalidOperationException("Working node resources require scoped bounded reads.");
            }
            return Read(address, checked(Length - (int)(address - Address))).ToImmutableArray();
        }
        ImmutableArray<byte> bytes = readResource(address);
        if (bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("An addressed effect resource requires its complete original byte span.");
        }
        return bytes;
    }

    internal void ReplaceData(ImmutableArray<byte> data)
    {
        if (operation is not null)
        {
            throw new InvalidOperationException("Working node stores require their exact bounded written range.");
        }
        if (data.IsDefault || data.Length != this.data.Length)
        {
            throw new InvalidDataException("Native node executable writes cannot change the retained bank's extent.");
        }
        this.data = data;
    }
}

internal enum NativeBattleEffectNodeOrientation2Policy
{
    ReverseHorizontal,
    TargetF1MinusCasterF0Look3D
}

internal sealed record NativeBattleEffectNodeInputs(NativeBattleActionMatrix? CopiedCamera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<uint, sbyte, sbyte, int?> AllocateEmitter, Func<int, sbyte, int?> AllocateGeometry)
{
    public Func<NativeBattleEffectNodeBank, uint, int, byte, ImmutableArray<byte>>? ResourceUpdate { get; init; }
    public Action<NativeBattleEffectNodeBank, uint, int, byte>? MutateResource { get; init; }
    public Action<uint, uint, uint, short>? MorphResource { get; init; }
    public Action<uint, uint, uint, uint>? SelectedMorph { get; init; }
    public NativeBattleModelScaleVector? ActorScale { get; init; }
    public NativeBattleEffectNodeOrientation2Policy Orientation2Policy { get; init; }
    public Action<NativeBattleEffectNodeMemory, int, NativeBattleVector, NativeBattleVector>? LookBasis8DDA50 { get; init; }
    public NativeBattleEffectNodeSpawnOrigin? SpawnOrigin { get; init; }
    public NativeBattleEffectNodeActorAnchor? ActorAnchor { get; init; }
    public Action? EmitterAdvanced { get; init; }
    public Action<int, bool>? GeometryAdvanced { get; init; }
    public Action<int, int>? SpriteNodeSelected { get; init; }
    public Action<int, int, int>? SpriteCall { get; init; }
}
internal delegate NativeBattleGfGeometryBatch NativeBattleEffectNodeMeshInstance(NativeBattleGfSourceReader resource, NativeBattleActionMatrix matrix);
internal delegate NativeBattleEffectNodeMeshInstance NativeBattleEffectNodePrepareMesh(int node, byte[] source);

internal delegate NativeBattleVector NativeBattleEffectNodeSpawnOrigin(ref NativeCrtRandomState random);

internal delegate NativeBattleVector NativeBattleEffectNodeActorAnchor(int actor, ref NativeCrtRandomState random);
