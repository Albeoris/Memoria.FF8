using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathDrawingTests
{
    [Fact]
    public void Dark_quad_tests_the_retained_previous_index_and_keeps_copied_temporary_padding()
    {
        var fixture = Fixture(4096);
        NativeBattleDeathDraw draw = Draw(fixture);
        Assert.Single(draw.Packets.Attempts.Where(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C));
        Assert.Equal(3u, draw.Frame.Memory.PrimitiveProcess!.Temporary5082B0Lanes!.Dword44);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, draw.Frame.Memory.Death14!.Module.Read(0x6D0 + 6, 2));
        NativeBattleDeathFrame restored = JsonSerializer.Deserialize<NativeBattleDeathFrame>(JsonSerializer.Serialize(draw.Frame))!;
        NativeBattleDeathDraw expected = NativeBattleDeathDrawing.Dark(draw.Frame, draw.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true));
        NativeBattleDeathDraw actual = NativeBattleDeathDrawing.Dark(restored, draw.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Dark_quad_does_not_hydrate_unknown_high_index_bytes_and_preserves_unknown_copied_word()
    {
        var fixture = Fixture(4096);
        NativeBattlePrimitiveProcess process = fixture.Frame.Memory.PrimitiveProcess!;
        NativeBattleDeathFrame missing = fixture.Frame with { Memory = fixture.Frame.Memory with { PrimitiveProcess = process with { Temporary5082B0Lanes = null } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathDrawing.Dark(missing, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true)));
        missing = fixture.Frame with { Memory = fixture.Frame.Memory with { PrimitiveProcess = process with { Temporary5082B0Lanes = new(0, 0, null) } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathDrawing.Dark(missing, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true)));
        NativeBattleDeathFrame unknownPadding = fixture.Frame with { Memory = fixture.Frame.Memory with { PrimitiveProcess = process with { Temporary5082B0Lanes = new(null, 0, 0) } } };
        NativeBattleDeathDraw draw = NativeBattleDeathDrawing.Dark(unknownPadding, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true));
        Assert.Throws<NotSupportedException>(() => draw.Frame.Memory.Death14!.Module.Read(0x6D0 + 6, 2));
    }

    [Fact]
    public void Dark_faces_do_not_apply_the_normal_renderer_near_flag_gate()
    {
        var fixture = Fixture(100);
        Assert.NotEqual(0u, NativeBattleModuleProjection.Project45FE10(fixture.Projection.View!, new(-50, -100, 0), fixture.Projection).Flags & 0x60000);
        NativeBattleDeathDraw draw = Draw(fixture);
        Assert.Contains(draw.Packets.Attempts, attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xFC) == 0x2C);
    }

    private static NativeBattleShadowSource Shadow => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());

    private static NativeBattleDeathDraw Draw((NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState Model, NativeBattleDeathResources Resources, NativeBattleCameraProjection Projection) fixture) => NativeBattleDeathDrawing.Dark(fixture.Frame, fixture.Task, fixture.Model, fixture.Resources, fixture.Projection, Shadow, new(true));

    private static (NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState Model, NativeBattleDeathResources Resources, NativeBattleCameraProjection Projection) Fixture(int depth)
    {
        byte[] bytes = new byte[104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [24, 88, 96, 100, 104];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource source = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        byte[] quad = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(6), 3);
        NativeBattleRawMesh mesh = new([new(new(-50, -100, 0), 0), new(new(50, -100, 0), 0), new(new(-50, 0, 0), 0), new(new(50, 50, 0), 0)], [new(true, quad.ToImmutableArray()), new(true, quad.ToImmutableArray())]);
        NativeBattleEffectOwnedModelResource model = new(0x1000, bytes.ToImmutableArray(), source, [mesh]);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(new(1, 0, null, 0x8B6F70, new byte[0x140 - 12].ToImmutableArray()), 0x2742450, model, 3, 0x808080);
        NativeBattleModulePoolTask task = initialized.Task.WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
        NativeBattleEffectOwnedModelState state = initialized.State with { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false) };
        NativeBattleDeathResources resources = new(new("camera", []), model, [], [], new("first", []), new("second", []), new(0, 1, []), [], 1, 2);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Death14 = NativeBattleDeathState.FromColdProcess(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess() with { Temporary5082B0Lanes = new(0xBEEF, 0, 0) } };
        NativeBattleDeathFrame frame = new(memory, new(14, 13, new(14, 0x8B68C0, 0x8B68A0), new(0, 1, 8, 0, 0, 0), null), new(1));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, depth, 4096), 216, 160, 108);
        return (frame, task, state, resources, projection);
    }
}
