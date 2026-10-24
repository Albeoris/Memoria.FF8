namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleModelCpuResources? enemyResourceViewCpu;
    private NativeBattleFileStaging? enemyResourceViewStaging;
    private readonly Dictionary<int, NativeBattleEnemyGeometryCache> enemyGeometryViews = [];
    private readonly Dictionary<NativeBattleGraphicsResource, Dictionary<int, NativeBattleGraphicsResource>> enemyResourceViews = new(ReferenceEqualityComparer.Instance);

    private NativeBattleGraphicsResource ResolveNativeEnemyResource(int row, NativeBattleGraphicsResource original)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native enemy resource view requires its retained CPU rows.");
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native enemy resource view requires its physical shared heap.");
        if (staging.Transaction is not null || !ReferenceEquals(cpu, enemyResourceViewCpu) || !ReferenceEquals(staging, enemyResourceViewStaging))
        {
            enemyResourceViews.Clear();
            enemyResourceViewCpu = cpu;
            enemyResourceViewStaging = staging;
        }
        if (!enemyResourceViews.TryGetValue(original, out Dictionary<int, NativeBattleGraphicsResource>? rows))
        {
            rows = [];
            enemyResourceViews.Add(original, rows);
        }
        if (!rows.TryGetValue(row, out NativeBattleGraphicsResource? resource))
        {
            if (!enemyGeometryViews.TryGetValue(row, out NativeBattleEnemyGeometryCache? geometry))
            {
                geometry = new();
                enemyGeometryViews.Add(row, geometry);
            }
            resource = NativeBattleEnemyResourceView.Resolve(cpu, staging, row, original, geometry);
            rows.Add(row, resource);
        }
        return resource;
    }
}
