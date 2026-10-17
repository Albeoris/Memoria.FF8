using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleQuezacotlPathResourcesReader
{
    public static NativeBattleQuezacotlPathResources Read(byte[] executable)
    {
        NativeBattleQuezacotlPathResources resources = new(0x12AFE58, NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AFE58, 1944));
        resources.Validate();
        return resources;
    }
}
