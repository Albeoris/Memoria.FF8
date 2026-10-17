using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlSecondWindowTests
{
    [Fact]
    public void SecondWorkspaceUsesActualResourceAddressAndEntire7FCByteChildSuffix()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleOriginalEffectState effect = Effect();
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x12A9664);
        Assert.Equal(0x12A9664u, source.Address);
        Assert.Equal(NativeBattleQuezacotlExecutableMemory.SecondaryLength, source.Data.Length);
        byte[] bytes = Enumerable.Repeat((byte)0xA5, NativeBattleQuezacotlWorkspace.SecondLength).ToArray();
        NativeBattleQuezacotlWorkspaceWrite write = NativeBattleQuezacotlWorkspace.Write(memory, effect, new(bytes.ToImmutableArray()));
        Assert.Equal(bytes, NativeBattleQuezacotlWorkspace.Read(write.Memory, NativeBattleQuezacotlWorkspace.SecondLength).Data.ToArray());
        Assert.Equal((byte)0xA5, write.Effect.Pools.Single().Slots[0].Payload[^1]);
    }

    [Fact]
    public void CapturedMatrixIsWrittenOnlyByActualPrimaryCaptureAndPersistsAcrossLoadsAndJson()
    {
        NativeBattleEffectMemory memory = Memory();
        Assert.Null(memory.QuezacotlCapturedMatrix1780);
        NativeBattleActionMatrix context = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1, 2, 3, 4096);
        byte[] bytes = new byte[44];
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        //The synthetic geometry has zero vertices/polygons, so color interpolation and projection are not exercised here.
        NativeBattleQuezacotlGeometryStep captured = NativeBattleQuezacotlGeometry.Emit6C53A0(memory, Effect(), node, new(context, 216, 160, 108), new(0, 0, 0, 0), context, true, 0);
        Assert.True(captured.CapturedPackets);
        Assert.Empty(captured.Batch.Attempts);
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(captured.Memory.QuezacotlCapturedMatrix1780);
        Assert.Equal((1, 2, 3), (matrix.X, matrix.Y, matrix.Z));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(captured.Memory))!;
        Assert.Equal(JsonSerializer.Serialize(matrix), JsonSerializer.Serialize(restored.BeginLoad(1).QuezacotlCapturedMatrix1780));
        NativeBattleQuezacotlGeometryStep generic = NativeBattleQuezacotlGeometry.Emit6C53A0(captured.Memory, Effect(), node, new(context, 216, 160, 108), new(0, 0, 0, 0), context with { X = 999 }, false, 0);
        Assert.False(generic.CapturedPackets);
        Assert.Equal(matrix, generic.Memory.QuezacotlCapturedMatrix1780);
    }

    private static NativeBattleEffectMemory Memory()
    {
        byte[] data = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        int source = NativeBattleQuezacotlExecutableMemory.PrimaryLength;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(source + 8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(source + 16), 12);
        return NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlExecutableMemory = new(data.ToImmutableArray()) };
    }

    private static NativeBattleOriginalEffectState Effect()
    {
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        return new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child] };
    }
}
