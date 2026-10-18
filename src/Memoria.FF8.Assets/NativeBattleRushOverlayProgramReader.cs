using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRushOverlayProgramReader
{
    public static NativeBattleFullscreenOverlayProgram Read(byte[] executable)
    {
        const uint address = 0x1547144;
        byte[] bytes = new NativeExecutableMemory(executable).Read(address, 5 * 4);
        return new(address, Enumerable.Range(0, 5).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray());
    }
}
public sealed partial class GameAssets
{
    private NativeBattleFullscreenOverlayProgram? nativeRushOverlayProgram;
    public NativeBattleFullscreenOverlayProgram ReadNativeRushOverlayProgram() => nativeRushOverlayProgram ??= NativeBattleRushOverlayProgramReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
