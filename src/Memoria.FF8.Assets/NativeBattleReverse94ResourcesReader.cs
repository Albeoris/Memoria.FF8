using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleReverse94ResourcesReader
{
    public static NativeBattleRecover93Resources Read(byte[] executable) => NativeBattleRecover93ResourcesReader.Read(executable, NativeBattleAngeloRestorationProfile.Reverse94);
}

public sealed partial class GameAssets
{
    private NativeBattleRecover93Resources? nativeReverse94Resources;
    public NativeBattleRecover93Resources ReadNativeReverse94Resources() => nativeReverse94Resources ??= NativeBattleReverse94ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
