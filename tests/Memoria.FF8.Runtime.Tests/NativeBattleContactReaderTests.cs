using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactReaderTests
{
    private const uint RinoaCursor = 0x2556620;

    [Theory]
    [InlineData(false, 47)]
    [InlineData(true, 48)]
    public void RinoaConsumesStoredOccurrencesAcrossTheOriginalBoundary(bool expanded, int first)
    {
        BattleModelOwner final = expanded ? new BattleEnemyActorModel(new("extra")) : OriginalOwner(6);
        NativeBattleActionPacketsState packets = expanded ? ExpandedPackets(final) : OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 8, 0, -1, 0, 89, null));
        packets = packets.AppendContacts(Enumerable.Repeat(Packet(OriginalOwner(0), 0), first));
        (packets, _) = packets.Publish(first);
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(OriginalOwner(0), 8, 0, -1, 0, 89, null));
        packets = packets.AppendContacts([Packet(OriginalOwner(0), 0x1234), Packet(final, 0x5678)]);
        // The original Rinoa header byte encodes the occurrence count in bits 3 through 7.
        (packets, _) = packets.Publish(16);
        var parameters = new NativeBattleEffectParameters(OriginalOwner(0), 0, BattleModelTargets.From([OriginalOwner(0), final]), header, 0, 0);
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), parameters, null);
        BattleContactCursor cursor = new(first);
        NativeBattleFileStaging staging = NativeBattleRinoa89Contacts.WriteCursor(NativeBattleFileStaging.Unknown(), cursor);
        NativeBattleRinoa89Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, staging, new(1));
        List<BattleContactOccurrence> applied = [];

        NativeBattleRinoa89Frame advanced = NativeBattleRinoa89Contacts.Apply6FC410(frame, packets, (current, occurrence) =>
        {
            applied.Add(occurrence);
            return current;
        });

        Assert.Equal(new[] { new BattleContactOccurrence(first), new BattleContactOccurrence(first + 1) }, applied);
        Assert.Equal(final, packets.ReadContact(applied[1]).Primary.Recipient);
        Assert.Equal(cursor.Advance(2), advanced.Staging.ReadExecutionReference<BattleExecutionReference.Cursor>(RinoaCursor, 4).Reference);
        if (expanded)
        {
            Assert.Throws<NotSupportedException>(() => advanced.Staging.Read(RinoaCursor, 4));
        }
        else
        {
            Assert.Equal(NativeBattleContactAddress.EncodeOriginalCursor(cursor.Advance(2)), BinaryPrimitives.ReadUInt32LittleEndian(advanced.Staging.Read(RinoaCursor, 4)));
        }
    }

    [Fact]
    public void Effect312RawReaderUsesTheActualPacketOwnerAndRejectsRowCrossing()
    {
        BattleModelOwner extra = new BattleEnemyActorModel(new("extra"));
        NativeBattleActionPacketsState packets = ExpandedPackets(extra);
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 8, 0, -1, 0, 312, null));
        packets = packets.AppendContacts(Enumerable.Repeat(Packet(OriginalOwner(0), 0), 48).Concat([Packet(OriginalOwner(6), 0x1234), Packet(extra, 0x5678), Packet(extra, 0x9ABC)]));
        (packets, _) = packets.Publish(51);
        BattleSession battle = new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
        const BindingFlags instancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BattleSession).GetField("nativeActionPackets", instancePrivate)!.SetValue(battle, packets);
        MethodInfo reader = typeof(BattleSession).GetMethod("ReadNativeEffect312Physical", instancePrivate)!;
        NativeBattleGraphicsState graphics = OriginalGraphics([]);
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, 1, 1), null, null);
        NativeBattleEffect312Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect312State.FromColdProcess())
        {
            Staging = NativeBattleFileStaging.Unknown().Write(0x1D287DC, new byte[] { 0xA1, 0xB2 })
        };

        NativeBattleEffectArena Read(uint address, int length) => (NativeBattleEffectArena)reader.Invoke(battle, [graphics, frame, address, length])!;

        Assert.Equal(new byte[] { 0x34, 0x12 }, Read(0x1D287CA, 2).Read(0, 2));
        Assert.Equal(extra, packets.ReadContact(new BattleContactOccurrence(49)).Primary.Recipient);
        Assert.Throws<NotSupportedException>(() => Read(0xF2000000, 2).Read(0, 2));
        Assert.Equal(new byte[] { 0xA1, 0xB2 }, Read(0x1D287DC, 2).Read(0, 2));
        TargetInvocationException crossing = Assert.Throws<TargetInvocationException>(() => Read(0x1D287DB, 2));
        InvalidDataException inner = Assert.IsType<InvalidDataException>(crossing.InnerException);
        Assert.Contains("crosses a current contact row", inner.Message);
    }

    private static NativeBattleActionPacketsState ExpandedPackets(BattleModelOwner extra)
    {
        BattleModelOwner[] originals = Enumerable.Range(0, 7).Select(OriginalOwner).ToArray();
        BattleModelRoster roster = new([.. originals, extra]);
        NativeBattleModelAliases aliases = new(roster, originals.Select((owner, slot) => (NativeBattleModelSlot.Decode((byte)slot), owner)));
        return NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
    }

    private static NativeBattleContactPacket Packet(BattleModelOwner owner, ushort amount) => new(new(owner, 0, 0, 0, 0, amount, 0), null);
}
