using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRayBomb307Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleRayBomb307State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleRayBomb307State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Particles)
{
    public const uint GlobalsAddress = SavedBindingAddress;
    public const int GlobalsLength = 0x1678;
    public static NativeBattleRayBomb307State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public static NativeBattleRayBomb307State FromUnknown() => new(Unknown(GlobalsLength), Unknown(ParticlesLength));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleRayBomb307State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }
    public NativeBattleRayBomb307State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), Unknown(length), 0, length);
    public NativeBattleRayBomb307State WriteView(uint address, NativeBattleEffectArena view, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(view, 0, Globals, Require(address, length), length);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleRayBomb307State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(Require(address, 4), pointer);
        return this;
    }
    public void Validate()
    {
        ValidateArena(Globals, GlobalsLength);
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            _ = Require(checked(GlobalsAddress + (uint)region.Offset), region.Length);
        }
        ValidateArena(Particles, ParticlesLength);
    }
    private static void ValidateArena(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
    private static NativeBattleEffectArena Unknown(int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length);
    private static int Require(uint address, int length)
    {
        bool binding = address >= SavedBindingAddress && (ulong)address + (uint)Math.Max(length, 0) <= SavedBindingAddress + 32;
        bool saved = address >= SavedActorsAddress && (ulong)address + (uint)Math.Max(length, 0) <= SavedActorsAddress + 0xB8;
        if (length < 0 || !binding && !saved)
        {
            throw new InvalidDataException("Ray-Bomb globals exclude its task pools and separate particle rows.");
        }
        return checked((int)(address - GlobalsAddress));
    }
}
