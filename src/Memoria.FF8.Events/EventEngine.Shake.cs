using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeShakeTables? ShakeTables { get; set; }

    private string? UnsupportedShake(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.SHAKE or Jsm.Opcode.SHAKEOFF))
        {
            return null;
        }
        if (Scene.Shake is null || ShakeTables is null || NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native SHAKE requires preserved process-history alternatives and authenticated original lookup tables";
        }
        Scene.Shake.Validate();
        if (operation.Opcode == Jsm.Opcode.SHAKE)
        {
            int[] top = fiber.Stack.ToArray();
            if (top.Length < 4 || (short)top[0] is < 0 or short.MaxValue || (short)top[2] is < 0 or short.MaxValue)
            {
                return "Native SHAKE requires four original WORD operands and supported signed-word durations";
            }
        }
        return null;
    }

    private int[] ExecuteShake(Fiber fiber, bool enabled)
    {
        NativeFieldShake shake = Scene.Shake!;
        int[] args = enabled ? Pop(fiber, 4) : [];
        Scene.Shake = enabled ? shake with { X = shake.X with { Enabled = true, Amplitude = (short)args[0], Duration = (short)args[1] }, Y = shake.Y with { Enabled = true, Amplitude = (short)args[2], Duration = (short)args[3] } } : shake with { X = shake.X with { Enabled = false }, Y = shake.Y with { Enabled = false } };
        if (enabled)
        {
            // 520BA0 returns3;520C20 returns2 and preserves interpolation/output for decay.
            WaitFrames(fiber, 1);
        }
        return args;
    }

    private void StepNativeShake()
    {
        if (Scene.Shake is NativeFieldShake shake)
        {
            Scene.Shake = shake with { X = shake.X.Step(ShakeTables!), Y = shake.Y.Step(ShakeTables!) };
        }
    }
}
