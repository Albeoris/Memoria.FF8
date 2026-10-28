namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect207State? Effect207
    {
        get => effectState.Effect207;
        init => effectState = effectState with { Effect207 = value };
    }
}
