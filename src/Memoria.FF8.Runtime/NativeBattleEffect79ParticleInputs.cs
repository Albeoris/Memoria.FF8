namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79ParticleBasisStep(NativeBattleEffect79Frame Frame, NativeBattlePackedMatrix32 Matrix);

public sealed partial record NativeBattleEffect79Inputs
{
    public Func<NativeBattleEffect79Frame, uint, ushort, bool, short, NativeBattleEffect79ParticleBasisStep>? ParticleBasis { get; init; }
}

public sealed record NativeBattleEffect79Point502170(uint TaskAddress, ushort Selector) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79StageTransition(int Intensity, bool ClearRgb) : NativeBattleEffect79Operation;
