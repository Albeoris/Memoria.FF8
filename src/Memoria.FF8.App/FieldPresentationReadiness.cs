using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class FieldPresentationReadiness
{
    public static bool HasUnobservedShakeOutput(NativeFieldShake? shake)
    {
        if (shake is null)
        {
            return false;
        }
        shake.Validate();
        // Native4765A0 preserves output on load;4765C0 first defines it on an ordinary callback.
        // Initial SHAKE activation also preserves output, so frame/active flags cannot establish readiness.
        return shake.X.Alternatives.All(item => item.Output is null) || shake.Y.Alternatives.All(item => item.Output is null);
    }
}
