using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattlePrimitiveTables? nativePrimitiveTables;

    public NativeBattlePrimitiveTables ReadNativePrimitiveTables() => nativePrimitiveTables ??= NativeBattlePrimitiveTablesReader.Read(File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
}
