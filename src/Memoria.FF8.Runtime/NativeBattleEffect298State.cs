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
        foreach (var region in Globals.EnumerateDataRanges())
        {
            _ = RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (var region in Globals.EnumerateUnknownRanges())
        {
            _ = RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (var region in ScriptCounterWrites.EnumerateDataRanges())
        {
            _ = RequireCounter(checked(ScriptAddress + (uint)region.Offset), region.Length);
        }
        foreach (var region in ScriptCounterWrites.EnumerateUnknownRanges())
        {
            _ = RequireCounter(checked(ScriptAddress + (uint)region.Offset), region.Length);
        }
        if (RootBankStorage is { } bank)
        {
            bank.Validate();
            bank.ValidateExtent(RootBankLength);
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
            ScriptCounterWrites.Write(RequireCounter(address, bytes.Length), bytes);
            return this;
        }
        Globals.Write(RequireGlobal(address, bytes.Length), bytes);
        return this;
    }

    public NativeBattleEffect298State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect298State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect298State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, RequireGlobal(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

    public NativeBattleEffect298State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsCounter(address, length))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, ScriptCounterWrites, RequireCounter(address, length), length);
            return this;
        }
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, RequireGlobal(address, length), length);
        return this;
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
                if (ScriptCounterWrites.EnumerateUnknownRanges().Contains(offset))
                {
                    throw new NotSupportedException("Effect298 script counter has no observed current physical value.");
                }
                if (ScriptCounterWrites.EnumerateDataRanges().Contains(offset))
                {
                    result[checked((int)(lane - address))] = ScriptCounterWrites.ReadByte(offset);
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
