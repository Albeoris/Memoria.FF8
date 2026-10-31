using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect324Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect324Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect324LowerTests
{
    [Theory]
    [InlineData(1, 14)]
    [InlineData(2, 12)]
    public void Two_successful_record_births_preserve_exact_CRT_order_and_current_point_W(int kind, int draws)
    {
        Fixture fixture = new(0x596E30);
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(0x2201AD6, 0xCAFE).WriteWord(0x2201AEE, 0xBEEF) };
        NativeBattleEffect324Frame result = NativeBattleEffect324Records.Birth(fixture.Frame, 20, kind, fixture.Inputs());
        Assert.Equal(expected, result.Random);
        Assert.Equal(2, fixture.PointQueries);
        Assert.Equal((uint)kind, result.State.ReadDword(0x2201AC8));
        Assert.Equal((uint)kind, result.State.ReadDword(0x2201AE0));
        Assert.Equal((ushort)0xCAFE, result.State.ReadWord(0x2201AD6));
        Assert.Equal((ushort)0xBEEF, result.State.ReadWord(0x2201AEE));
        Assert.Equal(0, result.State.ReadWord(0x2201ACC));
    }

    [Fact]
    public void Full_record_bank_consumes_no_random_or_point_service()
    {
        Fixture fixture = new(0x596E30);
        for (int index = 0; index < 100; index++)
        {
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x2201AC8 + (uint)(24 * index), 1) };
        }
        NativeBattleEffect324Frame result = NativeBattleEffect324Records.Birth(fixture.Frame, 0, 1, fixture.Inputs());
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.Equal(0, fixture.PointQueries);
    }

    [Fact]
    public void Paused_records_still_draw_and_load_alternate_controls_but_do_not_birth_move_or_retire()
    {
        Fixture fixture = new(0x596E30) { Flags = 0x201 };
        fixture.Task(task => task.WriteWord(12, 47));
        fixture.Record(1, 2, 512, -10);
        NativeCrtRandomState random = fixture.Frame.Random;
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(1u, result.Frame.State.ReadDword(0x2201AC8));
        Assert.Equal(2, result.Frame.State.ReadWord(0x2201ACC));
        Assert.Equal(47, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Single(fixture.Draws);
        Assert.NotNull(result.Frame.Memory.ScriptedLighting!.LightMatrix);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Terminal_sprite_directory_retires_own_records_without_age_or_Y_motion()
    {
        Fixture fixture = new(0x596E30);
        fixture.Task(task => task.WriteWord(12, 47));
        fixture.Record(1, 2, 512, -10);
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(0u, result.Frame.State.ReadDword(0x2201AC8));
        Assert.Equal(2, result.Frame.State.ReadWord(0x2201ACC));
        Assert.Equal(0, result.Frame.State.ReadWord(0x2201AD2));
    }

    [Fact]
    public void Surviving_type1_record_moves_Y_after_draw_and_preserves_unknown_point_W()
    {
        Fixture fixture = new(0x596E30);
        fixture.Task(task => task.WriteWord(12, 47));
        fixture.Record(1, 0, 512, -10);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x2201AD6, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(1, result.Frame.State.ReadWord(0x2201ACC));
        Assert.Equal(unchecked((ushort)-10), result.Frame.State.ReadWord(0x2201AD2));
        Assert.DoesNotContain(true, result.Frame.State.ReadView(0x2201AD6, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(3936, result.Frame.Memory.GeometryRegisters!.Translation[2]);
    }

    [Fact]
    public void Other_record_type_is_not_a_survivor_of_this_callback()
    {
        Fixture fixture = new(0x596E30);
        fixture.Task(task => task.WriteWord(12, 47));
        fixture.Record(2, 0, 512, 0);
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(2u, result.Frame.State.ReadDword(0x2201AC8));
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Bone_snapshots_copy_only_actual28_matrices_with_unknown_padding_through_json()
    {
        Fixture fixture = new(0x597520);
        NativeBattleEffect324Frame saved = NativeBattleEffect324History.Capture597750(fixture.Frame, Fixture.Model, 0x22010A8, fixture.Inputs());
        saved = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect324Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(saved)))!);
        Assert.DoesNotContain(true, saved.State.ReadView(0x22010A8 + 18, 2).ReadSnapshot(0, 2).Known);
        fixture.Bones = fixture.Bones.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix.ReadMatrix() with { X = 999 }, null)).ToImmutableArray();
        NativeBattleEffect324History.Restore597790(saved, Fixture.Model, 0x22010A8, fixture.Inputs());
        Assert.Equal(28, fixture.Bones.Length);
        Assert.Equal(27, fixture.Bones[27].ReadMatrix().X);
        Assert.False(fixture.Bones[27].Known[18]);
    }

    [Fact]
    public void History_restores_current_world_mask_color_and_bones_but_retains_native_Alpha7_zero()
    {
        Fixture fixture = new(0x597520);
        NativeBattleEffect324Frame saved = NativeBattleEffect324History.Capture597750(fixture.Frame, Fixture.Model, 0x22010A8, fixture.Inputs());
        saved = saved with { State = saved.State.WriteView(0x2201080, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 123 }, null)), 32) };
        fixture.Frame = saved;
        fixture.Task(task => task.WriteWord(12, 16));
        NativeBattleEffectArena before = fixture.ModelBytes;
        ImmutableArray<NativeBattlePackedMatrix32> bones = fixture.Bones;
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(1, fixture.BodyDraws);
        Assert.Equal(before.Read(40, 4), fixture.ModelBytes.Read(40, 4));
        Assert.Equal(before.Read(124, 4), fixture.ModelBytes.Read(124, 4));
        Assert.Equal(before.ReadSnapshot(64, 32).Known, fixture.ModelBytes.ReadSnapshot(64, 32).Known);
        Assert.Equal(before.Read(64, 18), fixture.ModelBytes.Read(64, 18));
        Assert.Equal(0, fixture.ModelBytes.Read(7, 1)[0]);
        Assert.Equal(bones[0].Bytes, fixture.Bones[0].Bytes);
        Assert.Equal(17, Read(result.Frame, fixture.Address).ReadWord(12));
    }

    [Fact]
    public void Paused_age0_history_still_captures_and_draws_without_advancing()
    {
        Fixture fixture = new(0x597520) { Flags = 0x201 };
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(0, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Equal(1, fixture.BodyDraws);
        Assert.Equal(27, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.State.Read(0x22010A8 + 27 * 32 + 20, 4)));
        Assert.Equal(0xFFFFFFFDu, Dword324(fixture.ModelBytes, 124));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Eye_uses_current_anchor_and_side_offset_then_retires_on_actual_directory(int side)
    {
        Fixture fixture = new(0x597870);
        fixture.Task(task => task.WriteWord(12, 2).WriteWord(14, (ushort)side).WriteWord(28, 2048));
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x2203C58, NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)4000)).MarkUnknown(6, 2), 8) };
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(side == 0 ? -200 : 200, result.Frame.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(-60, result.Frame.Memory.GeometryRegisters.Translation[1]);
        Assert.Equal(3872, result.Frame.Memory.GeometryRegisters.Translation[2]);
        Assert.Equal(3, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Single(fixture.Draws);
    }

    [Fact]
    public void Paused_palette_still_writes_current_color_and_flag_without_increment()
    {
        Fixture fixture = new(0x597410) { Flags = 0x201 };
        fixture.Task(task => task.WriteWord(12, 4).WriteWord(14, 3));
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(4, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Equal(fixture.Resources.MaterialColors24[0], Dword324(fixture.ModelBytes, 40));
        Assert.True((Word324(fixture.ModelBytes, 0) & 0x800) != 0);
    }

    [Fact]
    public void Terminal_palette_clears_only_0800_after_current_DPCS_write()
    {
        Fixture fixture = new(0x597410);
        fixture.Task(task => task.WriteWord(12, 49).WriteWord(14, 3).WriteWord(28, 13));
        fixture.ModelBytes = PutWord324(fixture.ModelBytes, 0, 0x1845);
        NativeBattleEffect324Step result = NativeBattleEffect324Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(50, Read(result.Frame, fixture.Address).ReadWord(12));
        Assert.Equal((short)0x1045, Word324(fixture.ModelBytes, 0));
        Assert.Equal(3584u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Actual_eye_capture_preserves_native_step_and_current_world_after_side_transform(int side)
    {
        Fixture Build(bool capture)
        {
            Fixture fixture = new(0x597870);
            fixture.View = NativeBattleCuragaMatrices.Identity8DD770();
            fixture.Task(task => task.WriteWord(12, 2).WriteWord(14, unchecked((ushort)side)).WriteWord(28, 2048));
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x2203C58, NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)4000)).MarkUnknown(6, 2), 8), CapturePresentation = capture };
            var resource = NativeBattleEffect318LowerTests.Fixture.Projected() with { Address = 0xCD0B2C };
            fixture.Resources = fixture.Resources with { Sprites = fixture.Resources.Sprites.SetItem(resource.Address, resource) };
            return fixture;
        }
        Fixture plainFixture = Build(false);
        var plain = NativeBattleEffect324Controller.AdvanceTask(plainFixture.Frame, plainFixture.Address, plainFixture.Resources, plainFixture.Inputs());
        string expected = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain));
        string packets = JsonSerializer.Serialize(plainFixture.Draws);
        Fixture capturedFixture = Build(true);
        var captured = NativeBattleEffect324Controller.AdvanceTask(capturedFixture.Frame, capturedFixture.Address, capturedFixture.Resources, capturedFixture.Inputs());
        Assert.Equal(expected, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        Assert.Equal(packets, JsonSerializer.Serialize(capturedFixture.Draws));
        Assert.Null(captured.Frame.PilotIdentity);
        var batch = NativeBattleActor33Presentation.Depths(Assert.Single(capturedFixture.Draws).Batch);
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null)).Primitive!.PilotGeometry);
        Assert.Equal((ushort)324, visual.Identity.Effect);
        Assert.True(visual.Identity.Generation > 0);
        Assert.NotEqual(0, visual.Identity.ResourceGeneration);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.UseSourceDepths);
    }

    [Fact]
    public void Real_record_birth_keeps_private_storage_and_CRT_while_binding_distinct_admitted_rows()
    {
        Fixture plainFixture = new(0x596E30);
        var plain = NativeBattleEffect324Records.Birth(plainFixture.Frame, 0, 1, plainFixture.Inputs());
        string expected = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain));
        Fixture fixture = new(0x596E30);
        var captured = NativeBattleEffect324Records.Birth(fixture.Frame with { CapturePresentation = true }, 0, 1, fixture.Inputs());
        Assert.Equal(expected, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        Assert.Equal(plainFixture.PointQueries, fixture.PointQueries);
        Assert.Equal(plain.Random, captured.Random);
        NativeBattlePilotVisualIdentity parent = NativeBattlePilotVisualLifetimes.Identity(324, Read(captured, fixture.Address));
        var first = NativeBattleActor33Presentation.Row(parent, captured.State.Storage, 0x2201AC8, 0xCD0B88);
        var second = NativeBattleActor33Presentation.Row(parent, captured.State.Storage, 0x2201AE0, 0xCD0B88);
        Assert.NotEqual(first, second);
        Assert.Equal(first, NativeBattleActor33Presentation.Row(parent, captured.State.Storage, 0x2201AC8, 0xCD0B88));
        Assert.Equal(1u, captured.State.ReadDword(0x2201AC8));
        Assert.Equal(1u, captured.State.ReadDword(0x2201AE0));
    }

    private sealed class Fixture
    {
        public const uint Model = 0x1D97494;
        public uint Address => 0x2202E38;
        public NativeBattleEffect324Frame Frame;
        public NativeBattleEffectArena ModelBytes;
        public ImmutableArray<NativeBattlePackedMatrix32> Bones;
        public NativeBattleEffect324Resources Resources;
        public uint Flags;
        public int PointQueries;
        public int BodyDraws;
        public NativeBattleActionMatrix? View;
        public List<NativeBattleEffect324DrawBatch> Draws = [];

        public Fixture(uint callback)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2202E28, Address, 36, 100);
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[24]));
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            Frame = new(memory, new(324, 323, new(324, 0x596BC0, 0x596BA0), null, null) { Pools = [pool] }, new(42), NativeBattleEffect324State.FromColdProcess().DetachBank(1).WriteDword(0x2203C4C, 3)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            ModelBytes = NativeBattleEffectNodeContextMemory.Copy(PackedArena(NativeBattlePackedMatrix32.FromMatrix(identity with { X = 77 }, null)), 0, NativeBattleEffectArena.Clear().Write(7, [99]), 64, 32).Write(40, BitConverter.GetBytes(0x04030201u)).Write(124, BitConverter.GetBytes(uint.MaxValue));
            Bones = Enumerable.Range(0, 28).Select(index => NativeBattlePackedMatrix32.FromMatrix(identity with { X = index }, null)).ToImmutableArray();
            byte[] sprite = new byte[22];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 3);
            for (int index = 0; index < 3; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), 18);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(16), 0xFFFF);
            byte[] palette = new byte[96];
            for (int index = 0; index < 24; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(palette.AsSpan(index * 4), (uint)(0x10203 + index));
            }
            Resources = new(null!, null!, null!, new uint[] { 0xCD0B88, 0xCD0D00, 0xCD0B2C }.ToImmutableDictionary(address => address, address => new NativeBattleModuleResource(5, address, 22, 0, sprite.ToImmutableArray())), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0xCD1218, palette.ToImmutableArray()));
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public void Record(int kind, int age, int scale, int velocity) => Frame = Frame with { State = Frame.State.WriteView(0x2201AC8, PutWord324(PutWord324(PutWord324(PutWord324(Put324(NativeBattleEffectArena.Clear(), 0, (uint)kind), 4, age), 6, scale), 12, 4000), 18, velocity), 24) };
        public NativeBattleEffect324Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, null!, (_, model) =>
        {
            Assert.Equal(Model, model);
            return ModelBytes;
        }, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(View, 512, 160, 108), _ => 0, (frame, model, _, _, retained) =>
        {
            Assert.Equal(Model, model);
            PointQueries++;
            return new(frame, retained.Write(0, new byte[6]));
        }, (_, model) =>
        {
            Assert.Equal(Model, model);
            return Bones;
        }, (_, _) => 0x123456, null!, (frame, operation) =>
        {
            switch (operation)
            {
                case NativeBattleEffect324WriteModelBytes write:
                    Assert.Equal(Model, write.ModelAddress);
                    ModelBytes = NativeBattleEffectNodeContextMemory.Copy(write.Data, 0, ModelBytes, write.Offset, write.Length);
                    break;
                case NativeBattleEffect324WriteCurrentBoneMatrices32 write:
                    Bones = write.Matrices;
                    break;
                case NativeBattleEffect324DrawModel5977D0:
                    BodyDraws++;
                    Assert.Equal(2u, Dword324(ModelBytes, 124));
                    Bones = Bones.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix.ReadMatrix() with { X = 444 }, null)).ToImmutableArray();
                    break;
                case NativeBattleEffect324DrawBatch draw:
                    Draws.Add(draw);
                    break;
                default:
                    throw new InvalidDataException();
            }
            return frame;
        });
    }
}
