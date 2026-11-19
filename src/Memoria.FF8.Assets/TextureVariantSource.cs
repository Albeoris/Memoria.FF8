using System.Buffers.Binary;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets
{
    public sealed record TextureVariantSourceInfo(bool HasInstalledImage, IReadOnlyList<string> Variants);

    public interface ITextureVariantSource
    {
        TextureVariantSourceInfo DescribeTextureVariants(TextureBuildContext context);
    }
}

namespace Memoria.FF8.Assets.Remastered
{
    public sealed partial class RemasteredGameDataSource : ITextureVariantSource
    {
        private readonly Dictionary<string, TextureVariantSourceInfo> textureVariantMetadata = [];

        public TextureVariantSourceInfo DescribeTextureVariants(TextureBuildContext context)
        {
            ImageBinding? binding = FieldModelImageBinding(context);
            if (binding is null && !imageBindings.TryGetValue(context.Key, out binding))
            {
                return new(false, []);
            }
            if (textureVariantMetadata.TryGetValue(binding.Path, out TextureVariantSourceInfo? cached))
            {
                return cached;
            }
            using Stream image = Open(binding.Path);
            Span<byte> header = stackalloc byte[24];
            image.ReadExactly(header);
            if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                throw new InvalidDataException("The installed model variant source is not a PNG image.");
            }
            int width = BinaryPrimitives.ReadInt32BigEndian(header[16..]);
            TextureVariantSourceInfo result = new(true, binding.EyeVariants && width == 768 ? ["eyes-closed"] : []);
            textureVariantMetadata.Add(binding.Path, result);
            return result;
        }
    }
}
