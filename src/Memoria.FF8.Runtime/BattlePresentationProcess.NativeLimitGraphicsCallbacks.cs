namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState RunNativeLimitGraphicsCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        if (callback is 0x48D1A0 or 0x4AD7C0)
        {
            graphics = ApplyNativeShotReadiness(graphics, callback);
        }
        else if (callback is 0x48E5A0 or 0x48E5E0)
        {
            EmitExpandedHostedDuelReadiness(graphics, callback);
        }
        else
        {
            throw new InvalidDataException($"Unknown native timed-input callback {callback:X}.");
        }
        return graphics;
    }


}
