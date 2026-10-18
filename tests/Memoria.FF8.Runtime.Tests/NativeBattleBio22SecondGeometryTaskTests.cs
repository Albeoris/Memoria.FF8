using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22SecondGeometryTaskTests
{
    [Fact]
    public void Initial_draw_precedes_all_five_CRT_pulsation_calls_and_preserves_parent_fourth_word()
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = fixture.Frame with { Primary = fixture.Frame.Primary.WriteWord(0x86, unchecked((ushort)-4097)).WriteWord(0x82, 123).WriteWord(0x7E, 0xBEEF).WriteWord(0x88, 1) };
        int draws = 0;
        NativeBattleBio22SecondGeometryInputs inputs = Inputs() with
        {
            Draw = (current, mesh, deformed) =>
            {
                draws++;
                Assert.Equal(unchecked((uint)-4097), current.Task.ReadDword(0x30));
                Assert.Equal((ushort)123, current.Task.ReadWord(0x46));
                Assert.Equal((ushort)0xBEEF, current.Task.ReadWord(0x22));
                Assert.Equal((byte)0, current.Task.ReadByte(0x29));
                Assert.Same(mesh, current.Memory.Bio22ThirdGeometry);
                Assert.True(deformed);
                Assert.Equal(frame.Random, current.Random);
                return current;
            }
        };
        NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(0x123400, frame, fixture.Resources, inputs);
        Assert.Equal(1, draws);
        Assert.Equal(Draws(frame.Random, 5), step.Frame.Random);
        Assert.Equal((ushort)169, step.Frame.Task.ReadWord(0x64));
        Assert.Equal((ushort)99, step.Frame.Task.ReadWord(0x66));
        Assert.Equal((ushort)78, step.Frame.Task.ReadWord(0x68));
        Assert.Equal((ushort)36, step.Frame.Task.ReadWord(0x44));
        Assert.Equal((ushort)156, step.Frame.Task.ReadWord(0x46));
        Assert.Equal(unchecked((uint)(-4097 + -1024 * NativeBattleCameraMath.Sine(169) / 4096)), step.Frame.Task.ReadDword(0x30));
        Assert.Equal((byte)1, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((ushort)1, step.Frame.Task.ReadWord(0x24));
    }

    [Fact]
    public void Phase_one_switch_draws_fourth_mesh_at_zero_ratio_without_an_extra_random_draw()
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = Initialized(fixture.Frame, fixture.Resources) with { Random = new(1) };
        frame = frame with { Task = frame.Task.WriteWord(0x84, 0xC00), Primary = frame.Primary.WriteWord(0x88, 2) };
        int draws = 0;
        NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(10, frame, fixture.Resources, Inputs() with
        {
            Draw = (current, mesh, deformed) =>
            {
                draws++;
                Assert.False(deformed);
                Assert.Same(mesh, current.Memory.Bio22FourthGeometry);
                Assert.Equal(0x161F750u, current.Task.ReadDword(0x4C));
                Assert.Equal((ushort)0, current.Task.ReadWord(0x84));
                Assert.Equal((ushort)4, current.Task.ReadWord(0x74));
                Assert.Equal((ushort)0, current.Task.ReadWord(0x76));
                return current;
            }
        });
        Assert.Equal(1, draws);
        Assert.Equal(Draws(frame.Random, 5), step.Frame.Random);
        Assert.Same(frame.Memory.Bio22ThirdGeometry, step.Frame.Memory.Bio22ThirdGeometry);
    }

    [Fact]
    public void Fifth_transition_draws_then_emits_thirty_one_children_and_publishes_primary80_before_overlay_append()
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = Fourth(fixture.Frame, fixture.Resources);
        frame = frame with { Task = frame.Task.WriteWord(0x84, 0xC00).WriteWord(0x8A, 4), Random = new(1) };
        List<string> order = [];
        int particles = 0;
        NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(0x300000, frame, fixture.Resources, Inputs() with
        {
            Draw = (current, mesh, deformed) =>
            {
                order.Add("draw");
                Assert.Equal((ushort)3, current.Task.ReadWord(0x76));
                Assert.Equal((ushort)5, current.Task.ReadWord(0x8A));
                Assert.Equal(Draws(frame.Random, 1), current.Random);
                return current;
            },
            AppendParticle = (current, request) =>
            {
                order.Add("particle");
                particles++;
                Assert.Equal(0x300000u, request.ParentAddress);
                Assert.Equal(0x89C930u, NativeBattleBio22ParticleAppend.Callback);
                return current with { Task = current.Task.WriteByte(0x28, (byte)(current.Task.ReadByte(0x28) + 1)) };
            },
            AppendOverlay = (current, request) =>
            {
                order.Add("overlay");
                Assert.Equal((ushort)1, current.Primary.ReadWord(0x80));
                Assert.Equal((byte)31, current.Task.ReadByte(0x28));
                Assert.Equal(0x300000u, request.ParentAddress);
                Assert.Equal(0x162693Cu, request.ProgramAddress);
                return current with { Task = current.Task.WriteByte(0x28, 32), Memory = current.Memory with { Parameter80 = 77 } };
            }
        });
        Assert.Equal(31, particles);
        Assert.Equal("draw", order[0]);
        Assert.Equal("overlay", order[^1]);
        Assert.Equal((byte)3, step.Frame.Task.ReadByte(0x29));
        Assert.Equal((byte?)77, step.Frame.Memory.Parameter80);
        Assert.Equal(Draws(frame.Random, 6), step.Frame.Random);
        Assert.False(step.CompleteRequested);
    }

    [Theory]
    [InlineData((byte)3, 31, 4, false)]
    [InlineData((byte)4, 31, 5, false)]
    [InlineData((byte)5, 0, 6, true)]
    [InlineData((byte)6, 0, 6, true)]
    public void Emission_only_and_terminal_phases_never_draw_but_always_consume_five_random_values(byte phase, int expectedParticles, int nextPhase, bool complete)
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = Fourth(fixture.Frame, fixture.Resources);
        frame = frame with { Task = frame.Task.WriteByte(0x29, phase).WriteByte(0x26, phase == 6 ? (byte)1 : (byte)0).WriteWord(0x24, 0xFFFF), Random = new(1) };
        int particles = 0;
        NativeBattleBio22SecondGeometryInputs inputs = Inputs() with
        {
            Draw = (_, _, _) => throw new InvalidOperationException("Emission-only phase drew geometry."),
            AppendParticle = (current, request) =>
            {
                particles++;
                return current;
            }
        };
        NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(5, frame, fixture.Resources, inputs);
        Assert.Equal(expectedParticles, particles);
        Assert.Equal((byte)nextPhase, step.Frame.Task.ReadByte(0x29));
        Assert.Equal(complete, step.CompleteRequested);
        Assert.Equal(Draws(frame.Random, 5), step.Frame.Random);
        Assert.Equal((ushort)0, step.Frame.Task.ReadWord(0x24));
    }

    [Fact]
    public void Emission_uses_current_mesh_saturated_transform_and_WORD_delta_before_signed_division()
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = Fourth(fixture.Frame, fixture.Resources);
        byte[] bytes = frame.Memory.Bio22FourthGeometry!.Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), short.MaxValue);
        frame = frame with
        {
            Task = frame.Task.WriteWord(0x1C, unchecked((ushort)-30000)).WriteWord(0x44, 0).WriteWord(0x46, 0).WriteWord(0x48, 0).WriteDword(0x30, 8192).WriteDword(0x34, 4096).WriteDword(0x38, 4096),
            Memory = frame.Memory with { Bio22FourthGeometry = new(bytes.ToImmutableArray()) }
        };
        List<NativeBattleBio22ParticleAppend> requests = [];
        NativeBattleBio22SecondGeometryFrame after = NativeBattleBio22SecondGeometryTask.Emit89C750(123, frame, 0, (current, request) =>
        {
            requests.Add(request);
            return current with { Task = current.Task.WriteByte(0x28, (byte)(current.Task.ReadByte(0x28) + 1)) };
        });
        Assert.Equal(31, requests.Count);
        Assert.Equal(short.MaxValue, requests[0].Position.X);
        Assert.Equal((short)-346, requests[0].Velocity.X);
        Assert.Equal((short)21, requests[0].Acceleration.X);
        Assert.Equal((byte)31, after.Task.ReadByte(0x28));
        Assert.Equal(frame.Random, after.Random);
        Assert.Equal(frame.Memory, after.Memory);
    }

    [Fact]
    public void Every_phase_continues_identically_from_an_independent_JSON_frame()
    {
        var fixture = Fixture();
        NativeBattleBio22SecondGeometryFrame frame = fixture.Frame;
        NativeBattleBio22SecondGeometryInputs inputs = Inputs();
        int draws = 0;
        int particles = 0;
        int overlays = 0;
        inputs = inputs with
        {
            Draw = (current, mesh, deformed) =>
            {
                draws++;
                return current;
            },
            AppendParticle = (current, request) =>
            {
                particles++;
                return current;
            },
            AppendOverlay = (current, request) =>
            {
                overlays++;
                return current;
            }
        };
        for (int tick = 0; tick < 100; tick++)
        {
            NativeBattleBio22SecondGeometryFrame restored = JsonSerializer.Deserialize<NativeBattleBio22SecondGeometryFrame>(JsonSerializer.Serialize(frame))!;
            NativeBattleBio22SecondGeometryStep expected = NativeBattleBio22SecondGeometryTask.Advance89C270(123, restored, fixture.Resources, Inputs());
            NativeBattleBio22SecondGeometryStep step = NativeBattleBio22SecondGeometryTask.Advance89C270(123, frame, fixture.Resources, inputs);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(step));
            frame = step.Frame;
            if (step.CompleteRequested)
            {
                Assert.Equal(93, particles);
                Assert.Equal(1, overlays);
                Assert.Equal(24, draws);
                return;
            }
        }
        Assert.Fail("Synthetic second geometry did not reach its explicit terminal flag.");
    }

    private static NativeBattleBio22SecondGeometryInputs Inputs() => new(KeepDraw, KeepParticle, KeepOverlay);
    private static NativeBattleBio22SecondGeometryFrame KeepDraw(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22Geometry mesh, bool deformed) => frame;
    private static NativeBattleBio22SecondGeometryFrame KeepParticle(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22ParticleAppend request) => frame;
    private static NativeBattleBio22SecondGeometryFrame KeepOverlay(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22OverlayAppend request) => frame;
    private static NativeBattleBio22SecondGeometryFrame Initialized(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22Resources resources) => NativeBattleBio22SecondGeometryTask.Advance89C270(1, frame, resources, Inputs()).Frame;
    private static NativeBattleBio22SecondGeometryFrame Fourth(NativeBattleBio22SecondGeometryFrame frame, NativeBattleBio22Resources resources)
    {
        frame = Initialized(frame, resources);
        return NativeBattleBio22SecondGeometryTask.Advance89C270(1, frame with { Task = frame.Task.WriteWord(0x84, 0xC00) }, resources, Inputs()).Frame;
    }
    private static NativeCrtRandomState Draws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
    private static (NativeBattleBio22SecondGeometryFrame Frame, NativeBattleBio22Resources Resources) Fixture()
    {
        var first = NativeBattleBio22GeometryTaskTests.Fixture();
        NativeBattleEffectMemory memory = first.Memory with { Bio22ThirdGeometry = NativeBattleBio22Geometry.FromColdProcess(first.Resources.ThirdMeshBaseline), Bio22FourthGeometry = NativeBattleBio22Geometry.FromColdProcess(first.Resources.FourthMeshBaseline) };
        NativeBattleModulePoolTask task = first.Task with { Callback = 0x89C270 };
        return (new(task, first.Parent, memory, NativeCrtRandomState.FromColdProcess()), first.Resources);
    }
}
