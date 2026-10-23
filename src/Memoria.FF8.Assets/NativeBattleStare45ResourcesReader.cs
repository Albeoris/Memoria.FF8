using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleStare45Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleStare45ResourcesReader
{
    public static NativeBattleStare45Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag044.tim\0" || BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(EffectObjectPointer, 4)) != EffectObjectAddress)
        {
            throw new InvalidDataException("Stare requires its original initializer and actual pointed mutable-object identities.");
        }
        NativeBattleStare45Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new("FF8_EN:15C3584", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleStare45Resources? nativeStare45Resources;
    public NativeBattleStare45Resources ReadNativeStare45Resources() => nativeStare45Resources ??= NativeBattleStare45ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
