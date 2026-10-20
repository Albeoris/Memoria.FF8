using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216Resources(ImmutableArray<byte> StaticData, ImmutableArray<byte> ColdGlobals, NativeBattleEffectHeapAllocation InitialTim, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    public ImmutableArray<byte> SingleWindow { get; init; } = [];

    public const uint Address = 0x113C1AC;
    public const uint EndAddress = 0x1154F48;
    public const uint ActorDirectory = 0x1153B30;
    public const int ActorDirectoryLength = 5112;

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = (long)address - Address;
        if (StaticData.IsDefault || StaticData.Length != EndAddress - Address || offset < 0 || length < 0 || offset > StaticData.Length - length)
        {
            throw new InvalidDataException("Native Duel216 read leaves its original executable resource owner.");
        }
        return StaticData.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4).AsSpan());
    public short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2).AsSpan());
    public NativeBattleGfAnimationSource Hierarchy(uint address) => new(address, Read(address, checked((int)(EndAddress - address))));
    public ImmutableArray<byte> ReadActorDat() => Read(ActorDirectory, ActorDirectoryLength);

    public void Validate()
    {
        _ = Read(Address, checked((int)(EndAddress - Address)));
        if (SingleWindow.IsDefault || !SingleWindow.AsSpan().SequenceEqual(new byte[] { 49, 0 }) || ColdGlobals.IsDefault || ColdGlobals.Length != 192 || ColdGlobals.Any(value => value != 0) || InitialTim is null || InitialTim.Identity != "mag215.tim" || InitialTim.Data.IsDefaultOrEmpty || Files.IsDefault || Files.Length != 3 || Files[0].FileId != 360 || Files[1].FileId != 361 || Files[2].FileId != 362 || Files.Any(file => file.Data.IsDefaultOrEmpty))
        {
            throw new InvalidDataException("Native Duel216 requires its original texture, queued resource files and loader globals.");
        }
        ImmutableArray<byte> actor = ReadActorDat();
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 0);
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 1);
        if (NativeBattleDatDirectory.Section(actor.AsSpan(), 2) != ActorDirectoryLength)
        {
            throw new InvalidDataException("Native Duel216 actor directory differs from its original extent.");
        }
    }
}

public sealed record NativeBattleDuel216State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] NativeBattleDuel216Resources? Resources)
{
    public static NativeBattleDuel216State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Resources is null)
            {
                throw new InvalidDataException("Native Duel216 requires its actual entered parameters and resources.");
            }
            Resources.Validate();
        }
        else if (Parameters is not null || Resources is not null)
        {
            throw new InvalidDataException("Native Duel216 cannot retain entered resources before entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel216State? Duel216 { get; init; }
}

public sealed record NativeBattleDuel216Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleDuel216Step(NativeBattleDuel216Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
