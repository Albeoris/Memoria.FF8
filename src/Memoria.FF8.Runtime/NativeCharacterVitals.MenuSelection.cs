namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    public bool IsMenuCharacterAvailable(int row, NativeWorldControlState? controls)
    {
        Validate();
        if ((uint)row >= Records.Length)
        {
            return false;
        }
        NativeCharacterVital character = Records[row];
        return CombatParty.Contains((byte)row) || (character.CharacterId < 8 ? (character.MemberFlags & 1) != 0 : controls is not null && (controls.EventFlags104 & (1u << (character.CharacterId - 8))) != 0);
    }
}
