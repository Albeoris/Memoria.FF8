namespace Memoria.FF8.Runtime;

/// <summary>One prepared52B4F0(0) publication, including the already proved common field-resume tail.</summary>
public sealed record NativeBattleFieldReturnOwners(NativeBattleMusicReturnPreparation Music, NativeWorldControlState Controls, NativeSeeDState SeeD, NativeExamTimerState Timer, NativeSalaryPresentation Salary, NativeVibrationProcessState Vibration, NativeFieldFadeState Fade, NativeEventMemory Memory)
{
    public NativeBattleRetainedMemory? ReloadedMemory { get; init; }
    public NativeCrtRandomState? CrtRandomBefore { get; init; }
    public NativeCrtRandomState? CrtRandomAfter { get; init; }
}
