using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco100LoadedModelResourcesTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Actual_file498_has_independent_three_pose_geometry_and_external_programs_without_declared_padding()
    {
        using GameAssets game = Installed();
        NativeBattleChoco100LoadedModelResources resources = game.ReadNativeChoco100LoadedModelResources();
        Assert.Same(resources, game.ReadNativeChoco100LoadedModelResources());
        resources.Validate();
        NativeBattleEffectFileAsset file = Assert.Single(game.ReadNativeChoco100IoResources().Files, item => item.FileId == 498);
        Assert.Equal(0x5090, file.Data.Length);
        Assert.Equal(file.Data.ToArray(), resources.Model.Bytes.ToArray());
        Assert.Equal(17, resources.Model.PoseSource.BoneCount);
        Assert.Equal(new byte[] { 1, 28, 1 }, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()));
        Assert.Equal(508, Assert.Single(resources.Model.Geometry).Vertices.Length);
        Assert.Equal(912, resources.Model.Geometry[0].Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(42, resources.Model.Geometry[0].Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(new short[] { unchecked((short)0xC000), unchecked((short)0xC000), unchecked((short)0xC000), unchecked((short)0xC000), unchecked((short)0xF400), 0 }, resources.YProgram.Values);
        Assert.Equal(0x1529F38u, resources.TextureProgram.Address);
        Assert.Equal(0x2C, resources.TextureProgram.Data.Length);
        Assert.Equal(0x00808080u, resources.DefaultFadeColor58);
        NativeBattleChoco100LoadedModelResources restored = JsonSerializer.Deserialize<NativeBattleChoco100LoadedModelResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        byte[] padded = new byte[0xECA0];
        file.Data.AsSpan().CopyTo(padded);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco100LoadedModelResourcesReader.ReadModel(padded.ToImmutableArray()));
    }

    [Fact]
    public void Actual_loaded_model_all_ten_phases_preserve_pose_texture_RAW_colors_and_independent_cold_next()
    {
        using GameAssets game = Installed();
        NativeBattleChoco100LoadedModelResources resources = game.ReadNativeChoco100LoadedModelResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, 0x200140);
        Assert.Equal((ushort)0x3C, initialized.Task.ReadWord(0xDE));
        Assert.Equal(uint.MaxValue, initialized.Task.ReadDword(0xAC));
        Saved current = new(initialized.Task, initialized.State, Enumerable.Repeat((byte)0xA5, 0xFE0).ToImmutableArray());
        HashSet<byte> phases = [];
        HashSet<byte> poses = [];
        List<short> ySamples = [];
        int copies = 0;
        int restoredBoundaries = 0;
        bool terminal = false;
        for (int callback = 0; !terminal && callback < 256; callback++)
        {
            byte phase = current.Task.ReadByte(0x29);
            bool firstPhase = phases.Add(phase);
            Next next = Step(current);
            if (firstPhase || phase == 5 || callback % 19 == 0)
            {
                Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(current))!;
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Step(restored)));
                restoredBoundaries++;
            }
            if (phase == 5)
            {
                ySamples.Add(unchecked((short)next.Saved.Task.ReadWord(0x4E)));
            }
            poses.Add(next.Saved.Task.ReadByte(0x9C));
            copies += next.Advance.TextureCopies.Length;
            if (next.Advance.Drawing is { } draw)
            {
                Assert.Equal(0xFE0, draw.ProjectedBytesWritten);
                Assert.Equal(0xFE0, draw.ProjectedScratch.Length);
                Assert.NotEmpty(draw.Packets.Attempts);
            }
            if (phase == 0 || phase == 1 && next.Saved.Task.ReadByte(0x29) == 1 || next.Saved.Task.ReadByte(0x29) == 9)
            {
                Assert.Null(next.Advance.Drawing);
            }
            Assert.Equal((uint)0x2800, next.Saved.Task.ReadDword(0x114));
            Assert.Equal(uint.MaxValue, next.Saved.Task.ReadDword(0xAC));
            Assert.Equal((ushort)0x3C, next.Saved.Task.ReadWord(0xDE));
            Assert.Equal((byte)0, next.Saved.Task.ReadByte(0x37));
            current = next.Saved;
            terminal = next.Advance.ReadyToComplete;
        }
        Assert.True(terminal);
        phases.Add(current.Task.ReadByte(0x29));
        Assert.Equal(Enumerable.Range(0, 10).Select(value => (byte)value).ToArray(), phases.Order().ToArray());
        Assert.Equal(new byte[] { 0, 1, 2 }, poses.Order().ToArray());
        Assert.Equal(resources.YProgram.Values.Skip(1).ToArray(), ySamples.ToArray());
        Assert.True(restoredBoundaries >= 10);
        Assert.True(copies > 0);
        Assert.Equal((ushort)0x1000, current.Task.ReadWord(0x13A));
        Assert.Equal((ushort)0x1000, current.Task.ReadWord(0x13C));
        Assert.Equal(0x02000000u, current.Task.ReadDword(0x58));
        Assert.Equal(resources.DefaultColor44 & 0xFF000000u, current.Task.ReadDword(0x5C));
        Assert.False(Step(current with { Task = current.Task.WriteByte(0x28, 1) }).Advance.ReadyToComplete);
        Assert.True(Step(current).Advance.ReadyToComplete);
        NativeBattleChoco97ModelAdvance waiting = NativeBattleChoco100LoadedModel.Advance7175F0(current.Task.WriteByte(0x29, 3).WriteByte(0x26, 4).WriteWord(0x13E, 0x2400).WriteWord(0x13C, 0x1000), current.State, resources, new([10, 0, 20, 0, 30, 0, 40, 0], _ => false, _ => false, _ => throw new InvalidOperationException()));
        Assert.Equal((byte)3, waiting.Task.ReadByte(0x29));
        Assert.Equal((ushort)(0x2400 - 0x78), waiting.Task.ReadWord(0x13E));
        Assert.Equal((ushort)(0x1000 - 0x69), waiting.Task.ReadWord(0x13C));

        Next Step(Saved saved)
        {
            NativeBattleChoco100LoadedModelInputs inputs = new([10, 0, 20, 0, 30, 0, 40, 0], mode => mode == 9 && saved.Task.ReadWord(0x134) >= 3, mode => mode == 8, model => Draw(model, saved.Scratch, resources.Model, rules));
            NativeBattleChoco97ModelAdvance advance = NativeBattleChoco100LoadedModel.Advance7175F0(saved.Task, saved.State, resources, inputs);
            return new(advance, new(advance.Task, advance.State, advance.Drawing?.ProjectedScratch ?? saved.Scratch));
        }
    }

    [Fact]
    public void Loaded_FE0_and_static_CD0_draws_share_one_projected_owner_and_static_preserves_loaded_suffix()
    {
        using GameAssets game = Installed();
        NativeBattleChoco100LoadedModelResources loaded = game.ReadNativeChoco100LoadedModelResources();
        NativeBattleChoco97ModelResources normal = game.ReadNativeChoco100ModelResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep first = Initialize(loaded, 0x200140);
        first = NativeBattleEffectOwnedModel.Select8DD190(first.Task.WriteDword(0x114, 0x2800).WriteDword(0x118, 0x2800).WriteDword(0x11C, 0x2800), first.State, loaded.Model, 1);
        ImmutableArray<byte> shared = Enumerable.Repeat((byte)0xA5, 0xFE0).ToImmutableArray();
        NativeBattleEffectOwnedModelDraw loadedDraw = Draw(first, shared, loaded.Model, rules);
        Assert.Equal(0xFE0, loadedDraw.ProjectedBytesWritten);
        NativeBattleEffectOwnedModelStep second = NativeBattleChoco100Model.Initialize(new(1, 0, null, 0x715540, new byte[0x140 - 12].ToImmutableArray()), 0x200000, normal);
        second = NativeBattleEffectOwnedModel.Select8DD190(second.Task.WriteDword(0x114, 6144).WriteDword(0x118, 6144).WriteDword(0x11C, 6144), second.State, normal.Model, 0);
        NativeBattleEffectOwnedModelDraw shortDraw = Draw(second, loadedDraw.ProjectedScratch, normal.Model, rules);
        Assert.Equal(0xCD0, shortDraw.ProjectedBytesWritten);
        Assert.Equal(0xFE0, shortDraw.ProjectedScratch.Length);
        Assert.Equal(loadedDraw.ProjectedScratch.AsSpan().Slice(0xCD0).ToArray(), shortDraw.ProjectedScratch.AsSpan().Slice(0xCD0).ToArray());
        Shared saved = new(first, second, shortDraw.ProjectedScratch);
        Shared restored = JsonSerializer.Deserialize<Shared>(JsonSerializer.Serialize(saved))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(first, saved.Scratch, loaded.Model, rules)), JsonSerializer.Serialize(Draw(restored.Loaded, restored.Scratch, loaded.Model, rules)));
        Assert.Equal(JsonSerializer.Serialize(first.State), JsonSerializer.Serialize(saved.Loaded.State));
        Assert.NotEqual(first.State.TaskAddress, second.State.TaskAddress);
        Assert.NotEqual(first.State.ResourceAddress, second.State.ResourceAddress);
    }

    private static NativeBattleEffectOwnedModelStep Initialize(NativeBattleChoco100LoadedModelResources resources, uint address) => NativeBattleChoco100LoadedModel.Initialize(new(1, 0, null, NativeBattleChoco100LoadedModel.Callback, new byte[0x140 - 12].ToImmutableArray()), address, resources);
    private static NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep model, ImmutableArray<byte> scratch, NativeBattleEffectOwnedModelResource resource, NativeBattleGraphicsRules rules) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(model.Task, model.State, resource, View, Projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, scratch);
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, ImmutableArray<byte> Scratch);
    private sealed record Next(NativeBattleChoco97ModelAdvance Advance, Saved Saved);
    private sealed record Shared(NativeBattleEffectOwnedModelStep Loaded, NativeBattleEffectOwnedModelStep Static, ImmutableArray<byte> Scratch);
}
