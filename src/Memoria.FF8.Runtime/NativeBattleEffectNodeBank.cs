using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A callback's current addressed executable view, published back to the same typed retained bank after its exact stores.</summary>
internal sealed class NativeBattleEffectNodeBank : IDisposable
{
    private readonly Func<uint, int, byte[]>? readExternal;
    private readonly Func<uint, ImmutableArray<byte>>? readResource;
    internal uint Address { get; }
    private readonly NativeBattleNodeBankOperation operation;
    internal NativeBattleNodeSource Source => operation.Source;
    internal int Length => operation.Length;

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

    internal void Publish() => operation.Publish();
    public void Dispose() => operation.Dispose();

    internal byte ReadByte(uint address)
    {
        return address >= Address && (ulong)address < (ulong)Address + (uint)Length ? operation.ReadByte(checked((int)(address - Address))) : Read(address, 1)[0];
    }

    internal ushort ReadUInt16(uint address)
    {
        return address >= Address && (ulong)address + 2 <= (ulong)Address + (uint)Length ? operation.ReadUInt16(checked((int)(address - Address))) : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    }

    internal uint ReadUInt32(uint address)
    {
        return address >= Address && (ulong)address + 4 <= (ulong)Address + (uint)Length ? operation.ReadUInt32(checked((int)(address - Address))) : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    }

    internal void WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        operation.Write(offset, bytes);
    }

    internal void WriteUInt16(int offset, ushort value)
    {
        operation.WriteUInt16(offset, value);
    }

    internal NativeBattleGfSourceReader OpenWholeReader() => operation.OpenReader(0, Length);

    internal void Write(int offset, ReadOnlySpan<byte> bytes) => operation.Write(offset, bytes);

    internal NativeBattleGfSourceReader OpenResourceReader(uint address)
    {
        if (readResource is not null && (address < Address || (ulong)address >= (ulong)Address + (uint)Length))
        {
            return new(ReadResource(address));
        }
        int offset = checked((int)(address - Address));
        return operation.OpenReader(offset, checked(Length - offset));
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
            byte[] bytes = new byte[length];
            operation.ReadInto(checked((int)(address - Address)), bytes);
            return bytes;
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
            throw new InvalidOperationException("Working node resources require scoped bounded reads.");
        }
        ImmutableArray<byte> bytes = readResource(address);
        if (bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("An addressed effect resource requires its complete original byte span.");
        }
        return bytes;
    }


}

internal enum NativeBattleEffectNodeOrientation2Policy
{
    ReverseHorizontal,
    TargetF1MinusCasterF0Look3D
}

internal sealed record NativeBattleEffectNodeInputs(NativeBattleActionMatrix? CopiedCamera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<uint, sbyte, sbyte, int?> AllocateEmitter, Func<int, sbyte, int?> AllocateGeometry)
{
    public Action<NativeBattleEffectNodeBank, uint, int, byte>? MutateResource { get; init; }
    public Func<NativeBattlePilotVisualIdentity, uint, NativeBattlePilotVisualIdentity>? PilotResourceIdentity { get; init; }
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
