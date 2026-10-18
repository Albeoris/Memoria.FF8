using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWater222ResourcesReader
{
    public static NativeBattleEffectBinding Binding(byte[] executable)
    {
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[221];
        NativeBattleEffectInitializerResource[] operands = NativeBattleEffectInitializerPolicy.Resources(binding).ToArray();
        if (binding is not { Effect: 222, Entry: 0xA9B200, Initializer: 0xA9B1D0 } || operands.Length != 2 || !string.Equals(operands[0].Name, "mag221_b.00", StringComparison.OrdinalIgnoreCase) || !string.Equals(operands[1].Name, "mag221_b.01", StringComparison.OrdinalIgnoreCase) || operands[0].GlobalAddress != 0x2796BDC || operands[1].GlobalAddress != 0x2796BD8)
        {
            throw new InvalidDataException("Native Water requires its actual two-file initializer profile.");
        }
        return binding;
    }

    public static NativeBattleWater222Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectHeapAllocation> files)
    {
        NativeBattleEffectBinding binding = Binding(executable);
        if (files.IsDefault || files.Length != 2 || !NativeBattleEffectInitializerPolicy.Resources(binding).Select(operand => operand.Name).SequenceEqual(files.Select(file => file.Identity), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Native Water files must follow the initializer's actual resource order.");
        }
        NativeExecutableMemory memory = new(executable);
        NativeBattleWater222Resources result = new(files, new(new("FF8_EN:186C86C", memory.Read(0x186C86C, 0x70).ToImmutableArray())));
        result.Validate();
        for (int index = 0; index < 5; index++)
        {
            NativeBattleWater222TexturePair.Resolve(result.Profile, result.Files, index, false);
            NativeBattleWater222TexturePair.Resolve(result.Profile, result.Files, index, true);
        }
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWater222Resources? nativeWater222Resources;

    public NativeBattleWater222Resources ReadNativeWater222Resources()
    {
        if (nativeWater222Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            NativeBattleEffectBinding binding = NativeBattleWater222ResourcesReader.Binding(executable);
            using IFlArchive magic = OpenArchive("magic");
            ImmutableArray<NativeBattleEffectHeapAllocation> files = NativeBattleEffectInitializerPolicy.Resources(binding).Select(operand => new NativeBattleEffectHeapAllocation(operand.Name, Read(magic, operand.Name).ToImmutableArray())).ToImmutableArray();
            nativeWater222Resources = NativeBattleWater222ResourcesReader.Read(executable, files);
        }
        return nativeWater222Resources;
    }
}
