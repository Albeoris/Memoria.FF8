namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect251BoneStep(NativeBattleEffect251Frame Frame, NativeBattleEffectArena Direction12, NativeBattleEffectArena Position8);

public sealed partial record NativeBattleEffect251Inputs
{
    internal Func<byte, BattleModelOwner>? AuthoredOperand { get; init; }

    internal BattleModelOwner ResolveTrailModel(NativeBattleEffect251Frame frame)
    {
        byte operand = NativeBattleEffect251Profile.TrailModelOperand;
        return AuthoredOperand?.Invoke(operand) ?? ReadPackets(frame).ModelAliases.DecodeLiteral(operand);
    }
    public Func<NativeBattleEffect251Frame, BattleModelOwner, int, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect251BoneStep> BoneWorld5FC0 { get; init; } = (_, _, _, _, _) => throw new NotSupportedException("Effect251 requires its current bone and world owner.");
    public Func<NativeBattleEffect251Frame, BattleModelOwner, NativeBattleEffect251PointStep> Bounds6690 { get; init; } = (_, _) => throw new NotSupportedException("Effect251 requires its current projected model bounds.");
}
