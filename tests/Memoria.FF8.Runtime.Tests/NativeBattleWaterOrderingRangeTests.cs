using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterOrderingRangeTests
{
    [Fact]
    public void Selected_clear_replaces_only_its_roots_and_terminates_at_the_first_cleared_cell()
    {
        NativeBattleLogicalOrdering initial = NativeBattleLogicalOrdering.Clear(1);
        initial = initial with { Table = initial.Table.Select((_, index) => new NativeBattlePrimitiveLink(true, index + 50)).ToImmutableArray(), ProxyCursor = 7 };
        NativeBattleLogicalOrdering cleared = initial.ClearRange45D550(17, 4096);
        Assert.Equal(new(false, -1), cleared.Table[17]);
        Assert.Equal(new(false, 17), cleared.Table[18]);
        Assert.Equal(new(false, 4111), cleared.Table[4112]);
        Assert.Equal(initial.Table[16], cleared.Table[16]);
        Assert.Equal(initial.Table[4113], cleared.Table[4113]);
        Assert.Equal(initial.OtherTable, cleared.OtherTable);
        Assert.Equal(initial.ProxyCursor, cleared.ProxyCursor);
        Assert.Equal(initial.TableBuffer, cleared.TableBuffer);
        Assert.Equal(initial.Packets, cleared.Packets);
        Assert.Equal(initial.Proxies, cleared.Proxies);
        Assert.Equal(JsonSerializer.Serialize(cleared), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(cleared))));
    }
}
