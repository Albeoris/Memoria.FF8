using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianContinuationTests
{
    [Fact]
    public void Continue_the_named_completed_guardian_save_for_sixteen_native_callbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SETTLE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SETTLE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "A concrete completed GF source and a distinct after-event output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        Assert.Equal((ushort)116, snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, snapshot.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, snapshot.ReadFixturePresentation().NativeEffectMemory!.Active);
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        List<string> colors = [$"{snapshot.ReadFixturePresentation().NativeGraphics!.Callback}:{ReadOriginalModelCell(snapshot.ReadFixturePresentation().NativeGraphics!, 0).Color40:X8}"];
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int index = 0; index < 16; index++)
        {
            battle.AdvanceNativeGraphicsPresentation(callback);
            independent.AdvanceNativeGraphicsPresentation(callback);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
            colors.Add($"{battle.Presentation!.Graphics.Callback}:{ReadOriginalModelCell(battle.Presentation!.Graphics, 0).Color40:X8}");
        }
        BattleSnapshot after = battle.CaptureForContinuation();
        Assert.Equal(snapshot.ReadFixturePresentation().NativeGraphics!.Callback + 16, after.ReadFixturePresentation().NativeGraphics!.Callback);
        Assert.Equal(0u, after.ReadFixturePresentation().NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(0, after.ReadFixturePresentation().NativeEffectMemory!.Active);
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, snapshot.Definition);
        Assert.Equal(scene.Render(battle, 320, 224).ToArray(), scene.Render(independent, 320, 224).ToArray());
        using FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, after);
        Console.WriteLine($"Completed GF after-event continuation: frame{after.Frame}/callback{after.ReadFixturePresentation().NativeGraphics!.Callback}; caster Color40 trace {string.Join(',', colors)}; independent states and copied render agree.");
    }

}
