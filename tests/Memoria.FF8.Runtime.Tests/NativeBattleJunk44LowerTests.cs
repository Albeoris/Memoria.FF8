using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleJunk44Controller;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleJunk44LowerTests
{
    [Fact]
    public void Sprite_immediate_operation_retains_actual_task_birth_and_source_before_temporary_reuse()
    {
        NativeBattleJunk44DrawSprite180? nativeDraw = null;
        NativeBattleJunk44Frame native = Draw(false, draw => nativeDraw = draw);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleJunk44DrawSprite180? capturedDraw = null;
        NativeBattleJunk44Frame captured = Draw(true, draw => capturedDraw = draw);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.NotNull(nativeDraw);
        Assert.NotNull(capturedDraw);
        Assert.Null(nativeDraw.PilotIdentity);
        NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(capturedDraw.PilotIdentity);
        Assert.Equal((ushort)44, identity.Effect);
        Assert.Equal(0x2545130u, identity.Bank);
        Assert.Equal(0x1354B80, identity.ResourceGeneration);
        Assert.True(identity.Generation > 0);
        Assert.Equal(nativeDraw.TemporaryAddress, capturedDraw.TemporaryAddress);
        Assert.Equal(nativeDraw.SourceAddress, capturedDraw.SourceAddress);

        NativeBattleJunk44Frame Draw(bool capture, Action<NativeBattleJunk44DrawSprite180> receive)
        {
            NativeBattleJunk44Frame frame = Frame();
            (frame, uint address) = Append(frame, 0x2545130, 0x6E82C0);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x0C, 100).WriteWord(0x0E, 200).WriteWord(0x10, 300).WriteWord(0x12, 13).WriteWord(0x1C, 4001));
            NativeBattleJunk44Inputs inputs = Inputs((current, operation) =>
            {
                receive(Assert.IsType<NativeBattleJunk44DrawSprite180>(operation));
                return current;
            }) with { CapturePresentation = capture };
            return AdvanceSprite6E82C0(frame, address, NativeBattleJunk44StateTests.Resources(), inputs).Frame;
        }
    }

    [Fact]
    public void Bone_producer_publishes_once_even_when_sprite_pool_is_full_and_consumes_no_CRT()
    {
        var frame = Frame();
        for (int index = 0; index < 32; index++)
        {
            (frame, _) = Append(frame, 0x2545130, 0x6E82C0);
        }
        int calls = 0;
        var actual = SpawnBoneSprites6E81A0(frame, 3, 23, 200, 2000, 2000, null!, Inputs() with
        {
            ReadBone502170 = (current, model, selector, distance) =>
            {
                calls++;
                Assert.Equal(Model(3), model);
                Assert.Equal(23, selector);
                Assert.Equal(0, distance);
                return new(current with { State = current.State.WriteWord(0x2545174, 0x1234) }, PointView(new(100, 200, 300)));
            }
        });
        Assert.Equal(1, calls);
        Assert.Equal((ushort)0x1234, actual.State.ReadWord(0x2545174));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Point_birth_preserves_literal_two_draw_order_signed_divisions_and_partial_writes()
    {
        var frame = Frame();
        var expected = frame.Random.Draw(out ushort angle).Draw(out ushort scale);
        frame = SpawnPointSprites6E8360(frame, 1, PointView(new(100, -999, 300)), 200, 2000, 2000, null!, Inputs());
        NativeBattleModulePoolTask task = frame.Effect.Pools[3].Slots[0];
        short x = unchecked((short)(NativeBattleCameraMath.Cosine(angle) * 200 / 4096));
        short z = unchecked((short)(NativeBattleCameraMath.Sine(angle) * 200 / 4096));
        Assert.Equal(expected, frame.Random);
        Assert.Equal(unchecked((short)(100 + x)), Signed(task, 0x0C));
        Assert.Equal((short)0, Signed(task, 0x0E));
        Assert.Equal(unchecked((short)(300 + z)), Signed(task, 0x10));
        Assert.Equal((short)(x / 8), Signed(task, 0x14));
        Assert.Equal((short)-25, Signed(task, 0x16));
        Assert.Equal((short)(z / 8), Signed(task, 0x18));
        Assert.Equal((ushort)(scale * 2000 / 32768 + 2000), task.ReadWord(0x1C));
        var untouched = SpawnPointSprites6E8360(frame, 0, null!, 0, 0, 0, null!, null!);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(untouched));
    }

    [Theory]
    [InlineData(13, 0, 1)]
    [InlineData(1, 2, 13)]
    public void Sprite_draws_old_frame_before_decrement_and_terminal_callback_skips_motion(int remaining, int result, int phase)
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2545130, 0x6E82C0);
        var task = Read(frame, address).WriteWord(0x0C, 100).WriteWord(0x0E, 200).WriteWord(0x10, 300).WriteWord(0x12, (ushort)remaining).WriteWord(0x14, 10).WriteWord(0x16, unchecked((ushort)-20)).WriteWord(0x18, 30).WriteWord(0x1C, 4001);
        frame = Write(frame, address, task);
        int draws = 0;
        var actual = AdvanceSprite6E82C0(frame, address, NativeBattleJunk44StateTests.Resources(), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleJunk44DrawSprite180>(operation);
            Assert.Equal(NativeBattleTemporaryMemory.Address, draw.TemporaryAddress);
            Assert.Equal((ushort)(phase - 1), Word(Context(current, draw.TemporaryAddress, 180), 4));
            Assert.Equal((ushort)0, Word(Context(current, draw.TemporaryAddress, 180), 0x24));
            draws++;
            return current;
        }));
        Assert.Equal(result, actual.Result);
        Assert.Equal(1, draws);
        Assert.Equal((ushort)(remaining - 1), Read(actual.Frame, address).ReadWord(0x12));
        Assert.Equal((ushort)(result == 2 ? 100 : 110), Read(actual.Frame, address).ReadWord(0x0C));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(3, 0xC3, 1024)]
    public void Debris_passes_physical_88_suffix_after_32_matrix_bytes_and_keeps_padding(int remaining, uint flags, int cue)
    {
        var resources = NativeBattleJunk44StateTests.Resources();
        var frame = Frame();
        (frame, uint address) = SpawnDebris6E8470(frame, 100, 300, 0, 5, resources, Inputs());
        frame = Write(frame, address, Read(frame, address).WriteWord(0x12, (ushort)remaining));
        frame = PublishContext(frame, NativeBattleTemporaryMemory.Address, Context(frame, NativeBattleTemporaryMemory.Address, 120).Write(18, BitConverter.GetBytes((ushort)0xBEEF)), 120);
        var actual = AdvanceDebris6E84D0(frame, address, resources, Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleJunk44DrawStatic88>(operation);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 32, draw.TemporaryAddress);
            Assert.Equal(flags, Dword(Context(current, draw.TemporaryAddress, 88), 0x1C));
            Assert.Equal((ushort)0xBEEF, Word(Context(current, draw.TemporaryAddress - 32, 32), 18));
            uint expectedMesh = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadTable(5).AsSpan().Slice((remaining % 4) * 4));
            Assert.Equal(expectedMesh, draw.SourceAddress);
            if (flags != 0)
            {
                Assert.Equal((uint)cue, Dword(Context(current, draw.TemporaryAddress, 88), 12));
                Assert.Equal(0u, Dword(Context(current, draw.TemporaryAddress, 88), 8));
            }
            return current;
        }));
        Assert.Equal(0, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Terminal_debris_draws_then_returns_before_gravity_or_random()
    {
        var frame = Frame();
        (frame, uint address) = SpawnDebris6E8470(frame, 100, 300, 0, 0, null!, Inputs());
        frame = Write(frame, address, Read(frame, address).WriteWord(0x12, 1).WriteWord(0x0E, 0));
        var actual = AdvanceDebris6E84D0(frame, address, NativeBattleJunk44StateTests.Resources(), Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)800, Read(actual.Frame, address).ReadWord(0x16));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Model_collision_reads_current_anchor_and_births_eight_exact64_byte_workspaces()
    {
        var resources = NativeBattleJunk44StateTests.Resources();
        var frame = Frame();
        (frame, uint address) = SpawnDebris6E8470(frame, 100, 300, Model(0), 6, resources, Inputs());
        frame = Write(frame, address, Read(frame, address).WriteWord(0x0E, unchecked((ushort)-100)).WriteWord(0x16, 800));
        int anchors = 0;
        List<NativeBattleJunk44Operation> operations = [];
        var inputs = Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }) with
        {
            ReadAnchor571400 = (current, model) =>
            {
                anchors++;
                Assert.Equal(Model(0), model);
                return new(current, PointView(new(1000, 1234, 3000), unchecked((short)0xBEEF)));
            }
        };
        var actual = AdvanceDebris6E84D0(frame, address, resources, inputs);
        Assert.Equal(1, anchors);
        Assert.Equal(8, actual.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Single(operations, operation => operation is NativeBattleJunk44Contact506690);
        Assert.Equal(0u, Read(actual.Frame, address).ReadDword(0x30));
        Assert.Equal((ushort)80, Read(actual.Frame, address).ReadWord(0x18));
        for (int index = 0; index < 8; index++)
        {
            var task = actual.Frame.Effect.Pools[1].Slots[index];
            Assert.Equal((ushort)(index + 8), task.ReadWord(0x12));
            Assert.Equal(ProgrammeAddress, task.ReadDword(0x18));
            Assert.Equal(0u, task.ReadDword(0x1C));
        }
        var expected = frame.Random;
        for (int draw = 0; draw < 2 + 8 * 5; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.Frame.Random);
    }

    [Fact]
    public void Programme_positive_delay_does_not_read_source_or_camera()
    {
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2545110, 0x6E8810);
        frame = Write(frame, address, Read(frame, address).WriteWord(0x12, 1).WriteWord(0x14, 0xFFF0).WriteWord(0x16, 32).MarkPayloadUnknown(0x18, 64));
        var actual = AdvanceProgramme6E8810(frame, address, null!, null!);
        Assert.Equal(0, actual.Result);
        Assert.Equal((ushort)0, Read(actual.Frame, address).ReadWord(0x12));
        Assert.Equal((ushort)16, Read(actual.Frame, address).ReadWord(0x14));
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Programme_retirement_uses_native_remaining_and_skips_last_angle_update()
    {
        var resources = NativeBattleJunk44StateTests.Resources();
        var frame = Frame();
        (frame, uint address) = Append(frame, 0x2545110, 0x6E8810);
        var task = Read(frame, address).WriteWord(0x14, 100).WriteWord(0x16, 33);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[64].ToImmutableArray()).Initialize7016B0(resources.Programme, 64);
        byte[] bytes = NativeBattleStorageSnapshots.Capture(workspace).Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 11);
        frame = WriteBytes(Write(frame, address, task), address + 0x18, NativeBattleEffectArena.Clear().Write(0, bytes), 64);
        var actual = AdvanceProgramme6E8810(frame, address, resources, Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)100, Read(actual.Frame, address).ReadWord(0x14));
        Assert.Equal((ushort)0, Read(actual.Frame, address).ReadWord(0x12));
        Assert.Equal(12u, Read(actual.Frame, address).ReadDword(0x1C));
    }

    [Theory]
    [InlineData(-32768)]
    [InlineData(1234)]
    public void Local_yaw_uses_negated_signed_WORD_and_canonical_trig(int angle)
    {
        var actual = Rotation6E8A70((short)angle);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(-angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(-angle));
        Assert.Equal(new short[] { cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine }, actual.Rotation);
    }

    internal static NativeBattleJunk44Frame Frame()
    {
        var pools = Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray();
        NativeBattleOriginalEffectState effect = new(44, 43, new(44, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x128C), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0xCAFE) };
        return new(memory, effect, new(123), NativeBattleJunk44State.FromColdProcess().WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, Model(3)).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    internal static NativeBattleJunk44Inputs Inputs(Func<NativeBattleJunk44Frame, NativeBattleJunk44Operation, NativeBattleJunk44Frame>? execute = null)
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 15, -1, 226, 44, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, uint.MaxValue, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 0x1234), _ => new(null, 100, 160, 108), (_, model) => new(checked((int)((model - Model(0)) / 156)), 2) { MinimumY146 = 0, Transform = new(new(0, 0, 0), new(100, 200, 300), 0, 0, 4096) }, (current, _) => new(current, PointView(new(100, 200, 300), 99)), (current, _, _, _) => new(current, PointView(new(100, 200, 300))), _ => packets, current => new(current, false), execute ?? ((current, _) => current));
    }

    private static uint Model(int actor) => checked(0x1D972C0u + (uint)actor * 156);
    private static ushort Word(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(view.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));
}
