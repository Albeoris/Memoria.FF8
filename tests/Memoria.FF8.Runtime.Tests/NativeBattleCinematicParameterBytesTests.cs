using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicParameterBytesTests
{
    [Fact]
    public void AddressedParametersPreserveTheRealHeaderPointerAndIndependentByteOwners()
    {
        NativeBattleEffectParameters parameters = new(OriginalOwner(5), 0xA1, BattleModelTargets.From([OriginalOwner(0), OriginalOwner(1), OriginalOwner(5)]), new BattleActionHeaderReference(31), 0x82, 0x84) { Parameter80 = 0x81, Parameter82 = 0x83 };
        parameters = parameters with { OriginalWindow = NativeBattleEffectParameterWindow.Capture(parameters, OriginalAliases) };
        byte[] bytes = NativeBattleCinematicParameterBytes.Read(parameters, 0, 12);
        Assert.Equal(new byte[] { 5, 0xA1, 0x23, 0 }, bytes.AsSpan(0, 4).ToArray());
        Assert.Equal(0x1D280C4u + 31u * 20, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
        Assert.Equal(new byte[] { 0x81, 0x82, 0x83, 0x84 }, bytes.AsSpan(8, 4).ToArray());
    }

    [Fact]
    public void ActorReadDoesNotRequireUnrelatedUnwrittenOptionalBytes()
    {
        NativeBattleEffectParameters parameters = new(OriginalOwner(6), 0, BattleModelTargets.From([OriginalOwner(0)]), new BattleActionHeaderReference(0), 0, 0);
        parameters = parameters with { OriginalWindow = NativeBattleEffectParameterWindow.Capture(parameters, OriginalAliases) };
        Assert.Equal(new byte[] { 6 }, NativeBattleCinematicParameterBytes.Read(parameters, 0, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, 8, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, 10, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, 11, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, -1, 1));
    }
}
