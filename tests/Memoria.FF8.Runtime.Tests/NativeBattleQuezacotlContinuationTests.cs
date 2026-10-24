using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlContinuationTests
{
    [Fact]
    public void Named_active_summon_continues_and_restores_through_completion()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_GF_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A concrete active GF standalone is required.");
        BattleSnapshot saved = GameSession.Load(source!).Battle ?? throw new InvalidDataException("The selected standalone has no battle.");
        BattleSession battle = BattleSession.Restore(saved);
        Assert.Equal((ushort)116, battle.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.True(battle.NativeGraphics.OriginalEffect.ReturnedDescriptor > 0);
        TimeSpan elapsed = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond + 1);
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.Zero);
        for (int frame = 0; frame < 512; frame++)
        {
            if (frame == 1)
            {
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                battle.AdvanceNativeGraphicsPresentation(elapsed);
                restored.AdvanceNativeGraphicsPresentation(elapsed);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
            else
            {
                battle.AdvanceNativeGraphicsPresentation(elapsed);
            }
            if (battle.NativeGraphics!.OriginalEffect is { ReturnedDescriptor: 0 } && battle.NativeEffectMemory!.Active == 0)
            {
                return;
            }
        }
        Assert.Fail("The selected active Quezacotl summon did not complete within the bounded continuation.");
    }
}
