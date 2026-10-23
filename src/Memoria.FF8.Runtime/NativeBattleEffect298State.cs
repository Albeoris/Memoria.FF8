using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect298State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena ScriptCounterWrites)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 12), (CursorGlobal, 8), (ArenaGlobal, 4)];

    public static NativeBattleEffect298State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleEffect298State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        NativeBattleEffectArena counters = NativeBattleEffectArena.Clear();
        foreach (uint address in ScriptCounterAddresses)
        {
            counters = counters.MarkUnknown(checked((int)(address - ScriptAddress)), 2);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), counters);
    }

    public void Validate()
    {
        Globals.Validate();
        ScriptCounterWrites.Validate();
        foreach (var region in Globals.Regions)
        {
            _ = RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (var region in Globals.UnknownRegions)
        {
            _ = RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (var region in ScriptCounterWrites.Regions)
        {
            _ = RequireCounter(checked(ScriptAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (var region in ScriptCounterWrites.UnknownRegions)
        {
            _ = RequireCounter(checked(ScriptAddress + (uint)region.Offset), region.Length);
        }
        if (RootBankStorage is { } bank)
        {
            bank.Validate();
            if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > RootBankLength - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > RootBankLength - region.Length))
            {
                throw new InvalidDataException("Effect298 retained root leaves its physical bank.");
            }
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(RequireGlobal(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect298State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (IsCounter(address, bytes.Length))
        {
            return this with { ScriptCounterWrites = ScriptCounterWrites.Write(RequireCounter(address, bytes.Length), bytes) };
        }
        return this with { Globals = Globals.Write(RequireGlobal(address, bytes.Length), bytes) };
    }

    public NativeBattleEffect298State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect298State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect298State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, RequireGlobal(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

    public NativeBattleEffect298State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsCounter(address, length))
        {
            return this with { ScriptCounterWrites = NativeBattleEffectNodeContextMemory.Copy(source, 0, ScriptCounterWrites, RequireCounter(address, length), length) };
        }
        return this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, RequireGlobal(address, length), length) };
    }

    public byte[] ReadStatic(NativeBattleEffect298Resources resources, uint address, int length)
    {
        byte[] result = resources.ReadStatic(address, length).ToArray();
        foreach (uint counter in ScriptCounterAddresses)
        {
            for (uint lane = counter; lane < counter + 2; lane++)
            {
                if (lane < address || (ulong)lane >= (ulong)address + (uint)length)
                {
                    continue;
                }
                int offset = checked((int)(lane - ScriptAddress));
                if (ScriptCounterWrites.UnknownRegions.Any(region => offset >= region.Offset && offset < region.Offset + region.Length))
                {
                    throw new NotSupportedException("Effect298 script counter has no observed current physical value.");
                }
                if (ScriptCounterWrites.Regions.Any(region => offset >= region.Offset && offset < region.Offset + region.Data.Length))
                {
                    result[checked((int)(lane - address))] = ScriptCounterWrites.Read(offset, 1)[0];
                }
            }
        }
        return result;
    }

    private static bool IsCounter(uint address, int length) => length > 0 && ScriptCounterAddresses.Any(start => address >= start && (ulong)address + (uint)length <= start + 2UL);

    private static int RequireCounter(uint address, int length)
    {
        if (!IsCounter(address, length))
        {
            throw new InvalidDataException("Effect298 PE writes leave the selected script counter WORDs.");
        }
        return checked((int)(address - ScriptAddress));
    }

    private static int RequireGlobal(uint address, int length)
    {
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Effect298 globals exclude banks, descriptors, COMMON and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
