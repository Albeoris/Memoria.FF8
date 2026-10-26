using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect30ResourcesReader
{
    public static NativeBattleEffect30Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x160DF70, 11)) != "mag029.tim\0")
        {
            throw new InvalidDataException("Effect30 requires its original initializer resource name.");
        }
        ImmutableDictionary<uint, ImmutableArray<byte>> meshes = new uint[] { 0x160D538, 0x160DCF8, 0x160DC50 }.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address).Bytes);
        ImmutableArray<NativeBattleEffect30Scatter> scatter = Enumerable.Range(0, 19).Select(index =>
        {
            uint address = checked(0x160DED8u + (uint)(index * 8));
            short count = BinaryPrimitives.ReadInt16LittleEndian(memory.Read(address + 4, 2));
            if (count <= 0)
            {
                return new NativeBattleEffect30Scatter(0, 0, count);
            }
            byte[] operands = memory.Read(address, 4);
            return new NativeBattleEffect30Scatter(BinaryPrimitives.ReadInt16LittleEndian(operands), BinaryPrimitives.ReadInt16LittleEndian(operands.AsSpan(2)), count);
        }).ToImmutableArray();
        NativeBattleEffect30Resources result = new(new("FF8_EN:160D3EC", memory.Read(NativeBattleEffect30Profile.CameraAddress, NativeBattleEffect30Profile.CameraLength).ToImmutableArray()), new(memory.Read(NativeBattleEffect30Profile.SourceAddress, NativeBattleEffect30Profile.SourceLength).ToImmutableArray()) { ResourceAddress = NativeBattleEffect30Profile.SourceAddress }, meshes, new(0, 0x160DE10, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x160DE10)), scatter, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x160D3E8, 4)));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect30Resources? nativeEffect30Resources;
    public NativeBattleEffect30Resources ReadNativeEffect30Resources() => nativeEffect30Resources ??= NativeBattleEffect30ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
