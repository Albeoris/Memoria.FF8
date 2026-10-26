using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect326StateTests
{
    [Fact]
    public void ColdBssAndStaticUnknownsRemainDistinctFromUnknownProcessHistory()
    {
        NativeBattleEffect326State cold = NativeBattleEffect326State.FromColdProcess();
        cold.Validate();
        Assert.True(cold.ColdSourceOrigin);
        Assert.False(cold.StaticSourcesBound);
        Assert.Equal(0u, cold.ReadDword(0x24F0BD0));
        Assert.Throws<NotSupportedException>(() => cold.ReadDword(0xE19668));
        Assert.Throws<NotSupportedException>(() => cold.ReadDword(0xE196B0));
        NativeBattleEffect326State unknown = NativeBattleEffect326State.FromUnknown();
        unknown.Validate();
        Assert.False(unknown.ColdSourceOrigin);
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(0x24F0BD0));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(ModelBindings[0].Model));
    }

    [Fact]
    public void ThreeModelAndControlOwnersAreDisjointFromTheirPhysicalTaskBanks()
    {
        NativeBattleEffect326State state = NativeBattleEffect326State.FromColdProcess();
        for (int index = 0; index < ModelBindings.Length; index++)
        {
            state = state.WriteWord(ModelBindings[index].Model, checked((ushort)(index + 2))).WriteWord(ModelBindings[index].Control + 2, checked((ushort)(index + 15)));
        }
        for (int index = 0; index < ModelBindings.Length; index++)
        {
            Assert.Equal((ushort)(index + 2), state.ReadWord(ModelBindings[index].Model));
            Assert.Equal((ushort)(index + 15), state.ReadWord(ModelBindings[index].Control + 2));
        }
        Assert.Equal(new byte[32], state.ReadBank(0).Read(0, 32));
        state.Validate();
    }

    [Fact]
    public void AnActiveBankHasExactlyOneOwnerAndRetainsItsReleasedBytes()
    {
        NativeBattleEffect326State state = NativeBattleEffect326State.FromColdProcess();
        NativeBattleEffectArena bank = state.ReadBank(1).Write(14, [0xA5, 0x5A]);
        state = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
        state = state.AttachBank(1, bank);
        Assert.Equal(new byte[] { 0xA5, 0x5A }, state.ReadBank(1).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, bank));
        state.Validate();
    }
}
