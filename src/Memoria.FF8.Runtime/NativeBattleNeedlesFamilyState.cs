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
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(Profile.BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > Profile.Length - region.Length) || Packets.Regions.Any(region => region.Offset < 0 || region.Offset > NativeBattleNeedlesFamilyProfile.PacketBytes - region.Data.Length) || Packets.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleNeedlesFamilyProfile.PacketBytes - region.Length))
        {
            throw new InvalidDataException("Needles retained memory leaves its physical owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleNeedlesFamilyState Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleNeedlesFamilyState WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleNeedlesFamilyState WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    public byte[] ReadPacket(uint address, int length) => Packets.Read(PacketOffset(address, length), length);
    public NativeBattleNeedlesFamilyState WritePacket(uint address, ReadOnlySpan<byte> bytes) => this with { Packets = Packets.Write(PacketOffset(address, bytes.Length), bytes) };
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
