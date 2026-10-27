using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePreparedPresentationTests
{
    [Fact]
    public void Prepared_commands_do_not_retain_pending_packet_authority()
    {
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 20), 1, 0x68, [0x68010203, 0x00020001], 0, [], null);
        NativeBattlePrimitiveFrame source = NativeBattlePrimitiveFrame.Clear() with { LastCommands = [new(command, new(0), 1)] };
        NativeBattlePreparedPrimitiveFrame prepared = NativeBattlePreparedPrimitiveFrame.From(source);
        string before = JsonSerializer.Serialize(prepared);
        _ = source.Pending.Prepend(20, new("off_B6D070", 0, 20), [0x01000000, 0x68040506], 1, []);
        source = source with { LastCommands = [new(command with { Words = [0x68070809, 0x00040003] }, new(0), 1)] };
        Assert.Equal(before, JsonSerializer.Serialize(prepared));
        Assert.NotEqual(source.LastCommands, prepared.LastCommands);
        Assert.Null(typeof(NativeBattlePreparedPrimitiveFrame).GetProperty("Pending"));
    }

    [Fact]
    public void Prepared_scan_services_keep_prior_banks_and_knownness_without_live_battle_callbacks()
    {
        NativeBattleScanProcess process = NativeBattleScanProcess.FromColdProcess();
        NativeBattleScanByteOwner name = new(new byte[24].ToImmutableArray(), Enumerable.Repeat(true, 24).ToImmutableArray());
        NativeBattleScanByteOwner extra = new(new byte[71].ToImmutableArray(), Enumerable.Repeat(true, 71).ToImmutableArray());
        NativeBattlePreparedScan prepared = new(process.Text.ReadFrame(0), process.Results, name.WriteByte(0, 65), ImmutableDictionary<int, NativeBattlePreparedScanExtra>.Empty.Add(3, new(extra.WriteByte(0, 66), NativeBattlePreparedScanExtraFailure.None)), ImmutableDictionary<byte, ImmutableArray<byte>>.Empty.Add(2, [67, 0]), null, new([0xA5], [false]), null);
        NativeBattleScanTextExpansionServices oldServices = prepared.CreateExpansionServices();
        NativeBattlePreparedScan next = prepared with { Name = name.WriteByte(0, 68), EnemyExtra = prepared.EnemyExtra.SetItem(3, new(extra.WriteByte(0, 69), NativeBattlePreparedScanExtraFailure.None)), Names = prepared.Names.SetItem(2, [70, 0]) };
        NativeBattleScanTextExpansionServices newServices = next.CreateExpansionServices();
        Assert.Equal(65, oldServices.ReadOriginalByte(0x269AA78));
        Assert.Equal(68, newServices.ReadOriginalByte(0x269AA78));
        Assert.Equal(66, oldServices.ReadOriginalByte(NativeBattleScanResults.EnemyExtraAddress));
        Assert.Equal(69, newServices.ReadOriginalByte(NativeBattleScanResults.EnemyExtraAddress));
        Assert.Equal(new byte[] { 67, 0 }, oldServices.ReadNameBytes!(2).ToArray());
        Assert.Equal(new byte[] { 70, 0 }, newServices.ReadNameBytes!(2).ToArray());
        Assert.Throws<NotSupportedException>(() => oldServices.ReadOriginalByte(NativeBattleEffect68Profile.ResultEmptyAddress));
        Assert.Throws<NotSupportedException>(() => oldServices.ReadNameBytes!(3));
    }
}
