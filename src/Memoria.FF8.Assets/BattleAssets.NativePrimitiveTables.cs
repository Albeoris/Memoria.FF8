using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattlePrimitiveTables? nativePrimitiveTables;

    public NativeBattlePrimitiveTables ReadNativePrimitiveTables() => nativePrimitiveTables ??= NativeBattlePrimitiveTablesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
