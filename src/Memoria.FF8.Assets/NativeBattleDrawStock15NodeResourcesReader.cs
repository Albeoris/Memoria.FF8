using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDrawStock15NodeResourcesReader
{
    public static NativeBattleCuragaExecutableSource Read(byte[] executable)
    {
        return NativeBattleCuragaResourcesReader.ReadExecutableSource(executable, 15);
    }
}
