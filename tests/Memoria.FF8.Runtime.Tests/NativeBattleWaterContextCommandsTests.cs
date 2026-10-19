using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterContextCommandsTests
{
    [Fact]
    public void NullRingOnlyClearsBackupCountAndPreservesOtherContextAndCachedRows()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        frame = frame with { Core = frame.Core.Write(Header + 0x174, new byte[] { 6 }).Write(Header + 0x17A, new byte[] { 3 }).Write(Header + 0x48, new byte[] { 5 }).Write(Header + 0x41, new byte[] { 9 }) };
        byte[] slot = frame.Arena.Read(0x2000, 0x100);
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ReadLinkedModelPointer = (_, model) =>
        {
            Assert.Equal(Model(4), model);
            return 0;
        } });
        Assert.Equal((byte)0, result.Core.Read(Header + 0x17A, 1)[0]);
        Assert.Equal((byte)6, result.Core.Read(Header + 0x174, 1)[0]);
        Assert.Equal((byte)5, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)9, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(slot, result.Arena.Read(0x2000, 0x100));
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void RingResolverReceivesActualModelAfterBackupsAndPublishesPhysicalMinimumIndex()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        byte[] before = frame.Arena.Read(0x2000, 0x100);
        NativeBattleWaterContextServices services = Services() with
        {
            ReadLinkedModelPointer = (_, model) =>
            {
                Assert.Equal(Model(4), model);
                return Model(2);
            },
            ResolveRing501FF0 = (value, model) =>
            {
                Assert.Equal(Model(4), model);
                Assert.Equal((byte)4, value.Core.Read(Header + 0x174, 1)[0]);
                Assert.Equal((byte)1, value.Core.Read(Header + 0x17A, 1)[0]);
                return Model(1);
            }
        };
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal((byte)1, result.Arena.Read(0x201B, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x41, 1)[0]);
        before[0x1B] = 1;
        Assert.Equal(before, result.Arena.Read(0x2000, 0x100));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Execute(restored, services)));
    }

    [Fact]
    public void BrokenRingReturnOriginalIsRetainedAndInvalidResolverResultFails()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xCF);
        NativeBattleWaterContextServices services = Services() with { ReadLinkedModelPointer = (_, _) => Model(1), ResolveRing501FF0 = (_, model) => model };
        Assert.Equal((byte)4, Execute(frame, services).Arena.Read(0x201B, 1)[0]);
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ResolveRing501FF0 = (_, _) => Model(2) + 1 }));
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ResolveRing501FF0 = (_, _) => Model(7) }));
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ResolveRing501FF0 = (_, _) => throw new InvalidDataException("Actual ring does not return to its origin.") }));
        frame = WaterSchedulerFixture.CoreDword(frame, Header + 0x70, 0);
        Assert.Throws<InvalidDataException>(() => Execute(frame, services));
    }

    [Fact]
    public void ContactWithoutBackupsChoosesFirstDuplicateAndReloadsPcAfterActualCallback()
    {
        NativeBattleWaterExecutionFrame frame = Contacts(3, 0, 4, 4, 2);
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ApplyContact506690 = (value, contact) =>
        {
            calls.Add(contact);
            return WaterSchedulerFixture.CoreDword(value, 0x2797450, 0xFFFFFFFE);
        } });
        Assert.Equal(new[] { Address(0x6000) }, calls);
        Assert.Equal(0u, Pc(result));
    }

    [Fact]
    public void ContactBackupsWalkInReverseReadingCurrentBytesButKeepCapturedContactBaseAndCount()
    {
        NativeBattleWaterExecutionFrame frame = Contacts(2, 2, 2, 4);
        frame = frame with { Core = frame.Core.Write(Header + 0x174, new byte[] { 4, 2 }) };
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ApplyContact506690 = (value, contact) =>
        {
            calls.Add(contact);
            if (calls.Count == 1)
            {
                return value with { Core = value.Core.Write(Header + 0x174, new byte[] { 3 }), Arena = value.Arena.Write(0x5010, new byte[] { 0 }).Write(0x6018, new byte[] { 3 }) };
            }
            return value;
        } });
        Assert.Equal(new[] { Address(0x6000), Address(0x6018) }, calls);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0xFF, 0)]
    [InlineData(1, 0xFF)]
    public void SignedCountsAvoidUnconsumedContactsOrNegativeBackupWalk(byte contactCount, byte savedCount)
    {
        NativeBattleWaterExecutionFrame frame = Contacts(contactCount, savedCount);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x6000, 0x100) };
        if (savedCount == 0xFF)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x201B, 1) };
        }
        Assert.Equal(0x1002u, Pc(Execute(frame)));
    }

    [Fact]
    public void MissingContactIsNoCallAndExternalActionBytesAreExplicitAndExact()
    {
        NativeBattleWaterExecutionFrame frame = Contacts(1, 0, 2);
        Assert.Equal(0x1002u, Pc(Execute(frame)));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, 0x30000000);
        List<uint> reads = [];
        NativeBattleWaterContextServices services = Services() with { ReadActionBytes = (_, address, length) =>
        {
            reads.Add(address);
            return address switch
            {
                0x30000008 => BitConverter.GetBytes(0x30000100u),
                0x30000010 => new byte[] { 1 },
                0x30000100 => new byte[] { 2 },
                _ => throw new Xunit.Sdk.XunitException("Unexpected actual packet read.")
            };
        } };
        Execute(frame, services);
        Assert.Equal(new uint[] { 0x30000008, 0x30000010, 0x30000100 }, reads);
        Assert.Throws<InvalidDataException>(() => Execute(frame, services with { ReadActionBytes = (_, _, _) => [] }));
    }

    [Fact]
    public void CameraInvokesCurrentOwnerWithoutReadingUnusedOperandAndReloadsPc()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1039);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ApplyCamera50A730 = value =>
        {
            calls++;
            return WaterSchedulerFixture.CoreDword(value, 0x2797450, 0xFFFFFFFE);
        } });
        Assert.Equal(1, calls);
        Assert.Equal(2u, Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1002, 2));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(128, 2048)]
    [InlineData(32767, 4096)]
    public void StageColorClampsSignedChannelsAndDepthAndWritesEveryPhysicalGroup(short rawDepth, short expectedDepth)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-1));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 128);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 300);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, unchecked((ushort)rawDepth));
        List<int> groups = [];
        NativeBattleWaterContextServices services = Services() with { WriteStageColor = (value, index, depth, packed) =>
        {
            groups.Add(index);
            Assert.Equal(expectedDepth, depth);
            Assert.Equal(0x00FF8000u, packed);
            Assert.Equal(0u, value.ReadTemporaryDword(0xF0));
            Assert.Equal(128u, value.ReadTemporaryDword(0xF4));
            Assert.Equal(255u, value.ReadTemporaryDword(0xF8));
            Assert.Equal(packed, value.ReadTemporaryDword(0xFC));
            return value;
        } };
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal(new[] { 0, 1, 2, 3 }, groups);
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(frame.Arena.Read(0x2000, 0x100), result.Arena.Read(0x2000, 0x100));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Execute(restored, services)));
    }

    [Theory]
    [InlineData(0x0022)]
    [InlineData(0x0039)]
    [InlineData(0x1020)]
    public void UnselectedModesRemainUnsupported(ushort instruction)
    {
        Assert.Throws<NotSupportedException>(() => Execute(Fixture(instruction)));
    }

    [Theory]
    [InlineData(0x10CF)]
    [InlineData(0x1ECF)]
    public void Ring_mode1_copies_actual_actor_list_before_minimum_selection_and_preserves_original_order(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [3]).Write(Header + 0x48, [4, 2, 6]), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        NativeBattleWaterContextServices services = Services() with
        {
            ReadLinkedModelPointer = (_, _) => Model(2),
            ResolveRing501FF0 = (current, _) =>
            {
                Assert.Equal(new byte[] { 4, 2, 6 }, current.Core.Read(Header + 0x174, 3));
                Assert.Equal((byte)3, current.Core.Read(Header + 0x17A, 1)[0]);
                Assert.Equal((byte)4, current.Core.Read(Header + 0x48, 1)[0]);
                return Model(1);
            }
        };
        NativeBattleWaterExecutionFrame result = Execute(frame, services);
        Assert.Equal(new byte[] { 4, 2, 6 }, result.Core.Read(Header + 0x174, 3));
        Assert.Equal((byte)1, result.Core.Read(Header + 0x48, 1)[0]);
        Assert.Equal((byte)1, result.Core.Read(Header + 0x41, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
        string json = JsonSerializer.Serialize(result);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(json)!));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)255)]
    public void Ring_mode1_signed_nonpositive_count_does_not_read_actor_list(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x10CF);
        frame = frame with { Core = frame.Core.Write(Header + 0x41, [count]).MarkUnknown(Header + 0x48, 3) };
        NativeBattleWaterExecutionFrame result = Execute(frame, Services() with { ReadLinkedModelPointer = (_, _) => Model(2), ResolveRing501FF0 = (_, _) => Model(1) });
        Assert.Equal(count, result.Core.Read(Header + 0x17A, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void Other_ring_modes_are_literal_returns_without_actor_or_PC_reads()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x20CF);
        frame = frame with { Core = frame.Core.MarkUnknown(Header, 0x17C).MarkUnknown(0x2797450, 4), Arena = frame.Arena.MarkUnknown(0x201B, 1) };
        Assert.Same(frame, Execute(frame));
    }

    private const uint Header = NativeBattleScriptedEffectMemory.Address;
    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x201B, new byte[] { 4 }) };
        return WaterSchedulerFixture.CoreDword(frame, Header + 0x70, Model(4));
    }
    private static NativeBattleWaterExecutionFrame Contacts(byte count, byte saved, params byte[] actors)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, Address(0x5000));
        frame = frame with { Core = frame.Core.Write(Header + 0x17A, new byte[] { saved }), Arena = frame.Arena.Write(0x5010, new byte[] { count }) };
        frame = ArenaDword(frame, 0x5008, Address(0x6000));
        for (int i = 0; i < actors.Length; i++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x6000 + 0x18 * i, new byte[] { actors[i] }) };
        }
        return frame;
    }
    private static NativeBattleWaterContextServices Services() => new((_, _) => throw new Xunit.Sdk.XunitException("Unexpected linked-model read."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected ring resolver."), (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected external action packet read."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected actual contact callback."), _ => throw new Xunit.Sdk.XunitException("Unexpected camera callback."), (_, _, _, _) => throw new Xunit.Sdk.XunitException("Unexpected stage color callback."));
    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices? services = null) => NativeBattleWaterContextCommands.Execute(frame, new(0x1000, 0x100, 0x1000), services ?? Services());
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Model(int index) => 0x1D972C0u + (uint)(0x9C * index);
    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(offset, bytes) };
    }
}
