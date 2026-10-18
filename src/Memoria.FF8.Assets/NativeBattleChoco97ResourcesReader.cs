using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleChoco97Resources? nativeChoco97Resources;

    public NativeBattleChoco97Resources ReadNativeChoco97Resources()
    {
        if (nativeChoco97Resources is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            ImmutableArray<byte> vibrationData = memory.Read(0x1683420, 0x12C).ToImmutableArray();
            NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(4))], vibrationData);
            vibration.Validate();
            nativeChoco97Resources = new(ReadNativeChoco97IoResources(), ReadNativeChoco97NodeResources(), ReadNativeChoco97ProgramResources().ProgramSource, ReadNativeChoco97ModelResources(), ReadNativeChoco97CameraResources(), ReadNativeChoco97SpriteResources(), new(0x1683420, vibration), Sound(0x152B5BC), Sound(0x152B5C0), Sound(0x152B5C4));

            uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        }
        return nativeChoco97Resources;
    }
}
