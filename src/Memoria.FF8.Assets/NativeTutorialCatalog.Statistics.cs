using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets;

public sealed partial class NativeTutorialCatalog
{
    private KernelData? statusKernel;

    public BattlePlayerStats ReadStatistics(NativeCharacterVitals party, int row) => new BattlePlayerStatReader(kernel).Read(party, row);
    public BattleNativePlayerStatRules ReadStatRules(NativeCharacterVitals party, int row) => new BattlePlayerStatReader(kernel).ReadRules(party, row);
    public KernelData ReadStatusKernel() => statusKernel ??= KernelData.ReadFromArray(kernel, NativeTextEncoding.Create(language));
}
