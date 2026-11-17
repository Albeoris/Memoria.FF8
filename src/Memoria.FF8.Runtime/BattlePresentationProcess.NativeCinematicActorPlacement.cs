namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorPlacement(NativeBattleWaterExecutionFrame frame, bool radial, NativeBattleGraphicsState graphics)
    {
        NativeBattleGraphicsModel Read(BattleModelOwner actorOwner) => graphics.ActorModels.Get(actorOwner);
        void Write(BattleModelOwner actorOwner, NativeBattleGraphicsModel actor)
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(actorOwner, actor) };
        }
        NativeBattleWaterExecutionFrame result = radial ? NativeBattleCinematicActorPlacement.DrawRadial64(frame, Read, Write) : NativeBattleCinematicActorPlacement.DrawScaled65(frame, Read, Write);
        return (result, graphics);
    }
}
