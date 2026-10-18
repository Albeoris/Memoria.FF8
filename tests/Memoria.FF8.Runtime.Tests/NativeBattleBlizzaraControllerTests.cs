using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzaraControllerTests
{
    [Fact]
    public void SingleHeaderUsesActualFiveBanksSamePassChildrenContactAnd72Callbacks()
    {
        var fixture = Fixture();
        NativeBattleBlizzaraFrame frame = fixture.Frame;
        List<(int Callback, NativeBattleBlizzaraOperation Operation)> operations = [];
        Assert.Single(frame.Effect.Pools);
        for (int callback = 1; callback <= 72; callback++)
        {
            int current = callback;
            frame = NativeBattleBlizzaraController.Advance6DA3A0(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                operations.Add((current, operation));
                return value;
            }));
            Assert.Equal((callback & 1) != 0 ? 0x296D4 : 0x196D4, frame.Effect.DrawArenaOffset);
            if (callback == 1)
            {
                Assert.Single(frame.Effect.Pools);
                Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
            }
            if (callback == 2)
            {
                Assert.Equal(new ushort[] { 20, 0x53CC, 0x28, 0x20, 0x24, 0x28 }, frame.Effect.Pools.Select(pool => pool.Stride));
                Assert.Equal(new[] { 1, 3, 12, 1, 32, 64 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                Assert.Equal((ushort)1, primary.ReadWord(12));
                Assert.Equal((ushort)123, primary.ReadWord(30));
                Assert.Equal(NativeBattleBlizzaraController.PrimaryBank + 0x10BE4, Dword(primary, 20));
                NativeBattleModulePoolTask sky = frame.Effect.Pools[2].Slots[0];
                Assert.Equal((ushort)1, sky.ReadWord(12));
                Assert.Equal(NativeBattleBlizzaraController.PrimaryBank, Dword(sky, 16));
            }
            if (callback == 22)
            {
                Assert.Equal(2, frame.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 1));
                Assert.Equal(4, frame.Effect.Pools[5].Slots.Count(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 1));
            }
            if (callback < 72)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(8, Assert.Single(operations, item => item.Operation is NativeBattleBlizzaraSound).Callback);
        Assert.Equal(62, Assert.Single(operations, item => item.Operation is NativeBattleBlizzaraContact).Callback);
        Assert.Equal(new uint[] { 0x6600C, 0x660C0 }, operations.Where(item => item.Callback == 32 && item.Operation is NativeBattleBlizzaraMainDraw).Select(item => ((NativeBattleBlizzaraMainDraw)item.Operation).Flags));
        Assert.Equal(52, operations.Count(item => item.Operation is NativeBattleBlizzaraMainDraw));
        Assert.Equal(22 * 16, operations.Count(item => item.Operation is NativeBattleBlizzaraDraw { Task.Callback: 0x6DBEA0 }));
        Assert.Equal(84 * 16, operations.Count(item => item.Operation is NativeBattleBlizzaraDraw { Task.Callback: 0x6DBFF0 }));
        NativeCrtRandomState expected = new(1);
        HashSet<int> ordinals = [];
        while (ordinals.Count < 4)
        {
            expected = expected.Draw(out ushort value);
            ordinals.Add(value * 4 >> 15);
        }
        for (int index = 0; index < 1041; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void PauseBitsDoNotGateAndIndependentMidFaceRestoreContinuesExactly()
    {
        var fixture = Fixture();
        NativeBattleBlizzaraFrame normal = fixture.Frame;
        NativeBattleBlizzaraFrame flagged = fixture.Frame;
        for (int callback = 1; callback <= 72; callback++)
        {
            normal = NativeBattleBlizzaraController.Advance6DA3A0(normal, Inputs(fixture.Packets));
            flagged = NativeBattleBlizzaraController.Advance6DA3A0(flagged, Inputs(fixture.Packets) with { GraphicsFlags = 0x201 });
            Assert.Equal(JsonSerializer.Serialize(normal), JsonSerializer.Serialize(flagged));
            if (callback == 37)
            {
                flagged = JsonSerializer.Deserialize<NativeBattleBlizzaraFrame>(JsonSerializer.Serialize(normal))!;
                Assert.NotSame(normal.Effect.Pools[1].Slots[0], flagged.Effect.Pools[1].Slots[0]);
            }
        }
    }

    [Fact]
    public void RepeatedActorRetriesEachCallbackUntilPreviousPrimaryExceeds60()
    {
        var fixture = Fixture(2);
        NativeBattleBlizzaraFrame frame = fixture.Frame;
        List<int> births = [];
        for (int callback = 1; callback <= 140 && frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            int before = frame.Effect.Pools[0].Slots[0].ReadByte(14);
            frame = NativeBattleBlizzaraController.Advance6DA3A0(frame, Inputs(fixture.Packets));
            if (frame.Effect.Pools[0].Slots[0].ReadByte(14) != before)
            {
                births.Add(callback);
            }
            if (callback is >= 22 and <= 62)
            {
                Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
            }
        }
        Assert.Equal(new[] { 2, 63 }, births);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void PhysicalOwnerBoundsAndActualWrapperAreRequired()
    {
        var fixture = Fixture();
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzaraController.Initialize6DA310(fixture.Frame.Effect with { Binding = fixture.Frame.Effect.Binding with { Entry = 0x6DA310 } }, fixture.Packets));
        Assert.Throws<NotSupportedException>(() => Fixture(4));
        Assert.Equal(2, NativeBattleBlizzaraController.PrimarySlot(NativeBattleBlizzaraController.PrimaryBank + 2 * 0x53CC));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzaraController.PrimarySlot(NativeBattleBlizzaraController.PrimaryBank + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzaraController.PrimarySlot(NativeBattleBlizzaraController.PrimaryBank + 3 * 0x53CC));
        Assert.Equal(6, NativeBattleBlizzaraController.ActorIndex(0x1D972C0 + 6 * 156));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzaraController.ActorIndex(0));
    }

    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12, 4));
    private static NativeBattleBlizzaraInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleBlizzaraFrame, NativeBattleBlizzaraOperation, NativeBattleBlizzaraFrame>? execute = null) => new(0, packets, _ => new(100, -200, 300), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), execute ?? ((frame, _) => frame));

    private static (NativeBattleBlizzaraFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 103, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleOriginalEffectState effect = new(103, 102, new(103, 0x6DA300, 0x6DA2E0) { BlizzaraResources = NativeBattleBlizzaraFaceTests.Resources() }, new(0, 0, 8, 0, 0, 0), null);
        effect = NativeBattleBlizzaraController.Initialize6DA310(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1)), packets);
    }
}
