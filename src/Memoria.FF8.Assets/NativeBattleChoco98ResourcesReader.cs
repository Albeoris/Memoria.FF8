using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleChoco98Resources? nativeChoco98Resources;

    public NativeBattleChoco98Resources ReadNativeChoco98Resources()
    {
        if (nativeChoco98Resources is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            ImmutableArray<byte> vibrationData = memory.Read(0x16746FC, 0x130).ToImmutableArray();
            NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(4))], vibrationData);
            vibration.Validate();
            nativeChoco98Resources = new(ReadNativeChoco98IoResources(), [ReadNativeChoco98NodeResource()], ReadNativeChoco98ProgramResources().ProgramSource, ReadNativeChoco98ModelResources(), ReadNativeChoco98CameraResources(), ReadNativeChoco98SpriteResources(), new(0x16746FC, vibration), Sound(0x152AF34), Sound(0x152AF38), Sound(0x152AF3C));

            uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        }
        return nativeChoco98Resources;
    }
}
