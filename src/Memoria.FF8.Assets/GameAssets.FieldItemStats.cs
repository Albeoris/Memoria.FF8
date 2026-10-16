using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private BattlePlayerStatReader? fieldItemStats;

    public BattleNativePlayerStatRules ReadFieldItemStatRules(NativeCharacterVitals vitals, int row)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Field stat items require the authenticated original stat projection contract.");
        }
        if (fieldItemStats is null)
        {
            using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
            fieldItemStats = new BattlePlayerStatReader(Read(main, "kernel.bin"));
        }
        //4F7DB7..4F7E0C clears junctions and equips B88B18's native identity-specific initial weapon.
        byte[] weapons = [0, 7, 11, 15, 19, 24, 28, 29, 30, 31, 32];
        NativeCharacterVitals temporary = vitals.DeepCopy();
        NativeCharacterVital character = temporary.Records[row];
        temporary.Records[row] = character with { Status = 0, Weapon = weapons[character.CharacterId], Junction = character.Junction.Reset() };
        if (character.CharacterId >= 8)
        {
            temporary.DreamWeapons[character.CharacterId - 8] = weapons[character.CharacterId];
        }
        return fieldItemStats.ReadRules(temporary, row);
    }
}
