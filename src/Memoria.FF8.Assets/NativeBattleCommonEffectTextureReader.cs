using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeBattleEffectInitializerAsset? nativeCommonEffectTexture;

    public NativeBattleEffectInitializerAsset ReadNativeCommonEffectTexture()
    {
        if (nativeCommonEffectTexture is null)
        {
            using IFlArchive battle = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "battle.fs"), FlArchiveRepresentation.Files);
            nativeCommonEffectTexture = new("a8def.tim", Read(battle, "a8def.tim").ToImmutableArray());
        }
        return nativeCommonEffectTexture;
    }
}
