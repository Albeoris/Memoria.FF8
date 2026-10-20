using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? Script, [property: JsonRequired] ImmutableArray<byte>? ModelData)
{
    public const uint GlobalAddress = 0x25BC390;
    public const int GlobalLength = 0x5FBC;
    public static NativeBattleTheEnd85State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, null);
    public void Validate()
    {
        Globals.Validate();
        if (Script is { } script && (script.IsDefault || script.Length != 80) || ModelData is { } data && (data.IsDefault || data.Length != 0xA0C4) || Globals.Regions.Any(region => region.Offset > GlobalLength - region.Data.Length) || Globals.UnknownRegions.Any(region => region.Offset > GlobalLength - region.Length))
        {
            throw new InvalidDataException("Native The End requires its bounded physical globals and complete retained IO script.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Offset(address, length), length);
    public NativeBattleTheEnd85State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Offset(address, bytes.Length), bytes) };
    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleTheEnd85State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    private static int Offset(uint address, int length)
    {
        if (address < GlobalAddress || length < 0 || length > GlobalLength || (ulong)address - GlobalAddress > (uint)(GlobalLength - length))
        {
            throw new InvalidDataException("Native The End access leaves its physical module span.");
        }
        return checked((int)(address - GlobalAddress));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTheEnd85State? TheEnd85 { get; init; }
}

public sealed record NativeBattleTheEnd85Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
