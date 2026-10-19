using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleIfritResourcesReader
{
    public static NativeBattleScriptedInitializationDirectories Read(byte[] executable)
    {
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[200];
        ImmutableArray<NativeBattleEffectInitializerResource> operands = NativeBattleEffectInitializerPolicy.Resources(binding);
        if (binding is not { Effect: 201, Entry: 0xB25780, Initializer: 0xB25750 } || operands.Length != 2 || operands[0] is not { Name: "mag200_b.00", GlobalAddress: 0x2796E4C } || operands[1] is not { Name: "mag200_b.01", GlobalAddress: 0x2796E48 })
        {
            throw new InvalidDataException("Native Ifrit requires its original ordered two-file initializer.");
        }
        NativeExecutableMemory memory = new(executable);
        NativeBattleScriptedInitializationDirectories directories = new(new("FF8_EN:1874894", memory.Read(0x1874894, 0x1C).ToImmutableArray()), new("FF8_EN:1874B58", memory.Read(0x1874B58, 0x14).ToImmutableArray()));
        directories.Validate();
        return directories;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleScriptedInitializationDirectories? nativeIfritInitializationDirectories;
    private NativeBattleCinematicResources? nativeIfritCinematicResources;

    public NativeBattleScriptedInitializationDirectories ReadNativeIfritInitializationDirectories()
    {
        return nativeIfritInitializationDirectories ??= NativeBattleIfritResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }

    public NativeBattleCinematicResources ReadNativeIfritCinematicResources()
    {
        if (nativeIfritCinematicResources is null)
        {
            using var magic = OpenArchive("magic");
            using var battle = OpenArchive("battle");
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            nativeIfritCinematicResources = NativeBattleCinematicResourcesReader.ReadIfrit(executable, Read(magic, "mag200_b.00").ToImmutableArray()) with { Files = NativeBattleCinematicFilesReader.ReadIfrit(executable, name => Read(battle, name).ToImmutableArray()) };
        }
        return nativeIfritCinematicResources;
    }
}
