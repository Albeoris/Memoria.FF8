using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleChoco99Resources? nativeChoco99Resources;

    public NativeBattleChoco99Resources ReadNativeChoco99Resources()
    {
        if (nativeChoco99Resources is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            ImmutableArray<byte> vibrationData = memory.Read(0x16659D8, 0x130).ToImmutableArray();
            NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(4))], vibrationData);
            vibration.Validate();
            nativeChoco99Resources = new(ReadNativeChoco99IoResources(), ReadNativeChoco99NodeResources(), ReadNativeChoco99ProgramResources().ProgramSource, ReadNativeChoco99ModelResources(), ReadNativeChoco99CameraResources(), ReadNativeChoco99SpriteResources(), ReadNativeChoco99ParticleResources(), ReadNativeChoco99MeteoResources(), ReadNativeChoco99SecondaryResources(), new(0x16659D8, vibration), Sound(0x1529FA4), Sound(0x1529FA8), Sound(0x1529FAC), Sound(0x1529FB0));

            uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        }
        return nativeChoco99Resources;
    }
}
