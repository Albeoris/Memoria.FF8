using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect303Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect303ControllerTests
{
    [Fact]
    public void Entry_uses_selected_headers_first_physical_target_and_preserves_retained_owners()
    {
        var harness = new Harness();
        var frame = Inactive();
        var actual = Initialize(frame, Resources(), harness.Inputs());
        Assert.Equal(ModelAddress(3), actual.State.ReadDword(CasterGlobal));
        Assert.Equal(ModelAddress(4), actual.State.ReadDword(TargetGlobal));
        Assert.Equal(new[] { "copy", "select13", "enter" }, harness.Log);
        Assert.Equal(new NativeBattleEffect303CopyPackedPosition(ModelAddress(3), ModelAddress(4)), harness.Operations[0]);
        Assert.Equal(harness.Models[3].Transform!.Position28, harness.Models[4].Transform!.Position28);
        Assert.Equal(harness.Models[3].Transform!.Vertical34, harness.Models[4].Transform!.Vertical34);
        Assert.NotEqual(harness.Models[3].Transform!.Angle36, harness.Models[4].Transform!.Vertical34);
        Assert.Single(actual.Effect.Pools);
        Assert.Null(actual.State.RootBankStorage);
        Assert.Equal(new byte[] { 0xA5 }, actual.Memory.Arena!.Read(0, 1));
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(0xABCDu, actual.State.ReadDword(SeedGlobal));
        Assert.Equal(new byte[8], TaskView(Read(actual, RootBank), 12, 8).Read(0, 8));
    }

    [Fact]
    public void Entry_rejects_a_missing_physical_first_contact_instead_of_substituting_target_mask()
    {
        var harness = new Harness();
        harness.Packets = harness.Packets with { Headers = harness.Packets.Headers.SetItem(1, harness.Packets.Headers[1] with { FirstContactIndex = null }) };
        Assert.Throws<NotSupportedException>(() => Initialize(Inactive(), Resources(), harness.Inputs()));
        Assert.Empty(harness.Operations);
    }

    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0x80000000u, FirstPacketOffset, 0u)]
    public void Root_parity_tests_entire_DWORD_without_touching_current_COMMON(uint before, int offset, uint next)
    {
        var harness = new Harness();
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        harness.Operations.Clear();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, before));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(ArenaAddress + (uint)offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal((ushort)1, Read(actual, RootBank).ReadWord(12));
        Assert.Empty(harness.Operations);
        Assert.Equal(new byte[] { 0xA5 }, actual.Memory.Arena!.Read(0, 1));
    }

    [Fact]
    public void First_root_creates_exact_children_main_then_programme_particles_sprites()
    {
        var harness = new Harness();
        var frame = Initialize(Inactive(), Resources(), harness.Inputs());
        harness.Log.Clear();
        harness.Operations.Clear();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(new[] { 20, 10176, 312, 36, 20 }, actual.Effect.Pools.Select(pool => (int)pool.Stride));
        Assert.Equal((ushort)1, Read(actual, Pools[1].Bank).ReadWord(12));
        Assert.Equal(4, actual.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(32, actual.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(actual.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Equal((ushort)1, task.ReadWord(18)));
        Assert.Equal(new[] { 24, 9, 14, 19 }, actual.Effect.Pools[2].Slots.Select(task => (int)task.ReadWord(30)));
        Assert.Equal(new[] { 5000, 4096, 4096, 3000 }, actual.Effect.Pools[2].Slots.Select(task => (int)task.ReadWord(22)));
        Assert.Equal("vibration", harness.Log[0]);
        Assert.True(harness.Log.IndexOf("io-init") < harness.Log.IndexOf("restore"));
        Assert.True(harness.Log.IndexOf("sound") < harness.Log.IndexOf("static"));
        Assert.True(harness.Log.LastIndexOf("static") < harness.Log.IndexOf("sprite"));
        Assert.DoesNotContain("io-advance", harness.Log);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 96; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Random);
        Assert.Equal(0xABCDu, actual.State.ReadDword(SeedGlobal));
    }

    [Theory]
    [InlineData(22, false, false)]
    [InlineData(23, false, false)]
    [InlineData(24, true, false)]
    [InlineData(37, true, true)]
    [InlineData(39, true, false)]
    [InlineData(40, false, false)]
    public void Main_body_window_and_vibration_use_exact_old_age(int age, bool body, bool stop)
    {
        var harness = new Harness();
        var frame = Active((ushort)age);
        var actual = AdvanceMain(frame, Pools[1].Bank, Resources(), harness.Inputs());
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)(age + 1), Read(actual.Frame, Pools[1].Bank).ReadWord(12));
        Assert.Equal(body ? 1 : 0, harness.Operations.OfType<NativeBattleEffect303DrawBody188>().Count());
        Assert.Equal(stop ? 1 : 0, harness.Operations.OfType<NativeBattleEffect303StopVibration4A2940>().Count());
        if (body)
        {
            var draw = harness.Operations.OfType<NativeBattleEffect303DrawBody188>().Single();
            Assert.Equal(ModelAddress(4), draw.ModelAddress);
            Assert.Equal(new byte[] { 192, 2, 0, 1, 128, 0, 128, 0 }, draw.TextureRectangle8.Read(0, 8));
            Assert.Equal((ushort)0x3DD4, draw.Clut);
            Assert.Equal(age * 2, draw.VOffset);
            Assert.Equal(age * 4, draw.UOffset);
            byte rgb = unchecked((byte)(NativeBattleCameraMath.Sine((age - 24) * 128) >> 6));
            Assert.Equal(new byte[] { rgb, rgb, rgb, 0 }, draw.Color4.Read(0, 4));
            Assert.Equal((ushort)4, (ushort)(harness.Models[4].Flags0 & 4));
        }
        else if (age == 40)
        {
            Assert.Equal((ushort)0, (ushort)(harness.Models[4].Flags0 & 4));
        }
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(0xABCDu, actual.Frame.State.ReadDword(SeedGlobal));
    }

    [Theory]
    [InlineData(41)]
    [InlineData(42)]
    [InlineData(32767)]
    public void Main_queue_is_type1_at_the_current_cursor_and_precedes_all_work(int age)
    {
        var harness = new Harness();
        var frame = Active((ushort)age);
        var actual = AdvanceMain(frame, Pools[1].Bank, Resources(), harness.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)age, Read(actual.Frame, Pools[1].Bank).ReadWord(12));
        Assert.Equal(new NativeBattleEffect303QueueFile5341D0(360, ArenaAddress + FirstPacketOffset + 124, 1), Assert.Single(harness.Operations));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(JsonSerializer.Serialize(frame.Staging), JsonSerializer.Serialize(actual.Frame.Staging));
    }

    [Fact]
    public void First_programme_window_ends_after_old21_even_when_workspace_would_still_run()
    {
        var harness = new Harness();
        var before = Active(21);
        uint main = Pools[1].Bank;
        before = WriteBytes(before, main + MainProgrammeWorkspaceOffset + 4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0)), 4);
        var first = AdvanceMain(before, main, Resources(), harness.Inputs()).Frame;
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(first, main + MainProgrammeWorkspaceOffset + 4, 4).Read(0, 4)));
        var second = AdvanceMain(first, main, Resources(), harness.Inputs()).Frame;
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(second, main + MainProgrammeWorkspaceOffset + 4, 4).Read(0, 4)));
    }

    [Fact]
    public void Full_burst_pools_consume_no_CRT_or_muzzle_on_second_birth_age()
    {
        var harness = new Harness();
        var frame = Active(1);
        var pool = frame.Effect.Pools[3];
        for (int index = 0; index < 64; index++)
        {
            (pool, _) = pool.Append(0x66C100);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(3, pool) } };
        var actual = AdvanceMain(frame, Pools[1].Bank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(5, harness.Operations.Count);
        Assert.All(harness.Operations, operation => Assert.IsType<NativeBattleEffect303DrawMesh7043B0>(operation));
        Assert.Empty(harness.Bones);
    }

    [Fact]
    public void Eight_sprites_use_bone39_and_do_not_add_muzzle_Y_to_the_negative_random_Y()
    {
        var harness = new Harness();
        var frame = Active(6);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort rx).Draw(out ushort ry).Draw(out ushort rz);
        var actual = AdvanceMain(frame, Pools[1].Bank, Resources(), harness.Inputs()).Frame;
        Assert.Equal(new[] { 39 }, harness.Bones);
        Assert.Equal(8, actual.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        var first = Read(actual, Pools[4].Bank);
        Assert.Equal(unchecked((ushort)(564 + (rx & 511) - 256)), first.ReadWord(12));
        Assert.Equal(unchecked((ushort)-(ry & 511)), first.ReadWord(14));
        Assert.Equal(unchecked((ushort)(3942 + (rz & 511) - 256)), first.ReadWord(16));
        Assert.Equal((ushort)0, first.ReadWord(18));
        for (int index = 3; index < 24; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Random);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Root_waits_for_actual_IO_after_main_only_and_preserves_live_children(bool busy)
    {
        var harness = new Harness { Busy = busy };
        var frame = Active(41);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 55).WriteByte(15, 1));
        (var children, _) = frame.Effect.Pools[4].Append(0x66C080);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, children) } };
        frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(12, 1).WriteWord(14, 2).WriteWord(16, 3).WriteWord(18, 0));
        var actual = AdvanceTask(frame, RootBank, Resources(), harness.Inputs());
        Assert.Equal(busy ? 0 : 2, actual.Result);
        Assert.Equal((ushort)55, Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Equal((ushort)1, Read(actual.Frame, Pools[4].Bank).ReadWord(18));
        Assert.True(harness.Log.IndexOf("sprite") < harness.Log.IndexOf("io-advance"));
        Assert.True(harness.Log.IndexOf("io-advance") < harness.Log.IndexOf("poll0"));
        Assert.Equal(busy ? 0 : 1, harness.Operations.OfType<NativeBattleEffect303Activate487670>().Count());
        if (!busy)
        {
            Assert.Equal(new NativeBattleEffect303Activate487670(ModelAddress(4)), harness.Operations.OfType<NativeBattleEffect303Activate487670>().Single());
            Assert.Equal("fields4", harness.Log[^1]);
        }
    }

    [Fact]
    public void Completed_root_detaches_remaining_child_bytes_with_current_JSON_knownness()
    {
        var harness = new Harness { Busy = false };
        var frame = Active(41);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 55).WriteByte(15, 1));
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).MarkPayloadUnknown(0x400, 1));
        (var sprites, _) = frame.Effect.Pools[4].Append(0x66C080);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, sprites) } };
        frame = Write(frame, Pools[4].Bank, Read(frame, Pools[4].Bank).WriteWord(12, 1).WriteWord(14, 2).WriteWord(16, 3).WriteWord(18, 0));
        var actual = Advance(frame, Resources(), harness.Inputs());
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        Assert.Equal(0xABCDu, actual.State.ReadDword(SeedGlobal));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0x400, 1));
        Assert.Equal(new byte[] { 1, 0 }, actual.Memory.Arena!.Read(0x35A0 + 18, 2));
        var restored = JsonSerializer.Deserialize<NativeBattleEffect303Frame>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => restored.Memory.Arena!.Read(0x400, 1));
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(Advance(restored, Resources(), harness.Inputs())));
    }

    private static NativeBattleEffect303Resources Resources()
    {
        byte[] children = new byte[32];
        short[] values = [0, -100, 40, 25, 50, -80, 20, 10, -50, -80, 20, 15, 0, -50, 40, 20];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(children.AsSpan(index * 2), values[index]);
        }
        byte[] muzzle = new byte[16];
        short[] points = [1789, 0, -1099, 3, 464, -13, -1058, 39];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(muzzle.AsSpan(index * 2), points[index]);
        }
        return NativeBattleEffect303StateTests.Resources() with { ChildPoints32 = children.ToImmutableArray(), MuzzlePoints16 = muzzle.ToImmutableArray() };
    }

    private static NativeBattleEffect303Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        var memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0, [0xA5]), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        var effect = new NativeBattleOriginalEffectState(303, 302, new(303, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(12345), NativeBattleEffect303State.FromColdProcess().WriteDword(SeedGlobal, 0xABCD)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect303Frame Active(ushort age)
    {
        var frame = Inactive();
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        (var main, _) = pools[1].Append(MainCallback);
        (var root, _) = pools[0].Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = pools.SetItem(0, root).SetItem(1, main), ReturnedDescriptor = RootDescriptor }, State = frame.State.WriteDword(CasterGlobal, ModelAddress(3)).WriteDword(TargetGlobal, ModelAddress(4)).WriteDword(ArenaGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset + 124) with { RootBankStorage = null } };
        frame = ClearDwords701200(frame, Pools[1].Bank + 12, 0x9ED);
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).WriteWord(12, age).WriteDword(16, 0x12345678));
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[MainProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(Resources().MainProgramme, MainProgrammeWorkspaceLength);
        return WriteBytes(frame, Pools[1].Bank + MainProgrammeWorkspaceOffset, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), MainProgrammeWorkspaceLength);
    }

    private static uint ModelAddress(int index) => 0x1D972C0u + (uint)(156 * index);

    private sealed class Harness
    {
        public List<NativeBattleEffect303Operation> Operations { get; } = [];
        public List<string> Log { get; } = [];
        public List<int> Bones { get; } = [];
        public bool Busy { get; init; } = true;
        public NativeBattleActionPacketsState Packets { get; set; } = MakePackets();
        public Dictionary<int, NativeBattleGraphicsModel> Models { get; } = Enumerable.Range(0, 7).ToDictionary(index => index, index => new NativeBattleGraphicsModel(index, 6) { Rotation14 = 0, Transform = new(new(0, 0, 0), new((short)(100 + index * 10), 200, 300), (short)(1717 + index), 999, 4096) });

        public NativeBattleEffect303Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, pointer) => Models[Index(pointer)], (_, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _, bone) =>
        {
            Bones.Add(bone);
            return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 5000 }, null);
        }, _ => Packets, (frame, operation) =>
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect303CopyPackedPosition copy:
                    Log.Add("copy");
                    var source = Models[Index(copy.SourceModel)].Transform!;
                    int target = Index(copy.TargetModel);
                    Models[target] = Models[target] with { Transform = Models[target].Transform! with { Position28 = source.Position28, Vertical34 = source.Vertical34 } };
                    break;
                case NativeBattleEffect303SelectSequence505C00 select:
                    Log.Add("select" + select.Sequence);
                    break;
                case NativeBattleEffect303EnterSequence508F90:
                    Log.Add("enter");
                    break;
                case NativeBattleEffect303ModelFields fields:
                    int actor = Index(fields.ModelAddress);
                    Models[actor] = Models[actor] with { Flags0 = (ushort)((Models[actor].Flags0 & ~fields.ClearFlags0) | fields.SetFlags0) };
                    Log.Add("fields" + actor);
                    break;
                default:
                    Log.Add(operation switch
                    {
                        NativeBattleEffect303InitializeIo534110 => "io-init",
                        NativeBattleEffect303AdvanceIo534210 => "io-advance",
                        NativeBattleEffect303Sound501330 => "sound",
                        NativeBattleEffect303RestorePose508C90 => "restore",
                        NativeBattleEffect303DrawStaticMesh88 => "static",
                        NativeBattleEffect303DrawSprite180 => "sprite",
                        _ => operation.GetType().Name
                    });
                    break;
            }
            return frame;
        }, frame =>
        {
            Log.Add("vibration");
            return new(frame, 0x12345678);
        }, (frame, index) =>
        {
            Log.Add("poll" + index);
            return new(frame, Busy);
        });

        private static int Index(uint pointer) => checked((int)((pointer - 0x1D972C0) / 156));
        private static NativeBattleActionPacketsState MakePackets()
        {
            var packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(3, 8, 13, -1, 258, 303, null));
            (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            (packets, _) = packets.Prepare(new(3, 8, 13, -1, 258, 303, null));
            (packets, _) = packets.AppendContact(new(new(4, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(2);
            return packets;
        }
    }
}
