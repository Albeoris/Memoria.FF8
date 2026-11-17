using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleModelTextureCache RegisterNativeEnemyTextureMotion(NativeBattleModelTextureCacheLoad load, ImmutableArray<byte>? original)
    {
        if (load.Upload is null)
        {
            return load.Cache;
        }
        byte model = load.Cache.Rows[load.ResourceIndex].ModelId ?? throw new InvalidDataException("Native enemy texture heap has no loaded model identity.");
        return load.Cache with { EnemyTextureMotion = original is { } table ? load.Cache.EnemyTextureMotion.SetItem(model, table) : load.Cache.EnemyTextureMotion.Remove(model) };
    }
}
