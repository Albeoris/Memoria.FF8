using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_selects_actual_private_bank_before_child_initialization(uint toggle, int offset, uint next)
    {
        Fixture f = new();
        NativeBattleReflectBeam135Frame frame = f.Frame with { Effect = f.Frame.Effect with { Pools = [f.Frame.Effect.Pools[0]] } };
        frame = NativeBattleReflectBeam135Controller.Write(frame, RootBank, NativeBattleReflectBeam135Controller.Read(frame, RootBank).WriteDword(16, toggle));
        var actual = NativeBattleReflectBeam135Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(ArenaAddress + (uint)offset, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleReflectBeam135Controller.Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Empty(f.Operations);
    }

    [Fact]
    public void Root_empty_children_still_compose_and_restore_caster_and_retire_without_increment()
    {
        Fixture f = new();
        NativeBattleReflectBeam135Frame frame = f.Frame with { Effect = f.Frame.Effect with { Pools = f.Frame.Effect.Pools.Select((pool, index) => index == 0 ? pool : NativeBattleTaskFixture.EditPool(pool, data => data with { Head = null, Tail = null, Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Flags = 0 })).ToImmutableArray() })).ToImmutableArray() } };
        frame = NativeBattleReflectBeam135Controller.Write(frame, RootBank, NativeBattleReflectBeam135Controller.Read(frame, RootBank).WriteWord(12, 90).WriteByte(15, 1));
        var actual = NativeBattleReflectBeam135Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)90, NativeBattleReflectBeam135Controller.Read(actual.Frame, RootBank).ReadWord(12));
        Assert.Collection(f.Operations, value => Assert.Equal(new NativeBattleReflectBeam135ComposeCaster(0), value), value => Assert.Equal(new NativeBattleReflectBeam135RestoreCaster(0), value));
    }

    [Fact]
    public void Root_initializes_native_pools_descending_targets_WORD_means_and_main_workspace()
    {
        Fixture f = new();
        NativeBattleReflectBeam135Frame frame = f.Frame with { Effect = f.Frame.Effect with { Pools = [f.Frame.Effect.Pools[0]] } };
        frame = NativeBattleReflectBeam135Controller.Write(frame, RootBank, NativeBattleReflectBeam135Controller.Read(frame, RootBank).WriteWord(12, 1));
        var actual = NativeBattleReflectBeam135Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(new ushort[] { 20, 736, 620, 812, 36 }, actual.Frame.Effect.Pools.Select(pool => pool.Stride).ToArray());
        NativeBattleModulePoolTask main = NativeBattleReflectBeam135Controller.Read(actual.Frame, Fixture.MainAddress);
        Assert.Equal(new uint[] { Fixture.ModelAddress(3), Fixture.ModelAddress(3), Fixture.ModelAddress(4) }, Enumerable.Range(0, 3).Select(index => main.ReadDword(20 + index * 4)).ToArray());
        Assert.Equal(new byte[] { 0, 2, 1 }, Enumerable.Range(0, 3).Select(index => main.ReadByte(16 + index)).ToArray());
        Assert.Equal(unchecked((ushort)(unchecked((short)90000) / 3)), main.ReadWord(32));
        Assert.Equal(main.ReadWord(32), main.ReadWord(34));
        Assert.Equal(main.ReadWord(32), main.ReadWord(36));
        Assert.Equal(MainActAddress, main.ReadDword(40));
        Assert.Equal((ushort)1, main.ReadWord(12));
        int compose = f.Operations.FindIndex(value => value is NativeBattleReflectBeam135ComposeCaster);
        int restore = f.Operations.FindIndex(value => value is NativeBattleReflectBeam135RestoreCaster);
        Assert.InRange(compose, 0, restore - 1);
        Assert.Contains(f.Operations.Skip(compose + 1).Take(restore - compose - 1), value => value is NativeBattleReflectBeam135DrawStatic88);
    }

    [Theory]
    [InlineData(14, -1)]
    [InlineData(15, 0)]
    [InlineData(18, 1650)]
    [InlineData(19, 2200)]
    [InlineData(44, 2200)]
    [InlineData(45, 2200)]
    [InlineData(56, 183)]
    [InlineData(57, 0)]
    public void Main_stage_envelope_uses_signed_native_boundaries_and_terminal_reset(int age, int expected)
    {
        Fixture f = new();
        var actual = NativeBattleReflectBeam135Controller.AdvanceMain(f.Main(age), Fixture.MainAddress, f.Resources, f.Inputs);
        NativeBattleReflectBeam135Stage[] stages = f.Operations.OfType<NativeBattleReflectBeam135Stage>().ToArray();
        if (expected < 0)
        {
            Assert.Empty(stages);
        }
        else
        {
            Assert.Equal((short)expected, stages[0].Depth);
            Assert.All(stages, stage => Assert.Equal(0u, stage.ColorFlags));
        }
        Assert.Equal(age >= 57 ? 2 : 0, actual.Result);
        Assert.Equal((ushort)(age >= 57 ? age : age + 1), NativeBattleReflectBeam135Controller.Read(actual.Frame, Fixture.MainAddress).ReadWord(12));
        if (age == 57)
        {
            Assert.Equal(2, stages.Length);
            Assert.Equal(new NativeBattleReflectBeam135Stage(0, 0), stages[1]);
        }
    }

    [Fact]
    public void Main_scaling_retains_nonzero_mouth_translation_then_composes_current_camera_once()
    {
        Fixture f = new();
        NativeBattleReflectBeam135Frame frame = f.Main(17);
        frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(ScaleMatrixGlobal - BaseAddress)) + 18, 14) } };
        var actual = NativeBattleReflectBeam135Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(3, f.Statics.Count);
        Assert.All(f.Statics, draw => Assert.Equal(new int?[] { 110, 220, 330 }, draw.Matrix.Translation.ToArray()));
        Assert.All(f.Statics, draw => Assert.Null(draw.Matrix.Rotation20[18]));
        Assert.All(f.Statics, draw => Assert.Null(draw.Matrix.Rotation20[19]));
        Assert.Equal(new uint[] { 0xF0, 0xF0, 0x30 }, f.Statics.Select(draw => BinaryPrimitives.ReadUInt32LittleEndian(draw.Context.Read(28, 4))));
        Assert.Equal(new int[] { 3072, 2560, 2560 }, f.Statics.Select(draw => BinaryPrimitives.ReadInt32LittleEndian(draw.Context.Read(12, 4))));
        Assert.Throws<NotSupportedException>(() => actual.Frame.State.Read(ScaleMatrixGlobal + 18, 14));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(25u, actual.Frame.State.ReadDword(CursorGlobal));
        var context = Assert.Single(f.Acts);
        Assert.Equal((0x28, 0x2B8, MainActAddress), (context.Offset, context.Size, context.Source));
        Assert.Throws<NotSupportedException>(() => context.Context.Read(6, 2));
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(context.Context.Read(8, 4)));
        Assert.Equal(ArenaAddress + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(context.Context.Read(12, 4)));
    }

    [Fact]
    public void First_static_pass_retains_unwritten_cue_and_uses_three_explicit_zero_RGB_bytes()
    {
        Fixture f = new();
        NativeBattleReflectBeam135Controller.AdvanceMain(f.Main(15), Fixture.MainAddress, f.Resources, f.Inputs);
        var draw = Assert.Single(f.Statics);
        Assert.Equal(0x30u, BinaryPrimitives.ReadUInt32LittleEndian(draw.Context.Read(28, 4)));
        Assert.Equal(new byte[3], draw.Context.Read(8, 3));
        Assert.Throws<NotSupportedException>(() => draw.Context.Read(11, 1));
        Assert.Throws<NotSupportedException>(() => draw.Context.Read(12, 4));
    }

    internal sealed class Fixture
    {
        public const uint MainAddress = ArenaAddress + MainOffset;
        public NativeBattleReflectBeam135Frame Frame { get; }
        public NativeBattleReflectBeam135Resources Resources { get; }
        public NativeBattleReflectBeam135Inputs Inputs { get; }
        public int Remaining { get; set; } = 1;
        public List<NativeBattleReflectBeam135Operation> Operations { get; } = [];
        public List<(NativeBattleGeometryRegisters Matrix, NativeBattleEffectArena Context)> Statics { get; } = [];
        public List<(uint Source, int Offset, int Size, NativeBattleEffectArena Context)> Acts { get; } = [];
        public Fixture()
        {
            Resources = new(Act(MainActAddress, 20), Act(TargetActAddress, 24), Act(SecondaryActAddress, 13), Act(ImpactActAddress, 11), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new(0, StaticMeshAddress, []), new("unused", []), new short[] { 29, 30, 32 }.ToImmutableArray(), 0x641A0);
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, ArenaAddress + SecondaryOffset, SecondaryStride, SecondaryCount),
                NativeBattleEffectTaskPool.Initialize(TargetDescriptor, ArenaAddress + TargetOffset, TargetStride, TargetCount),
                NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, ArenaAddress, ParticleStride, ParticleCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            (pools[1], _) = pools[1].Append(MainCallback);
            pools[0] = NativeBattleTaskFixture.EditPool(pools[0], data => data with { Slots = pools[0].Slots.SetItem(0, pools[0].Slots[0].WriteDword(12, 0).WriteDword(16, 0)) });
            pools[1] = NativeBattleTaskFixture.EditPool(pools[1], data => data with { Slots = pools[1].Slots.SetItem(0, pools[1].Slots[0].WriteBytes(12, new byte[724])) });
            NativeBattleOriginalEffectState effect = new(135, 134, new(135, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            Frame = new(memory, effect, new(55), NativeBattleReflectBeam135State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, ModelAddress(0))) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 8, 177, 0, 15, 135, null));
            foreach (byte actor in new byte[] { 3, 4, 3 })
            {
                (packets, _) = packets.AppendContact(new(new(actor, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish(3);
            Inputs = new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, actor => new(actor, 2) { Transform = new(new(0, 0, 0), new(unchecked((short)(actor == 4 ? -1000 : 1000)), 0, 0), 0, 300, 4096) }, _ => NativeBattlePackedMatrix32.FromMatrix(identity with { X = 10, Y = 20, Z = 30 }, null), (frame, _) => new(frame, new(30000, 30000, 30000, 123)), (frame, _, _, _) => new(frame, new(100, 200, 300)), (_, _, _) => NativeBattlePackedMatrix32.FromMatrix(identity with { Y = 1000 }, null), (frame, _, source, offset, size, context) =>
            {
                Acts.Add((source.Address, offset, size, context));
                return new(frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + 1) }, Remaining);
            }, (frame, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleReflectBeam135DrawStatic88 draw)
                {
                    NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(frame.Staging!, draw.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
                    Statics.Add((frame.Memory.GeometryRegisters!, context));
                    frame = frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + 8) };
                }
                return frame;
            });
        }
        public NativeBattleReflectBeam135Frame Main(int age) => NativeBattleReflectBeam135Controller.Write(Frame, MainAddress, NativeBattleReflectBeam135Controller.Read(Frame, MainAddress).WriteWord(12, unchecked((ushort)age)));
        public static uint ModelAddress(int actor) => 0x1D972C0u + (uint)actor * 156;
        private static NativeBattleGfAnimationSource Act(uint address, int duration)
        {
            byte[] bytes = new byte[20];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), duration);
            return new(address, bytes.ToImmutableArray());
        }
    }
}
