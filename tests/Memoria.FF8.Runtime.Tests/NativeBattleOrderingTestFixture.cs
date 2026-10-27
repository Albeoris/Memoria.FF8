using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleOrderingTestFixture
{
    internal static NativeBattleLogicalOrderingData Inspect(NativeBattleLogicalOrdering source)
    {
        var table = source.Table;
        var proxies = source.Proxies;
        var packets = source.Packets;
        var other = source.OtherTable;
        return new(table.IsDefault ? default : table.ToImmutableArray(), proxies.IsDefault ? default : proxies.ToImmutableArray(), packets.IsDefault ? default : packets.ToImmutableArray(), source.ProxyCursor, source.TableBuffer, other is null ? null : other.Value.IsDefault ? default(System.Collections.Immutable.ImmutableArray<NativeBattlePrimitiveLink>) : other.Value.ToImmutableArray());
    }

    internal static NativeBattleLogicalOrdering Frozen(NativeBattleLogicalOrdering source, Func<NativeBattleLogicalOrderingData, NativeBattleLogicalOrderingData> edit)
    {
        NativeBattleLogicalOrdering frozen = source.IsSnapshot ? source : NativeBattleStorageSnapshots.Capture(source);
        return NativeBattleLogicalOrdering.FromSnapshot(edit(frozen.RequireSnapshot()));
    }

    internal static NativeBattleLogicalOrdering Working(NativeBattleLogicalOrdering source, Func<NativeBattleLogicalOrderingData, NativeBattleLogicalOrderingData> edit) => NativeBattleStorageSnapshots.Restore(Frozen(source, edit));
}
