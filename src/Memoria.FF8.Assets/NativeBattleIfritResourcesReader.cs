using System.Buffers.Binary;
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
        return Read(new NativeExecutableMemory(executable), profile);
    }

    internal static NativeBattleScriptedInitializationDirectories Read(NativeExecutableMemory memory, NativeBattleCinematicProfile profile)
    {
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(memory)[profile.Effect - 1];
        ImmutableArray<NativeBattleEffectInitializerResource> operands = NativeBattleEffectInitializerPolicy.Resources(binding);
        if (binding.Effect != profile.Effect || binding.Entry != profile.Entry || binding.Initializer != profile.Initializer || operands.Length != 2 || operands[0].Name != profile.InitialFile || operands[0].GlobalAddress != profile.File0Publication || operands[1].Name != Path.ChangeExtension(profile.InitialFile, "01") || operands[1].GlobalAddress != profile.File1Publication)
        {
            throw new InvalidDataException("Native cinematic initialization requires its original ordered two-file initializer.");
        }
        NativeBattleScriptedInitializationDirectories directories = new(new($"FF8_EN:{profile.InitializationAddress:X}", memory.Read(profile.InitializationAddress, 0x1C).ToImmutableArray()), new($"FF8_EN:{profile.OwnerDirectoryAddress:X}", memory.Read(profile.OwnerDirectoryAddress, 0x14).ToImmutableArray()));
        directories.Validate();
        return directories;
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<int, NativeBattleScriptedInitializationDirectories> nativeCinematicInitializationDirectories = [];
    private readonly Dictionary<int, NativeBattleCinematicResources> nativeCinematicResources = [];
    private readonly Dictionary<int, NativeBattleCinematicModelOperands> nativeCinematicModelOperands = [];
    private NativeExecutableMemory? nativeCinematicImage;

    private NativeExecutableMemory NativeCinematicImage => nativeCinematicImage ??= new(GameDataResources.ReadNativeExecutable(resources));

    public NativeBattleCinematicModelOperands ReadNativeCinematicModelOperands(NativeBattleEffectBinding binding)
    {
        if (!NativeBattleCinematicProfile.IsSupported(binding.Effect))
        {
            return new([], true);
        }
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(binding.Effect);
        ImmutableArray<NativeBattleEffectInitializerResource> files = NativeBattleEffectInitializerPolicy.Resources(binding);
        if (binding.Entry != profile.Entry || binding.Initializer != profile.Initializer || files.Length != 2 || files[0].Name != profile.InitialFile || files[0].GlobalAddress != profile.File0Publication)
        {
            throw new InvalidDataException("Cinematic model operands require the selected native initializer resource.");
        }
        if (!nativeCinematicModelOperands.TryGetValue(profile.Effect, out NativeBattleCinematicModelOperands? operands))
        {
            using var magic = OpenArchive("magic");
            NativeExecutableMemory image = NativeCinematicImage;
            byte[] instructionTable = image.Read(profile.InstructionTable, 0x200 * sizeof(uint));
            operands = NativeBattleCinematicOperandReader.Read(Read(magic, files[0].Name).ToImmutableArray(), profile, opcode => BinaryPrimitives.ReadUInt32LittleEndian(instructionTable.AsSpan(opcode * sizeof(uint))));
            nativeCinematicModelOperands.Add(profile.Effect, operands);
        }
        return operands;
    }

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
            directories = NativeBattleIfritResourcesReader.Read(NativeCinematicImage, NativeBattleCinematicProfile.ForEffect(effect));
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
            NativeExecutableMemory image = NativeCinematicImage;
            NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
            cinematic = NativeBattleCinematicResourcesReader.Read(image, Read(magic, profile.InitialFile).ToImmutableArray(), profile) with { Files = NativeBattleCinematicFilesReader.Read(image, profile, name => Read(battle, name).ToImmutableArray()) };
            nativeCinematicResources.Add(effect, cinematic);
        }
        return cinematic;
    }
}
