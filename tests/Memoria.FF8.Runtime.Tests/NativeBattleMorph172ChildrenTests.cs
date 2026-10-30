using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMorph172Controller;
using static Memoria.FF8.Runtime.NativeBattleMorph172Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMorph172ChildrenTests
{
    [Theory]
    [InlineData(0x5D9F20u)]
    [InlineData(0x5DA0F0u)]
    [InlineData(0x5DA520u)]
    [InlineData(0x5DA2C0u)]
    public void Presentation_routes_actual_task_rewritten_source_body_and_physical_particle_rows_without_native_changes(uint callback)
    {
        (NativeBattleMorph172Frame Frame, List<NativeBattleMorph172Operation> Operations) Run(bool capture)
        {
            var frame = Frame(callback, callback == 0x5DA2C0 ? (ushort)43 : (ushort)1);
            if (callback == 0x5DA2C0)
            {
                frame = frame with { State = frame.State.WriteDword(ParticleBase, 1).WriteWord(ParticleBase + 6, 4096) };
            }
            if (callback is 0x5D9F20 or 0x5DA0F0)
            {
                frame = InitializeRadii5D9EE0(frame, Resources);
            }
            List<NativeBattleMorph172Operation> operations = [];
            var inputs = Inputs((current, operation) =>
            {
                operations.Add(operation);
                return operation is NativeBattleMorph172DrawSprite180 sprite ? Feedback(current, sprite.TemporaryAddress, -1) : current;
            }) with { CapturePresentation = capture };
            var result = callback switch
            {
                0x5DA520 => AdvanceBody(frame, ChildBank, Resources, inputs),
                0x5DA2C0 => AdvanceSlots(frame, ChildBank, Resources, inputs),
                _ => AdvanceMesh(frame, ChildBank, Resources, inputs)
            };
            return (result.Frame, operations);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Frame));
        string Operations(List<NativeBattleMorph172Operation> values) => string.Join("\n", values.Select(operation => JsonSerializer.Serialize(operation, operation.GetType())));
        Assert.Equal(Operations(native.Operations), Operations(captured.Operations));
        Assert.All(native.Operations, operation => Assert.Null(operation.PilotIdentity));
        var draws = captured.Operations.Where(operation => operation is NativeBattleMorph172DrawBody220 or NativeBattleMorph172DrawStatic88 or NativeBattleMorph172DrawSprite180).ToArray();
        var identity = Assert.Single(draws).PilotIdentity!.Value;
        Assert.Equal((ushort)172, identity.Effect);
        if (callback is 0x5D9F20 or 0x5DA0F0)
        {
            Assert.Equal((long)unchecked(0xD6299Cu * 397 ^ (callback == 0x5D9F20 ? 0x239BEB8u : 0x239B268u)), identity.ResourceGeneration);
        }
        if (callback == 0x5DA2C0)
        {
            Assert.Equal(ParticleBase, identity.Bank);
            Assert.Equal(0, identity.Slot);
        }
    }

    [Fact]
    public void Presentation_particle_native_retirement_and_rebirth_change_only_its_row_epoch()
    {
        var frame = Frame(0x5DA2C0, 0);
        List<NativeBattlePilotVisualIdentity> draws = [];
        var inputs = Inputs((current, operation) =>
        {
            var sprite = Assert.IsType<NativeBattleMorph172DrawSprite180>(operation);
            draws.Add(sprite.PilotIdentity!.Value);
            return Feedback(current, sprite.TemporaryAddress, -1);
        }) with { CapturePresentation = true, ReadBoneCount = (_, _) => 7, ReadBone502170 = (current, _, _, _) => new(current, NativeBattleEffectArena.Clear().Write(0, new byte[] { 10, 0, 20, 0, 30, 0 }).MarkUnknown(6, 2)) };
        frame = AdvanceSlots(frame, ChildBank, Resources, inputs).Frame;
        Assert.Empty(draws);
        frame = AdvanceSlots(frame, ChildBank, Resources, inputs).Frame;
        var first = Assert.Single(draws);
        draws.Clear();
        AdvanceSlots(frame, ChildBank, Resources, inputs);
        var next = Assert.Single(draws);
        Assert.Equal(first.Bank, next.Bank);
        Assert.Equal(first.Slot, next.Slot);
        Assert.Equal(first.ResourceGeneration, next.ResourceGeneration);
        Assert.NotEqual(first.Generation, next.Generation);
    }

    private static readonly NativeBattleMorph172Resources Resources = new(Mesh(), null!, Enumerable.Repeat(0x00804020u, 20).ToImmutableArray(), null!, 0);

    [Theory]
    [InlineData(0, 4096)]
    [InlineData(7, 512)]
    [InlineData(8, 0)]
    [InlineData(47, 0)]
    [InlineData(48, 0)]
    [InlineData(55, 3584)]
    [InlineData(56, -1)]
    public void Body_color_origin_hide_and_release_follow_literal_ages_before_pause(int age, int cue)
    {
        var frame = Frame(0x5DA520, (ushort)age);
        List<NativeBattleMorph172Operation> operations = [];
        var inputs = Inputs((current, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleMorph172DrawBody220 draw)
            {
                Assert.Equal(3, draw.Actor);
                var context = Context(current, draw.TemporaryAddress, 220);
                Assert.Equal(new int[] { 100, 200, 300 }, Enumerable.Range(0, 3).Select(axis => unchecked((int)Dword(context, 0xA8 + axis * 4))));
                Assert.Equal(1000u, Dword(context, 0xB4));
                Assert.Equal((uint)cue, current.Memory.ScriptedColorRegisters!.Ir0);
                Assert.Equal(NativeBattleTemporaryMemory.Address + 220, Cursor(current));
            }
            return current;
        });
        var paused = AdvanceBody(frame, ChildBank, Resources, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal((ushort)age, Read(paused.Frame, ChildBank).ReadWord(12));
        Assert.Equal(age < 56 ? 2 : 0, operations.Count);
        operations.Clear();
        var actual = AdvanceBody(frame, ChildBank, Resources, inputs);
        Assert.Equal(age >= 55 ? 2 : 0, actual.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, Cursor(actual.Frame));
        if (age >= 55)
        {
            Assert.Equal(new NativeBattleMorph172ActorFlags(3, 4, 0), operations[^1]);
        }
    }

    [Theory]
    [InlineData(0x5D9F20u, 0, 4096, 0x239BEB8u)]
    [InlineData(0x5DA0F0u, 7, 3200, 0x239B268u)]
    [InlineData(0x5D9F20u, 8, 3072, 0x239BEB8u)]
    [InlineData(0x5DA0F0u, 48, 3072, 0x239B268u)]
    [InlineData(0x5D9F20u, 55, 3968, 0x239BEB8u)]
    public void Mesh_selects_current_XYZ_bank_and_cue_before_pause_then_literal_spin_retirement(uint callback, ushort age, int cue, uint vertices)
    {
        var frame = InitializeRadii5D9EE0(Frame(callback, age), Resources);
        var inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleMorph172DrawStatic88>(operation);
            Assert.Equal(2, draw.DepthShift);
            var context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(vertices, Dword(context, 4));
            Assert.Equal((uint)cue, Dword(context, 12));
            Assert.Equal(0x20F3u, Dword(context, 0x1C));
            return current;
        });
        var paused = AdvanceMesh(frame, ChildBank, Resources, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal(age, Read(paused.Frame, ChildBank).ReadWord(12));
        var actual = AdvanceMesh(frame, ChildBank, Resources, inputs);
        Assert.Equal(age == 55 ? 2 : 0, actual.Result);
        Assert.Equal((ushort)7, Read(actual.Frame, ChildBank).ReadWord(0x18));
    }

    [Fact]
    public void Radii_and_zero_amplitude_write_only_mutable_XYZ_and_retain_unknown_W()
    {
        var frame = Frame(0x5D9F20, 0);
        frame = frame with { State = frame.State.WriteView(MutableVerticesB, NativeBattleEffectArena.Clear().MarkUnknown(0, 261 * 8), 261 * 8) };
        frame = InitializeRadii5D9EE0(frame, Resources);
        Assert.Equal((uint)NativeBattleRayBomb10Geometry.Sqrt56BEC0(100 * 100 + 200 * 200), frame.State.ReadDword(RadiiBase));
        var actual = DeformVertices(frame, Resources, true, 0);
        Assert.Equal(new byte[] { 100, 0, 200, 0, 44, 1 }, actual.State.ReadView(MutableVerticesB, 6).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadView(MutableVerticesB, 8).Read(6, 2));
    }

    [Fact]
    public void Nonzero_deformation_uses_separate_signed_product_shifts_and_retains_W()
    {
        var frame = InitializeRadii5D9EE0(Frame(0x5D9F20, 0), Resources);
        frame = frame with { State = frame.State.WriteWord(MutableVerticesA + 6, 0xBEEF) };
        var actual = DeformVertices(frame, Resources, false, 1337);
        int phase = unchecked(NativeBattleCameraMath.Sine(unchecked((int)frame.State.ReadDword(RadiiBase) * 3)) * 1337) >> 12;
        int x = (100 * NativeBattleCameraMath.Cosine(phase) >> 12) + (200 * NativeBattleCameraMath.Sine(phase) >> 12);
        int y = (-100 * NativeBattleCameraMath.Sine(phase) >> 12) + (200 * NativeBattleCameraMath.Cosine(phase) >> 12);
        Assert.Equal(unchecked((ushort)x), actual.State.ReadWord(MutableVerticesA));
        Assert.Equal(unchecked((ushort)y), actual.State.ReadWord(MutableVerticesA + 2));
        Assert.Equal((ushort)0xBEEF, actual.State.ReadWord(MutableVerticesA + 6));
    }

    [Fact]
    public void Birth_reads_current_count_before_six_RNG_draws_and_retains_point_W_and_suffix()
    {
        var frame = Frame(0x5DA2C0, 3);
        frame = frame with { State = frame.State.WriteWord(ParticleBase + 14, 0xBEEF).WriteWord(ParticleBase + 20, 0xCAFE).WriteWord(ParticleBase + 22, 0x1234) };
        int counts = 0;
        var inputs = Inputs() with
        {
            ReadBoneCount = (current, actor) =>
            {
                Assert.Equal(frame.Random, current.Random);
                Assert.Equal(3, actor);
                counts++;
                return 7;
            },
            ReadBone502170 = (current, actor, selector, distance) =>
            {
                var random = frame.Random.Draw(out _).Draw(out ushort expectedDistance).Draw(out ushort expectedSelector);
                Assert.Equal(random, current.Random);
                Assert.Equal(expectedSelector % 7, selector);
                Assert.Equal(expectedDistance & 4095, distance);
                Assert.Equal(1u, current.State.ReadDword(ParticleBase));
                return new(current, NativeBattleEffectArena.Clear().Write(0, new byte[] { 10, 0, 20, 0, 30, 0 }).MarkUnknown(6, 2));
            }
        };
        var actual = AdvanceSlots(frame, ChildBank, Resources, inputs);
        var expected = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal(1, counts);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)0xBEEF, actual.Frame.State.ReadWord(ParticleBase + 14));
        Assert.Equal((ushort)0xCAFE, actual.Frame.State.ReadWord(ParticleBase + 20));
        Assert.Equal((ushort)0x1234, actual.Frame.State.ReadWord(ParticleBase + 22));
    }

    [Fact]
    public void Paused_active_particle_draws_scales_without_birth_inputs_and_negative_feedback_retires_only_unpaused()
    {
        var frame = Frame(0x5DA2C0, 43);
        frame = frame with { State = frame.State.WriteDword(ParticleBase, 1).WriteWord(ParticleBase + 4, 12).WriteWord(ParticleBase + 6, 4096).WriteWord(ParticleBase + 16, unchecked((ushort)-4096)).WriteWord(ParticleBase + 18, 4096) };
        int draws = 0;
        var inputs = Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleMorph172DrawSprite180>(operation);
            var context = Context(current, draw.TemporaryAddress, 180);
            Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2)));
            Assert.Equal(-4096, unchecked((int)Dword(context, 12)));
            Assert.Equal(4096, unchecked((int)Dword(context, 16)));
            draws++;
            return Feedback(current, draw.TemporaryAddress, -1);
        });
        var paused = AdvanceSlots(frame, ChildBank, Resources, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal(1u, paused.Frame.State.ReadDword(ParticleBase));
        Assert.Equal(frame.Random, paused.Frame.Random);
        var actual = AdvanceSlots(frame, ChildBank, Resources, inputs);
        Assert.Equal(0u, actual.Frame.State.ReadDword(ParticleBase));
        Assert.Equal(2, actual.Result);
        Assert.Equal(2, draws);
    }

    private static NativeBattleMorph172Frame Frame(uint callback, ushort age)
    {
        var root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        var child = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (child, _) = child.Append(callback);
        NativeBattleOriginalEffectState effect = new(172, 171, new(172, Entry, Initializer), null, RootDescriptor) { Pools = [root, child] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleMorph172Frame frame = new(memory, effect, new(123), NativeBattleMorph172State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, 3) };
        return Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, age).WriteWord(14, 1).WriteWord(0x10, 100).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 77).WriteWord(0x18, 0).WriteWord(0x1A, 7).WriteWord(0x1C, 4096));
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[8 + 261 * 8];
        for (int index = 0; index < 261; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), 100);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), 200);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), 300);
        }
        return new(0, 0xD6299C, bytes.ToImmutableArray());
    }
    private static NativeBattleMorph172Inputs Inputs(Func<NativeBattleMorph172Frame, NativeBattleMorph172Operation, NativeBattleMorph172Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute ?? ((current, operation) => operation is NativeBattleMorph172DrawSprite180 draw ? Feedback(current, draw.TemporaryAddress, 0) : current));
    private static NativeBattleMorph172Frame Feedback(NativeBattleMorph172Frame frame, uint temporary, short value) => PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0x28, BitConverter.GetBytes(value)), 180);
    private static uint Cursor(NativeBattleMorph172Frame frame) => BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
}
