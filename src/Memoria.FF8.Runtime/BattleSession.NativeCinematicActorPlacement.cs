namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorPlacement(NativeBattleWaterExecutionFrame frame, bool radial, NativeBattleGraphicsState graphics)
    {
        NativeBattleGraphicsModel Read(uint address) => graphics.ActorModels.Get(Index(address));
        void Write(uint address, NativeBattleGraphicsModel actor)
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(Index(address), actor) };
        }
        NativeBattleWaterExecutionFrame result = radial ? NativeBattleCinematicActorPlacement.DrawRadial64(frame, Read, Write) : NativeBattleCinematicActorPlacement.DrawScaled65(frame, Read, Write);
        return (result, graphics);

        int Index(uint address)
        {
            if (!NativeBattleGraphicsModelAddress.TryDecode(address, out int actorIndex, graphics.Models.Length))
            {
                throw new NotSupportedException("Native actor placement requires its actual physical graphics pointer.");
            }
            return actorIndex;
        }
    }
}
