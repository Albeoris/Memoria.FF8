using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNeedlesFamilyState(ushort Effect, [property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    [JsonIgnore]
    public NativeBattleNeedlesFamilyProfile Profile => NativeBattleNeedlesFamilyProfile.ForEffect(Effect);
    public static NativeBattleNeedlesFamilyState FromColdProcess(ushort effect)
    {
        _ = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
        return new(effect, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    }
    public static NativeBattleNeedlesFamilyState FromUnknown(ushort effect)
    {
        NativeBattleNeedlesFamilyProfile profile = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
        return new(effect, NativeBattleEffectArena.Clear().MarkUnknown(0, profile.Length), NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleNeedlesFamilyProfile.PacketBytes));
    }
    public void Validate()
    {
        Globals.Validate();
        Packets.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(Profile.BaseAddress + (uint)region.Offset), region.Length);
        }
        if (Globals.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Profile.Length - region.Length) || Packets.EnumerateDataRanges().Any(region => region.Offset < 0 || region.Offset > NativeBattleNeedlesFamilyProfile.PacketBytes - region.Length) || Packets.EnumerateUnknownRanges().Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleNeedlesFamilyProfile.PacketBytes - region.Length))
        {
            throw new InvalidDataException("Needles retained memory leaves its physical owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleNeedlesFamilyState Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleNeedlesFamilyState WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleNeedlesFamilyState WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    public byte[] ReadPacket(uint address, int length) => Packets.Read(PacketOffset(address, length), length);
    public NativeBattleNeedlesFamilyState WritePacket(uint address, ReadOnlySpan<byte> bytes)
    {
        Packets.Write(PacketOffset(address, bytes.Length), bytes);
        return this;
    }
    private int Require(uint address, int length)
    {
        NativeBattleNeedlesFamilyProfile p = Profile;
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || address < p.BaseAddress || end > p.EndAddress || address < p.NeedleBank && end > p.RootBank || address < p.ParametersGlobal && end > p.ChildDescriptor || address < p.CursorGlobal && end > p.PacketBase)
        {
            throw new InvalidDataException("Needles globals exclude its task pools, descriptors and packet banks.");
        }
        return checked((int)(address - p.BaseAddress));
    }
    private int PacketOffset(uint address, int length)
    {
        if (length < 0 || address < Profile.PacketBase || (ulong)address + (uint)Math.Max(0, length) > Profile.PacketBase + NativeBattleNeedlesFamilyProfile.PacketBytes)
        {
            throw new InvalidDataException("Needles packet access leaves its fixed native banks.");
        }
        return checked((int)(address - Profile.PacketBase));
    }
}
