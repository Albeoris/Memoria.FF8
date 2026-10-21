namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshAttackProfile(int Effect, uint Callback, ushort MaterialVariant)
{
    public static NativeBattleGilgameshAttackProfile ForEffect(int effect) => effect switch
    {
        329 => new(329, 0x591250, 0),
        330 => new(330, 0x5920E0, 1),
        _ => throw new NotSupportedException("Native Gilgamesh attack requires its supported callback profile.")
    };

    public void Validate()
    {
        if (this != ForEffect(Effect))
        {
            throw new InvalidDataException("Native Gilgamesh attack profile contradicts its original callback or material selection.");
        }
    }
}
