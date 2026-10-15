using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ControllerFeedbackTests
{
    [Fact]
    public void Presentation_uses_both_native_motor_requests_without_mutating_game_state()
    {
        List<(float Low, float High)> requests = [];
        ControllerFeedback feedback = new((low, high) =>
        {
            requests.Add((low, high));
            return true;
        });
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        NativeVibrationProcessState process = cold with { Ports = cold.Ports.SetItem(0, new NativeVibrationPort(64, 192, true)) };
        Assert.True(feedback.Update(process, true, true));
        Assert.Equal((64 / 255f, 192 / 255f), Assert.Single(requests));
        Assert.True(process.Ports[0].Pending);
        Assert.Equal((byte)64, process.Ports[0].RequestedA);
        Assert.Equal((byte)192, process.Ports[0].RequestedB);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Focus_pause_capture_and_preference_mute_motor_output(bool active, bool enabled)
    {
        (float Low, float High) request = (-1, -1);
        ControllerFeedback feedback = new((low, high) =>
        {
            request = (low, high);
            return true;
        });
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        NativeVibrationProcessState process = cold with { Ports = cold.Ports.SetItem(0, new NativeVibrationPort(255, 255, true)) };
        Assert.True(feedback.Update(process, active, enabled));
        Assert.Equal((0f, 0f), request);
        Assert.Equal((byte)255, process.Ports[0].RequestedA);
        Assert.True(feedback.Update(null, true, true));
        Assert.Equal((0f, 0f), request);
    }

    [Fact]
    public void Unsupported_actuator_is_reported_without_changing_native_owners()
    {
        ControllerFeedback feedback = new((_, _) => false);
        Assert.False(feedback.Update(NativeVibrationProcessState.FromColdProcess(), true, true));
    }
}
