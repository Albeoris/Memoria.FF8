using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleIfritResourcesReader
{
    public static NativeBattleScriptedInitializationDirectories Read(byte[] executable)
    {
        return Read(executable, NativeBattleCinematicProfile.ForEffect(201));
    }

    public static NativeBattleScriptedInitializationDirectories Read(byte[] executable, NativeBattleCinematicProfile profile)
    {
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[profile.Effect - 1];
        ImmutableArray<NativeBattleEffectInitializerResource> operands = NativeBattleEffectInitializerPolicy.Resources(binding);
        if (binding.Effect != profile.Effect || binding.Entry != profile.Entry || binding.Initializer != profile.Initializer || operands.Length != 2 || operands[0].Name != profile.InitialFile || operands[0].GlobalAddress != profile.File0Publication || operands[1].Name != Path.ChangeExtension(profile.InitialFile, "01") || operands[1].GlobalAddress != profile.File1Publication)
        {
            throw new InvalidDataException("Native cinematic initialization requires its original ordered two-file initializer.");
        }
        NativeExecutableMemory memory = new(executable);
        NativeBattleScriptedInitializationDirectories directories = new(new($"FF8_EN:{profile.InitializationAddress:X}", memory.Read(profile.InitializationAddress, 0x1C).ToImmutableArray()), new($"FF8_EN:{profile.OwnerDirectoryAddress:X}", memory.Read(profile.OwnerDirectoryAddress, 0x14).ToImmutableArray()));
        directories.Validate();
        return directories;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<int, NativeBattleScriptedInitializationDirectories> nativeCinematicInitializationDirectories = [];
    private readonly Dictionary<int, NativeBattleCinematicResources> nativeCinematicResources = [];

    public NativeBattleScriptedInitializationDirectories ReadNativeIfritInitializationDirectories()
    {
        return ReadNativeCinematicInitializationDirectories(201);
    }

    public NativeBattleCinematicResources ReadNativeIfritCinematicResources()
    {
        return ReadNativeCinematicResources(201);
    }

    public NativeBattleScriptedInitializationDirectories ReadNativeCinematicInitializationDirectories(int effect)
    {
        if (!nativeCinematicInitializationDirectories.TryGetValue(effect, out NativeBattleScriptedInitializationDirectories? directories))
        {
            directories = NativeBattleIfritResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), NativeBattleCinematicProfile.ForEffect(effect));
            nativeCinematicInitializationDirectories.Add(effect, directories);
        }
        return directories;
    }

    public NativeBattleCinematicResources ReadNativeCinematicResources(int effect)
    {
        if (!nativeCinematicResources.TryGetValue(effect, out NativeBattleCinematicResources? cinematic))
        {
            using var magic = OpenArchive("magic");
            using var battle = OpenArchive("battle");
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
            cinematic = NativeBattleCinematicResourcesReader.Read(executable, Read(magic, profile.InitialFile).ToImmutableArray(), profile) with { Files = NativeBattleCinematicFilesReader.Read(executable, profile, name => Read(battle, name).ToImmutableArray()) };
            nativeCinematicResources.Add(effect, cinematic);
        }
        return cinematic;
    }
}
