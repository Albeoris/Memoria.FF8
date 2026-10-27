using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleClash26ControllerTests
{
    [Fact]
    public void EntrySeparatesLoadedTextureFromCurrentPacketArenasAndPreservesOriginalRootHeaderFields()
    {
        var fixture = Create();
        Assert.Equal(0x26DBC00u, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x2000, fixture.Frame.State.ArenaOffset);
        Assert.Null(fixture.Frame.State.DrawCursor);
        Assert.Equal(new[] { 2, 4, 64, 300, 100, 2 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal((byte)0, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.NotNull(fixture.Frame.Memory.Textures);
    }

    [Fact]
    public void MasterWaitUsesPreviousQueueCountThenReleasesItsActualDescriptorWithIndependentJsonContinuation()
    {
        var fixture = Create();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 3).WriteWord(0x5E, 1));
        NativeBattleClash26Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleClash26Step first = NativeBattleClash26Controller.Advance8886C0(frame, Resources(), fixture.Inputs, new(1));
        Assert.Equal(0xE000, first.Frame.State.DrawCursor);
        Assert.Equal((byte)3, first.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, first.Frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
        string json = NativeBattleTestSnapshots.Json(first.Frame);
        NativeBattleClash26Step left = NativeBattleClash26Controller.Advance8886C0(NativeBattleTestSnapshots.RestoreJson<NativeBattleClash26Frame>(json)!, Resources(), fixture.Inputs, first.Random);
        NativeBattleClash26Step right = NativeBattleClash26Controller.Advance8886C0(NativeBattleTestSnapshots.RestoreJson<NativeBattleClash26Frame>(json)!, Resources(), fixture.Inputs, first.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(left), NativeBattleTestSnapshots.Json(right));
        Assert.Equal(0u, left.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x2000, left.Frame.State.DrawCursor);
        Assert.Equal((ushort)0, left.Frame.Effect.Pools[0].Slots[0].Flags);
        Assert.Equal((byte)4, left.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
    }

    [Fact]
    public void BurstSparkStartsHiddenWithOneRandomDrawAndKeepsCopiedPositionFourthWord()
    {
        var fixture = Create();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(3, 0x888BF0, 0x70, root, (_, _, _) => new(3, 0));
        hierarchy = child.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(child.Address).WriteDword(0x20, 0x026E0030).WriteWord(0x1C, 10).WriteWord(0x1E, 20).WriteWord(0x6C, 0x100).WriteWord(0x6A, 0);
        hierarchy = hierarchy.Write(child.Address, task);
        NativeBattleClash26Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeCrtRandomState random = new(1);
        NativeCrtRandomState expected = random.Draw(out ushort sample);
        NativeBattleClash26Step result = NativeBattleClash26Controller.Advance8886C0(frame, Resources(), fixture.Inputs, random);
        NativeBattleModulePoolTask spark = new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(child.Address);
        Assert.Equal(expected, result.Random);
        Assert.Equal((byte)4, (byte)(spark.ReadByte(0x26) & 4));
        Assert.Equal((byte)1, spark.ReadByte(0x29));
        Assert.Equal((ushort)0x026E, spark.ReadWord(0x5E));
        int distance = sample % 0x100;
        Assert.Equal(unchecked((ushort)(0x30 + distance)), spark.ReadWord(0x20));
        Assert.Equal(unchecked((ushort)(distance / 8)), spark.ReadWord(0x5C));
        Assert.Equal(unchecked((ushort)(-(distance / 8) / 16)), spark.ReadWord(0x64));
        Assert.Equal((ushort)1, result.Frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
    }

    private static NativeBattleClash26Resources Resources() => new([], [], [], [], 0);

    private static (NativeBattleClash26Frame Frame, NativeBattleClash26Inputs Inputs) Create()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 12, 26, null));
        (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 1, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(26, 25, new(26, 0x8885D0, 0x8885B0), new(3, 0, 1, 0, 0, 0), null) { ResourcePointers = [new(0x26D90D8, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(25);
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(62), 1);
        memory = memory with { Arena = memory.Arena!.Write(0, tim), Cursor = 0x2000 };
        NativeBattleClash26Frame frame = NativeBattleClash26Controller.Initialize8885D0(memory, effect, packets);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 512, 160, 104);
        NativeBattleClash26Inputs inputs = new(packets, projection, 0, new(0, 0, 1f / 4096, 4503601774854144d), index => new(index, 0), _ => throw new InvalidOperationException("No loaded actor is selected."), (current, _) => throw new InvalidOperationException("A hidden spark or empty controller must not emit."));
        return (frame, inputs);
    }
}

