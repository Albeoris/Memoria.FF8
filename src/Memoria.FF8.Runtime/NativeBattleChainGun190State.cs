using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChainGun190State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBankStorage)
{
    [JsonIgnore]
    internal NativeBattleChainGun190HostedStorage? Hosted { get; init; }

    private static readonly (uint Address, int Length)[] GlobalRanges = [(FirstTargetGlobal, 4), (ParticleABase, ParticleBankBytes * 2), (RecordsAddress, RecordsLength), (ParametersGlobal, 12), (BonePointGlobal, 8), (CursorGlobal, 4)];

    public static NativeBattleChainGun190State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleChainGun190State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, PacketBytes), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildBankLength));
    }

    public void Validate()
    {
        Globals.Validate();
        if (Hosted is { } hosted)
        {
            foreach (NativeBattleEffectArena page in hosted.Pages)
            {
                page.Validate();
            }
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateUnknownRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        ValidateBounded(Packets, PacketBytes);
        if (RootBankStorage is { } root)
        {
            ValidateBounded(root, RootBankLength);
        }
        if (ChildBankStorage is { } child)
        {
            ValidateBounded(child, ChildBankLength);
        }
    }

    public byte[] Read(uint address, int length)
    {
        NativeBattleEffectArena owner = AddressedStorage(address, length, out int offset);
        return owner.Read(offset, length);
    }
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    internal BattleModelOwner ReadModelOperand(uint address) => Globals.ReadExecutionReference<BattleExecutionReference.Model>(Require(address, 4), 4).Owner;

    internal NativeBattleChainGun190State WriteModelOperand(uint address, BattleModelOwner owner, NativeBattleModelAliases aliases)
    {
        NativeBattleEffectArena globals = Globals.WriteExecutionReference(Require(address, 4), 4, new BattleExecutionReference.Model(owner), aliases.EncodeOperand(owner, 4));
        return this with { Globals = globals };
    }

    internal uint ShotAddress(int index)
    {
        int capacity = Hosted?.RecordCapacity ?? RecordCount;
        if ((uint)index >= (uint)capacity)
        {
            throw new InvalidDataException("Chain Gun shot record leaves its owned bank.");
        }
        return checked((Hosted?.Address ?? RecordsAddress) + (uint)(index * RecordStride));
    }

    internal (uint Address, int Capacity) ParticleBank(bool directional)
    {
        if (Hosted is { } hosted)
        {
            return directional ? (hosted.DirectionalParticles, hosted.DirectionalParticleCapacity) : (hosted.PositionedParticles, hosted.PositionedParticleCapacity);
        }
        return (directional ? ParticleABase : ParticleBBase, ParticleCount);
    }

    internal NativeBattleEffectArena ParticleStorage(uint address)
    {
        return AddressedStorage(address, ParticleStride, out _);
    }

    internal NativeBattleChainGun190State WriteShotTarget(uint address, BattleModelOwner owner, NativeBattleModelAliases aliases)
    {
        return WriteShotTarget(address, owner, aliases.EncodeOperand(owner, 4));
    }

    internal NativeBattleChainGun190State WritePositionedShot(uint address)
    {
        return WriteShotTarget(address, null, [255, 255, 255, 255]);
    }

    private NativeBattleChainGun190State WriteShotTarget(uint address, BattleModelOwner? owner, ImmutableArray<byte>? encoding)
    {
        NativeBattleEffectArena storage = AddressedStorage(address, 4, out int offset);
        storage.WriteExecutionReference(offset, 4, new BattleExecutionReference.TrailTarget(owner), encoding);
        return this;
    }

    internal bool ReadPositionedShot(uint address)
    {
        NativeBattleEffectArena storage = AddressedStorage(address, 4, out int offset);
        return storage.ReadExecutionReference<BattleExecutionReference.TrailTarget>(offset, 4).Owner is null;
    }

    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public byte[] ReadPacket(uint address, int length)
    {
        if (!IsPacket(address, length))
        {
            throw new InvalidDataException("Chain Gun packet bytes leave their original fixed allocation.");
        }
        return Packets.Read(checked((int)(address - PacketBase)), length);
    }
    public NativeBattleChainGun190State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        NativeBattleEffectArena owner = AddressedStorage(address, bytes.Length, out int offset);
        owner.Write(offset, bytes);
        return this;
    }
    public NativeBattleChainGun190State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleChainGun190State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleChainGun190State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        NativeBattleEffectArena owner = AddressedStorage(address, length, out int offset);
        return NativeBattleEffectNodeContextMemory.Copy(owner, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
    public NativeBattleChainGun190State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectArena owner = AddressedStorage(address, length, out int offset);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, owner, offset, length);
        return this;
    }

    private NativeBattleEffectArena AddressedStorage(uint address, int length, out int offset)
    {
        if (Hosted is { } hosted && hosted.TryGetStorage(address, length, out NativeBattleEffectArena? storage, out offset))
        {
            return storage;
        }
        if (IsPacket(address, length))
        {
            offset = checked((int)(address - PacketBase));
            return Packets;
        }
        offset = Require(address, length);
        return Globals;
    }

    private static bool IsPacket(uint address, int length) => length >= 0 && address >= PacketBase && (ulong)address + (uint)length <= PacketBase + PacketBytes;
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Chain Gun carrier bytes exclude its physical task banks, descriptors and unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
}
