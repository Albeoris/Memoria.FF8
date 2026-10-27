using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaraControllerTests
{
    [Fact]
    public void SingleHeaderUsesFourBanksStartupReversePointers168RandomDrawsAnd38Callbacks()
    {
        var fixture = Fixture();
        NativeBattleThundaraFrame frame = fixture.Frame;
        Assert.Throws<InvalidDataException>(() => NativeBattleThundaraController.Initialize6DC2E0(frame.Effect with { Binding = frame.Effect.Binding with { Entry = 0x6DC2E0 } }, fixture.Packets));
        Assert.Equal(new ushort[] { 20, 108, 44, 24 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 3, 60, 32 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        List<(int Callback, NativeBattleThundaraOperation Operation)> operations = [];
        for (int callback = 1; callback <= 38; callback++)
        {
            int current = callback;
            frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                operations.Add((current, operation));
                return value;
            }));
            if (callback == 1)
            {
                Assert.Equal((byte)1, Byte(frame.Effect.Pools[0].Slots[0], 15));
                Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(12));
                Assert.All(frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
                Assert.Null(frame.Effect.DrawArenaOffset);
            }
            else
            {
                Assert.Equal((callback & 1) == 0 ? 0x8000 : 0, frame.Effect.DrawArenaOffset);
            }
            if (callback == 2)
            {
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                Assert.Equal(0x1D972C0u + 3 * 156, Dword(primary, 16));
                Assert.Equal((ushort)100, primary.ReadWord(20));
                Assert.Equal(unchecked((ushort)-200), primary.ReadWord(22));
                Assert.Equal((ushort)300, primary.ReadWord(24));
                Assert.Equal((ushort)0, primary.ReadWord(26));
                for (int index = 0; index < 20; index++)
                {
                    Assert.Equal(NativeBattleThundaraController.SecondaryBank + (uint)index * 44, Dword(primary, 104 - 4 * index));
                    Assert.Equal((ushort)1, frame.Effect.Pools[2].Slots[index].ReadWord(12));
                }
            }
            if (callback < 38)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(6, Assert.Single(operations, item => item.Operation is NativeBattleThundaraSound).Callback);
        Assert.Equal(new[] { 14, 25 }, operations.Where(item => item.Operation is NativeBattleThundaraFlash).Select(item => item.Callback));
        Assert.Equal(28, Assert.Single(operations, item => item.Operation is NativeBattleThundaraContact).Callback);
        NativeCrtRandomState random = new(1);
        for (int index = 0; index < 168; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, frame.Random);
    }

    [Fact]
    public void PauseFlagsNeverGateAnyReachedControllerOrParticleWork()
    {
        var fixture = Fixture();
        NativeBattleThundaraFrame normal = fixture.Frame;
        NativeBattleThundaraFrame flagged = NativeBattleTestSnapshots.Independent(fixture.Frame);
        for (int callback = 0; callback < 38; callback++)
        {
            List<NativeBattleThundaraOperation> expected = [];
            List<NativeBattleThundaraOperation> actual = [];
            normal = NativeBattleThundaraController.Advance6DC390(normal, Inputs(fixture.Packets, (frame, operation) =>
            {
                expected.Add(operation);
                return frame;
            }));
            flagged = NativeBattleThundaraController.Advance6DC390(flagged, Inputs(fixture.Packets, (frame, operation) =>
            {
                actual.Add(operation);
                return frame;
            }) with { GraphicsFlags = 0x201 });
            Assert.Equal(NativeBattleTestSnapshots.Json(normal), NativeBattleTestSnapshots.Json(flagged));
            Assert.Equal(NativeBattleTestSnapshots.Json(expected.Select(operation => new { Kind = operation.GetType().Name, Json = NativeBattleTestSnapshots.Json(operation, operation.GetType()) })), NativeBattleTestSnapshots.Json(actual.Select(operation => new { Kind = operation.GetType().Name, Json = NativeBattleTestSnapshots.Json(operation, operation.GetType()) })));
        }
    }

    [Fact]
    public void RetargetedPhysicalChildrenAdvanceAfterPrimaryAndTheirPointersRestoreIndependently()
    {
        var fixture = Fixture();
        NativeBattleThundaraFrame frame = fixture.Frame;
        for (int index = 0; index < 13; index++)
        {
            frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets));
        }
        Assert.Equal((ushort)12, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        NativeBattleModulePoolTask before = NativeBattleStorageSnapshots.Capture(frame.Effect.Pools[2].Slots[0]);
        NativeBattleThundaraFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleThundaraFrame>(NativeBattleTestSnapshots.Json(frame))!;
        List<NativeBattleThundaraOperation> operations = [];
        frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets, (value, operation) =>
        {
            operations.Add(operation);
            return value;
        }));
        restored = NativeBattleThundaraController.Advance6DC390(restored, Inputs(fixture.Packets));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(restored));
        NativeBattleModulePoolTask after = frame.Effect.Pools[2].Slots[0];
        short velocity = unchecked((short)((Signed(before, 16) - 100) << 4));
        short updated = unchecked((short)(velocity - velocity / 10));
        Assert.Equal(unchecked((ushort)updated), after.ReadWord(24));
        Assert.Equal(unchecked((ushort)(Signed(before, 16) + (updated >> 4))), after.ReadWord(16));
        Assert.Equal(before.ReadWord(34), after.ReadWord(34));
        Assert.Equal(before.ReadWord(34), after.ReadWord(26));
        Assert.IsType<NativeBattleThundaraFlash>(operations[0]);
        Assert.Equal(0x131E878u, Assert.IsType<NativeBattleThundaraSprite>(operations[1]).ResourceAddress);
        Assert.Equal(0x6DCD90u, Assert.IsType<NativeBattleThundaraGeometry>(operations[2]).Helper);
        Assert.Equal(0x6DCEE0u, Assert.IsType<NativeBattleThundaraGeometry>(operations[3]).Helper);
    }

    [Fact]
    public void DuplicateActorDefersAt15UntilItsLivePrimaryPasses26()
    {
        var fixture = Fixture(2);
        NativeBattleThundaraFrame frame = fixture.Frame;
        List<int> contacts = [];
        for (int callback = 1; callback <= 68; callback++)
        {
            int current = callback;
            frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                if (operation is NativeBattleThundaraContact)
                {
                    contacts.Add(current);
                }
                return value;
            }));
            if (callback == 17)
            {
                Assert.Equal((byte)1, Byte(frame.Effect.Pools[0].Slots[0], 14));
                Assert.Single(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0);
            }
            if (callback == 32)
            {
                Assert.Equal((byte)2, Byte(frame.Effect.Pools[0].Slots[0], 14));
                Assert.Equal(2, frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 28, 58 }, contacts);
    }

    [Fact]
    public void OrderedPrimaryDrawingKeepsResourcePositionsFlagsAndGeometryParameters()
    {
        var fixture = Fixture();
        NativeBattleThundaraFrame frame = fixture.Frame;
        for (int index = 0; index < 21; index++)
        {
            frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets));
        }
        List<NativeBattleThundaraOperation> operations = [];
        frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets, (value, operation) =>
        {
            if (operation is NativeBattleThundaraGeometry || operation is NativeBattleThundaraSprite sprite && sprite.ResourceAddress is not (0x131F244 or 0x131E760))
            {
                operations.Add(operation);
            }
            return value;
        }));
        Assert.Equal(11, operations.Count);
        Assert.Equal(new uint[] { 0x131EABC, 0x131ECB0, 0x131F0E8, 0x6DCD90, 0x6DCEE0, 0x131FF74, 0x131EECC, 0x6DCD90, 0x6DCFE0, 0x131F458, 0x131FA68 }, operations.Select(operation => operation is NativeBattleThundaraSprite sprite ? sprite.ResourceAddress : ((NativeBattleThundaraGeometry)operation).Helper));
        Assert.Equal((short)0, ((NativeBattleThundaraSprite)operations[0]).Position.Y);
        Assert.Equal((short)-200, ((NativeBattleThundaraSprite)operations[1]).Position.Y);
        Assert.Equal((short)512, ((NativeBattleThundaraGeometry)operations[3]).AngleOrBaseY);
        Assert.Equal((short)-512, ((NativeBattleThundaraGeometry)operations[7]).AngleOrBaseY);
        Assert.Equal((short)123, ((NativeBattleThundaraGeometry)operations[8]).AngleOrBaseY);
        NativeBattleThundaraSprite grayscale = (NativeBattleThundaraSprite)operations[^1];
        Assert.Equal(0x808080u, grayscale.Color);
        Assert.Equal((ushort)4, grayscale.Flags);
    }

    [Fact]
    public void SecondFlashFollowsEveryPrimaryDrawAndPrecedesNewTertiaryDrawing()
    {
        var fixture = Fixture();
        NativeBattleThundaraFrame frame = fixture.Frame;
        for (int callback = 0; callback < 24; callback++)
        {
            frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets));
        }
        Assert.Equal((ushort)23, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        List<NativeBattleThundaraOperation> operations = [];
        frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets, (value, operation) =>
        {
            operations.Add(operation);
            return value;
        }));
        int flash = operations.FindIndex(operation => operation is NativeBattleThundaraFlash);
        Assert.True(flash > 0);
        Assert.Single(operations, operation => operation is NativeBattleThundaraFlash);
        NativeBattleThundaraSprite finalPrimary = Assert.IsType<NativeBattleThundaraSprite>(operations[flash - 1]);
        Assert.Equal(0x131FA68u, finalPrimary.ResourceAddress);
        Assert.Equal(3, finalPrimary.Frame);
        Assert.Equal(0x6DCFE0u, Assert.IsType<NativeBattleThundaraGeometry>(operations[flash - 4]).Helper);
        Assert.DoesNotContain(operations.Skip(flash + 1), operation => operation is NativeBattleThundaraGeometry);
        Assert.DoesNotContain(operations.Take(flash), operation => operation is NativeBattleThundaraSprite { ResourceAddress: 0x131E760 });
        Assert.Contains(operations.Skip(flash + 1), operation => operation is NativeBattleThundaraSprite { ResourceAddress: 0x131E760, Frame: 0 });
        Assert.Equal(2, frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 1));
    }

    [Fact]
    public void SecondaryDelayCounterAnimationAndSignedPhysicsRemainByteAndWordExact()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool secondary = fixture.Frame.Effect.Pools[2];
        (secondary, int slot) = secondary.Append(0x6DCC00);
        NativeBattleModulePoolTask task = secondary.Slots[slot].WriteWord(14, 0xFF02).WriteWord(16, 32760).WriteWord(18, 32760).WriteWord(24, 32767).WriteWord(26, 0x8000).WriteWord(32, 1).WriteWord(34, 0xFFFF).WriteWord(40, 127).WriteWord(42, 3);
        secondary = NativeBattleTaskFixture.EditPool(secondary, data => data with { Slots = secondary.Slots.SetItem(slot, task) });
        NativeBattleEffectTaskPool main = fixture.Frame.Effect.Pools[0];
        main = NativeBattleTaskFixture.EditPool(main, data => data with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, 1).WriteWord(14, 0x0100)) });
        NativeBattleThundaraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [main, fixture.Frame.Effect.Pools[1], secondary, fixture.Frame.Effect.Pools[3]] } };
        List<NativeBattleThundaraOperation> operations = [];
        NativeBattleThundaraInputs inputs = Inputs(fixture.Packets, (value, operation) =>
        {
            operations.Add(operation);
            return value;
        }) with { GraphicsFlags = 0x201 };
        frame = NativeBattleThundaraController.Advance6DC390(frame, inputs);
        frame = NativeBattleThundaraController.Advance6DC390(frame, inputs);
        Assert.Empty(operations);
        Assert.Equal((ushort)2, frame.Effect.Pools[2].Slots[slot].ReadWord(12));
        frame = NativeBattleThundaraController.Advance6DC390(frame, inputs);
        NativeBattleThundaraSprite draw = Assert.IsType<NativeBattleThundaraSprite>(Assert.Single(operations));
        Assert.Equal(15, draw.Frame);
        Assert.Equal(0x7F7F7Fu, draw.Color);
        NativeBattleModulePoolTask result = frame.Effect.Pools[2].Slots[slot];
        Assert.Equal((byte)0, Byte(result, 15));
        Assert.Equal((ushort)30712, result.ReadWord(16));
        Assert.Equal(unchecked((ushort)-30729), result.ReadWord(18));
        Assert.Equal((ushort)128, result.ReadWord(40));
    }

    [Fact]
    public void ExactPointerBoundsAndMissingPendingHeaderCompletionConditionRemainNative()
    {
        Assert.Equal(6, NativeBattleThundaraController.ActorIndex(0x1D972C0 + 6 * 156));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraController.ActorIndex(0x1D972C1));
        Assert.Equal(59, NativeBattleThundaraController.SecondarySlot(NativeBattleThundaraController.SecondaryBank + 59 * 44));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraController.SecondarySlot(NativeBattleThundaraController.SecondaryBank + 60 * 44));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraController.SecondarySlot(0));
        var fixture = Fixture();
        NativeBattleEffectTaskPool main = fixture.Frame.Effect.Pools[0];
        main = NativeBattleTaskFixture.EditPool(main, data => data with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, 7).WriteWord(14, 0x0100)) });
        NativeBattleThundaraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.SetItem(0, main) } };
        frame = NativeBattleThundaraController.Advance6DC390(frame, Inputs(fixture.Packets));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)7, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((byte)0, Byte(frame.Effect.Pools[0].Slots[0], 14));
    }

    private static byte Byte(NativeBattleModulePoolTask task, int offset) => NativeBattleStorageSnapshots.Capture(task).Payload[offset - 12];
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan()[(offset - 12)..]);
    private static NativeBattleThundaraInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleThundaraFrame, NativeBattleThundaraOperation, NativeBattleThundaraFrame>? execute = null) => new(0, packets, _ => new(100, -200, 300), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), execute ?? ((frame, _) => frame));

    private static (NativeBattleThundaraFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 102, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleOriginalEffectState effect = new(102, 101, new(102, 0x6DC2D0, 0x6DC2B0), new(0, 0, 8, 0, 0, 0), null);
        effect = NativeBattleThundaraController.Initialize6DC2E0(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1)), packets);
    }
}
