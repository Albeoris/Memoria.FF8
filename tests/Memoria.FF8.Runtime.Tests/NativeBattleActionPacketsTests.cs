using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionPacketsTests
{
    private static NativeBattleActionHeaderPreparation Header(byte command = 1) => new(0, command, 7, -1, 101, 0, null);
    private static NativeBattleContactPacket Contact(ushort damage) => new(new(3, 4, 0, 0, 0, damage, 0), new(255, 0, 0, 0, 0, 0, 0));

    [Fact]
    public void HeaderContactWalkUsesTheExplicitPhysicalHeaderAfterCountsResetInsteadOfTheActiveSingleton()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, _) = state.Prepare(Header());
        (state, _) = state.AppendContact(Contact(17));
        (state, _) = state.Publish(1);
        (state, int second) = state.Prepare(Header(254));
        (state, _) = state.AppendContact(Contact(29));
        (state, _) = state.AppendContact(Contact(31));
        (state, _) = state.Publish(2);
        state = state.ActivateHeader(0).ResetCounts();
        Assert.Equal(0, state.ActiveHeaderIndex);
        Assert.Equal(new ushort[] { 29, 31 }, state.ReadHeaderContacts506BA0(second).Select(packet => packet.Primary.Amount6).ToArray());
        Assert.Equal((ushort)17, Assert.Single(state.ReadHeaderContacts506BA0(0)).Primary.Amount6);
        NativeBattleActionPacketsState restored = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state.ReadHeaderContacts506BA0(second).ToArray(), restored.ReadHeaderContacts506BA0(second).ToArray());
    }

    [Fact]
    public void EmptyPhysicalHeaderDoesNotReadItsUnobservedContactPointer()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        NativeBattleActionHeaderSlot header = state.Headers[3] with { UnknownByteMask = state.Headers[3].UnknownByteMask & ~(1u << 16) };
        state = state with { Headers = state.Headers.SetItem(3, header) };
        Assert.Null(header.FirstContactIndex);
        Assert.Empty(state.ReadHeaderContacts506BA0(3));
        Assert.Throws<NotSupportedException>(() => state.ReadHeaderContacts506BA0(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ReadHeaderContacts506BA0(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ReadHeaderContacts506BA0(32));
    }

    [Fact]
    public void NonemptyPhysicalHeaderRetainsUnknownPointerAndContactBoundaries()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        byte[] raw = state.Headers[0].Payload.ToArray();
        raw[16] = 1;
        NativeBattleActionHeaderSlot header = state.Headers[0] with { Payload = System.Collections.Immutable.ImmutableArray.CreateRange(raw), UnknownByteMask = state.Headers[0].UnknownByteMask & ~(1u << 16) };
        state = state with { Headers = state.Headers.SetItem(0, header) };
        Assert.Throws<NotSupportedException>(() => state.ReadHeaderContacts506BA0(0));
        state = state with { Headers = state.Headers.SetItem(0, header with { FirstContactIndex = 49 }) };
        Assert.Throws<InvalidDataException>(() => state.ReadHeaderContacts506BA0(0));
        state = state with { Headers = state.Headers.SetItem(0, header with { FirstContactIndex = 0 }) };
        Assert.Throws<NotSupportedException>(() => state.ReadHeaderContacts506BA0(0));
    }

    [Fact]
    public void PreparationAndContactWritesDoNotPublishTheActionCount()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, int header) = state.Prepare(Header());
        Assert.Equal(0, header);
        Assert.Equal(0, state.ActionCount);
        Assert.Equal(0, state.Headers[header].FirstContactIndex);
        Assert.Equal(255, state.Headers[header].ReadByte(3));
        Assert.Equal(101, state.Headers[header].ReadByte(4));
        Assert.Throws<NotSupportedException>(() => state.Headers[header].ReadByte(16));
        (state, int contact) = state.AppendContact(Contact(37));
        Assert.Equal(0, contact);
        Assert.Equal(0, state.ActionCount);
        Assert.Equal(1, state.ContactCount);
        (state, int published) = state.Publish(1);
        Assert.Equal(header, published);
        Assert.Equal(1, state.ActionCount);
        Assert.Equal(1, state.Headers[header].ReadByte(16));
        state.Validate();
    }

    [Fact]
    public void ResetAndSlotReusePreserveRepeatAndUnknownTrailingBytes()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, _) = state.Prepare(Header());
        (state, _) = state.AppendContact(Contact(37));
        (state, _) = state.Publish(1);
        state = state.WritePlayerRepeatMode(0, 3).ActivateHeader(0).ResetCounts();
        Assert.Equal(0, state.ActiveHeaderIndex);
        Assert.Equal(37, state.ReadContact(0).Primary.Amount6);
        Assert.Equal(2, state.Headers[0].ReadByte(17));
        (state, _) = state.Prepare(Header(2));
        Assert.Equal(1, state.Headers[0].ReadByte(16));
        Assert.Equal(2, state.Headers[0].ReadByte(17));
        Assert.Throws<NotSupportedException>(() => state.Headers[0].ReadByte(18));
        Assert.Throws<NotSupportedException>(() => state.Headers[0].ReadByte(19));
        state.Validate();
    }

    [Fact]
    public void PlayerRepeatWriterUsesTheFirstHeaderOfItsDispatchGroup()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        (state, int first) = state.Prepare(Header());
        (state, _) = state.Publish(0);
        (state, int second) = state.Prepare(Header(2));
        (state, _) = state.Publish(0);
        state = state.WritePlayerRepeatMode(first, 0);
        Assert.Equal(255, state.Headers[first].ReadByte(17));
        Assert.Throws<NotSupportedException>(() => state.Headers[second].ReadByte(17));
    }

    [Fact]
    public void LogicalDataAndRetainedActivePointerSurviveStandaloneJson()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        NativeBattleActionDataReference data = new(0x47E970, 12);
        (state, _) = state.Prepare(Header(2) with { Data = data });
        (state, _) = state.AppendContact(Contact(81));
        (state, _) = state.Publish(1);
        state = state.ActivateHeader(0).ResetCounts();
        NativeBattleActionPacketsState restored = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(data, restored.Headers[0].Data);
        Assert.Equal(0, restored.ActiveHeaderIndex);
        Assert.Equal(81, restored.ReadContact(0).Primary.Amount6);
        Assert.Throws<NotSupportedException>(() => restored.Headers[0].ReadByte(12));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleActionPacketsState>("{}"));
    }

    [Fact]
    public void SessionRestoreRequiresTheCanonicalPacketOwner()
    {
        BattleDefinition definition = new()
        {
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeActionPackets = null }));
    }

    [Fact]
    public void CountsCannotAuthorizeUnknownPayloadOrCrossTheAdjacentArena()
    {
        NativeBattleActionPacketsState state = NativeBattleActionPacketsState.Fresh();
        Assert.Throws<InvalidDataException>(() => (state with { ActionCount = 1 }).Validate());
        Assert.Throws<NotSupportedException>(() => (state with { ContactCount = 1 }).Validate());
        for (int index = 0; index < NativeBattleActionPacketsState.HeaderCapacity; index++)
        {
            (state, _) = state.Prepare(Header());
            (state, _) = state.Publish(0);
        }
        Assert.Throws<InvalidOperationException>(() => state.Prepare(Header()));
        state = state.ResetCounts();
        for (int index = 0; index < NativeBattleActionPacketsState.ContactCapacity; index++)
        {
            (state, _) = state.AppendContact(Contact(1));
        }
        Assert.Throws<InvalidOperationException>(() => state.AppendContact(Contact(1)));
    }
}
