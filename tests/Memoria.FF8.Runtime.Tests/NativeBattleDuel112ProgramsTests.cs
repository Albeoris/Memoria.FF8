using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112ProgramsTests
{
    [Fact]
    public void OverlappingWindowsReadActualBonesAndPublishOnlyTheNativeBasisAndTranslationFields()
    {
        Fixture fixture = new(48);
        NativeBattleDuel112DrawingStep step = NativeBattleDuel112Target.DrawMiddle6CBF90(fixture.Frame, 0, fixture.Inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(new byte[] { 21, 22, 21 }, fixture.Bones);
        NativeBattleModulePoolTask task = step.Frame.Effect.Pools[1].Slots[0];
        Assert.Equal(1u, task.ReadDword(376));
        Assert.Equal(1u, task.ReadDword(648));
        Assert.Equal(1u, task.ReadDword(1472));
        Assert.Equal(unchecked((uint)-30), task.ReadDword(128));
        Assert.Equal(40u, task.ReadDword(132));
        Assert.Equal(50u, task.ReadDword(136));
        Assert.Equal((ushort)0xCCCC, task.ReadWord(90));
        Assert.Equal((ushort)0xCCCC, task.ReadWord(126));
        Assert.Equal((ushort)48, task.ReadWord(12));
        Assert.Empty(step.Ordering.Packets);
        Assert.Empty(fixture.Flashes);
    }

    [Fact]
    public void FirstContactWritesTheActualTargetFlagAfterItsFlashAndAdvancesThePhysicalDrawCursor()
    {
        Fixture fixture = new(52);
        NativeBattleDuel112DrawingStep step = NativeBattleDuel112Target.DrawMiddle6CBF90(fixture.Frame, 0, fixture.Inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal((ushort)0x808Bu, fixture.Enemy.Flags0);
        Assert.Equal(0x1EA0, step.Frame.Effect.DrawArenaOffset);
        Assert.Equal(0x20E1958u, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging.Read(0x2521914, 4)));
        Assert.Single(step.Ordering.Packets);
        Assert.Equal(0x2A1E3C50u, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena!.Read(0x1E8C, 4)));
    }

    [Theory]
    [InlineData(34, 1, 96)]
    [InlineData(38, 1, 96)]
    [InlineData(67, 6, 255)]
    public void CommonFlashesKeepTheNativePhaseAndArguments(short phase, int kind, int strength)
    {
        Fixture fixture = new(phase);
        _ = NativeBattleDuel112Target.DrawMiddle6CBF90(fixture.Frame, 0, fixture.Inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(new[] { (0, 1, kind, strength) }, fixture.Flashes);
    }

    private sealed class Fixture
    {
        public NativeBattleDuel112Frame Frame { get; }
        public NativeBattleDuel112DrawingInputs Inputs { get; }
        public List<byte> Bones { get; } = [];
        public List<(int, int, int, int)> Flashes { get; } = [];
        public NativeBattleGraphicsModel Enemy { get; private set; } = new(3, 0x8083);

        public Fixture(short phase)
        {
            byte[] bytes = new byte[NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address];
            foreach (uint source in new uint[] { 0x12C8020, 0x12CA7B4, 0x12CB40C })
            {
                int relative = checked((int)(source - NativeBattleDuel112Resources.Address));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(relative + 4), 16);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(relative + 20), 100);
            }
            NativeBattleDuel112Resources resources = new(bytes.ToImmutableArray(), [0, 0, 0, 0], [], []);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
            (target, _) = target.Append(0x6CBF90);
            NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(target.Slots[0], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xCC, 0xF48 - 12).ToImmutableArray() });
            task = task.WriteWord(12, unchecked((ushort)phase)).WriteDword(176, 0x1D972C0 + 156 * 3);
            for (int word = 0; word < 9; word++)
            {
                task = task.WriteWord(108 + word * 2, (ushort)(word % 4 == 0 ? 4096 : 0));
            }
            target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, task) });
            NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
            NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target], DrawArenaOffset = 0x1E88 };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Duel112 = new(true, parameters, resources) };
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x25218E8, BitConverter.GetBytes(0x20DFAB8u)).Write(0x2521914, BitConverter.GetBytes(0x20E1940u));
            Frame = new(memory, effect, staging, new(1));
            Frame = NativeBattleDuel112Workspaces.Initialize7016B0(Frame, 0, 372);
            Frame = NativeBattleDuel112Workspaces.Initialize7016B0(Frame, 0, 644);
            Frame = NativeBattleDuel112Workspaces.Initialize7016B0(Frame, 0, 1468);
            NativeBattleGraphicsModel actor = new(0, 3) { Rotation14 = 700 };
            NativeBattleDuel112ActorServices services = new(index => index == 0 ? actor : Enemy, (index, model) =>
            {
                Assert.Equal(3, index);
                Enemy = model;
            }, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
            NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
            Inputs = new(services, (index, bone, fraction) =>
            {
                Assert.Equal(0, index);
                Assert.Equal((short)0, fraction);
                Bones.Add(bone);
                return bone == 21 ? new(-30, 40, 50) : new(60, 70, 80);
            }, view, null!, null!, (a, b, c, d) => Flashes.Add((a, b, c, d)));
        }
    }
}
