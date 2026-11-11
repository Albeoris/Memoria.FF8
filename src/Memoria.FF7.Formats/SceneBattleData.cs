using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleAttack(byte Accuracy, byte ImpactEffect, byte HurtAction, ushort CastingCost, ushort ImpactSound, ushort SingleTargetCamera, ushort MultipleTargetCamera, byte TargetFlags, byte AttackEffect, byte DamageCalculation, byte Power, byte Condition, byte StatusChange, byte AdditionalEffect, byte AdditionalEffectModifier, uint StatusMask, ushort Elements, ushort SpecialFlags)
{
    public const int RecordSize = 28;

    public static Ff7BattleAttack Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        byte accuracy = cursor.U8();
        byte impactEffect = cursor.U8();
        byte hurtAction = cursor.U8();
        cursor.Skip(1);
        return new(accuracy, impactEffect, hurtAction, cursor.U16(), cursor.U16(), cursor.U16(), cursor.U16(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U8(), cursor.U32(), cursor.U16(), cursor.U16());
    }
}

public sealed record SceneBattleAttack(int Slot, ushort Id, ReadOnlyMemory<byte> Name, Ff7BattleAttack Data);
public sealed record SceneEnemyAttack(int Slot, ushort AttackId, byte ActionAnimation, ushort CameraOverride);
public sealed record SceneEnemyAffinity(byte Type, byte Rate);
public sealed record SceneCameraPlacement(Vector3 Position, Vector3 Focus)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public BattleCameraBasis? Basis { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public ushort? FocalLength { get; init; }
}
public sealed record SceneBattleEnemy(int FormationSlot, SceneEnemyInstance Placement, uint InitialConditionFlags, SceneEnemyReward Rewards, byte Level, byte Speed, byte Luck, byte Evasion, byte Strength, byte Defense, byte Magic, byte MagicDefense, uint MaxHitPoints, ushort MaxMagicPoints, uint NativeStatusMask, IReadOnlyList<SceneEnemyAffinity> Affinities, IReadOnlyList<SceneEnemyAttack> Attacks, IReadOnlyList<ReadOnlyMemory<byte>>? Scripts);

/// <summary>Resource data for one native formation, without simulation or progression side effects.</summary>
public sealed record SceneBattleData(int FormationId, IReadOnlyList<SceneCameraPlacement> Cameras, IReadOnlyList<SceneBattleEnemy> Enemies, IReadOnlyList<SceneBattleAttack> Attacks, IReadOnlyList<ReadOnlyMemory<byte>>? FormationScripts)
{
    private const int AttackRecords = 0x4C0;
    private const int AttackIds = 0x840;
    private const int AttackNames = 0x880;
    private const int FormationAi = 0xC80;
    private const int EnemyAi = 0xE80;

    public static SceneBattleData Read(SceneFormation formation)
    {
        ArgumentNullException.ThrowIfNull(formation);
        ReadOnlyMemory<byte> scene = formation.RawScene;
        if (scene.Length != 0x1E80 || (uint)formation.FormationId >= 1024 || formation.Enemies.Count != 6 || formation.EnemyRewards.Count != 3)
        {
            throw new InvalidDataException("FFVII battle data requires a complete PC scene and its formation records.");
        }

        int formationIndex = formation.FormationId & 3;
        BinaryCursor camera = new(scene.Span.Slice(0x58 + formationIndex * 48, 48));
        SceneCameraPlacement[] cameras = new SceneCameraPlacement[3];
        for (int index = 0; index < cameras.Length; index++)
        {
            cameras[index] = new(camera.Vector16(), camera.Vector16());
        }

        ReadOnlyMemory<byte>[] formationAi = ReadDirectory(scene.Slice(FormationAi, EnemyAi - FormationAi), 4, 32);
        ReadOnlyMemory<byte>[] enemyAi = ReadDirectory(scene[EnemyAi..], 3, 32);
        List<SceneBattleEnemy> enemies = new(formation.Enemies.Count);
        for (int slot = 0; slot < formation.Enemies.Count; slot++)
        {
            SceneEnemyInstance placement = formation.Enemies[slot];
            if (placement.EnemyId == -1)
            {
                continue;
            }

            int prototype = 0;
            while (prototype < formation.EnemyRewards.Count && formation.EnemyRewards[prototype].EnemyId != placement.EnemyId)
            {
                prototype++;
            }
            if (prototype == formation.EnemyRewards.Count)
            {
                throw new InvalidDataException($"FFVII formation {formation.FormationId} references absent enemy {placement.EnemyId}.");
            }

            BinaryCursor flags = new(scene.Span.Slice(0x118 + formationIndex * 96 + slot * 16 + 12, 4));
            enemies.Add(ReadEnemy(slot, placement, flags.U32(), formation.EnemyRewards[prototype], enemyAi[prototype]));
        }

        List<SceneBattleAttack> attacks = new(32);
        BinaryCursor attackIds = new(scene.Span.Slice(AttackIds, 64));
        for (int slot = 0; slot < 32; slot++)
        {
            ushort id = attackIds.U16();
            if (id != ushort.MaxValue)
            {
                attacks.Add(new(slot, id, scene.Slice(AttackNames + slot * 32, 32), Ff7BattleAttack.Read(scene.Span.Slice(AttackRecords + slot * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize))));
            }
        }

        return new(formation.FormationId, Array.AsReadOnly(cameras), enemies.AsReadOnly(), attacks.AsReadOnly(), ReadScripts(formationAi[formationIndex]));
    }

