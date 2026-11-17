using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanControlInputTests
{
    [Fact]
    public void PreservedModalScanAcceptsMappedConfirmsWithPhysicalInputNeutralAndRoundTripsItsPendingOwner()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_CONTROL_SNAPSHOT");
        Assert.SkipUnless(path is not null && File.Exists(path), "Select a standalone game snapshot at the native Scan confirmation boundary with MEMORIA_FF8_SCAN_CONTROL_SNAPSHOT.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(path!));
        GameState state = GameSession.Load(path!);
        BattleSnapshot source = state.Battle ?? throw new InvalidDataException("The configured Scan control snapshot must contain its active battle.");
        BattleSession first = BattleSession.Restore(source);
        string viewers = string.Join(",", first.Presentation!.Graphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback == 0x84F860).Select(task => task.ReadByte(0x29)));
        Assert.True(first.CanQueueNativeScanInput, $"Native callbacks={first.NativeHostCallbacks}, pause={first.Presentation!.Graphics.UiServices?.PauseDE9}, input={first.Presentation?.Memory?.Scan?.InputInitialized}, text={first.Presentation?.Memory?.Scan?.Text.Initialized}, effect={first.Presentation!.Graphics.OriginalEffect.RequestedEffect}/{first.Presentation!.Graphics.OriginalEffect.Binding.Effect}/{first.Presentation!.Graphics.OriginalEffect.Binding.Entry:X}, descriptor={first.Presentation!.Graphics.OriginalEffect.ReturnedDescriptor:X}, viewer phases={viewers}.");
        Assert.Equal((ushort)0, first.Presentation!.Memory.Scan!.Presentation.Exit269CE2C);
        string actor = first.ActiveAction!.ActorId;
        string target = first.ActiveAction.TargetId;
        int scans = first.Events.Count(entry => entry.Kind == "Scan");
        Assert.Equal(0, GameWorld.ExecuteBattleCommand(first, new(BattleInputKind.ScanInput) { ScanButton = BattleScanButton.Confirm }));
        Assert.Equal(source.Frame, first.Frame);
        Assert.Equal((ushort)0, first.Presentation!.Memory.Scan!.Presentation.Exit269CE2C);
        Assert.False(first.CanQueueNativeScanInput);
        Assert.NotNull(first.Presentation!.Memory.Scan!.ControlPulse);
        Assert.Throws<InvalidOperationException>(() => first.QueueNativeScanInput(BattleScanButton.Confirm));
        BattleSnapshot queued = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(first.Capture()))!;
        BattleSession second = BattleSession.Restore(queued);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int phase = 0; phase < 2 && first.Presentation!.Memory.Scan!.Presentation.Exit269CE2C == 0; phase++)
        {
            if (phase != 0)
            {
                Assert.True(first.CanQueueNativeScanInput, Describe(first));
                GameWorld.ExecuteBattleCommand(first, new(BattleInputKind.ScanInput) { ScanButton = BattleScanButton.Confirm });
                GameWorld.ExecuteBattleCommand(second, new(BattleInputKind.ScanInput) { ScanButton = BattleScanButton.Confirm });
            }
            for (int frame = 0; frame < 30; frame++)
            {
                first.PublishNativeScanMappedButtons(0);
                second.PublishNativeScanMappedButtons(0);
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
                if (first.Presentation!.Memory.Scan!.ControlPulse is null && (first.CanQueueNativeScanInput || first.Presentation!.Memory.Scan.Presentation.Exit269CE2C != 0))
                {
                    break;
                }
            }
        }
        Assert.Equal((ushort)1, first.Presentation!.Memory.Scan!.Presentation.Exit269CE2C);
        Assert.Null(first.Presentation!.Memory.Scan.ControlPulse);
        Assert.Equal(scans, first.Events.Count(entry => entry.Kind == "Scan"));
        Assert.Equal(actor, first.ActiveAction!.ActorId);
        Assert.Equal(target, first.ActiveAction.TargetId);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(path!)));
    }

    private static string Describe(BattleSession session)
    {
        string tasks = string.Join(",", session.Presentation!.Graphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0 && task.Callback is 0x84F860 or 0x84FAF0).Select(task => $"{task.Callback:X}:{task.ReadByte(0x29)}"));
        return $"Callbacks={session.NativeHostCallbacks}, pulse={session.Presentation!.Memory.Scan!.ControlPulse}, tasks={tasks}.";
    }
}
