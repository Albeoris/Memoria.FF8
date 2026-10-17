using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Decodes source geometry into the shared skeleton, mesh, texture and animation representation.</summary>
public interface IFieldModelSource
{
    /// <summary>Reports original ONE directory entries only when the source preserves that layout, independently of replacement textures.</summary>
    IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null);
}
