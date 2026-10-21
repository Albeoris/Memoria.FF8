using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStatusCureItemResourcesReader
{
    public static NativeBattleStatusCureItemResources Read(byte[] executable, ushort effect)
    {
        return Read(new NativeExecutableMemory(executable), effect);
    }

    public static NativeBattleStatusCureItemResources Read(IGameNativeMemorySource memory, ushort effect)
    {
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(effect);
        ImmutableArray<byte> data = NativeBattleEffectStaticResourcesReader.Read(memory, profile.SourceAddress, profile.SourceLength);
        NativeBattleGfAnimationSource source = new(profile.SourceAddress, data);
        int geometryCount = I32(0);
        int animation = I32(4);
        if (geometryCount is < 1 or > 64 || animation < checked(8 + geometryCount * 4) || animation > data.Length - 8)
        {
            throw new InvalidDataException("Native status-cure ACT requires its complete inline geometry and animation directory.");
        }
        for (int index = 0; index < geometryCount; index++)
        {
            int geometry = I32(8 + index * 4);
            int directory = I32(geometry);
            int vertices = I32(checked(geometry + 4));
            int shapes = I32(checked(geometry + 8));
            if (vertices is < 0 or > 4096 || shapes is < 1 or > 256 || directory != checked(12 + vertices * shapes * 8))
            {
                throw new InvalidDataException("Native status-cure ACT requires its original bounded vertex shapes.");
            }
            int cursor = checked(geometry + directory);
            foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
            {
                int count = I32(cursor);
                if (count is < 0 or > 4096)
                {
                    throw new InvalidDataException("Native status-cure ACT has an invalid primitive group count.");
                }
                cursor = checked(cursor + 4 + count * stride);
            }
            if (cursor > animation)
            {
                throw new InvalidDataException("Native status-cure ACT geometry overlaps its animation table.");
            }
        }
        int duration = I32(checked(animation + 4));
        if (duration is < 1 or > 65535)
        {
            throw new InvalidDataException("Native status-cure ACT requires its bounded original animation duration.");
        }
        NativeBattleGfAnimationWorkspace workspace = new(new byte[profile.WorkspaceBytes].ToImmutableArray());
        _ = workspace.Initialize7016B0(source, profile.WorkspaceBytes);
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, profile.SoundAddress, 4).AsSpan());
        string name = System.Text.Encoding.ASCII.GetString(NativeBattleEffectStaticResourcesReader.Read(memory, profile.FilenameAddress, profile.TextureName.Length + 1).AsSpan());
        if (name != profile.TextureName + "\0")
        {
            throw new InvalidDataException("Native status-cure TIM name contradicts its original initializer.");
        }
        return new(effect, source, sound);

        int I32(int offset)
        {
            if (offset < 0 || offset > data.Length - 4)
            {
                throw new InvalidDataException("Native status-cure ACT read leaves its original addressed envelope.");
            }
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
        }
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleStatusCureItemResources> nativeStatusCureItemResources = [];

    public NativeBattleStatusCureItemResources ReadNativeStatusCureItemResources(ushort effect)
    {
        if (!nativeStatusCureItemResources.TryGetValue(effect, out NativeBattleStatusCureItemResources? resources))
        {
            resources = NativeBattleStatusCureItemResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeStatusCureItemResources.Add(effect, resources);
        }
        return resources;
    }
}
