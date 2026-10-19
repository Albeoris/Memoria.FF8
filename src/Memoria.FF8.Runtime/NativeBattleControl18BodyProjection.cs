namespace Memoria.FF8.Runtime;

public static class NativeBattleControl18BodyProjection
{
    public static NativeBattleModuleDepthCueVertex Project50FD40(NativeBattleActionMatrix currentGte, NativeBattleVector input, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(currentGte, input, projection, dqa, dqb);
        return result with { Projected = result.Projected with { Depth = (ushort)Math.Max(10, (int)result.Projected.Depth) } };
    }
}
