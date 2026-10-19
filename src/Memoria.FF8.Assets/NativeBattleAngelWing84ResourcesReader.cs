using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84Resources? nativeAngelWing84;

    public NativeBattleAngelWing84Resources ReadNativeAngelWing84Resources()
    {
        if (nativeAngelWing84 is null)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            uint address = 0x17243F8;
            int first = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(address + 8, 4));
            int second = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(address + 8 + (uint)first), 4));
            int length = checked(8 + first + second);
            NativeBattleEffectHeapAllocation texture = new("native-limit84-scene-texture", NativeBattleEffectStaticResourcesReader.Read(memory, address, length));
            nativeAngelWing84 = new(ReadNativeAngelWing84BodyResources(), ReadNativeAngelWing84CameraResources(), ReadNativeAngelWing84GlowResources(), ReadNativeAngelWing84NodeResources(), ReadNativeAngelWing84ProgramResources(), texture);
        }
        return nativeAngelWing84;
    }
}
