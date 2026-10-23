using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleResonance49ResourcesReader
{
    public static NativeBattleResonance49Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag048.tim\0" || BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(EffectObjectPointer, 4)) != EffectObjectAddress || BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(ScreenObjectPointer, 4)) != ScreenObjectAddress)
        {
            throw new InvalidDataException("Resonance requires its original initializer and actual pointed mutable-object identities.");
        }
        NativeBattleResonance49Resources resources = new(new(ProgrammeAAddress, memory.Read(ProgrammeAAddress, ProgrammeALength).ToImmutableArray()), new(ProgrammeBAddress, memory.Read(ProgrammeBAddress, ProgrammeBLength).ToImmutableArray()), new("FF8_EN:15ADAF8", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleResonance49Resources? nativeResonance49Resources;
    public NativeBattleResonance49Resources ReadNativeResonance49Resources() => nativeResonance49Resources ??= NativeBattleResonance49ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
