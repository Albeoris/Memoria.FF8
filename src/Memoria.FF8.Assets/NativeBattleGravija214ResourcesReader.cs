using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleGravija214ResourcesReader
{
    public static NativeBattleGravija214Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 15)) != "mag213-310.tim\0")
        {
            throw new InvalidDataException("Gravija requires its original initializer texture identity.");
        }
        byte[] roots = memory.Read(SqrtTableAddress, SqrtTableLength);
        byte[] sine = memory.Read(SineTableAddress, SineTableLength);
        NativeBattleGravija214Resources resources = new(new("FF8_EN:1172D00", memory.Read(SourceMeshAddress, EntryCopyLength).ToImmutableArray()), new(ProgrammeAddress, memory.Read(ProgrammeAddress, ProgrammeLength).ToImmutableArray()), new(0, SpriteAddress, 0, 0, memory.Read(SpriteAddress, SpriteLength).ToImmutableArray()), new("FF8_EN:1178938", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Enumerable.Range(0, 192).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(roots.AsSpan(index * 2))).ToImmutableArray(), Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(sine.AsSpan(index * 2))).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGravija214Resources? nativeGravija214Resources;
    public NativeBattleGravija214Resources ReadNativeGravija214Resources() => nativeGravija214Resources ??= NativeBattleGravija214ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
