namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect322Trajectory
{
    public static NativeBattleEffect322PointStep Sample(NativeBattleEffect322Frame frame, NativeBattleEffectArena controls64, int segment, int parameter, NativeBattleEffectArena retained8, NativeBattleSummonColors colors)
    {
        NativeBattleTrajectoryStep result = NativeBattleTrajectorySampler.Sample(frame.Memory, controls64, 7, segment, parameter, retained8, colors);
        return new(frame with { Memory = result.Memory }, result.Output8);
    }
}
