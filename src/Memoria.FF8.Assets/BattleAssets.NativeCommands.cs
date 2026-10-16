using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
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
        result.Validate();
        return result;
    }
}
