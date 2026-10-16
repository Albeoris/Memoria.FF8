using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.KernelBin;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattleNativeKernelRecord(int Section, int Index, ImmutableArray<byte> Bytes);
internal sealed record BattleActionCatalog(IReadOnlyList<BattleActionDefinition> Actions, IReadOnlyList<BattleNativeAction> NativeActions, IReadOnlyList<BattleNativeKernelRecord> Records);

internal static partial class BattleActionKernelReader
{
    private sealed record Layout(int Size, int Magic, int Type, int Power, int Hits, int Target, int Flags, int Presentation, int Element, int StatusAttack, int SavedStatus, int BattleStatus, int Hit = -1, int Critical = -1, int ElementPercent = -1, int PowerModifier = -1, int LevelModifier = -1);

    private static readonly Layout MagicLayout = new(60, 4, 7, 8, 13, 10, 11, 6, 14, 22, 20, 16);
    private static readonly Layout GuardianLayout = new(132, 4, 6, 7, 12, 9, 10, 11, 13, 27, 14, 16, PowerModifier: 130, LevelModifier: 131);
    private static readonly Layout EnemyLayout = new(20, 2, 6, 7, 9, -1, 8, 5, 10, 12, 14, 16, Hit: 13);
    private static readonly Layout SquallLayout = new(24, 4, 6, 8, 12, 10, 11, 9, 13, 15, 18, 20, ElementPercent: 14);
    private static readonly Layout ItemLayout = new(24, 4, 6, 7, 22, 9, 10, 11, 23, 13, 14, 16, Hit: 20);
    private static readonly Layout IndependentGuardianLayout = new(20, 2, 4, 5, 10, 7, 8, 9, 11, 6, 16, 12, Hit: 19, PowerModifier: 18, LevelModifier: 19);
    private static readonly Layout AdditionalLayout = new(16, 0, 4, 5, 7, -1, 6, 3, 8, 9, 10, 12);
    private static readonly Layout NpcLayout = new(24, 4, 6, 7, 12, 10, 11, 8, 13, 15, 16, 20, ElementPercent: 14);
    private static readonly Layout BlueMagicLayout = new(16, 4, 7, -1, 11, 9, 10, 6, 12, 13, -1, -1, Critical: 14);
    private static readonly Layout IrvineLayout = new(24, 4, 6, 7, 12, 10, 11, 8, 13, 15, 16, 20, Critical: 19, ElementPercent: 14);
    private static readonly Layout ZellLayout = new(32, 4, 6, 7, 12, 10, 11, 8, 13, 15, 26, 28, ElementPercent: 14);
    private static readonly Layout AngeloLayout = new(20, 2, 4, 5, 10, 8, 9, 6, 11, 13, 14, 16, ElementPercent: 12);

