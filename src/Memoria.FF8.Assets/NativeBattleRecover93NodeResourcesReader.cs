using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRecover93NodeResourcesReader
{
    public static NativeBattleRecover93NodeResource Read(byte[] executable)
    {
        return Read(executable, NativeBattleAngeloRestorationProfile.Recover93);
    }

    internal static NativeBattleRecover93NodeResource Read(byte[] executable, NativeBattleAngeloRestorationProfile profile)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] bytes = memory.Read(profile.SourceAddress, profile.SourceLength);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0)
        {
            throw new InvalidDataException("Native Recover node source requires its original cold initialization DWORD.");
        }
        bool reverse = profile.Effect == 94;
        int[] emitterOffsets = reverse ? [0x23F0, 0x2548, 0x26A0, 0x27F8, 0x2950, 0x2AA8] : [0x1B10, 0x1C68, 0x1DC0, 0x1F18, 0x2070];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint expected = table switch
                {
                    0 when index < emitterOffsets.Length => (uint)emitterOffsets[index],
                    1 when reverse && index == 0 => 0x104,
                    1 when reverse && index == 1 => 0x248,
                    1 when reverse && index == 3 => 0x38C,
                    1 when reverse && index == 4 => 0x4D0,
                    1 when reverse && index == 5 => 0x614,
                    2 when reverse && index == 2 => 0x7DC,
                    _ when reverse => 0,
                    1 when index == 3 => 0x104,
                    1 when index == 4 => 0x1FC,
                    2 when index == 0 => 0x328,
                    2 when index == 2 => 0x388,
                    _ => 0
                };
                if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 + table * 64 + index * 4)) != expected)
                {
                    throw new InvalidDataException("Native Recover node source has a different complete directory owner.");
                }
            }
        }
        return new(System.Collections.Immutable.ImmutableArray.CreateRange(bytes));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRecover93NodeResource? nativeRecover93Nodes;
    public NativeBattleRecover93NodeResource ReadNativeRecover93NodeResource() => nativeRecover93Nodes ??= NativeBattleRecover93NodeResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
