using System.Buffers.Binary;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedGuardianFormPositioningTests
{
    [Fact]
    public void Unaliased_form_positioning_keeps_scalar_header_fields_and_rejects_raw_identity_reads()
    {
        const uint header = 0x1D280C4;
        const uint parameters = 0x1D99A78;
        BattlePartyActorModel caster = new(new("added-form"));
        BattleModelRoster roster = new([caster]);
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, new(roster, []), _ => new(0));
        MethodInfo read = typeof(BattleSession).GetMethod("ReadGuardianFormPositioning", BindingFlags.Static | BindingFlags.NonPublic)!;
        uint firstContact = NativeBattleContactAddress.EncodeOriginal(new(0));

        Assert.Equal(firstContact, BinaryPrimitives.ReadUInt32LittleEndian(Read(header + 8, 4)));
        Assert.Equal(new byte[] { 1, 1 }, Read(header + 0x10, 2));
        Assert.Equal(header, BinaryPrimitives.ReadUInt32LittleEndian(Read(parameters + 4, 4)));
        foreach ((uint address, int length) in new[] { (header, 1), (parameters + 2, 2), (firstContact, 1) })
        {
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Read(address, length));
            Assert.IsType<NotSupportedException>(error.InnerException);
        }

        byte[] Read(uint address, int length) => Assert.IsType<byte[]>(read.Invoke(null, [graphics, caster, address, length]));
    }
}
