using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMeltEye158Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltEye158ControllerTests
{
    [Fact]
    public void Initialize_preserves_warm_slot_suffixes_and_registers_camera_before_upload()
    {
        Fixture f = new();
        NativeBattleMeltEye158State state = f.Frame.State.WriteDword(SlotBankA, 7).WriteWord(SlotBankA + 6, 1234).WriteDword(SlotBankB, 9).WriteWord(SlotBankB + 6, 5678).WriteDword(CursorGlobal, 0x11111111);
        NativeBattleMeltEye158Frame initialized = NativeBattleMeltEye158Controller.Initialize(f.Frame with { State = state }, f.Resources, f.Inputs);
        Assert.Equal(0u, initialized.State.ReadDword(SlotBankA));
        Assert.Equal(0u, initialized.State.ReadDword(SlotBankB));
        Assert.Equal((ushort)1234, initialized.State.ReadWord(SlotBankA + 6));
        Assert.Equal((ushort)5678, initialized.State.ReadWord(SlotBankB + 6));
        Assert.Equal(0x11111111u, initialized.State.ReadDword(CursorGlobal));
        Assert.Equal(new ushort[] { 16, 36 }, initialized.Effect.Pools.Select(pool => pool.Stride).ToArray());
        Assert.Equal(100, initialized.Effect.Pools[1].Slots.Length);
        Assert.Equal(SpawnerCallback, initialized.Effect.Pools[1].Slots[0].Callback);
        Assert.Equal(4u, initialized.State.ReadDword(CasterGlobal));
        Assert.Equal(3u, initialized.State.ReadDword(TargetGlobal));
        Assert.Equal(f.Resources.Camera, initialized.Memory.Textures!.Animation!.Source);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0x8000)]
    public void Root_uses_native_old_BYTE_parity_and_increments_even_on_retirement(int age, int packetOffset)
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Set(f.Frame, 12, 42);
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, unchecked((ushort)age))) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root) } };
        NativeBattleMeltEye158Frame result = NativeBattleMeltEye158Controller.Advance(frame, f.Resources, f.Inputs);
        Assert.Equal(PacketBase + (uint)packetOffset, result.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)(age + 1), result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Paused_spawner_does_not_read_unknown_age_or_consume_random()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = NativeBattleMeltEye158Controller.Write(f.Frame, ChildBank, f.Task(f.Frame).MarkPayloadUnknown(12, 2));
        var result = NativeBattleMeltEye158Controller.AdvanceSpawner(frame, ChildBank, f.Resources, f.Inputs with { GraphicsFlags = 0x201 });
        Assert.Same(frame, result.Frame);
        Assert.Equal(0, result.Result);
        Assert.Empty(f.Bones);
        Assert.Empty(f.Operations);
    }

    [Fact]
    public void Spawner_old1_appends_path_then_two_forks_and_preserves_anchor_W()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Set(f.Frame, 12, 1);
        frame = NativeBattleMeltEye158Controller.Write(frame, ChildBank, f.Task(frame).WriteWord(22, 0xABCD));
        NativeCrtRandomState random = frame.Random.Draw(out ushort roll);
        (frame, _) = NativeBattleMeltEye158Controller.AdvanceSpawner(frame, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(random, frame.Random);
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(new uint[] { SpawnerCallback, PathCallback, ForkEmitterCallback, ForkEmitterCallback }, children.Select(task => task.Callback).ToArray());
        Assert.Equal((ushort)(roll % 6 + 1), children[1].ReadWord(24));
        Assert.Equal((ushort)1, children[2].ReadWord(14));
        Assert.Equal((ushort)2, children[3].ReadWord(14));
        Assert.Equal((ushort)7, children[2].ReadWord(24));
        Assert.Equal((ushort)9, children[3].ReadWord(24));
        Assert.Equal((ushort)0xABCD, children[2].ReadWord(22));
        Assert.Equal((4, (byte)0xF0, (short)0), Assert.Single(f.Bones));
        Assert.Single(f.Operations.OfType<NativeBattleMeltEye158Sound>());
    }

    [Theory]
    [InlineData(39, 0)]
    [InlineData(40, 1)]
    [InlineData(41, 0)]
    public void Contact_is_bulk_first_selected_header_only_at_old40(int age, int expected)
    {
        Fixture f = new();
        NativeBattleMeltEye158Controller.AdvanceSpawner(f.Set(f.Frame, 12, unchecked((ushort)age)), ChildBank, f.Resources, f.Inputs);
        Assert.Equal(expected, f.Operations.OfType<NativeBattleMeltEye158BulkContact>().Count());
        if (expected != 0)
        {
            Assert.Equal(new NativeBattleMeltEye158BulkContact(1, 2), Assert.Single(f.Operations.OfType<NativeBattleMeltEye158BulkContact>()));
        }
    }

    [Fact]
    public void Path_old0_consumes_four_random_values_publishes_curve_and_preserves_sample_W()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Path(0);
        frame = frame with { State = frame.State.WriteView(CurveBank + 0x38, NativeBattleEffectArena.Clear().MarkUnknown(0, 384), 384) };
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out _);
        }
        (frame, int result) = NativeBattleMeltEye158Controller.AdvancePath(frame, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(0, result);
        Assert.Equal(random, frame.Random);
        Assert.Equal(new NativeBattleVector(200, 200, 300), NativeBattleMeltEye158Controller.ReadVector(frame, CurveBank));
        Assert.Equal(NativeBattleMeltEye158Controller.ReadVector(frame, CurveBank), NativeBattleMeltEye158Controller.ReadVector(frame, CurveBank + 0x38));
        Assert.Equal(NativeBattleMeltEye158Controller.ReadVector(frame, CurveBank + 8), NativeBattleMeltEye158Controller.ReadVector(frame, CurveBank + 0x38 + 47 * 8));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(CurveBank + 0x38 + 6));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(CurveBank + 0x38 + 47 * 8 + 6));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(200, BinaryPrimitives.ReadInt32LittleEndian(frame.Staging.Read(NativeBattleTemporaryMemory.Address, 4)));
        Assert.Equal(0x12345678u, frame.Memory.ScriptedColorRegisters!.Material);
        Assert.Same(f.Frame.Memory.GeometryRegisters, frame.Memory.GeometryRegisters);
    }

    [Theory]
    [InlineData(1, HeadMeshCallback, 1)]
    [InlineData(7, StreamCallback, 1)]
    [InlineData(16, ImpactSpriteCallback, 5)]
    public void Path_births_use_native_ages_and_random_counts(int age, uint callback, int draws)
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Path(age);
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            random = random.Draw(out _);
        }
        (frame, _) = NativeBattleMeltEye158Controller.AdvancePath(frame, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(random, frame.Random);
        Assert.Equal(callback, frame.Effect.Pools[1].Slots[1].Callback);
        if (age == 16)
        {
            Assert.Equal(ImpactMeshCallback, frame.Effect.Pools[1].Slots[2].Callback);
            Assert.Equal(frame.Effect.Pools[1].Slots[1].ReadDword(20), frame.Effect.Pools[1].Slots[2].ReadDword(20));
        }
    }

    [Fact]
    public void Path_reads_ordinal_before_pause_but_requires_no_curve_or_W_bytes_while_paused()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Path(0) with { State = NativeBattleMeltEye158State.FromUnknown() };
        var paused = NativeBattleMeltEye158Controller.AdvancePath(frame, ChildBank, f.Resources, f.Inputs with { GraphicsFlags = 0x201 });
        Assert.Same(frame, paused.Frame);
        frame = NativeBattleMeltEye158Controller.Write(frame, ChildBank, f.Task(frame).MarkPayloadUnknown(14, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleMeltEye158Controller.AdvancePath(frame, ChildBank, f.Resources, f.Inputs with { GraphicsFlags = 0x201 }));
    }

    [Fact]
    public void Path_terminal_and_JSON_next_preserve_partial_state_and_current_R_T_owners()
    {
        Fixture f = new();
        NativeBattleMeltEye158Frame frame = f.Path(29);
        NativeBattleMeltEye158Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMeltEye158Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var first = NativeBattleMeltEye158Controller.AdvancePath(frame, ChildBank, f.Resources, f.Inputs);
        var second = NativeBattleMeltEye158Controller.AdvancePath(restored, ChildBank, f.Resources, f.Inputs);
        Assert.Equal(2, first.Result);
        Assert.Equal((ushort)30, f.Task(first.Frame).ReadWord(12));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Frame), NativeBattleTestSnapshots.Json(second.Frame));
    }

    private sealed class Fixture
    {
        public NativeBattleMeltEye158Frame Frame { get; }
        public NativeBattleMeltEye158Resources Resources { get; }
        public NativeBattleMeltEye158Inputs Inputs { get; }
        public List<NativeBattleMeltEye158Operation> Operations { get; } = [];
        public List<(int, byte, short)> Bones { get; } = [];
        public Fixture()
        {
            byte[] camera = new byte[CameraLength];
            BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
            Resources = new(new("FF8_EN:DAFBF8", camera.ToImmutableArray()), Sprite(SpriteAAddress, 0x224, 16), Sprite(SpriteBAddress, 0x1AC, 16), Sprite(SpriteCAddress, 0xDC, 8), new(0, MeshAAddress, new byte[0x3F0].ToImmutableArray()), new(0, MeshBAddress, new byte[0x270].ToImmutableArray()), Enumerable.Range(0, 11).Select(index => new NativeBattleModuleAnchor((short)(index * 100), 0, 0, 0x1234)).ToImmutableArray(), 0x61AC0);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
            (root, int rootSlot) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(rootSlot, root.Slots[rootSlot].WriteWord(12, 0)) });
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
            (children, int slot) = children.Append(SpawnerCallback);
            children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteBytes(12, new byte[24])) });
            NativeBattleOriginalEffectState effect = new(158, 157, new(158, Entry, Initializer), new(4, 0, 1, 1, 0, 0), null) { Pools = [root, children], ReturnedDescriptor = RootDescriptor, ResourcePointers = [new(InitializerGlobal, 0x30000)] };
            byte[] tim = new byte[546];
            BitConverter.GetBytes(0x10).CopyTo(tim, 0);
            BitConverter.GetBytes(9).CopyTo(tim, 4);
            BitConverter.GetBytes(524).CopyTo(tim, 8);
            BitConverter.GetBytes((short)256).CopyTo(tim, 16);
            BitConverter.GetBytes((short)1).CopyTo(tim, 18);
            BitConverter.GetBytes(14).CopyTo(tim, 532);
            BitConverter.GetBytes((short)1).CopyTo(tim, 540);
            BitConverter.GetBytes((short)1).CopyTo(tim, 542);
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) }, effect, new(123), NativeBattleMeltEye158State.FromColdProcess().WriteDword(CasterGlobal, 4)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            for (int header = 0; header < 2; header++)
            {
                (packets, _) = packets.Prepare(new(4, 8, 13, 0, 36, 158, null));
                for (int contact = 0; contact < header + 1; contact++)
                {
                    (packets, _) = packets.AppendContact(new(new((byte)(header == 0 ? 0 : 3), 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
                }
                (packets, _) = packets.Publish(checked((byte)(header + 1)));
            }
            NativeBattlePackedMatrix32 rotationOnly = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            rotationOnly = rotationOnly with { Known = Enumerable.Range(0, 32).Select(index => index < 18).ToImmutableArray() };
            Inputs = new(0, packets, (frame, actor, selector, distance) =>
            {
                Bones.Add((actor, selector, distance));
                return new(frame with { Memory = frame.Memory with { ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x12345678 } } }, new(100, 200, 300));
            }, (frame, _) => new(frame, new(1000, 0, 2000)), (_, _) => rotationOnly, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            });
        }
        public NativeBattleModulePoolTask Task(NativeBattleMeltEye158Frame frame) => NativeBattleMeltEye158Controller.Read(frame, ChildBank);
        public NativeBattleMeltEye158Frame Set(NativeBattleMeltEye158Frame frame, int offset, ushort value) => NativeBattleMeltEye158Controller.Write(frame, ChildBank, Task(frame).WriteWord(offset, value));
        public NativeBattleMeltEye158Frame Path(int age)
        {
            NativeBattleMeltEye158Frame frame = NativeBattleMeltEye158Controller.Write(Frame, ChildBank, NativeBattleTaskFixture.EditTask(Task(Frame), data => data with { Callback = PathCallback }));
            return Set(Set(Set(frame, 12, unchecked((ushort)age)), 14, 0), 24, 1);
        }
        private static NativeBattleModuleResource Sprite(uint address, int length, ushort frames)
        {
            byte[] bytes = new byte[length];
            BitConverter.GetBytes(frames).CopyTo(bytes, 8);
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }
}
