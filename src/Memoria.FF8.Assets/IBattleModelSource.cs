using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Decodes source-specific battle geometry into the shared model representation.</summary>
public interface IBattleModelSource
{
    BattleModel ReadModel(ResourceKey owner, byte[] content);

    BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character) => BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), content, character);
}
