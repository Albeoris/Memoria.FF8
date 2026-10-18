using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco100ProgramResourcesReader
{
    public static NativeBattleChoco100ProgramResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleChoco100ProgramFile>.Builder loaded = ImmutableArray.CreateBuilder<NativeBattleChoco100ProgramFile>(3);
        foreach ((NativeBattleChoco100LoadedProgram profile, short id) in new[] { (NativeBattleChoco100LoadedProgram.File492, (short)492), (NativeBattleChoco100LoadedProgram.File496, (short)496), (NativeBattleChoco100LoadedProgram.File502, (short)502) })
        {
            var definition = NativeBattleChoco100ProgramDrawing.LoadedProfile(profile);
            NativeBattleEffectFileAsset[] matches = files.Where(file => file.FileId == id).ToArray();
            if (matches.Length != 1 || matches[0].Data.IsDefault || matches[0].Data.Length != definition.Length)
            {
                throw new InvalidDataException("Native Choco program requires its exact original loaded file owner.");
            }
            loaded.Add(new(profile, new(definition.Address, matches[0].Data)));
        }
        return new(new(0x1663154, memory.Read(0x1663154, 0x2884).ToImmutableArray()), loaded.MoveToImmutable());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco100ProgramResources? nativeChoco100Program;

    public NativeBattleChoco100ProgramResources ReadNativeChoco100ProgramResources() => nativeChoco100Program ??= NativeBattleChoco100ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), ReadNativeChoco100IoResources().Files);
}
