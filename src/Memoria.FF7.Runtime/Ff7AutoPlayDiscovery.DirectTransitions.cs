using System.Globalization;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayDiscovery
{
    private const string DirectTransitionEffectPrefix = "native-field-transition:1:";

    internal static string? ReadDirectTransitionEffectFingerprint(FieldNavigationEntryProof proof)
    {
        if (!proof.Complete || proof.HasNoEffect || proof.DirectTransition is not { Map: > 64 and < 32767 } transition || proof.DirectGuardFingerprint is not { Length: 64 } guard || !guard.All(Uri.IsHexDigit))
        {
            return null;
        }
        return DirectTransitionEffectPrefix + transition.Map.ToString(CultureInfo.InvariantCulture) + ":" + guard;
    }

    internal static bool TryReadDirectTransitionEffect(string fingerprint, out int map)
    {
        map = 0;
        if (!fingerprint.StartsWith(DirectTransitionEffectPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        string[] fields = fingerprint[DirectTransitionEffectPrefix.Length..].Split(':');
        return fields.Length == 2 && fields[1].Length == 64 && fields[1].All(Uri.IsHexDigit) && int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out map) && map is > 64 and < short.MaxValue;
    }
}
