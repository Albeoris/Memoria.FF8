using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSnipeLaser302State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 16), (CursorGlobal, 4)];

    public static NativeBattleSnipeLaser302State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleSnipeLaser302State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (RootBankStorage is { } bank)
        {
            bank.Validate();
            if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > RootBankLength - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > RootBankLength - region.Length))
            {
                throw new InvalidDataException("Snipe Laser retained root bytes leave their physical bank.");
            }
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleSnipeLaser302State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleSnipeLaser302State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleSnipeLaser302State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleSnipeLaser302State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleSnipeLaser302State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Snipe Laser globals exclude module banks, descriptors, COMMON storage and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
