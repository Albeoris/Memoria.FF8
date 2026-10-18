using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco98ProgramResourcesReader
{
    public static NativeBattleChoco98ProgramResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleChoco98LoadedProgramResources>.Builder loaded = ImmutableArray.CreateBuilder<NativeBattleChoco98LoadedProgramResources>(4);
        foreach ((NativeBattleChoco98LoadedProgram profile, short id) in new[] { (NativeBattleChoco98LoadedProgram.File457, (short)457), (NativeBattleChoco98LoadedProgram.File461, (short)461), (NativeBattleChoco98LoadedProgram.File462, (short)462), (NativeBattleChoco98LoadedProgram.File463, (short)463) })
        {
            var definition = NativeBattleChoco98ProgramDrawing.LoadedProfile(profile);
            NativeBattleEffectFileAsset[] matches = files.Where(file => file.FileId == id).ToArray();
            if (matches.Length != 1 || matches[0].Data.IsDefault || matches[0].Data.Length != definition.Length)
            {
                throw new InvalidDataException("Native Choco program requires its exact original loaded file owner.");
            }
            loaded.Add(new(profile, new(definition.Address, matches[0].Data)));
        }
        return new(new(0x1680B9C, memory.Read(0x1680B9C, 0x2884).ToImmutableArray()), loaded.MoveToImmutable());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco98ProgramResources? nativeChoco98Program;

    public NativeBattleChoco98ProgramResources ReadNativeChoco98ProgramResources() => nativeChoco98Program ??= NativeBattleChoco98ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), ReadNativeChoco98IoResources().Files);
}
