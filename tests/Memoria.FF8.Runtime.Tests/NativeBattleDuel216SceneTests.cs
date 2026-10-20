using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216SceneTests
{
    [Fact]
    public void LastScenePassClearsTargetFlagsBeforeContactAndQueuesThreeOriginalFiles()
    {
        Fixture fixture = new(138);
        fixture.Models[3] = fixture.Models[3] with { Flags0 = 14 };
        NativeBattleDuel216Step result = fixture.Run();
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)139, result.Pool.Slots[0].ReadWord(12));
        Assert.Equal((ushort)2, fixture.Models[3].Flags0);
        Assert.Collection(fixture.Operations,
            operation => Assert.IsType<NativeBattleDuel216Contact506690>(operation),
            operation => Assert.Equal(new NativeBattleDuel216MainCamera(138), operation),
            operation => Assert.Equal(new NativeBattleDuel216MainFile(360, 0x20DFAB8 + 12696, 1), operation),
            operation => Assert.Equal(new NativeBattleDuel216MainFile(361, 0x20DFAB8 + 12696, 1), operation),
            operation => Assert.Equal(new NativeBattleDuel216MainFile(362, 0x20DFAB8 + 12696, 0), operation),
            operation => Assert.IsType<NativeBattleDuel216MainStageReset>(operation));
        Assert.Equal(225, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging.Read(0x25088E8, 4)));
    }

    [Fact]
    public void RisingPhaseDrawsTargetsBeforeEchoAndPreservesSignedMotion()
    {
        Fixture fixture = new(24);
        NativeBattleDuel216Step result = fixture.Run();
        Assert.Equal(-108, fixture.Models[0].Transform!.Position28.Y);
        Assert.Equal(new NativeBattleVector(0, 0, 0), Assert.Single(fixture.Operations.OfType<NativeBattleDuel216BodyDraw>()).Rotation);
        Assert.Collection(fixture.Operations,
            operation => Assert.Equal(new NativeBattleDuel216MorphDraw(1, 3, false), operation),
            operation => Assert.IsType<NativeBattleDuel216BodyDraw>(operation),
            operation => Assert.Equal(new NativeBattleDuel216EchoDraw(0), operation),
            operation => Assert.Equal(new NativeBattleDuel216EchoCapture(0), operation),
            operation => Assert.Equal(new NativeBattleDuel216MainCamera(24), operation));
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)25, result.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void Tick71UnschedulesSecondaryDescriptorWithoutClearingItsPhysicalActiveRows()
    {
        Fixture fixture = new(71);
        NativeBattleEffectTaskPool secondary = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel216Controller.Secondary, 0x20DFAB8 + 6128, 260, 8);
        (secondary, _) = secondary.Append(0x69A5D0);
        secondary = secondary with { Slots = secondary.Slots.SetItem(0, secondary.Slots[0].WriteWord(12, 0xBEEF)) };
        fixture.Frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.Add(secondary) } };
        NativeBattleDuel216Step result = fixture.Run();
        NativeBattleEffectTaskPool retained = result.Frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel216Controller.Secondary);
        Assert.Null(retained.Head);
        Assert.Null(retained.Tail);
        Assert.Equal((ushort)1, retained.Slots[0].Flags);
        Assert.Equal((ushort)0xBEEF, retained.Slots[0].ReadWord(12));
        Assert.Equal(4276, fixture.Models[0].Transform!.Position28.Z);
        Assert.Equal(5300, fixture.Models[3].Transform!.Position28.Z);
        Assert.Equal((ushort)0, result.Pool.Slots[0].ReadWord(6112));
    }

    private sealed class Fixture
    {
        public NativeBattleDuel216Frame Frame;
        public readonly NativeBattleGraphicsModel[] Models;
        public readonly List<NativeBattleDuel216SceneOperation> Operations = [];

        public Fixture(ushort tick)
        {
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel216Controller.Main, 0x20DFAB8, 6128, 1);
            (main, _) = main.Append(0x6960E0);
            main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, tick).WriteWord(6108, 100).WriteWord(6110, 200)) };
            NativeBattleDuel216Resources resources = new([], [], new("unused", []), []);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel216 = new(true, new(0, 0, 8, 0, 0, 0), resources) };
            NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Main) { Pools = [main] };
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2508880, BitConverter.GetBytes(0x1D972C0u)).Write(0x2508884, BitConverter.GetBytes(0x1D972C0u + 3 * 156)).Write(0x25088F0, new byte[6]).Write(0x2508908, BitConverter.GetBytes(900));
            Frame = new(memory, effect, staging, NativeCrtRandomState.FromColdProcess());
            Models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 2) { Transform = new(new(0, 0, 0), new(10, 100, 300), 0, 0, 4096) }).ToArray();
        }

        public NativeBattleDuel216Step Run()
        {
            NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
            NativeBattleDuel216SceneServices services = new(index => Models[index], (index, model) => Models[index] = model, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), [3], identity, (current, operation) =>
            {
                Operations.Add(operation);
                return current;
            });
            return NativeBattleDuel216Scene.Advance6960E0(Frame, Frame.Effect.Pools[0], 0, services);
        }
    }
}
