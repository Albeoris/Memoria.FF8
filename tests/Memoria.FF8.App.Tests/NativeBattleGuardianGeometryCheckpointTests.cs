using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianGeometryCheckpointTests
{
    [Fact]
    public void Resume_the_named_pre_drawable_checkpoint_for_one_actual_geometry_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PREFIX_SNAPSHOT");
        Assert.SkipUnless(path is not null, "A concrete standalone pre-drawable GF checkpoint is required.");
        BattleSnapshot before = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(path!))!;
        NativeBattleOriginalEffectState original = before.NativeGraphics!.OriginalEffect!;
        Assert.Equal((ushort)116, original.Binding.Effect);
        Assert.Equal((ushort)2, original.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        int expectedStart = original.Pools.Single(pool => pool.DescriptorAddress == 0x2521748).Slots[0].ReadWord(16) != 0 ? 0x2E50 : 0x15E50;
        BattleSession battle = BattleSession.Restore(before);
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        battle.AdvanceNativeGraphicsPresentation(callback);
        clone.AdvanceNativeGraphicsPresentation(callback);
        BattleSnapshot after = battle.Capture();
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(clone.Capture()));
        Assert.Equal(before.NativeGraphics.Callback + 1, after.NativeGraphics!.Callback);
        Assert.Equal((ushort)3, after.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleLogicalResolvedCommand[] commands = after.NativeGraphics.PrimitiveFrame!.LastCommands.Where(command => command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8").ToArray();
        Assert.NotEmpty(commands);
        Assert.Equal(expectedStart, commands.Min(command => command.Command.Packet.ByteOffset));
        Assert.True(after.NativeGraphics.OriginalEffect.DrawArenaOffset > expectedStart);
        Assert.Throws<NotSupportedException>(() => after.NativeEffectMemory!.Arena!.Read(expectedStart, 3));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, after.Definition, true).Render(battle, 320, 224).ToArray();
        Assert.Equal(320 * 224 * 4, pixels.Length);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(after))!);
        Assert.Equal(pixels, new BattleScenePresentation(assets, after.Definition, true).Render(restored, 320, 224));
        NativeBattlePrimitiveFrame controlFrame = after.NativeGraphics.PrimitiveFrame with { LastCommands = after.NativeGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
        BattleSession control = BattleSession.Restore(after with { NativeGraphics = after.NativeGraphics with { PrimitiveFrame = controlFrame } });
        byte[] controlPixels = new BattleScenePresentation(assets, after.Definition, true).Render(control, 320, 224);
        int differentPixels = Enumerable.Range(0, 320 * 224).Count(index => pixels[4 * index] != controlPixels[4 * index] || pixels[4 * index + 1] != controlPixels[4 * index + 1] || pixels[4 * index + 2] != controlPixels[4 * index + 2]);
        Assert.True(differentPixels > 0, "Actual GF geometry must change RGB pixels relative to removing only its same-frame packets.");
        Console.WriteLine($"Actual GF geometry changes{differentPixels} RGB pixels relative to removing only its same-frame packets.");
        Console.WriteLine($"First GF geometry: frame{after.Frame}, callback{after.NativeGraphics.Callback}, commands{commands.Length}, effectcursor{after.NativeGraphics.OriginalEffect.DrawArenaOffset:X}.");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_GF_EVIDENCE");
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return;
        }
        Directory.CreateDirectory(evidence);
        string name = "quezacotl116-first-draw-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N")[..8];
        using (FileStream output = new(Path.Combine(evidence, name + ".battle.json"), FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(output, after);
        }
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        using FileStream image = new(Path.Combine(evidence, name + ".png"), FileMode.CreateNew, FileAccess.Write);
        bitmap.Save(image, ImageFormat.Png);
    }
}
