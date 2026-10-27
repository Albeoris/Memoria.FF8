using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterOrderingRangeTests
{
    [Fact]
    public void Selected_clear_replaces_only_its_roots_and_terminates_at_the_first_cleared_cell()
    {
        NativeBattleLogicalOrdering initial = NativeBattleLogicalOrdering.Clear(1);
        initial = NativeBattleOrderingTestFixture.Working(initial, data => data with { Table = data.Table.Select((_, index) => new NativeBattlePrimitiveLink(true, index + 50)).ToImmutableArray(), ProxyCursor = 7 });
        NativeBattleLogicalOrdering retained = NativeBattleStorageSnapshots.Capture(initial);
        NativeBattleLogicalOrdering cleared = initial.ClearRange45D550(17, 4096);
        Assert.Equal(new(false, -1), cleared.Table[17]);
        Assert.Equal(new(false, 17), cleared.Table[18]);
        Assert.Equal(new(false, 4111), cleared.Table[4112]);
        Assert.Equal(retained.Table[16], cleared.Table[16]);
        Assert.Equal(retained.Table[4113], cleared.Table[4113]);
        Assert.Equal(retained.OtherTable!.Value.ToArray(), cleared.OtherTable!.Value.ToArray());
        Assert.Equal(retained.ProxyCursor, cleared.ProxyCursor);
        Assert.Equal(retained.TableBuffer, cleared.TableBuffer);
        Assert.Equal(retained.Packets.ToArray(), cleared.Packets.ToArray());
        Assert.Equal(retained.Proxies.ToArray(), cleared.Proxies.ToArray());
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cleared)), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(cleared)))));
    }
}
