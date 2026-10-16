using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public string[] ReadNativeCommandNames()
    {
        return battleKernel.BattleCommands.Select(command => command.Name ?? "").ToArray();
    }

    public BattleNativeCommandBinding ReadNativeCommand(byte command)
    {
        if (command >= battleKernel.BattleCommands.Count)
        {
            throw new InvalidDataException("Saved battle command is outside the original kernel command table.");
        }
        var row = battleKernel.BattleCommands[command];
        return new BattleNativeCommandBinding(command, row.Name ?? throw new InvalidDataException("Selected original battle command has no name."), row.Unknown1, row.AbilityId);
    }

    public BattleNativeLimitDefinition ReadNativeLimitDefinition(int characterId)
    {
        if ((uint)characterId >= battleKernel.Characters.Count)
        {
            throw new InvalidDataException("Battle character has no original Limit command record.");
        }
        var character = battleKernel.Characters[characterId];
        ImmutableArray<byte> timers = NativeKernelRecords.Single(record => record.Section == 29).Bytes;
        BattleNativeLimitDefinition result = new(characterId, character.LimitId, character.LimitParam, character.CrisisLevel, timers.AsSpan().Slice(16, 32).ToArray().ToImmutableArray());
        if (character.LimitId == 19)
        {
            result = result with { InvocationChoices = battleKernel.RinoaLimits.Select((choice, index) => new BattleNativeCommandBinding(19, choice.Name ?? throw new InvalidDataException("Native Rinoa Limit menu choice has no name."), choice.Target, (byte)index)).ToImmutableArray() };
        }
        else if (character.LimitId == 16)
        {
            ImmutableArray<byte> spellBytes = NativeKernelRecords.Where(record => record.Section == 27).OrderBy(record => record.Index).SelectMany(record => record.Bytes).ToImmutableArray();
            //1CF8918..1CF8953 is60 bytes; Lv100/crisis4 can index the contiguous following table.
            result = result with { SlotsSelectionBytes = NativeKernelRecords.Where(record => record.Section == 26).OrderBy(record => record.Index).SelectMany(record => record.Bytes).Concat(spellBytes).ToImmutableArray(), SlotsSpellBytes = spellBytes };
        }
        else if (character.LimitId == 14)
        {
            result = result with { ShotTimers = timers.AsSpan().Slice(56, 4).ToArray().ToImmutableArray() };
        }
        else if (character.LimitId is 5 or 11)
        {
            result = result with { InvocationChoices = [ReadNativeCommand(character.LimitId)] };
            if (character.LimitId == 11)
            {
                result = result with { DuelTimersAndStarts = timers.AsSpan().Slice(48, 8).ToArray().ToImmutableArray(), DuelMoveSets = NativeKernelRecords.Where(record => record.Section == 23).OrderBy(record => record.Index).SelectMany(record => record.Bytes).ToImmutableArray() };
            }
        }
        result.Validate();
        return result;
    }
}
