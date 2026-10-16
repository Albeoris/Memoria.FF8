using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public ImmutableArray<byte> ReadPlayerNativeCommandSlots(NativeCharacterVitals vitals, int row)
    {
        NativeCharacterVital character = vitals.Records[row];
        uint passives = 0;
        for (int slot = 0; slot < 4; slot++)
        {
            int ability = unchecked((byte)(character.Junction.Abilities >> (slot * 8)));
            if (ability is >= 58 and < 78)
            {
                var passive = battleKernel.CharacterAbilities[ability - 58];
                passives |= (uint)(passive.Flag1 | passive.Flag2 << 8 | passive.Flag3 << 16);
            }
        }
        byte[] commands = [(passives & 1) != 0 ? (byte)12 : (byte)1, 0, 0, 0];
        //495530 creates Attack/Mug plus exactly the first three saved command slots, preserving holes and duplicates.
        for (int slot = 0; slot < 3; slot++)
        {
            int ability = unchecked((byte)(character.Junction.Commands >> (slot * 8)));
            if (ability is >= 20 and < 39)
            {
                commands[slot + 1] = vitals.MenuDefinitions.CommandAbilityCommands[ability - 20];
            }
        }
        return commands.ToImmutableArray();
    }
}
