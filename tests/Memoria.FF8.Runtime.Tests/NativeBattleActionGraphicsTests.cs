using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionGraphicsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(0xFD, 0)]
    [InlineData(0xFF, 2)]
    public void Sequence_camera_flag_reads_only_bit_one_of_the_active_physical_header(int cameraChoice, int expected)
    {
        NativeBattleActionPacketsState packets = Prepare();
        (packets, _) = packets.Prepare(new(3, 1, 13, unchecked((sbyte)cameraChoice), 0, 0, null));
        (packets, _) = packets.Publish(0);
        packets = packets.ActivateHeader(1);
        Assert.Equal(expected, NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets));
        NativeBattleActionPacketsState restored = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(packets))!;
        Assert.Equal(expected, NativeBattleActionGraphics.ReadCameraFlag50A6E0(restored));
        Assert.Equal(2, NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets.ActivateHeader(0)));
    }

    [Fact]
    public void Sequence_camera_flag_requires_a_known_active_header_byte()
    {
        NativeBattleActionPacketsState packets = Prepare();
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets));
        packets = packets.ActivateHeader(0);
        NativeBattleActionHeaderSlot unknown = packets.Headers[0] with { UnknownByteMask = packets.Headers[0].UnknownByteMask | 8 };
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.ReadCameraFlag50A6E0(packets with { Headers = packets.Headers.SetItem(0, unknown) }));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0x50BD00)]
    [InlineData(0, 0, 0, 1, 0x50BD80)]
    [InlineData(0x26, 1, 0, 0, 0x50B2A0)]
    [InlineData(0xEC, 1, 0, 0, 0x50B830)]
    [InlineData(0xF5, 1, 0, 0, 0x50B830)]
    [InlineData(0xED, 1, 0, 0, 0x50BEE0)]
    [InlineData(0xEE, 1, 0, 0, 0x50BEE0)]
    [InlineData(0xF1, 1, 0, 0, 0x50BDC0)]
    [InlineData(0xF7, 1, 0, 0, 0x50B0C0)]
    [InlineData(0xFC, 65535, 0, 0, 0x50B190)]
    [InlineData(2, 65535, 0, 0, 0x50BC20)]
    [InlineData(2, 1, 0, 0, 0x50BB00)]
    [InlineData(2, 1, 187, 0, 0x50B190)]
    public void SelectorUsesOriginalCommandAndHeaderBranches(int command, int action, int effect, int count, int expected)
    {
        NativeBattleActionPacketsState packets = Prepare((byte)command, (ushort)action, (ushort)effect, 0, (byte)count);
        Assert.Equal((uint)expected, NativeBattleActionGraphics.SelectService(packets.Headers[0]).Callback);
    }

    [Theory]
    [InlineData(0x26, 15)]
    [InlineData(0xF4, 70)]
    [InlineData(0xFE, 15)]
    public void SpecialGuardianActionsOwnSequence11AndTheirGraphicsFlag(int command, int action)
    {
        NativeBattleActionServiceSelection selected = NativeBattleActionGraphics.SelectService(Prepare((byte)command, (ushort)action).Headers[0]);
        Assert.Equal(0x50A9A0u, selected.Callback);
        Assert.Equal(0x40000000u, selected.SetGraphicsFlags);
        Assert.Equal((byte?)11, selected.Sequence);
    }

    [Fact]
    public void NormalSequenceAndDarksideFlagFollowTheDefaultBranch()
    {
        NativeBattleActionServiceSelection selected = NativeBattleActionGraphics.SelectService(Prepare(0x1C, 8, 100, 7).Headers[0]);
        Assert.Equal(0x50A9A0u, selected.Callback);
        Assert.Equal(0x10000000u, selected.SetGraphicsFlags);
    }

    [Fact]
    public void RepeatedPhysicalHeadersNormalizeAllContactsAndPersistLogicalData()
    {
        NativeBattleActionDataReference data = new(0x47E970, 12);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 240, 0, -1, 0, 187, data));
        (packets, _) = packets.AppendContact(Contact(3, 0x30, 0x88));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(0, 240, 0, -1, 0, 187, null));
        (packets, _) = packets.AppendContact(Contact(4, 0, 0x48));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 2);
        NativeBattleActionGraphicsState state = NativeBattleActionGraphics.Normalize(packets, 0);
        Assert.Equal((ushort)0x18, state.TargetMask);
        Assert.Equal((ushort)6, state.CoverMask);
        Assert.Equal((byte?)1, state.CoverSource);
        Assert.Equal((byte?)3, state.FirstTarget);
        Assert.Equal(2, state.HitFlags);
        Assert.Equal(data, state.PendingData);
        Assert.Equal(state, JsonSerializer.Deserialize<NativeBattleActionGraphicsState>(JsonSerializer.Serialize(state)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleActionGraphicsState>("{}"));
    }

    [Fact]
    public void EmptyHeaderRetainsFirstTargetAndCoverSourceButClearsMasks()
    {
        NativeBattleActionPacketsState packets = Prepare();
        NativeBattleActionGraphicsState prior = new(0, 0, 0, 1, 4, 2, 5, 2, null, 0x50B190);
        NativeBattleActionGraphicsState next = NativeBattleActionGraphics.Normalize(packets, 0, prior);
        Assert.Equal((byte?)5, next.FirstTarget);
        Assert.Equal((byte?)2, next.CoverSource);
        Assert.Equal((ushort)0, next.TargetMask);
        Assert.Equal((ushort)0, next.CoverMask);
        Assert.Equal(0, next.HitFlags);
    }

    [Fact]
    public void UnknownRetainedRepeatByteCannotBeInventedForAiHeaders()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 240, 0, -1, 0, 187, null));
        (packets, _) = packets.Publish(0);
        Assert.Contains("byte17", Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.Normalize(packets, 0)).Message);
    }

    private static NativeBattleActionPacketsState Prepare(byte command = 240, ushort action = 0, ushort effect = 187, byte sequence = 0, byte count = 0)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, command, sequence, -1, action, effect, null));
        if (count != 0)
        {
            (packets, _) = packets.AppendContact(Contact(3, 0, 0));
        }
        (packets, _) = packets.Publish(count);
        return packets.WritePlayerRepeatMode(0, 1);
    }

    private static NativeBattleContactPacket Contact(byte actor, byte flags, byte hit) => new(new(actor, 0, flags, hit, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0));
}
