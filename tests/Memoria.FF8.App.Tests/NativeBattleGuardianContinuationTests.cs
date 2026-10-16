using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianContinuationTests
{
    [Fact]
    public void Continue_the_named_standalone_guardian_save_to_its_next_strict_boundary()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_CONTINUE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_CONTINUE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "A concrete standalone GF source and distinct boundary output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal((ushort)116, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        bool sawPhase73Flash = false;
        for (int index = 0; index < 512; index++)
        {
            BattleSnapshot before = battle.Capture();
            ushort phase = before.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12);
            try
            {
                battle.AdvanceNativeGraphicsPresentation(callback);
                if (phase == 73)
                {
                    Assert.Contains(battle.NativeGraphics!.PrimitiveFrame!.LastCommands, command => command.Pass > 0 && command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode == 0x2A && command.Command.OrderingBucket == 8);
                    sawPhase73Flash = true;
                }
            }
            catch (NotSupportedException exception)
            {
                BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(clone.Capture()));
                NotSupportedException repeated = Assert.Throws<NotSupportedException>(() => clone.AdvanceNativeGraphicsPresentation(callback));
                Assert.Equal(exception.Message, repeated.Message);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                Assert.True(before.Frame > snapshot.Frame, "The standalone continuation must make progress before its next unsupported boundary.");
                if (snapshot.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12) <= 73 && phase > 73)
                {
                    Assert.True(sawPhase73Flash);
                }
                using FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(stream, before);
                Console.WriteLine($"Continued frame{snapshot.Frame}/callback{snapshot.NativeGraphics.Callback} to stable frame{before.Frame}/callback{before.NativeGraphics.Callback}, phase{phase}: {exception.Message}");
                return;
            }
        }
        Assert.Fail("The bounded standalone continuation did not reach its next strict boundary.");
    }
}
