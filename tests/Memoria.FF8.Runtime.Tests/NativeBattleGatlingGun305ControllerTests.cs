using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun305ControllerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, PacketBankBytes)]
    public void Paused_main_refreshes_bone_before_pause_and_root_selects_old_parity(int age, int bank)
    {
        Fixture fixture = new();
        NativeBattleGatlingGun305Frame frame = NativeBattleGatlingGun305Controller.Write(fixture.Frame, RootBank, NativeBattleGatlingGun305Controller.Read(fixture.Frame, RootBank).WriteWord(12, (ushort)age));
        frame = frame with { State = frame.State.WriteWord(AnchorGlobal + 6, 0xCAFE) };
        NativeBattleGatlingGun305Frame result = fixture.Step(frame, 0x201);
        Assert.Equal(PacketBase + (uint)bank, result.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)(age + 1), NativeBattleGatlingGun305Controller.Read(result, RootBank).ReadWord(12));
        Assert.Equal((ushort)0, NativeBattleGatlingGun305Controller.Read(result, ChildBank).ReadWord(12));
        Assert.Equal((ushort)10, result.State.ReadWord(AnchorGlobal));
        Assert.Equal((ushort)20, result.State.ReadWord(AnchorGlobal + 2));
        Assert.Equal((ushort)200, result.State.ReadWord(AnchorGlobal + 4));
        Assert.Equal((ushort)0xCAFE, result.State.ReadWord(AnchorGlobal + 6));
        Assert.Single(fixture.Bones);
        Assert.Equal((0, (short)0xF0, (short)0xC00), fixture.Bones[0]);
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Main_age_one_binds_before_sequence_and_advances_once()
    {
        Fixture fixture = new();
        NativeBattleGatlingGun305Frame frame = NativeBattleGatlingGun305Controller.Write(fixture.Frame, ChildBank, NativeBattleGatlingGun305Controller.Read(fixture.Frame, ChildBank).WriteWord(12, 1));
        NativeBattleGatlingGun305Frame result = fixture.Step(frame);
        Assert.Collection(fixture.Operations, operation => Assert.Equal(new NativeBattleGatlingGun305BindCaster(0, BindingSave, 0xCEB5E8), operation), operation => Assert.Equal(new NativeBattleGatlingGun305RingSequence(0, 1), operation), operation => Assert.Equal(new NativeBattleGatlingGun305Stage(192, 0), operation));
        Assert.Equal((ushort)2, NativeBattleGatlingGun305Controller.Read(result, ChildBank).ReadWord(12));
    }

    [Fact]
    public void Contact_is_unconditional_at_old_thirty_and_terminal_stage_is_cleared()
    {
        Fixture fixture = new();
        NativeBattleGatlingGun305Frame frame = NativeBattleGatlingGun305Controller.Write(fixture.Frame, ChildBank, NativeBattleGatlingGun305Controller.Read(fixture.Frame, ChildBank).WriteWord(12, 30));
        frame = fixture.Step(frame);
        Assert.Equal(new NativeBattleGatlingGun305Contact(0), Assert.Single(fixture.Operations.OfType<NativeBattleGatlingGun305Contact>()));
        fixture.Operations.Clear();
        frame = NativeBattleGatlingGun305Controller.Write(frame, ChildBank, NativeBattleGatlingGun305Controller.Read(frame, ChildBank).WriteWord(12, 36));
        frame = fixture.Step(frame);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, fixture.Operations.OfType<NativeBattleGatlingGun305Stage>().Count());
        Assert.All(fixture.Operations.OfType<NativeBattleGatlingGun305Stage>(), operation => Assert.Equal((short)0, operation.Depth));
    }

    [Fact]
    public void First_mesh_draws_twice_through_same_physical_context_before_pause()
    {
        Fixture fixture = new();
        NativeBattleGatlingGun305Frame frame = fixture.Frame;
        (frame, uint address) = NativeBattleGatlingGun305Controller.Append(frame, 0x5A5C50);
        frame = NativeBattleGatlingGun305Controller.Write(frame, address, NativeBattleGatlingGun305Controller.Read(frame, address).WriteWord(12, 1).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 300).WriteWord(0x16, 0x9876).WriteWord(0x18, 0).WriteWord(0x1A, 0).WriteWord(0x1C, 1024));
        NativeBattleGatlingGun305Frame result = fixture.Step(frame, 0x201);
        NativeBattleGatlingGun305DrawStatic88[] draws = fixture.Operations.OfType<NativeBattleGatlingGun305DrawStatic88>().ToArray();
        Assert.Equal(2, draws.Length);
        Assert.Equal(draws[0], draws[1]);
        Assert.Equal(0xCED698u, draws[0].SourceAddress);
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(result.Staging!.Read(draws[0].TemporaryAddress + 0x54, 4)));
        Assert.Equal((ushort)1, NativeBattleGatlingGun305Controller.Read(result, address).ReadWord(12));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Delayed_birth_obeys_countdown_before_RNG_and_preserves_unknown_packed_tail()
    {
        Fixture fixture = new();
        NativeBattleGatlingGun305Frame frame = fixture.WithOnlyChild(0x5A6070);
        frame = NativeBattleGatlingGun305Controller.Write(frame, ChildBank, NativeBattleGatlingGun305Controller.Read(frame, ChildBank).WriteWord(12, 0).WriteWord(14, 1).WriteWord(0x10, 50).WriteWord(0x12, 60).WriteWord(0x14, 70).MarkPayloadUnknown(0x16, 2).WriteWord(0x18, 3));
        uint random = frame.Random.Seed;
        frame = fixture.Step(frame);
        Assert.Equal(random, frame.Random.Seed);
        Assert.Equal((ushort)0, NativeBattleGatlingGun305Controller.Read(frame, ChildBank).ReadWord(14));
        frame = fixture.Step(frame, 0x201);
        Assert.Equal(random, frame.Random.Seed);
        frame = fixture.Step(frame);
        Assert.NotEqual(random, frame.Random.Seed);
        NativeBattleModulePoolTask mesh = Assert.Single(frame.Effect.Pools[1].Slots.Where(task => task.Callback == 0x5A6160 && (task.Flags & 1) != 0));
        Assert.Equal((ushort)1280, mesh.ReadWord(0x1C));
        Assert.Equal((ushort)1, mesh.ReadWord(12));
        Assert.Throws<NotSupportedException>(() => mesh.ReadWord(0x16));
        Assert.Equal(0, NativeBattleGatlingGun305Controller.Read(frame, ChildBank).Flags & 1);
    }

    [Fact]
    public void Active_snapshot_reproduces_next_callbacks_and_required_memory_outputs()
    {
        Fixture first = new();
        NativeBattleGatlingGun305Frame frame = NativeBattleGatlingGun305Controller.Write(first.Frame, ChildBank, NativeBattleGatlingGun305Controller.Read(first.Frame, ChildBank).WriteWord(12, 1));
        NativeBattleGatlingGun305Frame restored = JsonSerializer.Deserialize<NativeBattleGatlingGun305Frame>(JsonSerializer.Serialize(frame))!;
        Fixture second = new();
        Assert.Equal(JsonSerializer.Serialize(first.Step(frame)), JsonSerializer.Serialize(second.Step(restored)));
        Assert.Equal(JsonSerializer.Serialize(first.Operations), JsonSerializer.Serialize(second.Operations));
    }

    private sealed class Fixture
    {
        public NativeBattleGatlingGun305Frame Frame { get; }
        public List<NativeBattleGatlingGun305Operation> Operations { get; } = [];
        public List<(int Actor, short Bone, short Distance)> Bones { get; } = [];
        private readonly NativeBattleActionPacketsState packets;
        private readonly NativeBattleGatlingGun305Resources resources;
        public Fixture()
        {
            packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 305, null));
            (packets, _) = packets.AppendContact(new(new(3, 1, 0, 4, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            (root, _) = root.Append(RootCallback);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, _) = children.Append(MainCallback);
            NativeBattleOriginalEffectState effect = new(305, 305, new(305, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(305) with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            Frame = new(memory, effect, new(1), NativeBattleGatlingGun305State.FromColdProcess().WriteDword(TargetGlobal, 3).WriteDword(CasterGlobal, 0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            resources = new(new("fixture", []), new("fixture", []), new(0, 0xCED698, []), new(0, 0xCED998, []), new(0, 0xCEDC60, []), new(0, 0xCED358, 0, 0, []), new(0, 0xCED15C, 0, 0, []), new(0, 0xCED504, 0, 0, []), 0x61AD6);
        }
        public NativeBattleGatlingGun305Frame WithOnlyChild(uint callback) => NativeBattleGatlingGun305Controller.Write(Frame, ChildBank, NativeBattleGatlingGun305Controller.Read(Frame, ChildBank) with { Callback = callback });
        public NativeBattleGatlingGun305Frame Step(NativeBattleGatlingGun305Frame frame, uint flags = 0) => NativeBattleGatlingGun305Controller.Advance(frame, resources, new(flags, packets, actor => new(actor, 2) { Rotation14 = 0, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770(), GeometryBounds52 = new(new(-10, -800, -10), new(10, 0, 10)) }, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0), (current, actor, bone, distance) =>
        {
            Bones.Add((actor, bone, distance));
            return new(current, new(10, 20, 300));
        }, (current, _) => new(current, new(100, -200, 500, 99)), (current, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleGatlingGun305DrawStatic88 draw)
            {
                return current with { Staging = current.Staging!.Write(draw.TemporaryAddress + 0x54, BitConverter.GetBytes(0x12345678u)) };
            }
            if (operation is NativeBattleGatlingGun305DrawSprite180 sprite)
            {
                return current with { Staging = current.Staging!.Write(sprite.TemporaryAddress + 0x28, BitConverter.GetBytes((short)-1)) };
            }
            return current;
        }));
    }
}
