using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274BasicMovesTests
{
    [Theory]
    [InlineData(2, 2784, 1192, 1624, 3500)]
    [InlineData(3, 4944, 2304, 3784, 2628)]
    [InlineData(4, 2124, 0, 964, 3256)]
    public void FirstPhaseUsesOriginalWorkspaceExtentsBoneMapsAndMoveSpecificContactDistance(byte selector, int advance, int workspace, int echo, short z)
    {
        Fixture fixture = new(selector);
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame);
        NativeBattleModulePoolTask target = frame.Effect.Pools[1].Slots[0];
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000u + (uint)advance, BitConverter.ToUInt32(frame.Staging.Read(0x2508434, 4)));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000u + (uint)workspace, target.ReadDword(72));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000u + (uint)echo, target.ReadDword(80));
        Assert.Equal(z, fixture.Models[0].Transform!.Position28.Z);
        Assert.Equal((short)20, fixture.Models[0].Transform!.Position28.Y);
        NativeBattleModulePoolTask history = NativeBattleDuel274ArenaEcho.Read(frame, target.ReadDword(80));
        byte[] bones = selector == 2 ? [13, 17, 21, 14, 18, 22] : selector == 3 ? [9] : [12, 16, 20];
        Assert.Equal(bones.Length, history.ReadByte(16));
        for (int index = 0; index < bones.Length; index++)
        {
            Assert.Equal(index, history.ReadByte(17 + bones[index]));
        }
        if (selector == 2)
        {
            Assert.Equal(32, frame.Effect.Pools[NativeBattleDuel274Banks.Index(frame, 0x2508498)].Slots.Length);
            Assert.Equal(2, frame.Effect.Pools[NativeBattleDuel274Banks.Index(frame, 0x2508488)].Slots.Length);
        }
        else if (selector == 3)
        {
            Assert.Equal(64, frame.Effect.Pools[NativeBattleDuel274Banks.Index(frame, 0x2508498)].Slots.Length);
        }
        else
        {
            Assert.Equal(2, frame.Effect.Pools.Length);
        }
    }

    [Fact]
    public void BooyaFollowsActualBoneDeltaAndRestoresTargetBeforeContactAcrossSnapshot()
    {
        Fixture fixture = new(3);
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame);
        frame = fixture.Advance(At(frame, 9));
        Assert.Equal(0, frame.Effect.Pools[1].Slots[0].ReadWord(102));
        fixture.Bone = new(15, 32, 27);
        frame = fixture.Advance(At(frame, 10));
        Assert.Equal(new NativeBattleVector(105, 212, 297), fixture.Models[3].Transform!.Position28);
        Assert.Throws<NotSupportedException>(() => frame.Effect.Pools[1].Slots[0].ReadWord(102));
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = fixture.Advance(At(frame, 32));
        Assert.Equal(new NativeBattleVector(100, 200, 300), fixture.ContactPosition);
        Assert.Equal(new NativeBattleVector(100, 200, 300), fixture.Models[3].Transform!.Position28);
        Assert.Equal((short)2048, fixture.Models[3].Rotation14);
    }

    private static NativeBattleDuel274Frame At(NativeBattleDuel274Frame frame, ushort counter)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, counter)) })) } };
    }

    private sealed class Fixture
    {
        public NativeBattleDuel274Frame Frame { get; }
        public NativeBattleGraphicsModel[] Models { get; }
        public NativeBattleVector Bone { get; set; } = new(10, 20, 30);
        public NativeBattleVector? ContactPosition { get; private set; }
        private readonly NativeBattleDuel274Resources resources;
        private readonly NativeBattleDuel274MoveInputs inputs;
        private readonly byte selector;
        private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

        public Fixture(byte selector)
        {
            this.selector = selector;
            Models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { Rotation14 = 0, GeometryWorldMatrix = Identity, Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 0, 1000) }).ToArray();
            Models[3] = Models[3] with { Transform = Models[3].Transform! with { Position28 = new(100, 200, 300), Scale38 = 2000 } };
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1);
            (targets, _) = targets.Append(0x689AC0);
            NativeBattleModulePoolTask task = targets.Slots[0].WriteByte(15, selector).WriteDword(20, NativeBattleDuel274Controller.Address(3)).WriteWord(40, 100).WriteWord(42, 200).WriteWord(44, 1000).WriteWord(48, 100).WriteWord(50, 200).WriteWord(52, 300).WriteWord(54, 40);
            targets = NativeBattleTaskFixture.EditPool(targets, data => data with { Slots = targets.Slots.SetItem(0, task) });
            NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [targets]);
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), root.DescriptorAddress) { Pools = [root, targets] };
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(0x2508430, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress)).Write(0x2508434, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000));
            Frame = new(memory, effect, staging, new(1));
            byte[] data = new byte[checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))];
            foreach (uint address in new uint[] { 0x10EA030, 0x10EC7D4, 0x10ED700, 0x10F3808, 0x10F6BE0 })
            {
                int offset = checked((int)(address - NativeBattleDuel274Resources.Address));
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 4), 16);
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 20), 100);
            }
            resources = new(data.ToImmutableArray(), []);
            inputs = new(index => Models[index], (index, model) => Models[index] = model, (_, _) => Bone, (_, _) => Identity, (current, _) => current, (current, _) => current, (current, _) => current, (current, _, _, _, _, _) => current, (current, _) => current, (current, _) =>
            {
                ContactPosition = Models[3].Transform!.Position28;
                return current;
            }, (current, _) => current, (current, _) => current);
        }

        public NativeBattleDuel274Frame Advance(NativeBattleDuel274Frame frame) => selector switch
        {
            2 => NativeBattleDuel274BasicMoves.Advance68A050(frame, 0, resources, Identity, null!, null!, inputs),
            3 => NativeBattleDuel274BasicMoves.Advance68B2F0(frame, 0, resources, Identity, null!, null!, inputs),
            4 => NativeBattleDuel274BasicMoves.Advance68B700(frame, 0, resources, Identity, null!, null!, inputs),
            _ => throw new InvalidOperationException()
        };
    }
}
