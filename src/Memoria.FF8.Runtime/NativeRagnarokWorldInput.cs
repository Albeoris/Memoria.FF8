namespace Memoria.FF8.Runtime;

public readonly record struct NativeRagnarokInputPacket(sbyte Steering13, sbyte Throttle11, sbyte Vertical12);

public static class NativeRagnarokWorldInput
{
    /// <summary>5573C0 ordinary non-foot digital controls, with analog axes absent. Action edges are consumed separately.</summary>
    public static NativeRagnarokInputPacket ReadDigitalButtons(uint buttons, byte variant = 0, byte scriptMode = 0)
    {
        if (variant != 0 || scriptMode != 0)
        {
            throw new NotSupportedException("Ragnarok digital controls require the ordinary variant0/scriptMode0 branch; scripted/autopilot and analog consumers need their actual owners.");
        }
        sbyte throttle = (buttons & 0x10) != 0 ? (sbyte)-127 : (buttons & 0x80) != 0 ? (sbyte)127 : (sbyte)0;
        sbyte steering = 0;
        sbyte vertical = 0;
        if ((buttons & 0x2000) != 0)
        {
            steering = -127;
        }
        if ((buttons & 0x8000) != 0)
        {
            steering = 127;
        }
        if ((buttons & 0x4000) != 0)
        {
            vertical = -127;
        }
        if ((buttons & 0x1000) != 0)
        {
            vertical = 127;
        }
        return new NativeRagnarokInputPacket(steering, throttle, vertical);
    }
}
