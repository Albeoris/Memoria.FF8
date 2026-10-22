namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGilgameshController
{
    private sealed partial class Execution
    {
        private int ZantetsukenFlare(uint address)
        {
            NativeBattleZantetsukenResources selected = resources.Zantetsuken ?? throw new NotSupportedException("Zantetsuken requires its original addressed flare resource.");
            return GilgameshScreenFlare(address, selected.Flare);
        }
    }
}
