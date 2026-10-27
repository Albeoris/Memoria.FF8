using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Controller;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279ControllerTests
{
    [Fact]
    public void Entry_draws_one_CRT_seed_without_resetting_COMMON_or_other_random_owners()
    {
        Harness harness = new();
        var before = Inactive();
        NativeCrtRandomState random = before.Random.Draw(out ushort seed);
        var actual = Initialize(before, Resources(), harness.Inputs());
        Assert.Equal(random, actual.Random);
        Assert.Equal((uint)seed, actual.State.ReadDword(SeedGlobal));
        Assert.Equal((byte)0xA5, actual.Memory.Arena!.Read(0, 1)[0]);
        Assert.Single(actual.Effect.Pools);
        Assert.Null(actual.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(actual, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(ModelAddress(3), actual.State.ReadDword(CasterGlobal));
    }

    [Theory]
    [InlineData(0u, 0x17C7Cu, 1u)]
    [InlineData(1u, 0x7C7Cu, 0u)]
    [InlineData(0x80000000u, 0x7C7Cu, 0u)]
    public void Root_parity_reads_full_DWORD_and_does_not_consume_random_before_activation(uint old, uint offset, uint next)
    {
        Harness harness = new();
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, Read(actual, RootBank).ReadWord(12));
        Assert.Equal(frame.Random, actual.Random);
        Assert.Empty(harness.Operations);
    }

    [Fact]
    public void Linked_reset_writes_only_sentinel_WORDs_and_reuse_hint()
    {
        var frame = Inactive();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(LinkedRowsOffset, LinkedRowCapacity * LinkedRowStride).Write(LinkedRowsOffset, new byte[] { 1, 2, 3, 4 }) } };
        var actual = ResetLinkedRows67F310(frame);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, actual.Memory.Arena!.Read(LinkedRowsOffset, 4));
        for (int index = 0; index < LinkedRowCapacity; index++)
        {
            Assert.Equal(new byte[] { 255, 255 }, actual.Memory.Arena.Read(LinkedRowsOffset + index * LinkedRowStride + 6, 2));
        }
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena.Read(LinkedRowsOffset + 8, 2));
        Assert.Equal(new byte[4], actual.Memory.Arena.Read(LinkedHintOffset, 4));
        var cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleMegaSpark279Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(actual.Memory.Arena.ReadSnapshot(LinkedRowsOffset, 40).Known, cold.Memory.Arena!.ReadSnapshot(LinkedRowsOffset, 40).Known);
    }

    [Fact]
    public void Root_zero_contact_count_keeps_native_division_fault()
    {
        Harness harness = new(Packets());
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        Assert.Throws<DivideByZeroException>(() => AdvanceTask(frame, RootBank, Resources(), harness.Inputs()));
    }

    [Theory]
    [InlineData((ushort)0, (short)1000, (short)0)]
    [InlineData((ushort)16, (short)-1500, (short)64)]
    [InlineData((ushort)31, (short)-3843, (short)124)]
    [InlineData((ushort)70, (short)-3687, (short)120)]
    public void Native_motion_uses_WORD_stores_and_current_model_fields(ushort age, short expectedZ, short expectedPitch)
    {
        Harness harness = new();
        var frame = MainAt(age);
        var actual = AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs());
        Assert.Equal(0, actual.Result);
        Assert.Equal(expectedZ, harness.Models[3].Transform!.Position28.Z);
        Assert.Equal(expectedPitch, harness.Models[3].Rotation12);
        Assert.Equal(0x1000, harness.Models[3].Flags0 & 0x1000);
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual.Frame, Pools[6].Bank).ReadWord(12));
    }

    [Fact]
    public void New_age_eighty_five_clears_only_native_mask_pitch_and_restores_original_Z()
    {
        Harness harness = new();
        harness.Models[3] = harness.Models[3] with { Flags0 = 0x1806 };
        var frame = MainAt(84);
        var actual = AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)85, Read(actual.Frame, Pools[6].Bank).ReadWord(12));
        Assert.Equal((ushort)0x806, harness.Models[3].Flags0);
        Assert.Equal((short)0, harness.Models[3].Rotation12);
        Assert.Equal(new NativeBattleVector(500, 0, 1000), harness.Models[3].Transform!.Position28);
        Assert.Equal((short)123, harness.Models[3].Transform!.Vertical34);
    }

    [Fact]
    public void B_sources_initialize_reverse_and_read_current_row100_bones_twenty_one_eighteen_seventeen()
    {
        Harness harness = new();
        var frame = MainAt(15);
        var actual = AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(new[] { 21, 18, 17 }, harness.Bones.Select(read => read.Bone));
        Assert.All(harness.Bones, read => Assert.Equal(NativeBattleModelResourceRow.Address100(harness.Models[3]), read.Row100));
        for (int index = 0; index < 3; index++)
        {
            var bytes = ReadBytes(actual, Pools[6].Bank + (uint)(0x75C + index * 328), ProgrammeBWorkspaceLength).Read(0, ProgrammeBWorkspaceLength);
            var workspace = new NativeBattleGfAnimationWorkspace(bytes.ToImmutableArray());
            Assert.Equal(ProgrammeBAddress, workspace.ResourceAddress);
            Assert.Equal(1, workspace.Frame);
        }
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void A_source_stops_after_thirty_six_frames_while_B_is_not_advanced_again()
    {
        Harness harness = new();
        var frame = MainAt(24);
        frame = Write(frame, Pools[5].Bank, Read(frame, Pools[5].Bank).WriteWord(12, 0));
        for (int age = 24; age <= 59; age++)
        {
            frame = Write(frame, Pools[6].Bank, Read(frame, Pools[6].Bank).WriteWord(12, (ushort)age));
            frame = AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs()).Frame;
        }
        var workspace = new NativeBattleGfAnimationWorkspace(ReadBytes(frame, Pools[6].Bank + 40, ProgrammeAWorkspaceLength).Read(0, ProgrammeAWorkspaceLength).ToImmutableArray());
        Assert.Equal(ProgrammeAAddress, workspace.ResourceAddress);
        Assert.Equal(36, workspace.Frame);
        Assert.Equal(90u, BinaryPrimitives.ReadUInt32LittleEndian(Resources().ProgrammeA.Data.AsSpan().Slice(0x104)));
        frame = Write(frame, Pools[6].Bank, Read(frame, Pools[6].Bank).WriteWord(12, 60));
        var after = AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(NativeBattleStorageSnapshots.Capture(workspace).Data.ToArray(), ReadBytes(after, Pools[6].Bank + 40, ProgrammeAWorkspaceLength).Read(0, ProgrammeAWorkspaceLength));
    }

    [Fact]
    public void Counted_contact_uses_current_physical_base_and_count_only_at_old_fifty_eight()
    {
        var packets = Packets(0, 1);
        Harness harness = new(packets);
        _ = AdvanceMain(MainAt(57), Pools[6].Bank, Resources(), harness.Inputs());
        Assert.Empty(harness.Operations.OfType<NativeBattleMegaSpark279Contact506BA0>());
        harness.Operations.Clear();
        _ = AdvanceMain(MainAt(58), Pools[6].Bank, Resources(), harness.Inputs());
        var contact = Assert.Single(harness.Operations.OfType<NativeBattleMegaSpark279Contact506BA0>());
        Assert.Equal(0, contact.FirstContactIndex);
        Assert.Equal(2, contact.Count);
    }

    [Theory]
    [InlineData((ushort)0, 0, 0, 0)]
    [InlineData((ushort)40, 1, 2, 128)]
    [InlineData((ushort)48, 1, 0, 128)]
    [InlineData((ushort)50, 1, 0, 64)]
    public void Sound_and_flash_literal_calls_are_not_contact_or_reload_substitutes(ushort age, int flashes, int mode, int alpha)
    {
        Harness harness = new();
        _ = AdvanceMain(MainAt(age), Pools[6].Bank, Resources(), harness.Inputs());
        Assert.Equal(flashes, harness.Operations.OfType<NativeBattleMegaSpark279Flash5712C0>().Count());
        if (flashes != 0)
        {
            var flash = Assert.Single(harness.Operations.OfType<NativeBattleMegaSpark279Flash5712C0>());
            Assert.Equal((0, 1, mode, alpha), (flash.Arg0, flash.Arg1, flash.Arg2, flash.Arg3));
        }
        Assert.Equal(age == 0 ? 1 : 0, harness.Operations.OfType<NativeBattleMegaSpark279Sound501330>().Count());
        Assert.Empty(harness.Operations.OfType<NativeBattleMegaSpark279Contact506BA0>());
    }

    [Fact]
    public void Current_packed_point_uses_actual_row100_and_full_MAC_without_republishing_bones()
    {
        Harness harness = new();
        var frame = MainAt(3);
        byte[] point = Resources().SourcePoints24.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(), 123);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), -456);
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), 789);
        var resources = Resources() with { SourcePoints24 = point.ToImmutableArray() };
        var inputs = harness.Inputs() with { ReadCurrentBoneMatrix = (_, row, bone) =>
        {
            harness.Bones.Add((row, bone));
            return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100000, Y = -80000, Z = 50000 }, null);
        } };
        var actual = ReadCasterPoint680C00(frame, SourcePointsAddress, resources, inputs);
        Assert.Single(harness.Bones);
        Assert.Equal(17, harness.Bones[0].Bone);
        Assert.Equal(NativeBattleModelResourceRow.Address100(harness.Models[3]), harness.Bones[0].Row100);
        Assert.Equal(100123, actual.Point.X);
        Assert.Equal(-80456, actual.Point.Y);
        Assert.Equal(50789, actual.Point.Z);
        Assert.Equal(actual.Point.X, actual.Frame.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Empty(harness.Operations.OfType<NativeBattleMegaSpark279ComposeCaster5095B0>());
        Assert.Empty(harness.Operations.OfType<NativeBattleMegaSpark279RestoreCaster508C90>());
    }

    [Fact]
    public void Root_restores_bones_before_other_programmes_and_retires_with_live_sprite_storage()
    {
        Harness harness = new();
        var frame = MainAt(3);
        var main = frame.Effect.Pools[6];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(6, NativeBattleEffectTaskPool.Initialize(main.DescriptorAddress, main.BankAddress, main.Stride, (ushort)main.Slots.Length)) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        (frame, uint sprite) = Append(frame, Pools[1].Descriptor, 0x6800F0);
        frame = ClearDwords701200(frame, sprite + 12, 2);
        var actual = Advance(frame, Resources(), harness.Inputs());
        Assert.Empty(actual.Effect.Pools);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(0, 2)) & 1);
        Assert.Equal(new[] { typeof(NativeBattleMegaSpark279ComposeCaster5095B0), typeof(NativeBattleMegaSpark279RestoreCaster508C90), typeof(NativeBattleMegaSpark279DrawSprite180) }, harness.Operations.Select(operation => operation.GetType()));
        var cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleMegaSpark279Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(actual.Memory.Arena.Read(0, 20), cold.Memory.Arena!.Read(0, 20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Target_birth_requires_the_current_observed_start_point_WORD(bool presentUnknown)
    {
        Harness harness = new();
        var frame = MainAt(40) with { StackBytes = presentUnknown ? NativeBattleStackBytes.Unknown("selected-root", -0x7C, 0x3C) : null };
        Assert.Throws<NotSupportedException>(() => AdvanceMain(frame, Pools[6].Bank, Resources(), harness.Inputs()));
    }

    [Fact]
    public void Target_birth_retains_the_observed_composed_WORD_through_JSON()
    {
        Harness harness = new();
        var frame = MainAt(40);
        ushort expected = frame.StackBytes!.ReadUInt16(-0x76);
        var cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleMegaSpark279Frame>(NativeBattleTestSnapshots.Json(frame))!;
        cold = cold with { Memory = cold.Memory with { Arena = cold.Memory.Arena!.Write(LinkedRowsOffset + 6, new byte[] { 255, 255 }) } };
        var actual = AdvanceMain(cold, Pools[6].Bank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(actual.Memory.Arena!.Read(LinkedRowsOffset + 6, 2)));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.StackBytes), NativeBattleTestSnapshots.Json(actual.StackBytes));
    }

    private static NativeBattleMegaSpark279Frame MainAt(ushort age)
    {
        var before = Inactive();
        var pools = Pools.Select(profile => NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity)).ToImmutableArray();
        (var main, _) = pools[6].Append(MainCallback);
        (var root, _) = pools[0].Append(RootCallback);
        pools = pools.SetItem(6, main).SetItem(0, root);
        var frame = before with { Effect = before.Effect with { Pools = pools, ReturnedDescriptor = RootDescriptor }, State = before.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(3)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null } };
        var task = Read(frame, Pools[6].Bank).WriteWord(12, age).WriteWord(14, 1).WriteWord(16, 1000).WriteDword(20, ModelAddress(0)).WriteWord(32, 500).WriteWord(34, 0).WriteWord(36, 1000).WriteWord(38, 123);
        frame = Write(frame, Pools[6].Bank, task);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[ProgrammeAWorkspaceLength].ToImmutableArray());
        workspace = workspace.Initialize7016B0(Resources().ProgrammeA, ProgrammeAWorkspaceLength);
        frame = WriteBytes(frame, Pools[6].Bank + 40, NativeBattleEffectArena.Clear().Write(0, NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan()), ProgrammeAWorkspaceLength);
        for (int index = 0; index < 3; index++)
        {
            NativeBattleGfAnimationWorkspace b = new(new byte[ProgrammeBWorkspaceLength].ToImmutableArray());
            b = b.Initialize7016B0(Resources().ProgrammeB, ProgrammeBWorkspaceLength);
            frame = WriteBytes(frame, Pools[6].Bank + (uint)(0x75C + index * 328), NativeBattleEffectArena.Clear().Write(0, NativeBattleStorageSnapshots.Capture(b).Data.AsSpan()), ProgrammeBWorkspaceLength);
        }
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix lastInput = world with { X = 123, Y = -456, Z = 789 };
        ulong storedZ = NativeBattleGfMatrices.StoredZ56C600(world, lastInput.X, lastInput.Y, lastInput.Z);
        return frame with { StackBytes = NativeBattleStackBytes.Unknown("selected-root", -0x7C, 0x3C).StoreUInt64(-0x7C, storedZ, 0x56C6E5, "compose-final-bone") };
    }

    private static NativeBattleMegaSpark279Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, new byte[] { 0xA5 }), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleOriginalEffectState effect = new(279, 278, new(279, Entry, Initializer), new(3, 0, 0x7F, 0, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleMegaSpark279State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleActionPacketsState Packets(params byte[] actors)
    {
        var packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[32 * 20], new byte[49 * 24]);
        (packets, _) = packets.Prepare(new(3, 8, 14, -1, 106, 279, null));
        foreach (byte actor in actors)
        {
            (packets, _) = packets.AppendContact(new(new(actor, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(checked((byte)actors.Length));
        return packets;
    }

    private sealed class Harness
    {
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(actor => new NativeBattleGraphicsModel(actor, 2) { ActiveResourceRow100 = actor, Rotation12 = 0, Transform = new(new(0, 0, 0), new(500, 0, 1000), 123, 777, 4096) }).ToArray();
        public List<NativeBattleMegaSpark279Operation> Operations { get; } = [];
        public List<(uint Row100, int Bone)> Bones { get; } = [];
        private readonly NativeBattleActionPacketsState packets;

        public Harness(NativeBattleActionPacketsState? packets = null)
        {
            this.packets = packets ?? Packets(0);
        }

        public NativeBattleMegaSpark279Inputs Inputs() => new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, model) => Models[Actor(model)], (_, _) => 8, (_, row, bone) =>
        {
            Bones.Add((row, bone));
            return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = bone, Y = bone * 2, Z = bone * 3 }, null);
        }, (current, _, bone, _) => new(current, PointView(new((short)(bone + 100), 200, 300))), (current, _) => new(current, PointView(new(100, 200, 300), 777)), _ => packets, (current, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleMegaSpark279ModelFields fields)
            {
                int actor = Actor(fields.ModelAddress);
                var model = Models[actor];
                model = model with { Flags0 = (ushort)((model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0), Rotation12 = fields.Rotation12 ?? model.Rotation12 };
                if (fields.PositionZ is short z)
                {
                    var transform = model.Transform!;
                    model = model with { Transform = transform with { Position28 = transform.Position28 with { Z = z } } };
                }
                Models[actor] = model;
            }
            return current;
        });
    }

    private static NativeBattleMegaSpark279Resources Resources() => NativeBattleMegaSpark279StateTests.Resources();
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static int Actor(uint model) => checked((int)((model - 0x1D972C0) / 156));
}