    private static SceneBattleEnemy ReadEnemy(int slot, SceneEnemyInstance placement, uint flags, SceneEnemyReward rewards, ReadOnlyMemory<byte> ai)
    {
        BinaryCursor cursor = new(rewards.Raw.Span);
        cursor.Seek(32);
        byte level = cursor.U8();
        byte speed = cursor.U8();
        byte luck = cursor.U8();
        byte evasion = cursor.U8();
        byte strength = cursor.U8();
        byte defense = cursor.U8();
        byte magic = cursor.U8();
        byte magicDefense = cursor.U8();

        ReadOnlySpan<byte> affinityTypes = cursor.Take(8);
        ReadOnlySpan<byte> affinityRates = cursor.Take(8);
        SceneEnemyAffinity[] affinities = new SceneEnemyAffinity[8];
        for (int index = 0; index < affinities.Length; index++)
        {
            affinities[index] = new(affinityTypes[index], affinityRates[index]);
        }

        ReadOnlySpan<byte> animations = cursor.Take(16);
        BinaryCursor ids = new(cursor.Take(32));
        BinaryCursor cameras = new(cursor.Take(32));
        List<SceneEnemyAttack> attacks = new(16);
        for (int index = 0; index < animations.Length; index++)
        {
            ushort id = ids.U16();
            ushort camera = cameras.U16();
            if (id != ushort.MaxValue)
            {
                attacks.Add(new(index, id, animations[index], camera));
            }
        }

        cursor.Seek(0x9C);
        ushort mp = cursor.U16();
        cursor.Seek(0xA4);
        uint hp = cursor.U32();
        cursor.Seek(0xB0);
        uint statusMask = cursor.U32();
        return new(slot, placement, flags, rewards, level, speed, luck, evasion, strength, defense, magic, magicDefense, hp, mp, statusMask, Array.AsReadOnly(affinities), attacks.AsReadOnly(), ReadScripts(ai));
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>>? ReadScripts(ReadOnlyMemory<byte> owner)
    {
        return owner.IsEmpty ? null : Array.AsReadOnly(ReadDirectory(owner, 16, 1));
    }

    private static ReadOnlyMemory<byte>[] ReadDirectory(ReadOnlyMemory<byte> owner, int count, int minimumEntrySize)
    {
        BinaryCursor cursor = new(owner.Span);
        int[] offsets = new int[count];
        for (int index = 0; index < count; index++)
        {
            offsets[index] = cursor.U16();
        }

        ReadOnlyMemory<byte>[] entries = new ReadOnlyMemory<byte>[count];
        int end = owner.Length;
        for (int index = count - 1; index >= 0; index--)
        {
            int start = offsets[index];
            if (start == ushort.MaxValue)
            {
                continue;
            }
            if (start < count * 2 || end - start < minimumEntrySize)
            {
                throw new InvalidDataException("FFVII battle AI offset crosses its owning table or the following record.");
            }
            entries[index] = owner[start..end];
            end = start;
        }
        return entries;
    }
}
