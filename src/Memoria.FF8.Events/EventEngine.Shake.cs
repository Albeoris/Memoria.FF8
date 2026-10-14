using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeShakeTables? ShakeTables { get; set; }
    public NativeFieldShake? FieldShake => services.FieldShake;

    private void ResetNativeShakeForLoad(NativeFieldRuntimeProfile? profile)
    {
        if (profile is not { IsVerified: true } || services.FieldShake is not NativeFieldShake current)
        {
            return;
        }
        current.Validate();
        if (!services.CanStoreFieldShake)
        {
            throw new NotSupportedException("Native normal field loading requires a writable current shake process owner.");
        }
        services.SetFieldShake(current.ResetForLoad());
    }

    private void ValidateNativeShakeCallback()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services.FieldShake is not NativeFieldShake shake)
        {
            return;
        }
        shake.Validate();
        if (!services.CanStoreFieldShake || ShakeTables is null && (shake.X.Enabled || shake.X.Oscillating || shake.X.Duration > shake.X.Counter || shake.Y.Enabled || shake.Y.Oscillating || shake.Y.Duration > shake.Y.Counter))
        {
            throw new NotSupportedException("Native shake callback needs its current writable process owner and consumed lookup tables before mutation.");
        }
    }

    private string? UnsupportedShake(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.SHAKE or Jsm.Opcode.SHAKEOFF))
        {
            return null;
        }
        if (services.FieldShake is not NativeFieldShake shake || !services.CanStoreFieldShake || operation.Opcode == Jsm.Opcode.SHAKE && ShakeTables is null || NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native SHAKE requires its current writable process owner and authenticated original lookup tables";
        }
        shake.Validate();
        if (operation.Opcode == Jsm.Opcode.SHAKE)
        {
            if (fiber.Stack.Count < 4)
            {
                return "Native SHAKE requires four original WORD operands";
            }
            if (fiber.Stack.Capture().Take(4).Any(value => !value.ToWidth(2).IsExact))
            {
                return "Native SHAKE consumes four exact lowWORD values; ambiguous consumed words remain unsupported";
            }
        }
        return null;
    }

    private int[] ExecuteShake(Fiber fiber, bool enabled)
    {
        NativeFieldShake shake = services.FieldShake!;
        int[] args = [];
        if (enabled)
        {
            args = new int[4];
            string[] expressions = new string[4];
            for (int index = 3; index >= 0; index--)
            {
                NativeScalarValue raw = fiber.Stack.PopValue();
                args[index] = raw.IsExact ? raw.RequireExact() : raw.ToWidth(2).RequireExact();
                expressions[index] = fiber.StackExpressions.Pop();
            }
            fiber.LastExpressions = expressions;
        }
        NativeFieldShake next = enabled ? shake with { X = shake.X with { Enabled = true, Amplitude = (short)args[0], Duration = (short)args[1] }, Y = shake.Y with { Enabled = true, Amplitude = (short)args[2], Duration = (short)args[3] } } : shake with { X = shake.X with { Enabled = false }, Y = shake.Y with { Enabled = false } };
        services.SetFieldShake(next);
        if (enabled)
        {
            // 520BA0 returns3;520C20 returns2 and preserves interpolation/output for decay.
            WaitFrames(fiber, 1);
        }
        return args;
    }

    private void StepNativeShake()
    {
        if (NativeRuntimeProfile is { IsVerified: true } && services.FieldShake is NativeFieldShake shake)
        {
            services.SetFieldShake(shake.Step(ShakeTables));
        }
    }
}
