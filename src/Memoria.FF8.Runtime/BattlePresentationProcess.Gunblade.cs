namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public BattleGunbladeMode GunbladeMode { get; private set; }

    public void SetGunbladeMode(BattleGunbladeMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        GunbladeMode = mode;
    }
}
