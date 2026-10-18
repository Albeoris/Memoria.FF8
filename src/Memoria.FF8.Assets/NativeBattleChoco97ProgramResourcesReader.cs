using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97ProgramResourcesReader
{
    public static ImmutableArray<NativeBattleChoco97LoadedProgramResources> ReadLoaded(ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        ImmutableArray<NativeBattleChoco97LoadedProgramResources>.Builder result = ImmutableArray.CreateBuilder<NativeBattleChoco97LoadedProgramResources>(3);
        foreach ((NativeBattleChoco97LoadedProgram profile, short fileId) in new[] { (NativeBattleChoco97LoadedProgram.File445, (short)445), (NativeBattleChoco97LoadedProgram.File449, (short)449), (NativeBattleChoco97LoadedProgram.File450, (short)450) })
        {
            var definition = NativeBattleChoco97ProgramDrawing.LoadedProfile(profile);
            NativeBattleEffectFileAsset[] matches = files.Where(file => file.FileId == fileId).ToArray();
            if (matches.Length != 1 || matches[0].Data.IsDefault || matches[0].Data.Length != definition.Length)
            {
                throw new InvalidDataException("Native Choco loaded program requires its exact original file extent and identity.");
            }
            result.Add(new(profile, new(definition.Address, matches[0].Data)));
        }
        return result.MoveToImmutable();
    }

    public static NativeBattleChoco97ProgramResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x168F8BC, memory.Read(0x168F8BC, 0x2884).ToImmutableArray()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ProgramResources? nativeChoco97Program;
    private ImmutableArray<NativeBattleChoco97LoadedProgramResources> nativeChoco97LoadedPrograms;

    public NativeBattleChoco97ProgramResources ReadNativeChoco97ProgramResources() => nativeChoco97Program ??= NativeBattleChoco97ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));

    public ImmutableArray<NativeBattleChoco97LoadedProgramResources> ReadNativeChoco97LoadedProgramResources()
    {
        if (nativeChoco97LoadedPrograms.IsDefault)
        {
            nativeChoco97LoadedPrograms = NativeBattleChoco97ProgramResourcesReader.ReadLoaded(ReadNativeChoco97IoResources().Files);
        }
        return nativeChoco97LoadedPrograms;
    }
}
