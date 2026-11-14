using System.Buffers.Binary;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactReaderTests
{
    private const uint RinoaCursor = 0x2556620;

    [Theory]
    [InlineData(false, 47, 0x1D287ACu, 0x1D287C4u, 0x1D287DCu)]
    [InlineData(true, 48, 0x1D287C4u, 0xF2000000u, 0xF2000018u)]
    public void RinoaConsumesStoredContactsAndAdvancesItsCursor(bool hosted, int firstRow, uint firstAddress, uint secondAddress, uint endAddress)
    {
        NativeBattleActionPacketsState packets = HostedOrNativeBank(hosted, hosted ? 64 : 49);
        // The original Rinoa header byte encodes the occurrence count in bits 3 through 7.
        (packets, _) = packets.Prepare(new(0, 8, 0, -1, 0, 89, null));
        packets = packets.AppendContacts(Enumerable.Repeat(Packet(0, 0), firstRow));
        (packets, _) = packets.Publish(firstRow);
        (packets, _) = packets.Prepare(new(0, 8, 0, -1, 0, 89, null));
        packets = packets.AppendContacts([Packet(0, 0x1234), Packet(hosted ? (byte)24 : (byte)6, 0x5678)]);
        if (hosted)
        {
            packets = packets.AppendContacts(Enumerable.Repeat(Packet(0, 0), 14));
        }
        (packets, _) = packets.Publish(16);
        packets.Validate();
        NativeBattleEffectParameters parameters = new(0, 0, 1, 1, 0, 0);
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), parameters, null);
        NativeBattleRinoa89Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown().Write(RinoaCursor, BitConverter.GetBytes(firstAddress)), new(1));
        List<uint> applied = [];

        NativeBattleRinoa89Frame advanced = NativeBattleRinoa89Contacts.Apply6FC410(frame, packets, (current, address) =>
        {
            applied.Add(address);
            return current;
        });

        Assert.Equal(new[] { firstAddress, secondAddress }, applied);
        Assert.Equal(endAddress, BinaryPrimitives.ReadUInt32LittleEndian(advanced.Staging.Read(RinoaCursor, 4)));
    }

    [Fact]
    public void Effect312RawReaderUsesTheActualPacketOwnerAndRejectsRowCrossing()
    {
        NativeBattleActionPacketsState packets = HostedOrNativeBank(true, 51);
        (packets, _) = packets.Prepare(new(0, 8, 0, -1, 0, 312, null));
        packets = packets.AppendContacts(Enumerable.Repeat(Packet(0, 0), 48).Concat([Packet(6, 0x1234), Packet(24, 0x5678), Packet(24, 0x9ABC)]));
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
        NativeBattleGraphicsState graphics = new([.. Enumerable.Range(0, 25).Select(row => new NativeBattleGraphicsModel(row, 0))], [], 0, 0);
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, 1, 1), null, null);
        NativeBattleEffect312Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect312State.FromColdProcess())
        {
            Staging = NativeBattleFileStaging.Unknown().Write(0x1D287DC, new byte[] { 0xA1, 0xB2 })
        };

        NativeBattleEffectArena Read(uint address, int length) => (NativeBattleEffectArena)reader.Invoke(battle, [graphics, frame, address, length])!;

        Assert.Equal(new byte[] { 0x34, 0x12 }, Read(0x1D287CA, 2).Read(0, 2));
        Assert.Equal(new byte[] { 24, 0 }, Read(0xF2000000, 2).Read(0, 2));
        Assert.Equal(new byte[] { 0x78, 0x56 }, Read(0xF2000006, 2).Read(0, 2));
        Assert.Equal(new byte[] { 0xA1, 0xB2 }, Read(0x1D287DC, 2).Read(0, 2));
        TargetInvocationException crossing = Assert.Throws<TargetInvocationException>(() => Read(0xF2000017, 2));
        InvalidDataException inner = Assert.IsType<InvalidDataException>(crossing.InnerException);
        Assert.Contains("crosses a current contact row", inner.Message);
    }

    private static NativeBattleActionPacketsState HostedOrNativeBank(bool hosted, int capacity)
    {
        if (!hosted)
        {
            return NativeBattleActionPacketsState.Fresh();
        }
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new("party", true);
        rows[6] = new("enemy-6", false);
        rows[24] = new("enemy-24", false);
        return NativeBattleActionPacketsState.FromPresentationStorage(new([.. rows]), capacity);
    }

    private static NativeBattleContactPacket Packet(byte row, ushort amount) => new(new(row, 0, 0, 0, 0, amount, 0), new(255, 0, 0, 0, 0, 0, 0));
}
