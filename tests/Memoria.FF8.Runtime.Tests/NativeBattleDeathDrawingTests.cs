using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathDrawingTests
{
    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Ground_test_uses_actual_second_segment_and_preserves_the_other_module_owner(ushort effect)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        uint index = (profile.ModuleAddress(0x2742868) - profile.ModuleAddress(0x273B550)) / 8;
        var fixture = Fixture(checked((int)index * 4), effect);
        NativeBattleDeathState selected = profile.RequireState(fixture.Frame.Memory);
        selected = selected with { Module = selected.Module.Write(profile.ModuleOffset(0x2742868) + 2, [1, 0]) };
        NativeBattleEffectMemory memory = profile.WithState(fixture.Frame.Memory, selected);
        NativeBattleDeathState other = effect == 14 ? memory.LevelDeath74! : memory.Death14!;
        NativeBattleDeathDraw draw = NativeBattleDeathDrawing.Dark(fixture.Frame with { Memory = memory }, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true));
        Assert.DoesNotContain(draw.Packets.Attempts, attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C);
        Assert.Same(other, effect == 14 ? draw.Frame.Memory.LevelDeath74 : draw.Frame.Memory.Death14);
        Assert.Equal(new byte[] { 1, 0 }, profile.RequireState(draw.Frame.Memory).Module.Read(profile.ModuleOffset(0x2742868) + 2, 2));
    }

    [Fact]
    public void Level_model_drawing_requires_its_own_retained_state_and_matching_binding()
    {
        var fixture = Fixture(4096, 74);
        NativeBattleDeathFrame missing = fixture.Frame with { Memory = fixture.Frame.Memory with { LevelDeath74 = null } };
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathDrawing.Dark(missing, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true)));
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathDrawing.Normal(missing, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true), new(0, 0, 1f / 4096, BitConverter.Int64BitsToDouble(0x4330000080000000L))));
        NativeBattleDeathFrame wrong = fixture.Frame with { Effect = fixture.Frame.Effect with { Binding = new(14, 0x8B68C0, 0x8B68A0) } };
        Assert.Throws<InvalidDataException>(() => NativeBattleDeathDrawing.Dark(wrong, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true)));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Dark_quad_tests_the_retained_previous_index_and_keeps_copied_temporary_padding(ushort effect)
    {
        var fixture = Fixture(4096, effect);
        NativeBattleDeathDraw draw = Draw(fixture);
        Assert.Single(draw.Packets.Attempts.Where(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C));
        Assert.Equal(BitConverter.GetBytes(NativeBattleTemporaryMemory.Address), draw.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal((uint?)1024, draw.Frame.Memory.GeometryRegisters!.Otz);
        Assert.NotNull(draw.Frame.Memory.ScriptedColorRegisters!.Flags);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, fixture.Resources.Profile.RequireState(draw.Frame.Memory).Module.Read(0x6D0 + 6, 2));
        NativeBattleDeathFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDeathFrame>(NativeBattleTestSnapshots.Json(draw.Frame))!;
        NativeBattleDeathDraw expected = NativeBattleDeathDrawing.Dark(draw.Frame, draw.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true));
        NativeBattleDeathDraw actual = NativeBattleDeathDrawing.Dark(restored, draw.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Dark_requires_physical_source_and_cursor_and_preserves_unknown_copied_word(ushort effect)
    {
        var fixture = Fixture(4096, effect);
        NativeBattleDeathFrame missing = fixture.Frame with { Staging = null };
        Assert.Throws<NotSupportedException>(() => Draw((missing, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection)));
        missing = fixture.Frame with { Staging = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame.Staging!)).MarkUnknown(NativeBattleTemporaryMemory.Cursor, 4) };
        Assert.Throws<NotSupportedException>(() => Draw((missing, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection)));
        NativeBattleDeathFrame unknownPadding = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 0x2E, 2) };
        NativeBattleDeathDraw draw = Draw((unknownPadding, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection));
        Assert.Throws<NotSupportedException>(() => fixture.Resources.Profile.RequireState(draw.Frame.Memory).Module.Read(fixture.Resources.Profile.ModuleOffset(0x273B550) + 6, 2));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Dark_faces_do_not_apply_the_normal_renderer_near_flag_gate(ushort effect)
    {
        var fixture = Fixture(100, effect);
        Assert.NotEqual(0u, NativeBattleModuleProjection.Project45FE10(fixture.Projection.View!, new(-50, -100, 0), fixture.Projection).Flags & 0x60000);
        NativeBattleDeathDraw draw = Draw(fixture);
        Assert.Contains(draw.Packets.Attempts, attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C);
    }

    private static NativeBattleShadowSource Shadow => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());

    private static NativeBattleDeathDraw Draw((NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState Model, NativeBattleDeathResources Resources, NativeBattleCameraProjection Projection) fixture) => NativeBattleDeathDrawing.Dark(fixture.Frame, fixture.Task, fixture.Model, fixture.Resources, NativeBattlePackedMatrix32.FromMatrix(fixture.Projection.View!, null), fixture.Projection, Shadow, new(true));

    private static (NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState Model, NativeBattleDeathResources Resources, NativeBattleCameraProjection Projection) Fixture(int depth, ushort effect = 14)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        byte[] bytes = new byte[188];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [24, 88, 180, 184, 188];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource source = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(88), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(92), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(96), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(100), 4);
        short[] positions = [-50, -100, 0, 50, -100, 0, -50, 0, 0, 50, 50, 0];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(102 + index * 2), positions[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(130), 2);
        for (int polygon = 0; polygon < 2; polygon++)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(140 + polygon * 20 + corner * 2), (ushort)corner);
            }
        }
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(bytes.AsSpan(88, 92).ToArray(), 1);
        NativeBattleEffectOwnedModelResource model = new(0x1000, bytes.ToImmutableArray(), source, meshes);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(new(1, 0, null, profile.Callback(0x8B6F70), new byte[0x140 - 12].ToImmutableArray()), profile.ModuleAddress(0x2742450), model, 3, 0x808080);
        NativeBattleModulePoolTask task = initialized.Task.WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
        NativeBattleEffectOwnedModelState state = initialized.State with { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false) };
        NativeBattleDeathResources resources = new(new("camera", []), model, [], [], new("first", []), new("second", []), new(0, 1, []), [], 1, 2) { Effect = effect };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Death14 = NativeBattleDeathState.FromColdProcess(), LevelDeath74 = NativeBattleDeathState.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() };
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address + 0x2E, [0xEF, 0xBE]);
        var prepared = NativeBattleDeathSourceMemory.Prepare(memory, staging, resources);
        staging = NativeBattleEffectOwnedModelRendering.PublishDecodedPose(prepared.Staging, state, model);
        NativeBattleDeathFrame frame = new(prepared.Memory, new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null), new(1)) { Staging = staging };
        frame = NativeBattleDeathModelDark.Initialize(frame, state.TaskAddress, task, resources);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, depth, 4096), 216, 160, 108);
        return (frame, task, state, resources, projection);
    }
}
