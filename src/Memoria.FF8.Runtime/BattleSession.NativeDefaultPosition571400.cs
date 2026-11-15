namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleVector ReadNativeDefaultPosition571400(NativeBattleGraphicsState graphics, BattleModelOwner actor)
    {
        NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
        return new(anchor.X, anchor.Y, anchor.Z);
    }
}
