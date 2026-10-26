using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusCountdownProjectionTests
{
    [Fact]
    public void Projection_retains_GTE_quarter_depth_and_exact_temporary_write_widths()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleStatusCountdownProjectionResult result = NativeBattleStatusCountdownProjection.Project50A410(Memory(), staging, Camera(1000), new(3, 4, 5), 0x2000);
        Assert.Equal(1005u, result.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal((ushort)251, result.QuarterDepth);
        Assert.Equal(0u, result.Flags);
        Assert.Equal(staging.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        (byte[] bytes, bool[] known) = result.Staging.ReadSnapshot(NativeBattleTemporaryMemory.Address, 24);
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(bytes));
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal((short)5, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        Assert.False(known[6]);
        Assert.False(known[7]);
        Assert.False(known[14]);
        Assert.False(known[15]);
        Assert.Equal(unchecked((short)((short)(result.Screen >> 16) - 16)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(10)));
        Assert.Equal(result.QuarterDepth, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(result.Flags, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(result.Memory.ScriptedColorRegisters!.Ir0, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
    }

    [Fact]
    public void Clipped_projection_keeps_only_the_unconditional_first_line_shift()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleStatusCountdownProjectionResult result = NativeBattleStatusCountdownProjection.Project50A410(Memory(), staging, Camera(0), new(3, 4, 5), 0x82000);
        Assert.NotEqual(0u, result.Flags & 0x60000);
        Assert.Equal(unchecked((short)((short)(result.Screen >> 16) - 8)), BinaryPrimitives.ReadInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 10, 2)));
    }

    [Fact]
    public void Released_scratch_padding_preserves_actual_prior_bytes()
    {
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Range(0, 24).Select(value => (byte)value).ToArray());
        NativeBattleStatusCountdownProjectionResult result = NativeBattleStatusCountdownProjection.Project50A410(Memory(), staging, Camera(1000), new(3, 4, 5), 0);
        Assert.Equal(new byte[] { 6, 7 }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 6, 2));
        Assert.Equal(new byte[] { 14, 15 }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 14, 2));
    }

    private static NativeBattleEffectMemory Memory()
    {
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = new(NativeBattleEffectArena.Clear(), [], 0, 0) };
    }

    private static NativeBattleCameraState Camera(int translationZ)
    {
        return NativeBattleCameraState.FromColdProcess() with { Projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, translationZ, 4096), 100, 10, 20), CanonicalViewPadding = null };
    }
}
