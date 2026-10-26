using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312ContactsTests
{
    [Fact]
    public void Header_padding_has_no_reader_and_first_matching_actor_uses_native24byte_contact_stride()
    {
        List<(uint Address, int Length)> reads = [];
        int actual = NativeBattleEffect312Contacts.Find(0x3000, 0, (address, length) =>
        {
            reads.Add((address, length));
            return address switch
            {
                0x3004 when length == 4 => Bytes(BitConverter.GetBytes(0x4000u)),
                0x4010 when length == 1 => Bytes([3]),
                0x4008 when length == 4 => Bytes(BitConverter.GetBytes(0x5000u)),
                0x5000 when length == 1 => Bytes([2]),
                0x5018 when length == 1 => Bytes([0]),
                _ => throw new NotSupportedException("The requested physical bytes have no writer.")
            };
        });
        Assert.Equal(1, actual);
        Assert.Equal(new (uint, int)[] { (0x3004, 4), (0x4010, 1), (0x4008, 4), (0x5000, 1), (0x5018, 1) }, reads);
    }

    [Fact]
    public void Zero_count_does_not_read_the_unproduced_contact_pointer()
    {
        int actual = NativeBattleEffect312Contacts.Find(0x3000, 0, (address, length) => address switch
        {
            0x3004 when length == 4 => Bytes(BitConverter.GetBytes(0x4000u)),
            0x4010 when length == 1 => Bytes([0]),
            _ => throw new NotSupportedException("The requested physical bytes have no writer.")
        });
        Assert.Equal(-1, actual);
    }

    [Fact]
    public void Missing_actor_returns_negative_one_after_the_declared_count()
    {
        int contactReads = 0;
        int actual = NativeBattleEffect312Contacts.Find(0x3000, 1, (address, length) =>
        {
            if (address == 0x3004 && length == 4)
            {
                return Bytes(BitConverter.GetBytes(0x4000u));
            }
            if (address == 0x4010 && length == 1)
            {
                return Bytes([2]);
            }
            if (address == 0x4008 && length == 4)
            {
                return Bytes(BitConverter.GetBytes(0x5000u));
            }
            Assert.Equal(1, length);
            Assert.Equal(0x5000u + 24u * (uint)contactReads, address);
            contactReads++;
            return Bytes([2]);
        });
        Assert.Equal(-1, actual);
        Assert.Equal(2, contactReads);
    }

    [Fact]
    public void Required_header_count_still_requires_its_physical_writer()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312Contacts.Find(0x3000, 0, (address, length) => address == 0x3004 && length == 4 ? Bytes(BitConverter.GetBytes(0x4000u)) : NativeBattleEffectArena.Clear().MarkUnknown(0, length)));
    }

    private static NativeBattleEffectArena Bytes(byte[] value) => NativeBattleEffectArena.Clear().Write(0, value);
}
