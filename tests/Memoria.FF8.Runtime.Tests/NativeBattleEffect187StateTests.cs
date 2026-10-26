namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187StateTests
{
    [Fact]
    public void ColdStatePreservesTheUninitializedWarpDestination()
    {
        NativeBattleEffect187State state = NativeBattleEffect187State.FromColdProcess();
        state.Validate();
        Assert.Equal(0u, state.ReadDword(0x24FE8E0));
        Assert.Equal(0u, state.ReadDword(NativeBattleEffect187Model.Address));
        Assert.Throws<NotSupportedException>(() => state.Read(0xFA94A8, 1));
        state = state.Write(0xFA94A8, new byte[0x4000]);
        Assert.Equal(new byte[0x4000], state.Read(0xFA94A8, 0x4000));
    }

    [Fact]
    public void HistoricalUnknownStateCannotInventModelOrGlobalBytes()
    {
        NativeBattleEffect187State state = NativeBattleEffect187State.FromUnknown();
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x24FE8E0));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(NativeBattleEffect187Model.Address));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(0xE41E5C));
    }

    [Fact]
    public void ActiveHierarchyOwnsEachDetachedBankExclusively()
    {
        NativeBattleEffect187State state = NativeBattleEffect187State.FromColdProcess();
        NativeBattleEffectArena retained = state.ReadBank(1).Write(31, [0xAB]).MarkUnknown(32, 1);
        state = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
        state = state.AttachBank(1, retained);
        Assert.Equal((byte)0xAB, state.ReadBank(1).Read(31, 1)[0]);
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(32, 1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, retained));
    }

    [Fact]
    public void MatrixPaddingKnownnessSurvivesMappedPhysicalStores()
    {
        NativeBattleEffect187State state = NativeBattleEffect187State.FromColdProcess();
        NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0x29, 32).ToArray()).MarkUnknown(18, 2);
        state = state.WriteView(0x24FE8A0, matrix, 32);
        Assert.Equal(Enumerable.Repeat((byte)0x29, 18).ToArray(), state.Read(0x24FE8A0, 18));
        Assert.Equal(Enumerable.Repeat((byte)0x29, 12).ToArray(), state.Read(0x24FE8A0 + 20, 12));
        Assert.Throws<NotSupportedException>(() => state.Read(0x24FE8A0, 32));
        Assert.Equal(0u, state.ReadDword(0x24FE8E0));
        state.Validate();
    }
}

