using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22ParticleTests
{
    [Theory]
    [InlineData((ushort)11, (ushort)12, 0u, (ushort)12, (byte)0x80)]
    [InlineData((ushort)12, (ushort)12, 1u, (ushort)12, (byte)0x84)]
    [InlineData((ushort)0x7FFF, (ushort)12, 0u, (ushort)0x8000, (byte)0x80)]
    public void Shared_frame_advance_uses_signed_comparison_equal_limit_and_only_hidden_bit(ushort frame, ushort limit, uint result, ushort next, byte flags)
    {
        NativeBattleModulePoolTask task = Task(0x89C930).WriteWord(0x50, frame).WriteWord(0x52, limit).WriteByte(0x26, 0x80);
        NativeBattleBio22FrameAdvance advanced = NativeBattleBio22Particles.AdvanceFrame89BEE0(task);
        Assert.Equal(result, advanced.Result);
        Assert.Equal(next, advanced.Task.ReadWord(0x50));
        Assert.Equal(flags, advanced.Task.ReadByte(0x26));
        Assert.Equal(task.ReadWord(0x24), advanced.Task.ReadWord(0x24));
        Assert.Equal(task.ReadByte(0x29), advanced.Task.ReadByte(0x29));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(10u)]
    public void Emitted_initialization_uses_one_CRT_and_first_level_signed_scale_without_resetting_motion(uint seed)
    {
        NativeBattleBio22ParticleFrame frame = Frame(0x89C930) with { Random = new(seed) };
        frame = frame with { Primary = frame.Primary.WriteWord(0x86, unchecked((ushort)-4097)), Task = frame.Task.WriteWord(0x58, 111).WriteWord(0x62, 222).WriteWord(0x1E, 0) };
        NativeCrtRandomState expected = frame.Random.Draw(out ushort value);
        NativeBattleBio22ParticleStep step = NativeBattleBio22Particles.Advance89C930(frame, Inputs());
        uint scale = unchecked((uint)(((value & 7) + 1) * -4097 / 8));
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(scale, step.Frame.Task.ReadDword(0x30));
        Assert.Equal(scale, step.Frame.Task.ReadDword(0x34));
        Assert.Equal(scale, step.Frame.Task.ReadDword(0x38));
        Assert.Equal((ushort)111, step.Frame.Task.ReadWord(0x58));
        Assert.Equal((ushort)222, step.Frame.Task.ReadWord(0x62));
        Assert.Equal(0x16156F8u, step.Frame.Task.ReadDword(0x4C));
    }

    [Fact]
    public void Emitted_physics_wraps_new_velocity_before_motion_and_retains_acceleration62()
    {
        NativeBattleBio22ParticleFrame frame = Frame(0x89C930);
        frame = frame with { Task = frame.Task.WriteByte(0x29, 1).WriteWord(0x52, 12).WriteWord(0x58, 0x7FFF).WriteWord(0x60, 1).WriteWord(0x1C, 1).WriteWord(0x5C, unchecked((ushort)-10)).WriteWord(0x64, 3).WriteWord(0x20, 30).WriteWord(0x5A, 5).WriteWord(0x62, 2).WriteWord(0x1E, unchecked((ushort)-100)) };
        NativeBattleBio22ParticleStep step = NativeBattleBio22Particles.Advance89C930(frame, Inputs());
        Assert.Equal((ushort)0x8000, step.Frame.Task.ReadWord(0x58));
        Assert.Equal((ushort)0x8001, step.Frame.Task.ReadWord(0x1C));
        Assert.Equal((ushort)23, step.Frame.Task.ReadWord(0x20));
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x5A));
        Assert.Equal(unchecked((ushort)-99), step.Frame.Task.ReadWord(0x1E));
        Assert.Equal((ushort)2, step.Frame.Task.ReadWord(0x62));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Only_emitted_particles_have_the_positive_Y_hidden_completion_gate_before_draw()
    {
        NativeBattleBio22ParticleFrame emitted = Frame(0x89C930);
        emitted = emitted with { Task = emitted.Task.WriteByte(0x29, 2).WriteWord(0x1E, 1).WriteDword(0x30, 0xFFFF8000) };
        int draws = 0;
        NativeBattleBio22ParticleInputs inputs = Inputs() with
        {
            DrawBillboard = (current, scale) =>
            {
                draws++;
                Assert.Equal((byte)5, current.Task.ReadByte(0x26));
                Assert.Equal(short.MinValue, scale);
                return current with { Memory = current.Memory with { Parameter80 = 67 } };
            }
        };
        NativeBattleBio22ParticleStep hidden = NativeBattleBio22Particles.Advance89C930(emitted, inputs);
        Assert.True(hidden.CompleteRequested);
        Assert.Equal((byte?)67, hidden.Frame.Memory.Parameter80);
        Assert.Equal(1, draws);
        NativeBattleBio22ParticleFrame billboard = emitted with { Task = emitted.Task with { Callback = 0x89BD20 } };
        NativeBattleBio22ParticleStep visible = NativeBattleBio22Particles.Advance89BD20(billboard, Inputs());
        Assert.False(visible.CompleteRequested);
        Assert.Equal((byte)0, visible.Frame.Task.ReadByte(0x26));
    }

    [Theory]
    [InlineData((ushort)0, 0x1614F74u)]
    [InlineData((ushort)0x8000, 0x1615120u)]
    public void Billboard_initialization_uses_two_CRT_values_and_update_uses_old_Y_velocity_without_RNG(ushort variant, uint resource)
    {
        NativeBattleBio22ParticleFrame frame = Frame(0x89BD20);
        frame = frame with { Task = frame.Task.WriteWord(0x68, variant).WriteWord(0x1E, 1000) };
        NativeCrtRandomState random = frame.Random.Draw(out ushort angle).Draw(out ushort speed);
        NativeBattleBio22ParticleStep initial = NativeBattleBio22Particles.Advance89BD20(frame, Inputs());
        Assert.Equal(random, initial.Frame.Random);
        Assert.Equal(resource, initial.Frame.Task.ReadDword(0x4C));
        Assert.Equal((ushort)(angle & 0xFFF), initial.Frame.Task.ReadWord(0x6A));
        Assert.Equal((ushort)((speed & 0x80) + 0x1C0), initial.Frame.Task.ReadWord(0x6C));
        NativeBattleBio22ParticleStep moving = NativeBattleBio22Particles.Advance89BD20(initial.Frame, Inputs());
        int newAngle = ((angle & 0xFFF) + (speed & 0x80) + 0x1C0) & 0xFFF;
        Assert.Equal((ushort)936, moving.Frame.Task.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)(NativeBattleCameraMath.Sine(newAngle) / 128)), moving.Frame.Task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)(NativeBattleCameraMath.Cosine(newAngle) / 128)), moving.Frame.Task.ReadWord(0x20));
        Assert.Equal(random, moving.Frame.Random);
        Assert.False(moving.CompleteRequested);
    }

    [Fact]
    public void Planar_final_equal_frame_draws_then_overflow_appends_full_scale_block_and_waits_for_child()
    {
        NativeBattleBio22ParticleFrame frame = Frame(0x89BF20);
        frame = frame with { Task = frame.Task.WriteWord(0x68, 7).WriteDword(0x30, 0x12348000).WriteDword(0x34, 2).WriteDword(0x38, 3).WriteDword(0x3C, 0xDDEEFF11) };
        NativeBattleBio22ParticleStep initialized = NativeBattleBio22Particles.Advance89BF20(99, frame, Inputs());
        Assert.Equal((ushort)0x400, initialized.Frame.Task.ReadWord(0x40));
        Assert.Equal(0x1615440u, initialized.Frame.Task.ReadDword(0x4C));
        int appends = 0;
        List<byte> drawFlags = [];
        NativeBattleBio22ParticleInputs inputs = Inputs() with
        {
            AppendBillboard = (current, request) =>
            {
                appends++;
                Assert.Equal(99u, request.ParentAddress);
                Assert.Equal((ushort)7, request.Variant);
                Assert.Equal(frame.Task.Payload.AsSpan().Slice(0x30 - 12, 16).ToArray(), request.ScaleBlock16.ToArray());
                Assert.Equal(0xDDEEFF11u, BinaryPrimitives.ReadUInt32LittleEndian(request.ScaleBlock16.AsSpan().Slice(12)));
                Assert.Equal((byte)4, current.Task.ReadByte(0x26));
                return current with { Task = current.Task.WriteByte(0x28, 1) };
            },
            DrawPlanar = current =>
            {
                drawFlags.Add(current.Task.ReadByte(0x26));
                return current;
            }
        };
        NativeBattleBio22ParticleStep equal = NativeBattleBio22Particles.Advance89BF20(99, initialized.Frame with { Task = initialized.Frame.Task.WriteWord(0x50, 11) }, inputs);
        Assert.Equal(0, appends);
        Assert.Equal((byte)0, equal.Frame.Task.ReadByte(0x26));
        NativeBattleBio22ParticleStep overflow = NativeBattleBio22Particles.Advance89BF20(99, equal.Frame, inputs);
        Assert.Equal(1, appends);
        Assert.Equal(new byte[] { 0, 5 }, drawFlags);
        Assert.False(overflow.CompleteRequested);
        Assert.Equal((byte)2, overflow.Frame.Task.ReadByte(0x29));
        NativeBattleBio22ParticleStep released = NativeBattleBio22Particles.Advance89BF20(99, overflow.Frame with { Task = overflow.Frame.Task.WriteByte(0x28, 0) }, inputs);
        Assert.True(released.CompleteRequested);
        Assert.Equal(1, appends);
        Assert.Equal(frame.Random, released.Frame.Random);
    }

    [Theory]
    [InlineData(0x89C930u)]
    [InlineData(0x89BD20u)]
    [InlineData(0x89BF20u)]
    public void All_particle_callbacks_preserve_retained_tail_and_independent_JSON_next(uint callback)
    {
        NativeBattleBio22ParticleFrame frame = Frame(callback);
        frame = frame with { Task = frame.Task.WriteWord(0x6E, 0xCAFE).WriteWord(0x1E, unchecked((ushort)-1000)) };
        for (int tick = 0; tick < 25; tick++)
        {
            NativeBattleBio22ParticleFrame restored = JsonSerializer.Deserialize<NativeBattleBio22ParticleFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleBio22ParticleStep expected = Advance(callback, restored, Inputs());
            NativeBattleBio22ParticleStep step = Advance(callback, frame, Inputs());
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(step));
            Assert.Equal((ushort)0xCAFE, step.Frame.Task.ReadWord(0x6E));
            frame = step.Frame;
            if (step.CompleteRequested)
            {
                return;
            }
        }
        Assert.Fail("Synthetic particle did not reach its explicit completion flag.");
    }

    [Fact]
    public void Real_same_pool_append_advances_new_billboard_in_same_pass_and_releases_planar_only_after_child()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x270E540, 0x270E550, 0x70, 3)]);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(0, 0x89BF20, 0x70);
        uint planarAddress = first.Address;
        hierarchy = first.Hierarchy;
        NativeBattleModulePoolTask planar = hierarchy.Read(planarAddress).WriteByte(0x29, 1).WriteWord(0x50, 12).WriteWord(0x52, 12).WriteWord(0x68, 1).WriteDword(0x3C, 0xABCDEF12).WriteDword(0x0C, 0x1111).WriteByte(0x2A, 2).WriteByte(0x2B, 3).WriteDword(0x14, 0x2222).WriteWord(0x22, 0xBEEF);
        hierarchy = hierarchy.Write(planarAddress, planar);
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        uint childAddress = 0;
        List<uint> order = [];
        void Tick()
        {
            hierarchy = hierarchy.Advance(0, (current, address) =>
            {
                order.Add(address);
                hierarchy = current;
                NativeBattleBio22ParticleFrame frame = Frame(current.Read(address).Callback) with { Task = current.Read(address), Random = random };
                NativeBattleBio22ParticleInputs inputs = Inputs() with
                {
                    AppendBillboard = (caller, request) =>
                    {
                        hierarchy = hierarchy.Write(address, caller.Task);
                        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, NativeBattleBio22BillboardAppend.Callback, NativeBattleBio22BillboardAppend.Size, request.ParentAddress, (parameter, header, target) =>
                        {
                            Assert.Equal(0x1111u, parameter);
                            Assert.Equal((sbyte)2, header);
                            Assert.Equal((sbyte)3, target);
                            return new(4, 5);
                        });
                        Assert.NotEqual(0u, append.Address);
                        hierarchy = append.Hierarchy;
                        childAddress = append.Address;
                        NativeBattleModulePoolTask child = hierarchy.Read(childAddress).WriteWord(0x68, request.Variant);
                        for (int index = 0; index < 4; index++)
                        {
                            child = child.WriteDword(0x30 + index * 4, BinaryPrimitives.ReadUInt32LittleEndian(request.ScaleBlock16.AsSpan().Slice(index * 4)));
                        }
                        hierarchy = hierarchy.Write(childAddress, child);
                        Assert.Equal(0x2222u, child.ReadDword(0x14));
                        Assert.Equal(planarAddress, child.ReadDword(0x18));
                        Assert.Equal((ushort)0xBEEF, child.ReadWord(0x22));
                        return caller with { Task = hierarchy.Read(address) };
                    }
                };
                NativeBattleBio22ParticleStep step = Advance(frame.Task.Callback, frame, inputs, address);
                random = step.Frame.Random;
                hierarchy = hierarchy.Write(address, step.Frame.Task);
                if (step.CompleteRequested)
                {
                    hierarchy = hierarchy.Complete8DC530(address);
                }
                return new(hierarchy, (int)step.Result);
            }).Hierarchy;
        }
        Tick();
        Assert.Equal(new uint[] { planarAddress, childAddress }, order);
        Assert.Equal((byte)1, hierarchy.Read(planarAddress).ReadByte(0x28));
        Assert.Equal((byte)1, hierarchy.Read(childAddress).ReadByte(0x29));
        Assert.Equal(0xABCDEF12u, hierarchy.Read(childAddress).ReadDword(0x3C));
        NativeCrtRandomState expected = NativeCrtRandomState.FromColdProcess().Draw(out _).Draw(out _);
        Assert.Equal(expected, random);
        for (int tick = 0; tick < 20; tick++)
        {
            Tick();
        }
        Assert.Null(hierarchy.Pools[0].Head);
        Assert.All(hierarchy.Pools[0].Slots, task => Assert.Equal(0, task.Flags & 1));
    }

    private static NativeBattleBio22ParticleStep Advance(uint callback, NativeBattleBio22ParticleFrame frame, NativeBattleBio22ParticleInputs inputs, uint address = 1) => callback switch
    {
        0x89C930 => NativeBattleBio22Particles.Advance89C930(frame, inputs),
        0x89BD20 => NativeBattleBio22Particles.Advance89BD20(frame, inputs),
        0x89BF20 => NativeBattleBio22Particles.Advance89BF20(address, frame, inputs),
        _ => throw new InvalidOperationException()
    };
    private static NativeBattleBio22ParticleInputs Inputs() => new(KeepBillboard, KeepPlanar, KeepAppend);
    private static NativeBattleBio22ParticleFrame KeepBillboard(NativeBattleBio22ParticleFrame frame, short scale) => frame;
    private static NativeBattleBio22ParticleFrame KeepPlanar(NativeBattleBio22ParticleFrame frame) => frame;
    private static NativeBattleBio22ParticleFrame KeepAppend(NativeBattleBio22ParticleFrame frame, NativeBattleBio22BillboardAppend request) => frame;
    private static NativeBattleBio22ParticleFrame Frame(uint callback) => new(Task(callback), new(0, 0, null, 0x89A8D0, new byte[0x90].ToImmutableArray()), NativeBattleEffectMemory.FromColdProcess(), NativeCrtRandomState.FromColdProcess());
    private static NativeBattleModulePoolTask Task(uint callback) => new(0, 0, null, callback, new byte[0x70 - 12].ToImmutableArray());
}
