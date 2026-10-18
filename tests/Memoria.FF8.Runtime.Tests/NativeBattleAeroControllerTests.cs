using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAeroControllerTests
{
    [Fact]
    public void TwoActualWorkspacesCompleteAtFrame37WhileParticlesRetainSequenceAndDrainAt65()
    {
        var fixture = Fixture();
        NativeBattleAeroFrame frame = fixture.Frame;
        List<(int Callback, NativeBattleAeroOperation Operation)> operations = [];
        for (int callback = 1; callback <= 65; callback++)
        {
            int current = callback;
            frame = NativeBattleAeroController.Advance6C0D20(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                operations.Add((current, operation));
                return value;
            }));
            Assert.Equal((callback & 1) != 0 ? 0x1A584 : 0x2584, frame.Effect.DrawArenaOffset);
            if (callback == 1)
            {
                Assert.Single(frame.Effect.Pools);
            }
            if (callback == 2)
            {
                Assert.Equal(new ushort[] { 20, 0x68C, 0x3C }, frame.Effect.Pools.Select(pool => pool.Stride));
                Assert.Equal(new[] { 1, 3, 64 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                Assert.Equal((ushort)123, primary.ReadWord(26));
                Assert.Equal(0, NativeBattleAeroController.ReadWorkspace(primary, true).Frame);
                Assert.Equal(0, NativeBattleAeroController.ReadWorkspace(primary, false).Frame);
            }
            if (callback == 41)
            {
                Assert.Equal((ushort)40, frame.Effect.Pools[1].Slots[0].ReadWord(12));
                Assert.All(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Equal((ushort)1, task.ReadWord(48)));
            }
            if (callback == 50)
            {
                NativeBattleModulePoolTask primary = frame.Effect.Pools[1].Slots[0];
                Assert.Equal((ushort)49, primary.ReadWord(12));
                Assert.Equal((ushort)0xFFFF, primary.ReadWord(14));
                Assert.Equal(38, NativeBattleAeroController.ReadWorkspace(primary, true).Frame);
                Assert.Equal(38, NativeBattleAeroController.ReadWorkspace(primary, false).Frame);
                Assert.Equal((ushort)0, primary.Flags);
            }
            if (callback < 65)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(10, Assert.Single(operations, item => item.Operation is NativeBattleAeroSound).Callback);
        Assert.Equal(50, Assert.Single(operations, item => item.Operation is NativeBattleAeroContact).Callback);
        Assert.Equal(76, operations.Count(item => item.Operation is NativeBattleAeroNode));
        Assert.Equal(20, operations.Count(item => item.Callback == 64 && item.Operation is NativeBattleAeroParticle));
        Assert.DoesNotContain(operations, item => item.Callback == 65 && item.Operation is NativeBattleAeroParticle);
        Assert.All(operations.Where(item => item.Operation is NativeBattleAeroParticle), item => Assert.Equal((ushort)0, ((NativeBattleAeroParticle)item.Operation).Task.ReadWord(12)));
        NativeBattleAeroNode[] firstNodes = operations.Where(item => item.Callback == 13 && item.Operation is NativeBattleAeroNode).Select(item => (NativeBattleAeroNode)item.Operation).ToArray();
        Assert.Equal(123, firstNodes[0].Config.Anchor.Y);
        Assert.Equal(-200, firstNodes[1].Config.Anchor.Y);
        Assert.Equal(-192, firstNodes[0].Config.DepthBias);
        Assert.Equal(4096, firstNodes[0].Config.ScaleX);
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 160; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void EightRandomOperandsAndTheirExactOrderDriveFirstSamePassParticle()
    {
        var fixture = Fixture();
        NativeBattleAeroFrame frame = fixture.Frame;
        frame = NativeBattleAeroController.Advance6C0D20(frame, Inputs(fixture.Packets));
        frame = NativeBattleAeroController.Advance6C0D20(frame, Inputs(fixture.Packets));
        NativeBattleModulePoolTask particle = frame.Effect.Pools[2].Slots[0];
        ushort[] draws = new ushort[8];
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws.Length; index++)
        {
            expected = expected.Draw(out draws[index]);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal((ushort)768, particle.ReadWord(16));
        Assert.Equal(unchecked((ushort)(draws[1] + 64)), particle.ReadWord(18));
        Assert.Equal(unchecked((ushort)((draws[0] & 255) - 4)), particle.ReadWord(28));
        for (int axis = 0; axis < 3; axis++)
        {
            byte velocity = unchecked((byte)(draws[5 + axis] + 128));
            Assert.Equal(velocity, particle.ReadByte(50 + axis));
            Assert.Equal(unchecked((ushort)(draws[2 + axis] + (unchecked((sbyte)velocity) << 3))), particle.ReadWord(42 + axis * 2));
        }
        Assert.Equal(NativeBattleAeroController.PrimaryBank, Dword(particle, 56));
        Assert.Equal((ushort)1, particle.Flags);
    }

    [Fact]
    public void SameActorThreeHeadersDoNotBlockAndKeepTheirPhysicalParentSequence()
    {
        var fixture = Fixture(3);
        NativeBattleAeroFrame frame = fixture.Frame;
        List<int> births = [];
        List<int> contacts = [];
        for (int callback = 1; callback <= 85; callback++)
        {
            byte before = frame.Effect.Pools[0].Slots[0].ReadByte(14);
            int current = callback;
            frame = NativeBattleAeroController.Advance6C0D20(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                if (operation is NativeBattleAeroContact)
                {
                    contacts.Add(current);
                }
                return value;
            }));
            if (before != frame.Effect.Pools[0].Slots[0].ReadByte(14))
            {
                births.Add(callback);
            }
            if (callback == 41)
            {
                Assert.Equal(new ushort[] { 0, 1, 2 }, frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.ReadWord(12)).Distinct().Order());
            }
        }
        Assert.Equal(new[] { 2, 12, 22 }, births);
        Assert.Equal(new[] { 50, 60, 70 }, contacts);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 480; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void DerivativeWorkspaceAndInactiveParentBytesSurviveIndependentRestoreAndPauseBits()
    {
        var fixture = Fixture();
        NativeBattleAeroFrame normal = fixture.Frame;
        NativeBattleAeroFrame flagged = fixture.Frame;
        for (int callback = 1; callback <= 65; callback++)
        {
            normal = NativeBattleAeroController.Advance6C0D20(normal, Inputs(fixture.Packets));
            flagged = NativeBattleAeroController.Advance6C0D20(flagged, Inputs(fixture.Packets) with { GraphicsFlags = 0x201 });
            Assert.Equal(JsonSerializer.Serialize(normal), JsonSerializer.Serialize(flagged));
            if (callback is 20 or 51)
            {
                flagged = JsonSerializer.Deserialize<NativeBattleAeroFrame>(JsonSerializer.Serialize(normal))!;
                Assert.NotSame(normal.Effect.Pools[1].Slots[0], flagged.Effect.Pools[1].Slots[0]);
            }
        }
    }

    [Fact]
    public void PhysicalBoundsAndWrapperAreStrictAndWorkspaceRemainingControlsContact()
    {
        var fixture = Fixture(durationA: 2, durationB: 3);
        Assert.Throws<InvalidDataException>(() => NativeBattleAeroController.Initialize6C0CB0(fixture.Frame.Effect with { Binding = fixture.Frame.Effect.Binding with { Entry = 0x6C0CB0 } }, fixture.Packets));
        Assert.Throws<NotSupportedException>(() => Fixture(4));
        Assert.Equal(2, NativeBattleAeroController.PrimarySlot(NativeBattleAeroController.PrimaryBank + 2 * 0x68C));
        Assert.Throws<NotSupportedException>(() => NativeBattleAeroController.PrimarySlot(NativeBattleAeroController.PrimaryBank + 3 * 0x68C));
        NativeBattleAeroFrame frame = fixture.Frame;
        List<int> contacts = [];
        for (int callback = 1; callback <= 15; callback++)
        {
            int current = callback;
            frame = NativeBattleAeroController.Advance6C0D20(frame, Inputs(fixture.Packets, (value, operation) =>
            {
                if (operation is NativeBattleAeroContact)
                {
                    contacts.Add(current);
                }
                return value;
            }));
        }
        Assert.Equal(new[] { 15 }, contacts);
        Assert.Equal((ushort)14, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12, 4));
    private static NativeBattleAeroInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleAeroFrame, NativeBattleAeroOperation, NativeBattleAeroFrame>? execute = null) => new(0, packets, _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), actor => new((short)(100 + actor * 20), -200, 300, 123), execute ?? ((frame, _) => frame));

    private static (NativeBattleAeroFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1, int durationA = 38, int durationB = 38)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 118, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleAeroResources resources = new(new("animation", []), Source(0x1287DBC, durationA), Source(0x128C110, durationB), new(1, 1, []), 300110);
        NativeBattleOriginalEffectState effect = new(118, 117, new(118, 0x6C0C80, 0x6C0C90) { AeroResources = resources }, new(0, 0, 8, 0, 0, 0), null);
        effect = NativeBattleAeroController.Initialize6C0CB0(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1)), packets);
    }

    private static NativeBattleGfAnimationSource Source(uint address, int duration)
    {
        byte[] data = new byte[0x100];
        Put(4, 0x20);
        Put(0x20, 1);
        Put(0x24, duration);
        Put(0x28, 0x20);
        Put(0x40, 0);
        Put(0x44, 7);
        Put(0x48, 0x40);
        Put(0x4C, 0x60);
        Put(0x50, 0x80);
        Stream(0x80, [0, 1 << 16, 2 << 16, 3 << 16, -1]);
        Stream(0xA0, [0, 1 << 16, 0, -1 << 16, -1]);
        Stream(0xC0, [0, 0, 1 << 16, 0, -1]);
        return new(address, data.ToImmutableArray());

        void Put(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
        void Stream(int offset, int[] values)
        {
            foreach (int value in values)
            {
                Put(offset, value);
                offset += 4;
            }
        }
    }
}
