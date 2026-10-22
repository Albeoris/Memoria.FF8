using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWhiteWind297ResourcesTests
{
    [Fact]
    public void Original_Blue_Magic_resources_keep_identical_ACT_payloads_but_distinct_camera_and_real_caster_binding()
    {
        using GameAssets game = OpenGame();
        NativeBattleWhiteWind297Resources resources = game.ReadNativeWhiteWind297Resources();
        Assert.Same(resources, game.ReadNativeWhiteWind297Resources());
        resources.Validate();
        NativeBattleWhiteWind226Resources enemy = game.ReadNativeWhiteWind226Resources();
        Assert.Equal(0x105AEFCu, resources.Caster.Address);
        Assert.Equal(0x105A494u, resources.Sweep.Address);
        Assert.Equal(0x10583FCu, resources.Target.Address);
        Assert.Equal(enemy.Caster.Data.ToArray(), resources.Caster.Data.ToArray());
        Assert.Equal(enemy.Sweep.Data.ToArray(), resources.Sweep.Data.ToArray());
        Assert.Equal(enemy.Target.Data.ToArray(), resources.Target.Data.ToArray());
        Assert.Equal(new[] { 0xA30, 0xA68, 0x2098 }, new[] { resources.Caster.Data.Length, resources.Sweep.Data.Length, resources.Target.Data.Length });
        Assert.Equal(0x168, resources.Camera.Data.Length);
        Assert.Equal(0x70, enemy.Camera.Data.Length);
        Assert.Equal(0x5578Au, resources.Sound);
        Assert.Equal("FF8_EN:1056888", resources.CasterDat.Identity);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(new uint[] { 3, 0x14, 0x1648, 0x1698, 0x1B74 }, Enumerable.Range(0, 5).Select(index => Dword(resources.CasterDat.Data, index * 4)));
        NativeBattleWhiteWind297Resources restored = JsonSerializer.Deserialize<NativeBattleWhiteWind297Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleWhiteWind297ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(0, 0x188, 22)]
    [InlineData(1, 0x194, 26)]
    [InlineData(2, 0x278, 26)]
    public void Original_ACT_workspace_retains_current_frame_and_continues_identically_after_JSON(int index, int size, int duration)
    {
        using GameAssets game = OpenGame();
        NativeBattleWhiteWind297Resources resources = game.ReadNativeWhiteWind297Resources();
        NativeBattleGfAnimationSource source = index == 0 ? resources.Caster : index == 1 ? resources.Sweep : resources.Target;
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[size].ToImmutableArray()).Initialize7016B0(source, size);
        NativeBattleGfAnimationAdvance first = Advance(workspace);
        Assert.Equal(duration - 1, first.Remaining);
        Assert.True(first.EmittedNodes > 0);
        NativeBattleGfAnimationWorkspace cold = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(first.Workspace))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(first.Workspace)), JsonSerializer.Serialize(Advance(cold)));
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance next = Advance(workspace);
            Assert.Equal(duration - frame - 1, next.Remaining);
            workspace = next.Workspace;
        }

        NativeBattleGfAnimationAdvance Advance(NativeBattleGfAnimationWorkspace current) => current.Advance701970(source, 0, (retained, node) =>
        {
            Assert.InRange(node.GeometryIndex, 0, (int)Dword(source.Data, 0) - 1);
            return retained;
        });
    }

    [Fact]
    public void Resource_validation_rejects_truncation_invalid_actor_directory_and_out_of_bounds_geometry()
    {
        using GameAssets game = OpenGame();
        NativeBattleWhiteWind297Resources resources = game.ReadNativeWhiteWind297Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0x167) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CasterDat = resources.CasterDat with { Data = SetDword(resources.CasterDat.Data, 4, 0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Caster = resources.Caster with { Data = resources.Caster.Data.RemoveAt(0xA2F) } }).Validate());
        NativeBattleGfAnimationSource invalidShape = resources.Caster with { Data = SetDword(resources.Caster.Data, 16, 121) };
        Assert.Throws<InvalidDataException>(() => NativeBattleWhiteWind297ResourcesReader.ValidatePayloads(resources with { Caster = invalidShape }));
        NativeBattleGfAnimationSource invalidFace = resources.Caster with { Data = resources.Caster.Data.SetItem(12 + (int)Dword(resources.Caster.Data, 12) + 4, 255) };
        Assert.Throws<InvalidDataException>(() => NativeBattleWhiteWind297ResourcesReader.ValidatePayloads(resources with { Caster = invalidFace }));
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Blue Magic White Wind ingress.");
        return new(directory!);
    }
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static ImmutableArray<byte> SetDword(ImmutableArray<byte> bytes, int offset, uint value)
    {
        byte[] result = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), value);
        return result.ToImmutableArray();
    }
}
