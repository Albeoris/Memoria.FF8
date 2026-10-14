using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private NativeVibrationBank? vibrationBank;
    public NativeVibrationBank VibrationBank => vibrationBank ??= NativeVibrationBankReader.ReadWorld(wmset);
}
