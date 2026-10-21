using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematic77TerminalContactTests
{
    private const uint HeaderAddress = 0x1D280C4;
    private const uint ContactAddress = 0x1D28344;

    [Theory]
    [InlineData(3, 1)]
    [InlineData(0, 0)]
    public void Terminal_self_selector_uses_retained_header_count_after_global_count_clear_and_preserves_suppressed_glyphs(byte recipient, int expectedCalls)
    {
        FixtureOwner owner = Owner(recipient);
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x0022).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, HeaderAddress);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x20DFC80);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [3]) };
        NativeBattleEffectsState beforeEffects = NativeBattleEffectsState.Clear();
        NativeBattleEffectsState effects = beforeEffects;
        List<uint> applied = [];
        List<(uint Address, int Length)> reads = [];
        NativeBattleWaterContextServices services = new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, address, length) =>
        {
            reads.Add((address, length));
            return owner.Read(address, length);
        }, (current, address) =>
        {
            Assert.Equal(ContactAddress, address);
            applied.Add(address);
            NativeBattleContactHalf half = owner.Packets.ReadContact(0).Primary;
            Assert.Equal((byte)3, half.ActorIndex);
            Assert.Equal((byte)0, half.Sequence);
            Assert.Equal((ushort)1, half.ContactFlags);
            effects = effects.PrependDamageText(half.ActorIndex, half.ContactFlags, half.Amount6);
            return current;
        }, _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, services);
        Assert.Equal(expectedCalls, applied.Count);
        Assert.Equal(new[] { (HeaderAddress + 8, 4), (HeaderAddress + 16, 1), (ContactAddress, 1) }, reads);
        Assert.Equal((byte)0, owner.Packets.ContactCount);
        Assert.Equal((byte)1, owner.Packets.Headers[0].ReadByte(16));
        Assert.Equal(0x20DFC82u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(beforeEffects, effects);
        Assert.Null(effects.Head);
        Assert.Empty(effects.TextDraws);
        FixtureOwner restored = JsonSerializer.Deserialize<FixtureOwner>(JsonSerializer.Serialize(owner))!;
        Assert.Equal(owner.Read(HeaderAddress + 8, 4), restored.Read(HeaderAddress + 8, 4));
        Assert.Equal(owner.Read(HeaderAddress + 16, 1), restored.Read(HeaderAddress + 16, 1));
        Assert.Equal((byte)0, restored.Packets.ContactCount);
    }

    private static FixtureOwner Owner(byte recipient)
    {
        byte[] payload = new byte[20];
        payload[0] = 3;
        payload[1] = 8;
        payload[2] = 18;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), 77);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), ContactAddress);
        payload[16] = 1;
        NativeBattleContactPacket contact = new(new(recipient, 0, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0));
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        packets = packets with { ActionCount = 1, ContactCount = 0, Headers = packets.Headers.SetItem(0, new(payload.ToImmutableArray(), 0, 0, null)), Contacts = packets.Contacts.SetItem(0, contact), ActiveHeaderIndex = 0 };
        return new(packets);
    }

    public sealed record FixtureOwner(NativeBattleActionPacketsState Packets)
    {
        public byte[] Read(uint address, int length)
        {
            if (length >= 0 && address >= HeaderAddress && (ulong)address + (uint)length <= HeaderAddress + 20)
            {
                int offset = checked((int)(address - HeaderAddress));
                return Enumerable.Range(offset, length).Select(Packets.Headers[0].ReadByte).ToArray();
            }
            if (length >= 0 && address >= ContactAddress && (ulong)address + (uint)length <= ContactAddress + 24)
            {
                return Packets.ReadContact(0).Encode().AsSpan(checked((int)(address - ContactAddress)), length).ToArray();
            }
            throw new InvalidDataException("Terminal contact fixture read leaves its typed header and contact owners.");
        }
    }
}
