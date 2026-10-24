using static Memoria.FF8.Runtime.NativeBattleEffect345Body;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect345BodyTests
{
    [Fact]
    public void Appearance_early_quad_test_reads_retained_unmasked_DWORD_instead_of_current_corner()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0x44, BitConverter.GetBytes(0x10000113u));
        List<uint> reads = [];
        bool admitted = AdmitEarlyCorners7067EE(temporary, new uint[] { 273, 272, 274 }, true, index =>
        {
            reads.Add(index);
            return index == 0x10000113u ? (short)1 : (short)-1;
        });
        Assert.False(admitted);
        Assert.Equal(new uint[] { 273, 272, 274, 0x10000113 }, reads);
        Assert.Equal(0x10000113u, BitConverter.ToUInt32(temporary.Read(0x44, 4)));
    }

    [Fact]
    public void Appearance_rejected_leading_corner_does_not_require_unknown_old_fourth_index()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0x44, 4);
        List<uint> reads = [];
        Assert.False(AdmitEarlyCorners7067EE(temporary, new uint[] { 1, 2, 3 }, true, index =>
        {
            reads.Add(index);
            return index == 2 ? (short)1 : (short)0;
        }));
        Assert.Equal(new uint[] { 1, 2 }, reads);
        Assert.Throws<NotSupportedException>(() => AdmitEarlyCorners7067EE(temporary, new uint[] { 1, 2, 3 }, true, _ => 0));
        Assert.True(AdmitEarlyCorners7067EE(temporary, new uint[] { 1, 2, 3 }, false, _ => 0));
    }

    [Fact]
    public void Shadow_quarter_depth_overlays_low_WORD_and_preserves_retained_high_WORD()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0x44, BitConverter.GetBytes(0x12340001u));
        temporary = temporary.Write(0x44, BitConverter.GetBytes((ushort)237));
        List<uint> reads = [];
        Assert.True(AdmitEarlyCorners7067EE(temporary, new uint[] { 0, 1, 2 }, true, index =>
        {
            reads.Add(index);
            return -1;
        }));
        Assert.Equal(0x123400EDu, reads[^1]);
    }

    [Fact]
    public void Released_shadow_temporary_and_cursor_reset_retain_the_actual_first_quad_input()
    {
        uint origin = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Cursor, BitConverter.GetBytes(origin)).Write(origin + 0x44, BitConverter.GetBytes(0x12340001u));
        NativeBattleTemporaryAllocation shadow = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x78);
        NativeBattleEffectArena shadowView = NativeBattleTemporaryMemory.ReadView(shadow.Staging, shadow.Address, 0x78).Write(0x44, BitConverter.GetBytes((ushort)237));
        staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.Address, shadowView, 0x78);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
        NativeBattleTemporaryAllocation body = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x5C);
        Assert.Equal(shadow.Address, body.Address);
        Assert.Equal(0x123400EDu, BitConverter.ToUInt32(NativeBattleTemporaryMemory.ReadView(body.Staging, body.Address, 0x5C).Read(0x44, 4)));
        staging = NativeBattleTemporaryMemory.Reset5082F0(body.Staging);
        Assert.Equal(0x123400EDu, BitConverter.ToUInt32(staging.Read(origin + 0x44, 4)));
    }

    [Theory]
    [InlineData(0, 1, 0x00FF8040u, 0x00FF8040u, 0)]
    [InlineData(4096, 1, 0x00FF8040u, 0x02000000u, 0xA5)]
    [InlineData(2048, 2, 0x00FF8040u, 0x02804020u, 0xA5)]
    [InlineData(-2048, 2, 0x000A141Eu, 0x020F1E2Du, 0xA5)]
    public void Material_uses_signed_fixed_point_and_retains_unwritten_alpha(short fade, short mode, uint source, uint expected, byte alpha)
    {
        NativeBattleEffectArena model = NativeBattleEffectArena.Clear().Write(7, [0xA5]);
        NativeBattleEffectArena result = Material70C5B0(model, fade, mode, source);
        Assert.Equal(expected, BitConverter.ToUInt32(result.Read(0x28, 4)));
        Assert.Equal(alpha, result.Read(7, 1)[0]);
    }

    [Fact]
    public void Material_mode_three_updates_alpha_with_BYTE_subtraction_before_shift()
    {
        NativeBattleEffectArena model = NativeBattleEffectArena.Clear().Write(7, [0xA5]);
        NativeBattleEffectArena result = Material70C5B0(model, 2048, 3, 0x00FF8040);
        Assert.Equal(32, result.Read(7, 1)[0]);
        Assert.Equal(0x02804020u, BitConverter.ToUInt32(result.Read(0x28, 4)));
    }
}
