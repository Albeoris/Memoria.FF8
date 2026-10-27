using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306ActorsTests
{
    [Fact]
    public void Selection_excludes_caster_target_hidden_and_loaded_target_ring_and_retains_row_tails()
    {
        Fixture fixture = new();
        fixture.Models[1] = fixture.Models[1] with { Flags0 = 0x802 };
        fixture.Models[2] = fixture.Models[2].WriteLinkedModelIndex140(3) with { Flags0 = 2 };
        fixture.Models[3] = fixture.Models[3].WriteLinkedModelIndex140(2);
        fixture.Models[4] = fixture.Models[4] with { Flags0 = 6, HasLinkedModelRing140 = null };
        fixture.Models[6] = fixture.Models[6] with { Flags0 = 2 };
        NativeBattleDegenerator306Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(0x2244184, 0xCAFE) };
        frame = NativeBattleDegenerator306Actors.Schedule5A51B0(frame, 8, fixture.Inputs());
        Assert.Equal(new uint[] { 0, 1, 0, 0, 0, 0, 1 }, Enumerable.Range(0, 7).Select(actor => frame.State.ReadDword(0x2244168u + (uint)(actor * 24))).ToArray());
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(0x2244184));
        NativeBattleModulePoolTask[] tasks = frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(new ushort[] { 1, 6 }, tasks.Select(task => task.ReadWord(14)).Order().ToArray());
        Assert.All(tasks, task => Assert.Equal(0x5A5270u, task.Callback));
        Assert.Equal((ushort)0x800, tasks.Single(task => task.ReadWord(14) == 1).ReadWord(0x1A));
        frame = NativeBattleDegenerator306Actors.Schedule5A5010(frame, 4, fixture.Inputs());
        Assert.Equal(2, frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0 && task.Callback == 0x5A5070));
    }

    [Theory]
    [InlineData(0x5A5070u, 1536, false)]
    [InlineData(0x5A5270u, 2560, true)]
    public void Paused_fade_publishes_color_and_optional_alpha_before_pause_without_age_or_flags(uint callback, int cue, bool alpha)
    {
        Fixture fixture = new();
        (NativeBattleDegenerator306Frame frame, uint address) = fixture.Task(callback, 3, 8);
        (frame, int result) = NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs(0x201));
        Assert.Equal(0, result);
        Assert.Equal((ushort)3, NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(12));
        Assert.Equal(cue, Assert.Single(fixture.Operations.OfType<NativeBattleDegenerator306ActorRecolor>()).Cue);
        Assert.Equal(alpha ? 1 : 0, fixture.Operations.OfType<NativeBattleDegenerator306ActorAlpha>().Count());
        Assert.Empty(fixture.Operations.OfType<NativeBattleDegenerator306ActorFlags>());
        Assert.Empty(fixture.Operations.OfType<NativeBattleDegenerator306RestoreShadow>());
    }

    [Theory]
    [InlineData(0x5A5070u, 0)]
    [InlineData(0x5A5270u, 4)]
    public void Final_fade_restores_saved_800_and_only_appearance_restores_shadow(uint callback, int hidden)
    {
        Fixture fixture = new();
        fixture.Models[1] = fixture.Models[1] with { Flags0 = 0x1802 };
        (NativeBattleDegenerator306Frame frame, uint address) = fixture.Task(callback, 7, 8, 0x800);
        (frame, int result) = NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs());
        Assert.Equal(2, result);
        Assert.Equal((ushort)8, NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(12));
        Assert.Equal(0x1802 | hidden, fixture.Models[1].Flags0);
        Assert.Equal(callback == 0x5A5070 ? 1 : 0, fixture.Operations.OfType<NativeBattleDegenerator306RestoreShadow>().Count());
    }

    [Fact]
    public void Fade_zero_divisor_faults_before_any_color_or_model_store()
    {
        Fixture fixture = new();
        (NativeBattleDegenerator306Frame frame, uint address) = fixture.Task(0x5A5070, 0, 0);
        Assert.Throws<DivideByZeroException>(() => NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs()));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Motion_saves_packed_owners_moves_while_paused_and_restores_exact_bytes_at_thirty()
    {
        Fixture fixture = new();
        fixture.Models[1] = fixture.Models[1] with { Flags0 = 0x1802 };
        NativeBattleGraphicsModel original = fixture.Models[1];
        (NativeBattleDegenerator306Frame frame, uint address) = fixture.Task(0x5A5540, 0, 4096);
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300);
        frame = NativeBattleDegenerator306Controller.Write(frame, address, task);
        (frame, int result) = NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs(1));
        Assert.Equal(0, result);
        Assert.Equal((ushort)0, NativeBattleDegenerator306Controller.Read(frame, address).ReadWord(12));
        Assert.Equal(new short[] { 0, 0, original.Rotation16!.Value }, new short[] { fixture.Models[1].Rotation12!.Value, fixture.Models[1].Rotation14!.Value, fixture.Models[1].Rotation16!.Value });
        Assert.Equal((ushort)(original.Flags0 & 0x1020), frame.State.ReadWord(0x2244184));
        Assert.Throws<NotSupportedException>(() => frame.State.Read(0x224418Eu, 2));
        Assert.Equal(original.Transform!.Vertical34, BinaryPrimitives.ReadInt16LittleEndian(frame.State.Read(0x2244196, 2)));
        task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 29);
        frame = NativeBattleDegenerator306Controller.Write(frame, address, task);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDegenerator306Frame>(NativeBattleTestSnapshots.Json(frame))!;
        fixture.Operations.Clear();
        (frame, result) = NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs());
        Assert.Equal(2, result);
        Assert.Equal(original.Transform, fixture.Models[1].Transform);
        Assert.Equal(original.Rotation12, fixture.Models[1].Rotation12);
        Assert.Equal(original.Rotation14, fixture.Models[1].Rotation14);
        Assert.Equal(original.Rotation16, fixture.Models[1].Rotation16);
        Assert.Null(fixture.Models[1].RotationPadding18);
        Assert.Equal(original.Flags0, fixture.Models[1].Flags0);
        Assert.IsType<NativeBattleDegenerator306ActorRotation8>(fixture.Operations[^1]);
        Assert.IsType<NativeBattleDegenerator306ActorPackedPosition8>(fixture.Operations[^2]);
    }

    [Theory]
    [InlineData(4096, 1774)]
    [InlineData(-4096, -1774)]
    public void Motion_uses_signed_product_divided_by_thirty_with_truncation(short speed, short expected)
    {
        Fixture fixture = new();
        (NativeBattleDegenerator306Frame frame, uint address) = fixture.Task(0x5A5540, 0, unchecked((ushort)speed));
        NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300);
        frame = NativeBattleDegenerator306Controller.Write(frame, address, task);
        (frame, _) = NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs(1));
        frame = NativeBattleDegenerator306Controller.Write(frame, address, NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, 13));
        NativeBattleDegenerator306Actors.Advance(frame, address, fixture.Inputs(1));
        Assert.Equal((short?)expected, fixture.Models[1].Rotation12);
        Assert.Equal((short?)1774, fixture.Models[1].Rotation14);
        Assert.Equal((short?)345, fixture.Models[1].Rotation16);
    }

    [Fact]
    public void Unknown_enabled_ring_pointer_is_required_but_disabled_rows_are_not_read()
    {
        Fixture fixture = new();
        fixture.Models[1] = fixture.Models[1] with { Flags0 = 2, HasLinkedModelRing140 = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleDegenerator306Actors.Schedule5A51B0(fixture.Frame, 8, fixture.Inputs()));
        fixture.Models[1] = fixture.Models[1] with { Flags0 = 0 };
        NativeBattleDegenerator306Actors.Schedule5A51B0(fixture.Frame, 8, fixture.Inputs());
    }

    private sealed class Fixture
    {
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index is 0 or 3 ? (ushort)2 : (ushort)0) { HasLinkedModelRing140 = false, Rotation12 = 123, Rotation14 = -234, Rotation16 = 345, Transform = new(new(0, 0, 0), new(20, -40, 60), -77, 0, 4096) }).ToArray();
        public List<NativeBattleDegenerator306Operation> Operations { get; } = [];
        public NativeBattleDegenerator306Frame Frame { get; }

        public Fixture()
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            NativeBattleOriginalEffectState effect = new(306, 306, new(306, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [root, children] };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleDegenerator306State.FromColdProcess().WriteDword(TargetGlobal, 3).WriteDword(CasterGlobal, 0));
        }

        public (NativeBattleDegenerator306Frame Frame, uint Address) Task(uint callback, ushort age, ushort duration, ushort saved = 0)
        {
            (NativeBattleDegenerator306Frame frame, uint address) = NativeBattleDegenerator306Controller.Append(Frame, callback);
            NativeBattleModulePoolTask task = NativeBattleDegenerator306Controller.Read(frame, address).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x18, duration).WriteWord(0x1A, saved);
            return (NativeBattleDegenerator306Controller.Write(frame, address, task), address);
        }

        public NativeBattleDegenerator306Inputs Inputs(uint flags = 0) => new(flags, NativeBattleActionPacketsState.Fresh(), actor => Models[actor], _ => throw new InvalidOperationException(), () => throw new InvalidOperationException(), actor => NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(Models[actor]), 0x0C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleDegenerator306ActorFlags change:
                    Models[change.Actor] = Models[change.Actor] with { Flags0 = (ushort)((Models[change.Actor].Flags0 & ~change.ClearMask) | change.SetMask) };
                    break;
                case NativeBattleDegenerator306ActorRotation8 change:
                    Models[change.Actor] = NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(Models[change.Actor], 0x0C, change.Packed8, 0, 4), 0x10, change.Packed8, 4, 4);
                    break;
                case NativeBattleDegenerator306ActorPosition change:
                    Models[change.Actor] = Models[change.Actor] with { Transform = Models[change.Actor].Transform! with { Position28 = change.Position } };
                    break;
                case NativeBattleDegenerator306ActorPackedPosition8 change:
                    Models[change.Actor] = NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(Models[change.Actor], 0x1C, change.Packed8, 0, 4), 0x20, change.Packed8, 4, 4);
                    break;
            }
            return frame;
        });
    }
}
