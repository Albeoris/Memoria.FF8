using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    public NativeGlobalRandomState ReadNewGameGlobalRandom()
    {
        _ = ReadNewGamePocketStation();
        //The same authenticated56D9F0/47E410 saved-range clear includes1CFEFB4;init.out ends before it.
        return new(0);
    }
}
