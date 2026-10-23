using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect289State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena MeshWStorage, [property: JsonRequired] bool ColdSourceMeshW)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 12), (CursorGlobal, 8)];

    public static NativeBattleEffect289State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, MeshWStorageLength), true);

    public static NativeBattleEffect289State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, MeshWStorageLength), false);
    }

    public void Validate()
    {
        Globals.Validate();
        MeshWStorage.Validate();
        if (MeshWStorage.Regions.Any(region => region.Offset < 0 || region.Offset > MeshWStorageLength - region.Data.Length) || MeshWStorage.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > MeshWStorageLength - region.Length))
        {
            throw new InvalidDataException("Effect289 current source W bytes leave their two physical mesh lane domains.");
        }
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
                throw new InvalidDataException("Effect289 retained root bytes leave their physical bank.");
            }
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect289State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleEffect289State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect289State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect289State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEffect289State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };

    public NativeBattleEffect289State WriteMeshW(uint sourceAddress, int vertex, short value)
    {
        int mesh = MeshAddresses.IndexOf(sourceAddress);
        if (mesh < 0 || (uint)vertex >= MeshVertexCount)
        {
            throw new InvalidDataException("Effect289 current W write leaves its selected source vertex lanes.");
        }
        return this with { MeshWStorage = MeshWStorage.Write((mesh * MeshVertexCount + vertex) * 2, BitConverter.GetBytes(value)) };
    }

    public NativeBattleEffectArena ReadStatic(NativeBattleEffect289Resources resources, uint address, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(address, length).AsSpan());
        for (int mesh = 0; mesh < MeshAddresses.Length; mesh++)
        {
            for (int vertex = 0; vertex < MeshVertexCount; vertex++)
            {
                uint lane = checked(MeshAddresses[mesh] + 14u + (uint)(vertex * 8));
                uint first = Math.Max(address, lane);
                ulong end = Math.Min((ulong)address + (uint)length, (ulong)lane + 2);
                if (first >= end)
                {
                    continue;
                }
                int source = (mesh * MeshVertexCount + vertex) * 2 + checked((int)(first - lane));
                int target = checked((int)(first - address));
                int bytes = checked((int)(end - first));
                (byte[] data, bool[] known) = MeshWStorage.ReadSnapshot(source, bytes);
                for (int index = 0; index < bytes; index++)
                {
                    if (known[index])
                    {
                        result = result.Write(target + index, [data[index]]);
                    }
                    else if (!ColdSourceMeshW)
                    {
                        result = result.MarkUnknown(target + index, 1);
                    }
                }
            }
        }
        return result;
    }

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Effect289 globals exclude module banks, descriptors, COMMON storage and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
