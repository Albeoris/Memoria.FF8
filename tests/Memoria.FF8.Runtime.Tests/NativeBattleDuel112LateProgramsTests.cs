using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112LateProgramsTests
{
    [Fact]
    public void FinalProgramHalvesTheActualSignedYawAndPublishesThePackedTargetAnchorOnce()
    {
        Fixture fixture = new();
        NativeBattleDuel112DrawingStep step = NativeBattleDuel112Target.DrawLate6CBF90(fixture.Frame, 0, fixture.Inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal((short)-512, fixture.Actor.Rotation14);
        Assert.Equal(new NativeBattleVector(1161, 0, -760), fixture.Actor.Transform!.Position28);
        Assert.Equal((short)77, fixture.Actor.Transform.Vertical34);
        Assert.Equal(1, fixture.AnchorReads);
        NativeBattleModulePoolTask task = step.Frame.Effect.Pools[1].Slots[0];
        Assert.Equal(1885u, task.ReadDword(160));
        Assert.Equal(0u, task.ReadDword(164));
        Assert.Equal(unchecked((uint)-1484), task.ReadDword(168));
        Assert.Equal((ushort)0xCCCC, task.ReadWord(106));
        Assert.Equal((ushort)0xCCCC, task.ReadWord(158));
        Assert.Equal(0u, task.ReadDword(2320));
        Assert.Equal(1u, task.ReadDword(2588));
        Assert.Equal(1u, task.ReadDword(3484));
        Assert.Equal(0u, task.ReadDword(3708));
        Assert.Equal((ushort)69, task.ReadWord(12));
    }

    [Fact]
    public void ImpactResumesFromItsActualWorkspaceAndInsertsTheTwoModesAroundTheFlash()
    {
        Fixture fixture = new();
        NativeBattleDuel112DrawingStep initialized = NativeBattleDuel112Target.DrawLate6CBF90(fixture.Frame, 0, fixture.Inputs, NativeBattleLogicalOrdering.Clear());
        NativeBattleEffectTaskPool pool = initialized.Frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, 107)) };
        NativeBattleDuel112Frame saved = initialized.Frame with { Effect = initialized.Frame.Effect with { Pools = initialized.Frame.Effect.Pools.SetItem(1, pool) } };
        saved = JsonSerializer.Deserialize<NativeBattleDuel112Frame>(JsonSerializer.Serialize(saved))!;
        NativeBattleDuel112DrawingStep step = NativeBattleDuel112Target.DrawLate6CBF90(saved, 0, fixture.Inputs, initialized.Ordering);
        Assert.Equal(1, fixture.AnchorReads);
        Assert.Equal(0x1EB0, step.Frame.Effect.DrawArenaOffset);
        Assert.Equal(new[] { 0x1EA0, 0x1E88, 0x1EA8 }, step.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(new uint[] { 0xE1000020, 0x2A808080, 0xE1000040 }, step.Ordering.Packets.Select(packet => packet.Words[1]));
        Assert.Equal(100u, step.Frame.Effect.Pools[1].Slots[0].ReadDword(160));
        Assert.Equal(200u, step.Frame.Effect.Pools[1].Slots[0].ReadDword(164));
        Assert.Equal(300u, step.Frame.Effect.Pools[1].Slots[0].ReadDword(168));
        Assert.Empty(fixture.Flashes);
    }

    private sealed class Fixture
    {
        public NativeBattleDuel112Frame Frame { get; }
        public NativeBattleDuel112LateInputs Inputs { get; }
        public NativeBattleGraphicsModel Actor { get; private set; } = new(0, 3) { Rotation14 = 3072, Transform = new(new(0, 0, 0), new(10, 20, 30), -40, 17, 4096) };
        public int AnchorReads { get; private set; }
        public List<(int, int, int, int)> Flashes { get; } = [];

        public Fixture()
        {
            byte[] bytes = new byte[NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address];
            foreach (uint source in new uint[] { 0x12D19F4, 0x12D4684, 0x12D8D28, 0x12D9DC0 })
            {
                int offset = checked((int)(source - NativeBattleDuel112Resources.Address));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4), 16);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 20), 100);
            }
            NativeBattleDuel112Resources resources = new(bytes.ToImmutableArray(), [0, 0, 0, 0], [], []);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
            (target, _) = target.Append(0x6CBF90);
            NativeBattleModulePoolTask task = target.Slots[0] with { Payload = Enumerable.Repeat((byte)0xCC, 0xF48 - 12).ToImmutableArray() };
            task = task.WriteWord(12, 69).WriteDword(172, 1000).WriteDword(176, 0x1D972C0 + 156 * 3);
            target = target with { Slots = target.Slots.SetItem(0, task) };
            NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
            NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target], DrawArenaOffset = 0x1E88 };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Duel112 = new(true, parameters, resources) };
            Frame = new(memory, effect, NativeBattleFileStaging.Unknown().Write(0x25218E8, BitConverter.GetBytes(0x20DFAB8u)).Write(0x2521914, BitConverter.GetBytes(0x20E1940u)), new(1));
            NativeBattleGraphicsModel enemy = new(3, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 1000) };
            NativeBattleDuel112ActorServices actor = new(index => index == 0 ? Actor : enemy, (index, model) =>
            {
                Assert.Equal(0, index);
                Actor = model;
            }, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
            NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
            NativeBattleDuel112DrawingInputs drawing = new(actor, (_, _, _) => throw new InvalidOperationException(), view, null!, null!, (a, b, c, d) => Flashes.Add((a, b, c, d)));
            Inputs = new(drawing, index =>
            {
                Assert.Equal(3, index);
                AnchorReads++;
                return new(100, 200, 300, 77);
            });
        }
    }
}
