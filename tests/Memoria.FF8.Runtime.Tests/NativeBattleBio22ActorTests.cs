using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22ActorTests
{
    [Fact]
    public void Initializer_writes_only_actual_fields_preserving_warm_phase_center_lighting_padding_and_unknown_holes()
    {
        NativeBattleBio22ActorMemory owner = NativeBattleBio22ActorMemory.Unknown().WriteWord(0xCC, 0xFED).WriteWord(0xDC, 123).WriteWord(0xDE, 456).WriteWord(0xE0, 789).WriteLighting(0x1F, new byte[] { 0xA7 });
        NativeBattleBio22ActorMemory next = NativeBattleBio22Actor.Initialize89DC30(owner, 6);
        Assert.Equal(0x2705160u, next.Dword(0));
        Assert.Equal(0x2714DE8u, next.Dword(4));
        Assert.Equal(0x1D972C0u + 6 * 156, next.Dword(8));
        Assert.Equal(0x1D8E054u, next.Dword(0xC));
        Assert.Equal(0x270456Cu, next.Dword(0x10));
        Assert.Equal((ushort)6, next.Word(0xDA));
        Assert.Equal((ushort)0xFED, next.Word(0xCC));
        Assert.Equal(new byte[] { 123, 0, 200, 1, 21, 3 }, next.ReadSettings(0xDC, 6));
        Assert.Equal(new byte[] { 0x80, 0x80, 0x80 }, next.ReadSettings(0xC4, 3));
        Assert.Equal((byte)0xA7, next.ReadLighting(0x1F, 1)[0]);
        Assert.Equal(0x270E560u, BinaryPrimitives.ReadUInt32LittleEndian(next.ReadLighting(4, 4)));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0x40, 1, 0, 0 }, next.ReadLighting(0x14, 8));
        Assert.Throws<NotSupportedException>(() => next.ReadSettings(0x14, 1));
        Assert.Throws<NotSupportedException>(() => next.ReadSettings(0xC7, 1));
        Assert.Throws<NotSupportedException>(() => next.ReadLighting(0, 1));
        Assert.Throws<NotSupportedException>(() => next.ReadProjected(0, 1));
    }

    [Fact]
    public void Initialization_suppresses_only_target_flags_low_byte_before_draw_and_keeps_root_release_signal()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = frame with { Task = frame.Task.WriteByte(0x2D, 3), Root = frame.Root.WriteByte(0x63, 7), Models = frame.Models.SetItem(3, frame.Models[3] with { Flags0 = 0xAB03 }) };
        int draws = 0;
        NativeBattleBio22ActorStep step = NativeBattleBio22Actor.Advance89D3D0(frame, current =>
        {
            draws++;
            Assert.Equal((ushort)0xAB07, current.Models[3].Flags0);
            Assert.Equal((byte)8, current.Task.ReadByte(0x26));
            Assert.Equal((byte)7, current.Root.ReadByte(0x63));
            Assert.Equal((ushort)0xA0, current.Memory.Bio22ActorMemory!.Word(0xCC));
            return current with { Memory = current.Memory with { Parameter80 = 83 } };
        });
        Assert.Equal(1, draws);
        Assert.Equal((byte?)83, step.Frame.Memory.Parameter80);
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x24));
        Assert.Equal(frame.Models[0], step.Frame.Models[0]);
        Assert.Equal(0u, step.Result);
    }

    [Theory]
    [InlineData((ushort)0x2C0, (ushort)0x300)]
    [InlineData((ushort)0x300, (ushort)0x300)]
    [InlineData((ushort)0x7FC0, (ushort)0x8000)]
    public void Ramp_uses_signed_new_WORD_and_clamps_only_above_300(ushort start, ushort expected)
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = WithOwner(frame with { Task = frame.Task.WriteByte(0x29, 1) }, frame.Memory.Bio22ActorMemory!.WriteWord(0xF2, start).WriteWord(0xCE, 0xE00));
        NativeBattleBio22ActorStep step = NativeBattleBio22Actor.Advance89D3D0(frame, RejectDraw);
        Assert.Equal(expected, step.Frame.Memory.Bio22ActorMemory!.Word(0xF2));
        Assert.Equal((ushort)0x1000, step.Frame.Memory.Bio22ActorMemory!.Word(0xCE));
        Assert.Equal((byte)2, step.Frame.Task.ReadByte(0x29));
    }

    [Fact]
    public void Terminal_fade_keeps_negative_amplitude_clears_current_target_and_root_signal_before_skipping_draw()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = frame with { Task = frame.Task.WriteByte(0x29, 3).WriteWord(0x26, 0xA508).WriteByte(0x2D, 4).WriteByte(0x28, 1), Root = frame.Root.WriteByte(0x63, 9), Models = frame.Models.SetItem(4, frame.Models[4] with { Flags0 = 0xCDFF }) };
        frame = WithOwner(frame, frame.Memory.Bio22ActorMemory!.WriteWord(0xF2, 0).WriteWord(0xCE, 0x80).WriteWord(0xCC, 0xFC0));
        NativeBattleBio22ActorStep step = NativeBattleBio22Actor.Advance89D3D0(frame, RejectDraw);
        Assert.Equal((ushort)0xFF80, step.Frame.Memory.Bio22ActorMemory!.Word(0xCE));
        Assert.Equal((ushort)0, step.Frame.Memory.Bio22ActorMemory!.Word(0xF2));
        Assert.Equal((ushort)0x60, step.Frame.Memory.Bio22ActorMemory!.Word(0xCC));
        Assert.Equal((ushort)0xCDFB, step.Frame.Models[4].Flags0);
        Assert.Equal((byte)0, step.Frame.Root.ReadByte(0x63));
        Assert.Equal((ushort)0xA501, step.Frame.Task.ReadWord(0x26));
        Assert.Equal(0u, step.Result);
        NativeBattleBio22ActorStep released = NativeBattleBio22Actor.Advance89D3D0(step.Frame with { Task = step.Frame.Task.WriteByte(0x28, 0) }, RejectDraw);
        Assert.Equal(2u, released.Result);
        Assert.Equal((ushort)0x100, released.Frame.Memory.Bio22ActorMemory!.Word(0xCC));
    }

    [Fact]
    public void Draw_uses_captured_global_actor_with_local_shadow_body_extra_local_order_and_retains_current_process_mutations()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(17, 223, 451)) with { X = 100, Y = -200, Z = 300 };
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Euler56CD50(new(53, 71, 91)) with { X = -100, Y = 200, Z = 5000 };
        frame = frame with { Models = frame.Models.SetItem(5, frame.Models[5] with { GeometryWorldMatrix = world, SequenceFrame = 91 }), Task = frame.Task.WriteByte(0x2D, 1) };
        NativeBattleBio22ActorMemory owner = NativeBattleBio22Actor.Initialize89DC30(frame.Memory.Bio22ActorMemory!, 5).WriteWord(0xB4, 31).WriteWord(0xB6, unchecked((ushort)-47)).WriteWord(0xB8, 59).WriteWord(0xDC, 23).WriteWord(0xDE, 29).WriteWord(0xE0, 41).WriteWord(0xCC, 123).WriteWord(0xCE, 456).WriteWord(0x46, 0xCAFE).WriteWord(0x86, 0xBEEF);
        frame = WithOwner(frame, owner);
        NativeBattleCureActorMatrices expected = NativeBattlePoseMatrices.CureActorMatrices8D7110(world, camera, new(31, -47, 59), 1, new(23, 29, 41), 8192);
        NativeBattleBodyBoneMatrices local = new(new string('0', 64), [NativeBattleCuragaMatrices.Identity8DD770()]);
        NativeBattleBodyBoneMatrices temporary = new(new string('1', 64), [world]);
        List<string> order = [];
        NativeBattleBio22ActorServices services = new((current, actor) =>
        {
            Assert.Equal(5, actor);
            order.Add("local");
            return current with { Models = current.Models.SetItem(actor, current.Models[actor] with { CurrentBodyBoneMatrices = local }) };
        }, (current, actor, matrix, bucket, shift) =>
        {
            order.Add("shadow");
            Assert.Same(local, current.Models[actor].CurrentBodyBoneMatrices);
            Assert.Equal(4112, bucket);
            Assert.Equal(16, shift);
            EqualMatrix(expected.MainCamera, matrix);
            NativeBattleBio22ActorMemory retained = current.Memory.Bio22ActorMemory!;
            retained = retained with { Settings = retained.Settings.MarkUnknown(0xB4, 6).MarkUnknown(0xD2, 2) };
            return current with { Memory = current.Memory with { Parameter80 = 37, Bio22ActorMemory = retained } };
        }, (current, actor, matrix, phase, amplitude) =>
        {
            order.Add("body+extra");
            Assert.Same(local, current.Models[actor].CurrentBodyBoneMatrices);
            Assert.Equal((short)123, phase);
            Assert.Equal((short)456, amplitude);
            Assert.Equal((byte?)37, current.Memory.Parameter80);
            EqualMatrix(expected.MainCamera, matrix);
            EqualMatrix(expected.OverlayCamera, NativeBattleBio22Actor.ReadMatrix(current.Memory.Bio22ActorMemory!, 0x74));
            return current with { Models = current.Models.SetItem(actor, current.Models[actor] with { CurrentBodyBoneMatrices = temporary }), Memory = current.Memory with { Parameter80 = 39, Bio22ActorMemory = current.Memory.Bio22ActorMemory!.WriteProjected(4, new byte[] { 1, 2, 3, 4, 5, 6 }) } };
        });
        NativeBattleBio22ActorFrame drawn = NativeBattleBio22Actor.Draw89D460(frame, camera, services);
        Assert.Equal(new[] { "local", "shadow", "body+extra", "local" }, order);
        Assert.Same(local, drawn.Models[5].CurrentBodyBoneMatrices);
        Assert.Equal(91, drawn.Models[5].SequenceFrame);
        Assert.Equal((byte?)39, drawn.Memory.Parameter80);
        EqualMatrix(world, NativeBattleBio22Actor.ReadMatrix(drawn.Memory.Bio22ActorMemory!, 0x14));
        EqualMatrix(expected.MovedModelWorld, NativeBattleBio22Actor.ReadMatrix(drawn.Memory.Bio22ActorMemory!, 0x54));
        Assert.Throws<NotSupportedException>(() => drawn.Memory.Bio22ActorMemory!.ReadSettings(0x26, 2));
        Assert.Throws<NotSupportedException>(() => drawn.Memory.Bio22ActorMemory!.ReadSettings(0x66, 2));
        Assert.Equal((ushort)0xCAFE, drawn.Memory.Bio22ActorMemory!.Word(0x46));
        Assert.Equal((ushort)0xBEEF, drawn.Memory.Bio22ActorMemory!.Word(0x86));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, drawn.Memory.Bio22ActorMemory!.ReadProjected(4, 6));
    }

    [Fact]
    public void Shadow_suppression_preserves_body_and_two_local_publications_and_overlay_input_read_is_deferred()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = WithOwner(frame, NativeBattleBio22Actor.Initialize89DC30(frame.Memory.Bio22ActorMemory!, 0));
        frame = frame with { Models = frame.Models.SetItem(0, frame.Models[0] with { Flags0 = 0x23, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() }) };
        int locals = 0;
        int bodies = 0;
        NativeBattleBio22ActorServices services = new((current, actor) =>
        {
            locals++;
            return current;
        }, (current, actor, matrix, bucket, shift) => throw new InvalidOperationException("Suppressed shadow was drawn."), (current, actor, matrix, phase, amplitude) =>
        {
            bodies++;
            return current;
        });
        NativeBattleBio22Actor.Draw89D460(frame, NativeBattleCuragaMatrices.Identity8DD770(), services);
        Assert.Equal(2, locals);
        Assert.Equal(1, bodies);
        bool shadowCalled = false;
        frame = frame with { Models = frame.Models.SetItem(0, frame.Models[0] with { Flags0 = 3 }), Memory = frame.Memory with { Bio22ActorMemory = frame.Memory.Bio22ActorMemory! with { Settings = frame.Memory.Bio22ActorMemory!.Settings.MarkUnknown(0xDC, 6) } } };
        services = services with { DrawShadow5088A0 = (current, actor, matrix, bucket, shift) =>
        {
            shadowCalled = true;
            return current;
        } };
        Assert.Throws<NotSupportedException>(() => NativeBattleBio22Actor.Draw89D460(frame, NativeBattleCuragaMatrices.Identity8DD770(), services));
        Assert.True(shadowCalled);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)2)]
    public void Factored_moved_world_kernel_matches_existing_combined_Cure_contract(short mode)
    {
        NativeBattleActionMatrix original = NativeBattleGfMatrices.Euler56CD50(new(97, 311, 521)) with { X = 10001, Y = -20002, Z = 30003 };
        NativeBattleVector offset = new(1000, -2000, 3000);
        NativeBattleActionMatrix moved = NativeBattlePoseMatrices.CureMovedModelWorld8D7200(original, offset, mode);
        NativeBattleCureActorMatrices combined = NativeBattlePoseMatrices.CureActorMatrices8D7110(original, NativeBattleCuragaMatrices.Identity8DD770(), offset, mode, new(5, 7, 11), 8192);
        EqualMatrix(combined.MovedModelWorld, moved);
        EqualMatrix(combined.OverlayCamera, NativeBattlePoseMatrices.CureOverlayMatrix8D72C0(original, moved, new(5, 7, 11), 8192));
    }

    [Fact]
    public void Warm_owner_masks_and_full_auxiliary_next_callback_survive_independent_JSON_without_missing_owner_hydration()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = frame with { Memory = frame.Memory with { Bio22ActorMemory = frame.Memory.Bio22ActorMemory! with { Projected = frame.Memory.Bio22ActorMemory!.Projected.MarkUnknown(10, 6) } } };
        NativeBattleBio22ActorStep first = NativeBattleBio22Actor.Advance89D3D0(frame, KeepFrame);
        NativeBattleBio22ActorFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBio22ActorFrame>(NativeBattleTestSnapshots.Json(first.Frame))!;
        NativeBattleBio22ActorStep next = NativeBattleBio22Actor.Advance89D3D0(first.Frame, KeepFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleBio22Actor.Advance89D3D0(restored, KeepFrame)));
        Assert.Throws<NotSupportedException>(() => restored.Memory.Bio22ActorMemory!.ReadProjected(10, 1));
        Assert.Same(first.Frame.Memory.Bio22ActorMemory, first.Frame.Memory.BeginLoad(22).Bio22ActorMemory);
        NativeBattleEffectMemory missing = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(first.Frame.Memory with { Bio22ActorMemory = null }))!;
        Assert.Null(missing.Bio22ActorMemory);
        Assert.Null(missing.BeginLoad(22).Bio22ActorMemory);
        Assert.Throws<NotSupportedException>(() => NativeBattleBio22Actor.Advance89D3D0(frame with { Memory = frame.Memory with { Bio22ActorMemory = null } }, KeepFrame));
        Assert.Throws<NotSupportedException>(() => NativeBattleBio22Actor.Advance89D3D0(frame with { Memory = frame.Memory with { Bio22ActorMemory = NativeBattleBio22ActorMemory.Unknown() } }, KeepFrame));
    }

    [Fact]
    public void Retained_work_partition_has_explicit_unknown_holes_and_rejects_cross_descriptor_aliases()
    {
        NativeBattleBio22ActorMemory cold = NativeBattleBio22ActorMemory.FromColdProcess();
        Assert.All(cold.ReadProjected(0, 0x6400), value => Assert.Equal((byte)0, value));
        NativeBattleBio22ActorMemory unknown = NativeBattleBio22ActorMemory.Unknown().WriteProjected(4, new byte[] { 1, 2, 3, 4, 5, 6 });
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, unknown.ReadProjected(4, 6));
        Assert.Throws<NotSupportedException>(() => unknown.ReadProjected(0, 4));
        Assert.Throws<NotSupportedException>(() => unknown.WriteProjected(0x63FF, new byte[2]));
        Assert.Throws<InvalidDataException>(() => (cold with { Projected = cold.Projected.Write(0x6400, new byte[] { 1 }) }).Validate());
    }

    [Fact]
    public void Finite_auxiliary_counter_holds_then_fades_and_releases_root_only_after_its_final_suppressed_draw()
    {
        NativeBattleBio22ActorFrame frame = Frame();
        frame = frame with { Root = frame.Root.WriteByte(0x63, 1) };
        int draws = 0;
        for (int tick = 0; tick < 100; tick++)
        {
            NativeBattleBio22ActorFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBio22ActorFrame>(NativeBattleTestSnapshots.Json(frame))!;
            NativeBattleBio22ActorStep expected = NativeBattleBio22Actor.Advance89D3D0(restored, KeepFrame);
            NativeBattleBio22ActorStep step = NativeBattleBio22Actor.Advance89D3D0(frame, current =>
            {
                draws++;
                Assert.Equal((byte)1, current.Root.ReadByte(0x63));
                Assert.True((current.Models[0].Flags0 & 4) != 0);
                return current;
            });
            Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(step));
            frame = step.Frame;
            if (step.Result == 2)
            {
                Assert.Equal(frame.Task.ReadWord(0x24) - 1, draws);
                Assert.Equal((byte)0, frame.Root.ReadByte(0x63));
                Assert.Equal((ushort)3, frame.Models[0].Flags0);
                Assert.Equal((ushort)0, frame.Memory.Bio22ActorMemory!.Word(0xCE));
                return;
            }
        }
        Assert.Fail("Synthetic actor auxiliary did not reach its explicit release signal.");
    }

    private static NativeBattleBio22ActorFrame KeepFrame(NativeBattleBio22ActorFrame frame) => frame;
    private static NativeBattleBio22ActorFrame RejectDraw(NativeBattleBio22ActorFrame frame) => throw new InvalidOperationException("No actor draw expected without active bit8.");
    private static NativeBattleBio22ActorFrame WithOwner(NativeBattleBio22ActorFrame frame, NativeBattleBio22ActorMemory owner) => frame with { Memory = frame.Memory with { Bio22ActorMemory = owner } };
    private static NativeBattleBio22ActorFrame Frame() => new(new(0, 0, null, 0x89D3D0, new byte[0x38 - 12].ToImmutableArray()), new(0, 0, null, 0x89A7B0, new byte[0x64 - 12].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess() with { Bio22ActorMemory = NativeBattleBio22ActorMemory.FromColdProcess() }, Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3)).ToImmutableArray());
    private static void EqualMatrix(NativeBattleActionMatrix expected, NativeBattleActionMatrix actual)
    {
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((expected.X, expected.Y, expected.Z, expected.Scale), (actual.X, actual.Y, actual.Z, actual.Scale));
    }
}
