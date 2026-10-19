namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicActorPlacement(NativeBattleWaterExecutionFrame frame, bool radial, NativeBattleGraphicsState graphics)
    {
        NativeBattleGraphicsModel Read(uint address) => graphics.Models[Index(address)];
        void Write(uint address, NativeBattleGraphicsModel actor)
        {
            graphics = graphics with { Models = graphics.Models.SetItem(Index(address), actor) };
        }
        NativeBattleWaterExecutionFrame result = radial ? NativeBattleCinematicActorPlacement.DrawRadial64(frame, Read, Write) : NativeBattleCinematicActorPlacement.DrawScaled65(frame, Read, Write);
        return (result, graphics);

        int Index(uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native actor placement requires its actual physical graphics pointer.");
            }
            return checked((int)((address - 0x1D972C0) / 156));
        }
    }
}
