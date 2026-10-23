using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129RibbonsTests
{
    [Theory]
    [InlineData(false, 15)]
    [InlineData(true, 14)]
    public void Fibre_projection_retains_quarter_depth_SZ_lanes_and_actual_CRT_stack_direction_after_degenerate_first_segment(bool degenerate, int packetCount)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().WriteDword(0x252117C, NativeBattleEffectNodeMemory.ArenaAddress + 0x3B70);
        NativeBattleBreath129Frame frame = new(memory, new(129, 128, new(129, 0x6B4C00, 0x6B4C10), new(0, 0, 0, 0, 0, 0), 0), new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        (frame, uint address) = NativeBattleBreath129Controller.Allocate(frame, 256);
        NativeBattleEffectArena points = NativeBattleBreath129Controller.Context(frame, address, 256);
        for (int index = 0; index < 16; index++)
        {
            points = points.Write(index * 16, BitConverter.GetBytes((short)(degenerate && index < 2 ? 0 : index * 100))).Write(index * 16 + 2, BitConverter.GetBytes((short)0)).Write(index * 16 + 4, BitConverter.GetBytes((short)8000)).Write(index * 16 + 6, BitConverter.GetBytes((short)512)).Write(index * 16 + 14, BitConverter.GetBytes((short)128));
        }
        frame = NativeBattleBreath129Controller.PublishContext(frame, address, points, 256);
        List<NativeBattleBreath129AppendPacket> packets = [];
        NativeBattleBreath129Inputs inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(identity, null), _ => new(identity, 256, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
            return current;
        });
        byte[] distances = Enumerable.Repeat((byte)4, 16384).ToArray();
        distances[0] = 0;
        NativeBattleBreath129Resources resources = new(new("unused", []), new(0, []), new(0, []), new(0, []), new(0, []), new(0, 0, 0, 0, []), new("unused", []), [], [], [], [], distances.ToImmutableArray(), [], [], 0);
        NativeBattleBreath129Frame actual = NativeBattleBreath129Geometry.DrawFibre6B6CC0(frame, address, resources, inputs, new(address));
        Assert.Equal(packetCount, packets.Count);
        Assert.All(packets, packet =>
        {
            Assert.Equal(new[] { 8000, 8000, 8000, 8000 }, packet.VertexPayload4);
            Assert.Equal(267, packet.Bucket);
            Assert.Equal(52, packet.Allocation);
            Assert.Equal(0x3E, packet.Data[7]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[50]);
        });
        NativeBattleEffectArena result = NativeBattleBreath129Controller.Context(actual, address, 256);
        Assert.Equal((short)(degenerate ? -1 : 2000), BitConverter.ToInt16(result.Read(4, 2)));
        if (degenerate)
        {
            Assert.Equal((short)1494, BitConverter.ToInt16(result.Read(16, 2)));
            Assert.Equal((short)1066, BitConverter.ToInt16(result.Read(24, 2)));
            Assert.Equal((short)736, BitConverter.ToInt16(result.Read(18, 2)));
            Assert.Equal((short)992, BitConverter.ToInt16(result.Read(26, 2)));
        }
        Assert.Equal(state.ReadDword(0x252117C) + (uint)packetCount * 52, actual.State.ReadDword(0x252117C));
        Assert.Equal(frame.Random, actual.Random);
    }
}
