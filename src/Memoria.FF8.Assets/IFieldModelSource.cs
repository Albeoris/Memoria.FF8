using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Decodes source geometry into the shared skeleton, mesh, texture and animation representation.</summary>
public interface IFieldModelSource
{
    IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter);
}
