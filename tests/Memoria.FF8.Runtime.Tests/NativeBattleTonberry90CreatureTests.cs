using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90CreatureTests
{
    [Fact]
    public void ConstructorPublishesOnlyActualSparseFieldsAndInitialStateSelectsRealPoseBeforeSound()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32123456);
        NativeBattleModulePoolTask task = Read(frame, fixture.Address);
        Assert.Equal(fixture.Address + 0xCC, task.ReadDword(0x94));
        Assert.Equal(0x32123456u, task.ReadDword(0x5C));
        Assert.Equal(0x00808080u, task.ReadDword(0x58));
        Assert.Equal((ushort)3, task.ReadWord(0xDE));
        Assert.Null(frame.Memory.Tonberry90!.CreatureModelState!.DecodedPose);
        uint director = frame.Memory.Tonberry90.DirectorAddress1547168!.Value;
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90.WriteDword(director, 0xFFFE1234).WriteDword(director + 4, 0xA55AFFFD) } };
        List<uint> sounds = [];
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException("Fade is not reached."), (current, descriptor, pan, volume) =>
        {
            Assert.NotNull(current.Memory.Tonberry90!.CreatureModelState!.DecodedPose);
            Assert.Equal(128, pan);
            Assert.Equal(127u, volume);
            sounds.Add(descriptor);
            return current;
        }, (_, _, _, _) => throw new InvalidOperationException("Initial creature is hidden."));
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        task = Read(step.Frame, fixture.Address);
        Assert.Equal(new uint[] { 0x154716C, 0x16D7DE8 }, sounds);
        Assert.Equal(0xFFFE1234u, task.ReadDword(0x4C));
        Assert.Equal(0xA55AFFFDu, task.ReadDword(0x50));
        Assert.Equal(fixture.Address + 0x114, task.ReadDword(0x60));
        Assert.Equal(4096u, task.ReadDword(0x118));
        Assert.Equal((byte)3, task.ReadByte(0x9C));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)4, (byte)(task.ReadByte(0x26) & 4));
    }

    [Fact]
    public void RecolorPreservesUnwrittenAlphaAndCodeExceptExactModeStoresAndUsesTruncation()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0, new byte[0x148 - 12].ToImmutableArray());
        task = task.WriteByte(0x37, 0xA5).WriteDword(0x58, 0x7F123456);
        NativeBattleModulePoolTask half = NativeBattleTonberry90Creature.Recolor7678D0(task, 2048, 1, 0x009783FF);
        Assert.Equal(0x02262140u, half.ReadDword(0x58));
        Assert.Equal((byte)0xA5, half.ReadByte(0x37));
        NativeBattleModulePoolTask unchanged = NativeBattleTonberry90Creature.Recolor7678D0(task, -1, 0, 0x009783FF);
        Assert.Equal(0x7F9783FFu, unchanged.ReadDword(0x58));
        NativeBattleModulePoolTask alpha = NativeBattleTonberry90Creature.Recolor7678D0(task, 4096, 3, 0x009783FF);
        Assert.Equal(0x02000000u, alpha.ReadDword(0x58));
        Assert.Equal((byte)0, alpha.ReadByte(0x37));
        NativeBattleModulePoolTask reset = NativeBattleTonberry90Creature.Recolor7678D0(task, 0, 3, 0xAB9783FF);
        Assert.Equal(0x009783FFu, reset.ReadDword(0x58));
        Assert.Equal((byte)0, reset.ReadByte(0x37));
    }

    [Fact]
    public void LiteralRetStillIntegratesSignedTruncatedVelocityAndWrapsClockBeforeCompletion()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Creature.Initialize8DD0F0(fixture.Frame, fixture.Address, Resource(), 3, 0x32808080);
        NativeBattleModulePoolTask task = Read(frame, fixture.Address).WriteByte(0x29, 13).WriteByte(0x26, 5).WriteWord(0x24, 0xFFFF).WriteWord(0x4E, 0xFFFF).WriteWord(0x124, unchecked((ushort)-31)).WriteWord(0x126, 31).WriteWord(0x128, unchecked((ushort)-15));
        frame = Write(frame, fixture.Address, task);
        NativeBattleTonberry90CreatureServices services = new(_ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleTonberry90TaskStep first = NativeBattleTonberry90Creature.Advance7674E0(frame, fixture.Address, Resource(), services);
        NativeBattleTonberry90TaskStep second = NativeBattleTonberry90Creature.Advance7674E0(JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(JsonSerializer.Serialize(frame))!, fixture.Address, Resource(), services);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.True(first.Complete);
        task = Read(first.Frame, fixture.Address);
        Assert.Equal((ushort)0xFFFF, task.ReadWord(0x4C));
        Assert.Equal((ushort)0, task.ReadWord(0x4E));
        Assert.Equal((ushort)0, task.ReadWord(0x50));
        Assert.Equal((ushort)0, task.ReadWord(0x24));
    }

    private static (NativeBattleTonberry90Frame Frame, uint Address) Fixture()
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(5, 0x7674E0, 0x148, NativeBattleTonberry90Controller.MainBank, (_, _, _) => new(0, 3));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools } }, appended.Address);
    }

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] data = new byte[140];
        BinaryPrimitives.WriteInt32LittleEndian(data, 4);
        int[] offsets = [24, 88, 96, 132, 140];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4 + index * 4), offsets[index]);
        }
        data[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(40), -1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(96), 4);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(100 + index * 4), 20 + index * 4);
            data[116 + index * 4] = 1;
        }
        NativeBattlePoseSource pose = new(data.AsSpan(24, 64).ToArray().ToImmutableArray(), data.AsSpan(96, 36).ToArray().ToImmutableArray());
        return new(0x1000, data.ToImmutableArray(), pose, []);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
