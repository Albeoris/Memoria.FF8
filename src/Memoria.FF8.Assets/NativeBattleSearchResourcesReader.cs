using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleSearchResourcesReader
{
    public static NativeBattleSearchResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource program = new(0x16C0DB8, NativeBattleEffectStaticResourcesReader.Read(memory, 0x16C0DB8, 0x510));
        if (U32(program.Data.AsSpan(), 0) != 2 || U32(program.Data.AsSpan(), 4) != 0x1B8 || U32(program.Data.AsSpan(), 8) != 0x10 || U32(program.Data.AsSpan(), 12) != 0xE4)
        {
            throw new InvalidDataException("Native Search program requires its complete original geometry and animation directories.");
        }
        foreach (int geometry in new int[] { 0x10, 0xE4 })
        {
            if (U32(program.Data.AsSpan(), geometry) != 0x54 || U32(program.Data.AsSpan(), geometry + 4) != 9 || U32(program.Data.AsSpan(), geometry + 8) != 1)
            {
                throw new InvalidDataException("Native Search program geometry requires its original12-byte header and single shape.");
            }
        }
        NativeBattleEffectHeapAllocation target = new("FF8_EN:16C96E8", NativeBattleEffectStaticResourcesReader.Read(memory, 0x16C96E8, 0x1D0C));
        int[] sections = [0x14, 0x1A68, 0x1A84, 0x1D0C];
        if (U32(target.Data.AsSpan(), 0) != 3)
        {
            throw new InvalidDataException("Native Search target override requires its original three-section resource.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(target.Data.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Search target override section leaves its complete original resource.");
            }
        }
        NativeBattleSearchSpriteResource sprite = new(0x153D1E8, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x153D1E8));
        if (sprite.Bytes.Length != 0xDC)
        {
            throw new InvalidDataException("Native Search projected sprite exceeds its complete original resource extent.");
        }
        return new(new("FF8_EN:153D324", NativeBattleEffectStaticResourcesReader.Read(memory, 0x153D324, 0x278)), NativeBattleSearchModelResourcesReader.Read(executable), sprite, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x153D2C4), program, target, Sound(0x153D1DC), Sound(0x153D1E0), Sound(0x153D1E4), Sound(0x16CB3F4), Sound(0x16CB3F8));

        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleSearchResources? nativeSearchResources;
    public NativeBattleSearchResources ReadNativeSearchResources() => nativeSearchResources ??= NativeBattleSearchResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
