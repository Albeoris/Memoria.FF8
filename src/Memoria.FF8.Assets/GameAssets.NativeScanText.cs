using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleScanTextResources? nativeScanTextResources;
    private long nativeScanTextGeneration = -1;
    private readonly Dictionary<ushort, NativeBattleScanCameraProfile> nativeScanCameraProfiles = [];

    public NativeBattleScanCameraProfile ReadNativeScanCameraProfile(ushort category)
    {
        if (!nativeScanCameraProfiles.TryGetValue(category, out NativeBattleScanCameraProfile? profile))
        {
            profile = NativeBattleScanTextResourcesReader.ReadCameraProfile(ReadNativeMemory(), category);
            nativeScanCameraProfiles.Add(category, profile);
        }
        return profile;
    }

    public NativeBattleScanTextResources ReadNativeScanTextResources()
    {
        long generation = resources.Generation;
        if (nativeScanTextResources is not null && nativeScanTextGeneration == generation)
        {
            return nativeScanTextResources;
        }
        using IFlArchive main = OpenArchive("main");
        byte[]? descriptions = resources.Source.Edition == GameDataEdition.Remastered ? resources.Read(new ResourceKey("text/enemy-scan")) : null;
        NativeBattleScanTextResources selected = NativeBattleScanTextResourcesReader.Read(ReadNativeMemory(), Read(main, "kernel.bin"), descriptions, resources.FindText(StaticTextResource.BattleScans));
        nativeScanTextResources = selected;
        nativeScanTextGeneration = generation;
        return selected;
    }
}
