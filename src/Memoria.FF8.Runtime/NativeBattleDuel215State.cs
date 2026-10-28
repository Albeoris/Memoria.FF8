using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215Resources(ImmutableArray<byte> StaticData, ImmutableArray<byte> ColdGlobals, NativeBattleEffectHeapAllocation InitialTim, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    public const uint Address = 0x1154F48;
    public const uint EndAddress = 0x1172B1C;
    public const uint ActorDirectory = 0x116D1AC;
    public const int ActorDirectoryLength = 0x1970;
    public const uint TrigonometricTable = 0x116EB1C;

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = (long)address - Address;
        if (StaticData.IsDefault || StaticData.Length != EndAddress - Address || offset < 0 || length < 0 || offset > StaticData.Length - length)
        {
            throw new InvalidDataException("Native Duel215 read leaves its original static resource owner.");
        }
        return StaticData.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4).AsSpan());
    public short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2).AsSpan());
    public short Sine(short angle) => Word(TrigonometricTable + (uint)(angle & 0xFFF) * 4);
    public short Cosine(short angle) => Word(TrigonometricTable + (uint)(angle & 0xFFF) * 4 + 2);
    public ImmutableArray<byte> ReadActorDat() => Read(ActorDirectory, ActorDirectoryLength);

    public void Validate()
    {
        _ = Read(Address, checked((int)(EndAddress - Address)));
        if (ColdGlobals.IsDefault || ColdGlobals.Length != 0x74 || ColdGlobals.Any(value => value != 0) || InitialTim is null || InitialTim.Data.IsDefaultOrEmpty || Files.IsDefault || Files.Length != 3 || Files[0].FileId != 360 || Files[1].FileId != 361 || Files[2].FileId != 362 || Files.Any(file => file.Data.IsDefaultOrEmpty))
        {
            throw new InvalidDataException("Native Duel215 requires its original texture and authenticated loader globals.");
        }
        ImmutableArray<byte> actor = ReadActorDat();
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 0);
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 1);
        if (NativeBattleDatDirectory.Section(actor.AsSpan(), 2) != ActorDirectoryLength)
        {
            throw new InvalidDataException("Native Duel215 actor directory differs from its declared resource extent.");
        }
    }
}

public sealed record NativeBattleDuel215State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] NativeBattleDuel215Resources? Resources)
{
    public static NativeBattleDuel215State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Resources is null)
            {
                throw new InvalidDataException("Native Duel215 requires its actual entered parameters and immutable resources.");
            }
            Resources.Validate();
        }
        else if (Parameters is not null || Resources is not null)
        {
            throw new InvalidDataException("Native Duel215 cannot retain entered owners before entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel215State? Duel215
    {
        get => effectState.Duel215;
        init => effectState = effectState with { Duel215 = value };
    }
}

public sealed record NativeBattleDuel215Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleDuel215Step(NativeBattleDuel215Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
