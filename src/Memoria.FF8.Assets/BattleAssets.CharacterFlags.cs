namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public bool ReadCharacterNativeConditionFlag100(int character)
    {
        if ((uint)character >= battleKernel.Characters.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(character));
        }
        //48B5F0 reads kernel character+3 bit0 and publishes actor flag0x100.
        return (battleKernel.Characters[character].Gender & 1) != 0;
    }
}
