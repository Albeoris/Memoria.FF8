using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativePocketStationText
{
    public static string? ChocoboName(NativePocketStationState? state)
    {
        state?.Validate();
        if (state?.ChocoboName is not { } name)
        {
            return null;
        }
        byte[] bytes = name.ToArray();
        return FF8Encoding.CreateEuropean().GetString(bytes.AsSpan(0, Array.IndexOf(bytes, (byte)0)));
    }
}
