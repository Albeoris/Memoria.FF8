using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleChoco100Resources? nativeChoco100Resources;

    public NativeBattleChoco100Resources ReadNativeChoco100Resources()
    {
        if (nativeChoco100Resources is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            ImmutableArray<byte> vibrationData = memory.Read(0x1656CB8, 0x12C).ToImmutableArray();
            NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(4))], vibrationData);
            vibration.Validate();
            nativeChoco100Resources = new(ReadNativeChoco100IoResources(), ReadNativeChoco100NodeResources(), ReadNativeChoco100ProgramResources().ProgramSource, ReadNativeChoco100ModelResources(), ReadNativeChoco100LoadedModelResources(), ReadNativeChoco100CameraResources(), ReadNativeChoco100SpriteResources(), ReadNativeChoco100CameraYProgram(), new(0x1656CB8, vibration), Sound(0x15297F4), Sound(0x15297F8), Sound(0x15297FC), Sound(0x1529800));

            uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        }
        return nativeChoco100Resources;
    }
}
