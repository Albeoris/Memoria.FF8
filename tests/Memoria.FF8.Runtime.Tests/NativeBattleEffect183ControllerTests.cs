using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect183Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect183ControllerTests
{
    private const uint Director = 0x22F3400;
    private const uint FirstChild = Director + 36;
    private static readonly NativeBattleEffect183Resources Resources = new(null!, null!, [0x808080, 0x608080, 0x408080, 0x406080, 0x404080, 0x604080, 0x804080, 0x804060, 0x804040, 0x806060], 0x49490);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void First_director_callback_writes_bone_XYZ_and_preserves_retained_point_suffix(bool unknownW)
    {
        NativeBattleEffect183Frame frame = Frame(0);
        if (unknownW)
        {
            frame = Write(frame, Director, Read(frame, Director).MarkPayloadUnknown(22, 2));
        }
        int calls = 0;
        var inputs = Inputs() with
        {
            ReadBone502170 = (current, actor, bone, scale) =>
            {
                Assert.Equal(5, actor);
                Assert.Equal(0xF1, bone);
                Assert.Equal(0, scale);
                calls++;
                current = current with { State = current.State.WriteDword(0x22F33C8, 99) };
                return new(current, Point(100, -200, 300).MarkUnknown(6, 2));
            }
        };
        var step = AdvanceDirector(frame, Director, Resources, inputs);
        Assert.Equal(1, calls);
        Assert.Equal(0, step.Result);
        Assert.Equal(99u, step.Frame.State.ReadDword(0x22F33C8));
        Assert.Equal(new byte[] { 100, 0, 56, 255, 44, 1 }, Read(step.Frame, Director).ReadBytes(16, 6));
        if (unknownW)
        {
            Assert.Throws<NotSupportedException>(() => Read(step.Frame, Director).ReadWord(22));
        }
        else
        {
            Assert.Equal((ushort)0xCAFE, Read(step.Frame, Director).ReadWord(22));
        }
        Assert.Equal((ushort)1, Read(step.Frame, Director).ReadWord(12));
        Assert.Null(step.Frame.Effect.Pools[1].Slots[0].Next);
    }

    [Theory]
    [InlineData(1, 0x5C9C40u, 0x2000)]
    [InlineData(6, 0x5C9DD0u, 0x700)]
    public void Spawn_copies_complete_point_and_heading_before_first_sound(short age, uint callback, ushort size)
    {
        NativeBattleEffect183Frame frame = Frame(age);
        frame = Write(frame, FirstChild, Read(frame, FirstChild).WriteWord(30, 0xBEEF));
        List<string> order = [];
        var inputs = Inputs() with
        {
            ReadHeading = actor =>
            {
                Assert.Equal(5, actor);
                order.Add("heading");
                return -1234;
            },
            ReadAnchor571400 = (current, actor) =>
            {
                Assert.Equal(5, actor);
                Assert.Equal(callback, Read(current, FirstChild).Callback);
                Assert.Equal(size, Read(current, FirstChild).ReadWord(28));
                order.Add("anchor");
                return new(current, Point(-7, 8, 9).MarkUnknown(6, 2));
            },
            Execute = (current, operation) =>
            {
                NativeBattleEffect183Sound sound = Assert.IsType<NativeBattleEffect183Sound>(operation);
                Assert.Equal(0x49490u, sound.Packed);
                Assert.Equal(new byte[] { 249, 255, 8, 0, 9, 0 }, sound.Anchor8.Read(0, 6));
                Assert.Throws<NotSupportedException>(() => sound.Anchor8.Read(6, 2));
                order.Add("sound");
                return current;
            }
        };
        var step = AdvanceDirector(frame, Director, Resources, inputs);
        NativeBattleModulePoolTask child = Read(step.Frame, FirstChild);
        Assert.Equal(callback, child.Callback);
        Assert.Equal((ushort)0, child.ReadWord(12));
        Assert.Equal(Read(frame, Director).ReadBytes(16, 8), child.ReadBytes(16, 8));
        Assert.Equal(unchecked((ushort)-1234), child.ReadWord(24));
        Assert.Equal(size, child.ReadWord(28));
        Assert.Equal(age == 1 ? (ushort)512 : (ushort)0xBEEF, child.ReadWord(30));
        Assert.Equal(age == 1 ? new[] { "heading", "anchor", "sound" } : new[] { "heading" }, order);
        Assert.Equal((ushort)(age + 1), Read(step.Frame, Director).ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Spawn_transports_unknown_point_suffix_without_consuming_it()
    {
        NativeBattleEffect183Frame frame = Frame(1);
        frame = Write(frame, Director, Read(frame, Director).MarkPayloadUnknown(22, 2));
        var step = AdvanceDirector(frame, Director, Resources, Inputs());
        Assert.Equal(Read(frame, Director).ReadBytes(16, 6), Read(step.Frame, FirstChild).ReadBytes(16, 6));
        Assert.Throws<NotSupportedException>(() => Read(step.Frame, FirstChild).ReadWord(22));
    }

    [Fact]
    public void Contact_uses_captured_first_header_at_thirty_and_increments_after_operation()
    {
        NativeBattleEffect183Frame frame = Frame(30);
        int calls = 0;
        var step = AdvanceDirector(frame, Director, Resources, Inputs() with
        {
            Execute = (current, operation) =>
            {
                Assert.Equal(7, Assert.IsType<NativeBattleEffect183ContactFirst>(operation).HeaderIndex);
                Assert.Equal((ushort)30, Read(current, Director).ReadWord(12));
                calls++;
                return current;
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)31, Read(step.Frame, Director).ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(30)]
    [InlineData(35)]
    public void Paused_director_does_not_consume_inputs_or_modify_state(short age)
    {
        NativeBattleEffect183Frame frame = Frame(age);
        var inputs = Inputs() with
        {
            GraphicsFlags = 0x201,
            ReadBone502170 = (_, _, _, _) => throw new InvalidOperationException(),
            ReadHeading = _ => throw new InvalidOperationException(),
            ReadAnchor571400 = (_, _) => throw new InvalidOperationException(),
            Execute = (_, _) => throw new InvalidOperationException()
        };
        var step = AdvanceDirector(frame, Director, Resources, inputs);
        Assert.Same(frame, step.Frame);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(34, 0)]
    [InlineData(35, 2)]
    public void Director_retirement_uses_postincrement_signed_age_and_roundtrips(short age, int expected)
    {
        NativeBattleEffect183Frame frame = Frame(age);
        var step = AdvanceDirector(frame, Director, Resources, Inputs());
        Assert.Equal(expected, step.Result);
        Assert.Equal((ushort)(age + 1), Read(step.Frame, Director).ReadWord(12));
        NativeBattleEffect183Frame restored = JsonSerializer.Deserialize<NativeBattleEffect183Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(step.Frame), JsonSerializer.Serialize(AdvanceDirector(restored, Director, Resources, Inputs()).Frame));
    }

    [Fact]
    public void Root_selects_packet_parity_and_visits_appended_child_in_same_pass()
    {
        NativeBattleEffect183Frame frame = Frame(1);
        List<string> order = [];
        List<uint> cursors = [];
        NativeBattleEffect183Inputs inputs = Inputs() with
        {
            Execute = (current, operation) =>
            {
                if (operation is NativeBattleEffect183Sound)
                {
                    Assert.Equal((ushort)0, Read(current, FirstChild).ReadWord(12));
                    order.Add("sound");
                }
                else
                {
                    Assert.IsType<NativeBattleEffect183DrawWindowed144>(operation);
                    Assert.Equal((ushort)0, Read(current, FirstChild).ReadWord(12));
                    order.Add("draw");
                    cursors.Add(current.State.ReadDword(0x230421C));
                }
                return current;
            }
        };
        NativeBattleEffect183Frame first = Advance(frame, Resources, inputs);
        Assert.Equal(new[] { "sound", "draw" }, order);
        Assert.Equal(new uint[] { 0x22F421C }, cursors);
        Assert.Equal((ushort)1, Read(first, FirstChild).ReadWord(12));
        Assert.Equal((ushort)0x1E00, Read(first, FirstChild).ReadWord(28));
        Assert.Equal((ushort)1, Read(first, 0x22F33D0).ReadWord(12));
        NativeBattleEffect183Frame restored = JsonSerializer.Deserialize<NativeBattleEffect183Frame>(JsonSerializer.Serialize(first))!;
        var nextInputs = Inputs() with
        {
            Execute = (current, operation) =>
            {
                Assert.IsType<NativeBattleEffect183DrawWindowed144>(operation);
                Assert.Equal(0x22FC21Cu, current.State.ReadDword(0x230421C));
                return current;
            }
        };
        Assert.Equal(JsonSerializer.Serialize(Advance(first, Resources, nextInputs)), JsonSerializer.Serialize(Advance(restored, Resources, nextInputs)));
    }

    [Fact]
    public void Root_captures_both_drained_banks_after_final_director_and_child_callback()
    {
        NativeBattleEffect183Frame frame = Frame(35);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        (children, int index) = children.Append(0x5C9C40);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        frame = Write(frame, FirstChild, Read(frame, FirstChild).WriteWord(12, 15).WriteWord(24, 0).WriteWord(28, 512).WriteWord(30, 512));
        int draws = 0;
        NativeBattleEffect183Frame completed = Advance(frame, Resources, Inputs() with
        {
            Execute = (current, operation) =>
            {
                Assert.IsType<NativeBattleEffect183DrawWindowed144>(operation);
                draws++;
                return current;
            }
        });
        Assert.Equal(1, index);
        Assert.Equal(1, draws);
        Assert.Equal(0u, completed.Effect.ReturnedDescriptor);
        Assert.Empty(completed.Effect.Pools);
        Assert.NotNull(completed.State.RootBankStorage);
        Assert.NotNull(completed.State.ChildBankStorage);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(completed.State.RootBankStorage!.Read(0, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(completed.State.RootBankStorage.Read(12, 2)));
        Assert.Equal((ushort)36, BinaryPrimitives.ReadUInt16LittleEndian(completed.State.ChildBankStorage!.Read(12, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(completed.State.ChildBankStorage.Read(36, 2)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(completed.State.ChildBankStorage.Read(48, 2)));
        Assert.Same(completed, Advance(completed, Resources, Inputs()));
    }

    private static NativeBattleEffect183Frame Frame(short age)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x22F33E0, 0x22F33D0, 16, 1);
        (roots, _) = roots.Append(0x5C9F80);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x22F33F0, Director, 36, 100);
        (children, _) = children.Append(0x5C9AE0);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(22, 0xCAFE)) };
        NativeBattleOriginalEffectState effect = new(183, 182, new(183, 0x5C9A50, 0x5C9A30), new(5, 0, 1, 7, 0, 0), 0x22F33E0) { Pools = [roots, children] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffect183State state = NativeBattleEffect183State.FromColdProcess().WriteDword(0x22F4214, 5);
        return new(memory, effect, state with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect183Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (current, _, _, _) => new(current, Point(10, 20, 30)), _ => 0, (current, _) => new(current, Point(40, 50, 60)), (current, _) => current);
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
}
