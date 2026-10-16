using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class NativeTutorialCatalog
{
    public BattlePlayerStats ReadStatistics(NativeCharacterVitals party, int row) => new BattlePlayerStatReader(kernel).Read(party, row);
}