    private static BattleNativeAction[] ReadNativeActions(byte[] bytes, KernelData kernel, out BattleNativeKernelRecord[] records)
    {
        Dictionary<int, int> sizes = new() { [0] = 8, [1] = 60, [2] = 132, [3] = 20, [4] = 12, [5] = 24, [6] = 36, [7] = 24, [9] = 20, [10] = 16, [18] = 24, [19] = 16, [20] = 8, [21] = 24, [22] = 32, [23] = 4, [24] = 8, [25] = 20, [26] = 1, [27] = 16, [29] = 60 };
        Dictionary<int, ImmutableArray<byte>[]> sections = sizes.ToDictionary(pair => pair.Key, pair => ReadRecords(bytes, pair.Key, pair.Value));
        records = sections.SelectMany(section => section.Value.Select((raw, index) => new BattleNativeKernelRecord(section.Key, index, raw))).ToArray();
        List<BattleNativeAction> result = [];
        AddFamily(1, MagicLayout, kernel.MagicSpells.Count, index => kernel.MagicSpells[index].Name, 2);
        AddFamily(2, GuardianLayout, kernel.Guardians.Count, index => kernel.Guardians[index].AttackName, 254, 64);
        AddFamily(3, EnemyLayout, kernel.EnemyAttacks.Count, index => kernel.EnemyAttacks[index].Name, 8);
        AddFamily(5, SquallLayout, kernel.SquallLimits.Count, index => kernel.SquallLimits[index].Name, 249, dispatch: 250, character: CharacterForCommand(5));
        AddFamily(7, ItemLayout, kernel.BattleItems.Count, index => kernel.BattleItems[index].Name, 4);
        AddFamily(9, IndependentGuardianLayout, kernel.IndependentGuardianAttacks.Count, index => kernel.IndependentGuardianAttacks[index].AttackName, 240);
        AddFamily(21, IrvineLayout, kernel.IrvinLimits.Count, index => kernel.IrvinLimits[index].Name, 237, dispatch: 14, character: CharacterForCommand(14));
        AddFamily(22, ZellLayout, kernel.ZellLimits.Count, index => kernel.ZellLimits[index].Name, 241, dispatch: 11, character: CharacterForCommand(11));
        AddFamily(25, AngeloLayout, kernel.RinoaAngeloAttacks.Count, index => kernel.RinoaAngeloAttacks[index].Name, 19, character: CharacterForCommand(19));

        foreach (byte command in new byte[] { 17, 18, 20, 21, 22 })
        {
            for (int index = 0; index < sections[18].Length; index++)
            {
                // These five native consumers share this table. Actual player selection belongs to its limit invocation owner.
                result.Add(Project(sections[18][index], NpcLayout, command, index, 18, index) with { Name = kernel.NpcLimits[index].Name, CharacterId = CharacterForCommand(command), DispatchCommand = command });
            }
        }

        for (int command = 0; command < kernel.BattleCommands.Count; command++)
        {
            if (command is not (7 or 23 or 24 or 25 or 26 or 27 or 29 or 30 or 31 or 32 or 33 or 34))
            {
                continue;
            }
            int index = kernel.BattleCommands[command].AbilityId;
            if ((uint)index >= sections[10].Length)
            {
                throw new InvalidDataException("Original battle command points outside its additional action records.");
            }
            result.Add(Project(sections[10][index], AdditionalLayout, (byte)command, index, 10, index) with { Name = kernel.BattleCommands[command].Name, DispatchCommand = (byte)command, TargetFlags = kernel.BattleCommands[command].Unknown1, SupplementalKernelIndex = command, SupplementalRecord = sections[0][command] });
        }

        if (sections[20].Length != sections[19].Length * 4)
        {
            throw new InvalidDataException("Original Blue Magic requires all four crisis records for every spell.");
        }
        for (int index = 0; index < sections[19].Length; index++)
        {
            for (int crisis = 1; crisis <= 4; crisis++)
            {
                int supplementalIndex = index * 4 + crisis - 1;
                ImmutableArray<byte> supplemental = sections[20][supplementalIndex];
                result.Add(Project(sections[19][index], BlueMagicLayout, 15, index, 19, index) with { Name = kernel.BlueMagics[index].Name, CharacterId = CharacterForCommand(15), DispatchCommand = 15, CrisisLevel = (byte)crisis, Power = supplemental[6], Hit = supplemental[7], Statuses0 = BinaryPrimitives.ReadUInt16LittleEndian(supplemental.AsSpan().Slice(4)), Statuses1 = BinaryPrimitives.ReadUInt32LittleEndian(supplemental.AsSpan()), SupplementalKernelIndex = supplementalIndex, SupplementalRecord = supplemental });
            }
        }
        //48FE20 reads the same kernel family for these command variants. Their invocation state remains separate from this data catalog.
        AddCommandVariants(2, 6, 16, 247);
        AddCommandVariants(4, 13);
        AddCommandVariants(8, 236);
        AddCommandVariants(237, 14, 238);
        AddCommandVariants(241, 239);
        AddCommandVariants(240, 244, 245);
        return result.ToArray();

        void AddCommandVariants(byte command, params byte[] variants)
        {
            BattleNativeAction[] family = result.Where(action => action.Command == command).ToArray();
            foreach (byte variant in variants)
            {
                result.AddRange(family.Select(action => action with { Command = variant }));
            }
        }

        void AddFamily(int section, Layout layout, int count, Func<int, string?> name, byte command, int idOffset = 0, byte? dispatch = null, int? character = null)
        {
            if (sections[section].Length != count)
            {
                throw new InvalidDataException("Original typed action catalog and raw kernel section count disagree.");
            }
            for (int index = 0; index < count; index++)
            {
                BattleNativeAction action = Project(sections[section][index], layout, command, index + idOffset, section, index) with { Name = name(index), DispatchCommand = dispatch, CharacterId = character };
                if (section == 3)
                {
                    action = action with { HitCount = (byte)(action.HitCount & 0x7F) };
                }
                result.Add(action);
            }
        }

        int? CharacterForCommand(int command)
        {
            int index = kernel.Characters.FindIndex(character => character.LimitId == command);
            return index < 0 ? null : index;
        }
    }

    private static BattleNativeAction Project(ImmutableArray<byte> raw, Layout layout, byte command, int id, int section, int index)
    {
        if (raw.Length != layout.Size)
        {
            throw new InvalidDataException("Original action record has an unexpected native width.");
        }
        return new BattleNativeAction(command, checked((ushort)id), section, index)
        {
            MagicId = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan().Slice(layout.Magic)),
            AttackType = raw[layout.Type],
            Power = layout.Power < 0 ? (byte)0 : raw[layout.Power],
            HitCount = layout.Hits < 0 ? (byte)1 : raw[layout.Hits],
            TargetFlags = ReadOptional(layout.Target),
            AttackFlags = raw[layout.Flags],
            DamagePresentation = layout.Presentation < 0 ? (byte)0 : raw[layout.Presentation],
            Element = raw[layout.Element],
            ElementPercent = ReadOptional(layout.ElementPercent),
            StatusAttack = layout.StatusAttack < 0 ? (byte)0 : raw[layout.StatusAttack],
            Statuses0 = layout.SavedStatus < 0 ? (ushort)0 : BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan().Slice(layout.SavedStatus)),
            Statuses1 = layout.BattleStatus < 0 ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan().Slice(layout.BattleStatus)),
            Hit = ReadOptional(layout.Hit),
            Critical = ReadOptional(layout.Critical),
            PowerModifier = ReadOptional(layout.PowerModifier),
            LevelModifier = ReadOptional(layout.LevelModifier),
            RawRecord = raw,
            SupplementalRecord = []
        };

        byte? ReadOptional(int offset) => offset < 0 ? null : raw[offset];
    }

    private static ImmutableArray<byte>[] ReadRecords(byte[] bytes, int section, int size)
    {
        if (bytes.Length < 228 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != 56)
        {
            throw new InvalidDataException("Original action records require the complete56-section kernel directory.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + section * 4));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8 + section * 4));
        if (start < 228 || end < start || end > bytes.Length || (end - start) % size != 0)
        {
            throw new InvalidDataException($"Original kernel section {section} does not contain complete {size}-byte records.");
        }
        ImmutableArray<byte>[] result = new ImmutableArray<byte>[(end - start) / size];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = bytes.AsSpan(start + index * size, size).ToArray().ToImmutableArray();
        }
        return result;
    }
}
