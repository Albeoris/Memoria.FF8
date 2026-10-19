using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274ControllerTests
{
    [Fact]
    public void OpeningSnapshotsSevenFlagsAndStartsItsExactPhysicalBank()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Enter();

        Assert.Equal(1, fixture.TextureCalls);
        Assert.True(frame.Memory.Duel274!.ColdGlobalsPublished);
        Assert.Equal(new byte[2], frame.Staging.Read(0x25084D8, 2));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal((uint)fixture.Flags[index], BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(checked(0x2508414u + (uint)index * 4), 4)));
        }
        NativeBattleEffectTaskPool root = Assert.Single(frame.Effect.Pools);
        Assert.Equal(0x25084B8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x25084C8u, root.BankAddress);
        Assert.Equal(24, root.Stride);
        Assert.Equal(0x689990u, root.Slots[0].Callback);
        Assert.All(root.Slots[0].Payload, value => Assert.Equal(0, value));
    }

    [Fact]
    public void RootPublishesReadyOnActualSecondPassAndRemainsAliveAcrossSnapshot()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Enter();
        frame = fixture.Advance(frame);

        Assert.Single(frame.Effect.Pools);
        Assert.Equal(1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(0, frame.Effect.Parameters!.Parameter81);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508438, 4)));
        frame = JsonSerializer.Deserialize<NativeBattleDuel274Frame>(JsonSerializer.Serialize(frame))!;
        fixture.Flags[3] = 0x1002;
        frame = fixture.Advance(frame);

        Assert.Equal(1, frame.Effect.Parameters!.Parameter81);
        Assert.Equal(0x25084B8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(0x1006, fixture.Flags[3]);
        Assert.Equal(2, fixture.BoundsModel);
        NativeBattleEffectTaskPool targets = frame.Effect.Pools[1];
        Assert.Equal(0x25084A8u, targets.DescriptorAddress);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, targets.BankAddress);
        Assert.Equal(116, targets.Stride);
        Assert.Equal(NativeBattleDuel274Controller.Address(2), targets.Slots[0].ReadDword(20));
        Assert.Equal(200, targets.Slots[0].ReadWord(92));
        Assert.Equal(unchecked((ushort)-50), targets.Slots[0].ReadWord(94));
        Assert.Equal(1, targets.Slots[0].ReadWord(12));
        for (int pass = 0; pass < 12; pass++)
        {
            frame = fixture.Advance(frame);
        }
        Assert.Equal(10, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(1, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(0x25084B8u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(1, fixture.BoundsCalls);
    }

    [Fact]
    public void DirectSelectorPreservesRootAndSnapshotsAndOnlyPublishesActualTransitionBytes()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Advance(fixture.Enter()));
        string pools = JsonSerializer.Serialize(frame.Effect.Pools);
        byte[] flags = frame.Staging.Read(0x2508414, 28);
        NativeBattleEffectParameters parameters = frame.Effect.Parameters! with { Parameter80 = 6 };
        frame = NativeBattleDuel274Controller.Enter6898B0(frame with { Effect = frame.Effect with { Parameters = parameters } }, fixture.Actors, _ => throw new InvalidOperationException("Direct selection must not enqueue initial texture."));

        Assert.Equal(new byte[] { 1, 6 }, frame.Staging.Read(0x25084D8, 2));
        Assert.Equal(pools, JsonSerializer.Serialize(frame.Effect.Pools));
        Assert.Equal(flags, frame.Staging.Read(0x2508414, 28));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        bool selected = false;
        NativeBattleDuel274TaskStep step = NativeBattleDuel274Controller.Advance689AC0(frame, 0, fixture.Actors, (current, slot) =>
        {
            selected = true;
            Assert.Equal(6, current.Effect.Pools[1].Slots[slot].ReadByte(15));
            return new(current, current.Effect.Pools[1], 0);
        });
        Assert.True(selected);
        Assert.Equal(0, step.Result);
        Assert.Equal(1, step.Frame.Effect.Parameters!.Parameter81);
    }

    [Fact]
    public void GenericUnknownProcessCannotSupplyColdTransitionBytes()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel274Controller.Enter6898B0(fixture.Frame with { Memory = fixture.Frame.Memory with { Duel274 = null } }, fixture.Actors, frame => frame));
    }

    private sealed class Fixture
    {
        public ushort[] Flags { get; } = [2, 6, 2, 0x1006, 0, 2, 6];
        public int TextureCalls { get; private set; }
        public int BoundsCalls { get; private set; }
        public int BoundsModel { get; private set; } = -1;
        public NativeBattleDuel274Frame Frame { get; }
        public NativeBattleDuel274ActorServices Actors { get; }

        public Fixture()
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(2352, BitConverter.GetBytes(0x12345678u)).Write(2348, BitConverter.GetBytes(0x87654321u));
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 0 }, null);
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Duel274 = NativeBattleDuel274State.FromColdProcess(), Arena = arena }, effect, NativeBattleFileStaging.Unknown(), new(1));
            Actors = new(index => Flags[index], (index, flags) => Flags[index] = flags, index => index switch { 2 => NativeBattleDuel274Controller.Address(3), 3 => NativeBattleDuel274Controller.Address(2), _ => 0 }, index => index == 3 ? 2 : throw new InvalidOperationException("Unexpected actual target."), index =>
            {
                BoundsCalls++;
                BoundsModel = index;
                return (200, -50);
            });
        }

        public NativeBattleDuel274Frame Enter()
        {
            return NativeBattleDuel274Controller.Enter6898B0(Frame, Actors, frame =>
            {
                TextureCalls++;
                return frame;
            });
        }

        public NativeBattleDuel274Frame Advance(NativeBattleDuel274Frame frame)
        {
            return NativeBattleDuel274Controller.Advance689990(frame, Actors, sequence => sequence == 0 ? 3 : throw new InvalidOperationException("Unexpected actual sequence."), (current, slot) => NativeBattleDuel274Controller.Advance689AC0(current, slot, Actors, (_, _) => throw new InvalidOperationException("Opening must not dispatch a selected move.")));
        }
    }
}
