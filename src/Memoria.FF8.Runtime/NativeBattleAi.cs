using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAiAbility(byte Command, byte Sequence, ushort Id, int AttackType = 0, int Power = 0, int Element = 0, ushort Statuses0 = 0, uint Statuses1 = 0, int StatusAttack = 0)
{
    public byte? NativeAttackFlags { get; init; }
}
public sealed record NativeBattleLastAction(byte? AttackType, byte? AttackerModelId, byte? Command, byte? ActionId, byte? ElementMask, byte? AttackerIndex);
public sealed record NativeBattleAiDefinition(byte[][] Scripts, NativeBattleAiAbility[] Abilities, int LevelBand, string[] Texts)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? DeathSequenceReleasesAllocation { get; init; }
    public byte[][] NameBuffers { get; init; } = [];
    public string[] DisplayNames { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAiHeapTextDirectory? HeapTextDirectory { get; init; }
    internal NativeBattleAiHeapTextDirectory? AdmittedHeapTextDirectory =>
        Texts.Length == 0 && HeapTextDirectory is { Offset: >= 16 } directory && !string.IsNullOrWhiteSpace(directory.Language) ? directory : null;
}

public sealed record NativeBattleAiHeapMessage(uint EntryAddress, ushort EntryOffset, uint? TextAddress, ImmutableArray<byte> Encoded);

public sealed record NativeBattleAiHeapTextDirectory(int Offset, string Language)
{
    internal (NativeBattleAiHeapMessage Message, string Text) Read(Func<uint, int, byte[]> readGuest, uint section, int index)
    {
        if (Offset < 16 || (uint)index > byte.MaxValue)
        {
            throw new InvalidDataException("Native battle AI text requires an installed offset and an unsigned byte table operand.");
        }
        byte[] header = ReadKnown(section, 16);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 3 ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != Offset || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) != Offset)
        {
            throw new InvalidDataException("Native battle AI text requires its installed zero-entry table at the loaded guest-heap boundary.");
        }

        uint textBase = checked(section + (uint)Offset);
        uint entry = checked(textBase + (uint)index * sizeof(ushort));
        ushort displacement = BinaryPrimitives.ReadUInt16LittleEndian(ReadKnown(entry, sizeof(ushort)));
        if (displacement == ushort.MaxValue)
        {
            return (new(entry, displacement, null, [0]), string.Empty);
        }

        uint address = checked(textBase + displacement);
        Span<byte> row = stackalloc byte[48];
        for (int i = 0; i < row.Length; i++)
        {
            byte value = ReadKnown(checked(address + (uint)i), 1)[0];
            row[i] = value;
            if (value == 0)
            {
                FF8TextLanguage language = FF8TextLanguages.Parse(Language);
                FF8Encoding encoding = language == FF8TextLanguage.JP ? FF8Encoding.CreateJapanese(language) : FF8Encoding.CreateEuropean(language);
                try
                {
                    string decoded = encoding.GetString(row[..i]);
                    return (new(entry, displacement, address, ImmutableArray.Create(row[..(i + 1)].ToArray())), decoded);
                }
                catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException or InvalidOperationException)
                {
                    throw new NotSupportedException($"Native battle AI text at guest address {address:X8} contains unsupported encoded bytes {Convert.ToHexString(row[..i])}.", exception);
                }
            }
        }
        throw new NotSupportedException("Native battle AI text overruns its 48-byte message row.");

        byte[] ReadKnown(uint location, int length)
        {
            try
            {
                return readGuest(location, length);
            }
            catch (NotSupportedException exception)
            {
                throw new NotSupportedException($"Native battle AI text operand {index} requires {length} known guest bytes at {location:X8}.", exception);
            }
        }
    }
}
public sealed record NativeBattleEnemyLevel(int MaximumHp, int Strength, int Vitality, int Magic, int Spirit, int Speed, int[] ElementDefenses, BattleDrawSpell[] DrawSpells, int[] DrawGuardianForces, int AiLevelBand)
{
    public int Evasion { get; init; }
    public int[] BaseStatistics { get; init; } = [];
}
public sealed record NativeBattleEnemyTemplate(byte SceneLevel, NativeBattleEnemyLevel[] Levels)
{
    public byte? Information246 { get; init; }
    public NativeBattleDrawSlot[][]? NativeDrawTables { get; init; }
    public byte? Information254 { get; init; }
    public NativeBattleMugDefinition? NativeMug { get; init; }
    public BattleNativeEnemySpecial? NativeSpecial { get; init; }
    public NativeBattleStatusAllocation? NativeStatusAllocation { get; init; }
    public uint NativeCombatantFlags { get; init; }
}
public readonly record struct NativeBattleAiActorValue(string ActorId, byte? OriginalOperand);

public sealed record NativeBattleParticipantSnapshot(bool Active, bool Hidden, bool Untargetable, bool Suspended, [property: System.Text.Json.Serialization.JsonRequired] BattleRosterSlot? OccupiedSlot, [property: System.Text.Json.Serialization.JsonRequired] NativeBattleModelSlot? OriginalSlot, int? ResolvedLevel, uint[] Variables, byte TurnCount, string? LastAttackerId, BattleAbility? LastAttackerAbility, bool DeathHandled)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReprojectedEncounterCopy { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, NativeBattleAiActorValue>? ActorReferences { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximumHitPoints { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public ushort? ReturnDamage { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattlePendingReaction? PendingReaction { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? RemainingHits { get; init; }
    public BattleNativeCharacterGrowth? Growth { get; init; }
    // Older valid snapshots predate opcode40 support, so their six allocation multipliers are still10.
    public byte[] StatMultipliers { get; init; } = [10, 10, 10, 10, 10, 10];
    public byte? LastHealing { get; init; }
    public bool AllocationPendingStatistics { get; init; }
    public NativeBattleLastAction? LastAction { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal BattlePreviousTarget? PreviousTarget { get; init; }
    public byte? DrawnSpell { get; init; }
    public ushort? KillCount { get; init; }
    public ushort? DeathCount { get; init; }
    public uint? NativeCombatantFlags { get; init; }
    public int? DisplayNameIndex { get; init; }
    public int[]? ElementDefenses { get; init; }
}
