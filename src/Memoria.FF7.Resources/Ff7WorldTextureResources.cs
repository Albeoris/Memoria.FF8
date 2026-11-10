using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed class Ff7WorldTextureResources
{
    private readonly Ff7WorldResources resources;
    private readonly Dictionary<string, PcTexture> textures = new(StringComparer.OrdinalIgnoreCase);

    public Ff7WorldTextureResources(Ff7Installation installation, Ff7WorldResources resources)
    {
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        Descriptors = Ff7PcExecutableData.LoadWorldTextureDescriptors(installation, resources.World);
    }

    public IReadOnlyList<Ff7WorldTextureDescriptor> Descriptors { get; }

    public PcTexture Read(int textureIndex, int animationFrame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(textureIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(textureIndex, Descriptors.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(animationFrame);
        Ff7WorldTextureDescriptor descriptor = Descriptors[textureIndex];
        string name = descriptor.AnimationFrames is { Count: > 0 } frames ? frames[animationFrame % frames.Count] : descriptor.ResourceName;
        if (!textures.TryGetValue(name, out PcTexture? texture))
        {
            texture = PcTexture.Read(resources.ReadRequired(name));
            textures.Add(name, texture);
        }
        return texture;
    }
}
