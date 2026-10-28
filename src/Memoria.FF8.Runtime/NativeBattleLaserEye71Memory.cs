namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleLaserEye71State? LaserEye71
    {
        get => effectState.LaserEye71;
        init => effectState = effectState with { LaserEye71 = value };
    }
}
