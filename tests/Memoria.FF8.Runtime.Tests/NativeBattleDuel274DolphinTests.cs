using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274DolphinTests
{
    [Fact]
    public void FirstPhasePublishesTwoActualWorkspacesAndReassignsParticleDescriptor()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool old = NativeBattleEffectTaskPool.Initialize(0x2508498, NativeBattleEffectNodeMemory.ArenaAddress + 0x9000, 36, 64);
        (old, _) = old.Append(0x68B140);
        old = old with { Slots = old.Slots.SetItem(0, old.Slots[0].WriteWord(26, 3).MarkPayloadUnknown(34, 2)) };
        NativeBattleDuel274Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.Add(old) } };
        frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [old]) };
        frame = fixture.Advance(frame);
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[0];

        Assert.Equal(new NativeBattleVector(100, 20, 3500), fixture.Models[0].Transform!.Position28);
        Assert.Equal(0x20DFAB8u + 0x1000, task.ReadDword(72));
        Assert.Equal(0x20DFAB8u + 0x1000 + 224, task.ReadDword(76));
        Assert.Equal(0x20DFAB8u + 0x1000 + 1128, frame.Effect.Pools[2].BankAddress);
        Assert.Equal(0x20DFAB8u + 0x1000 + 3432, BitConverter.ToUInt32(frame.Staging.Read(0x2508434, 4)));
        Assert.Equal(0x10FE310u, BitConverter.ToUInt32(frame.Memory.Arena!.Read(0x1000, 4)));
        Assert.Equal(1, BitConverter.ToInt32(frame.Memory.Arena.Read(0x1004, 4)));
        Assert.Equal(0x110198Cu, BitConverter.ToUInt32(frame.Memory.Arena.Read(0x1000 + 224, 4)));
        Assert.Equal(0, BitConverter.ToInt32(frame.Memory.Arena.Read(0x1000 + 228, 4)));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(frame.Memory.Arena.Read(0x9000, 2)));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x9022, 2));
        Assert.All(frame.Effect.Pools[2].Slots, particle => Assert.Equal(0, particle.Flags));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x1000 + 1128, 1));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, frame.Staging.Read(0x2508452, 2));
        NativeBattleDuel274Frame restored = JsonSerializer.Deserialize<NativeBattleDuel274Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(frame.Memory.Arena.Read(0x1000, 224), restored.Memory.Arena!.Read(0x1000, 224));
    }

    [Fact]
    public void PhaseThirtyBirthsAfterExistingParticlesAdvanceAndConsumesExactRandomDraws()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame);
        frame = At(frame, 30);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 48; draw++)
        {
            expected = expected.Draw(out _);
        }
        frame = fixture.Advance(frame);
        Assert.Equal(16, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(frame.Effect.Pools[2].Slots.Take(16), task => Assert.Equal(0, task.ReadWord(18)));
        Assert.Equal(expected, frame.Random);
        Assert.Equal(new[] { "bone241" }, fixture.Calls);
        fixture.Calls.Clear();
        frame = fixture.Advance(At(frame, 31));
        Assert.Equal(16, fixture.Calls.Count(call => call == "draw0"));
        Assert.Equal("flash", fixture.Calls[^1]);
        Assert.All(frame.Effect.Pools[2].Slots.Take(16), task => Assert.Equal(1, task.ReadWord(18)));
    }

    [Fact]
    public void ImpactPublishesContactBeforeChangingNativeWord32AndSoundUsesItsResourceAddress()
    {
        Fixture fixture = new();
        NativeBattleDuel274Frame frame = fixture.Advance(fixture.Frame);
        frame = fixture.Advance(At(frame, 24));
        Assert.Equal(new[] { "sound110BE68" }, fixture.Calls);
        fixture.Calls.Clear();
        short before = fixture.Models[3].Transform!.Position28.Z;
        frame = fixture.Advance(At(frame, 33));
        Assert.Equal(new[] { "bone241", "contact2" }, fixture.Calls);
        Assert.Equal(before, fixture.ContactTargetZ);
        Assert.Equal(unchecked((short)(before - 200)), fixture.Models[3].Transform!.Position28.Z);
        Assert.Equal(200, fixture.Models[3].Transform!.Position28.Y);
        Assert.Equal((ushort)33, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    private static NativeBattleDuel274Frame At(NativeBattleDuel274Frame frame, ushort phase)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, phase)) };
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }

    private sealed class Fixture
    {
        public NativeBattleDuel274Frame Frame { get; }
        public NativeBattleDuel274Resources Resources { get; }
        public NativeBattleGraphicsModel[] Models { get; }
        public List<string> Calls { get; } = [];
        public short? ContactTargetZ { get; private set; }
        private readonly NativeBattleDuel274DolphinInputs inputs;
        private static readonly NativeBattleActionMatrix View = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

        public Fixture()
        {
            Models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 0, 1000) }).ToArray();
            Models[3] = Models[3] with { Transform = Models[3].Transform! with { Position28 = new(100, 200, 300), Scale38 = 2000 } };
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1);
            (targets, _) = targets.Append(0x689AC0);
            NativeBattleModulePoolTask task = targets.Slots[0].WriteByte(14, 2).WriteByte(15, 6).WriteDword(20, NativeBattleDuel274Controller.Address(3)).WriteWord(40, 100).WriteWord(42, 200).WriteWord(44, 1000);
            targets = targets with { Slots = targets.Slots.SetItem(0, task) };
            NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0) { Parameter80 = 6 };
            NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), parameters, root.DescriptorAddress) { Pools = [root, targets] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
            memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, [targets]);
            NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2508430, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress)).Write(0x2508434, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x1000)).Write(0x2508452, [0xAA, 0xBB]);
            Frame = new(memory, effect, staging, new(1));
            byte[] raw = new byte[checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))];
            foreach (uint source in new uint[] { 0x10FE310, 0x110198C })
            {
                int offset = checked((int)(source - NativeBattleDuel274Resources.Address));
                BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset + 4), 16);
                BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset + 20), 100);
            }
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(checked((int)(0x110E3B8 - NativeBattleDuel274Resources.Address)) + 3 * 8 + 4), 14);
            Resources = new(raw.ToImmutableArray(), []);
            inputs = new(actor => Models[actor], (actor, model) => Models[actor] = model, bone =>
            {
                Calls.Add($"bone{bone}");
                return new(10, 20, 30);
            }, (current, task, sprite, counter, size, depth) =>
            {
                Calls.Add($"draw{counter}");
                return current;
            }, (current, _) => current, (current, sequence) =>
            {
                Calls.Add($"contact{sequence}");
                ContactTargetZ = Models[3].Transform!.Position28.Z;
                return current;
            }, current =>
            {
                Calls.Add("flash");
                return current;
            }, (current, address) =>
            {
                Calls.Add($"sound{address:X}");
                return current;
            });
        }

        public NativeBattleDuel274Frame Advance(NativeBattleDuel274Frame frame) => NativeBattleDuel274Dolphin.Advance68C7B0(frame, 0, Resources, View, null!, null!, inputs);
    }
}
