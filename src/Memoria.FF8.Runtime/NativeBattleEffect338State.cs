using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect338State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffect338Skeleton? Skeleton, [property: JsonRequired] bool ColdSourcePending)
{
    public const uint BaseAddress = 0x25561C8;
    public const int Length = 0x8C;

    public static NativeBattleEffect338State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), null, true);
    public static NativeBattleEffect338State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), NativeBattleEffectArena.Clear().MarkUnknown(0, 20), null, false);

    public void Validate()
    {
        Globals.Validate();
        RootBankStorage?.Validate();
        Skeleton?.Validate();
        if (Globals.EnumerateDataRanges().Any(region => region.Offset < 0 || region.Offset > Length - region.Length) || Globals.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Offset > Length - region.Length) || RootBankStorage is { } root && (root.EnumerateDataRanges().Any(region => region.Offset < 0 || region.Offset > 20 - region.Length) || root.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Offset > 20 - region.Length)) || ColdSourcePending && Skeleton is not null)
        {
            throw new InvalidDataException("Effect338 retained memory leaves its original current owners.");
        }
    }

    public NativeBattleEffect338State InitializeSource(NativeBattleEffect338Resources resources)
    {
        Validate();
        if (Skeleton is not null)
        {
            return this;
        }
        if (!ColdSourcePending)
        {
            throw new NotSupportedException("Effect338 requires the current mutable embedded skeleton or its original cold process source.");
        }
        return this with { Skeleton = NativeBattleEffect338Skeleton.FromOriginal(resources.Model), ColdSourcePending = false };
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Offset(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Offset(address, 2), 2));
    public NativeBattleEffect338State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Offset(address, 4), encoded);
        return this;
    }
    public NativeBattleEffect338State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Offset(address, 2), encoded);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Offset(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEffect338State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Offset(address, length), length);
        return this;
    }

    private static int Offset(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > (ulong)BaseAddress + Length)
        {
            throw new InvalidDataException("Effect338 global access leaves its actual selected field owners.");
        }
        return checked((int)(address - BaseAddress));
    }
}
