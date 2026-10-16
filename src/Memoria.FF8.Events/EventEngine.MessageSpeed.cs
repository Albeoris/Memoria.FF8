using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeMessageChannelTiming(ushort Speed16, ushort Counter20);

public sealed partial class EventEngine
{
    private int[] PrepareNativeMessageSpeed(Fiber fiber)
    {
        if (fiber.Stack.Count < 2)
        {
            throw new NotSupportedException("SETMESSPEED requires its two retained DWORD operands; native528E10 does not pop them.");
        }
        int[] top = fiber.Stack.Take(2).ToArray();
        if (top[1] is < 0 or > 3)
        {
            throw new NotSupportedException("SETMESSPEED requires a represented native message channel0..3; out-of-record aliases are unsupported.");
        }
        return [top[1], top[0]];
    }
}
