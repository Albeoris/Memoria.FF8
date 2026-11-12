using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class WindowTransferStateTests
{
    [Fact]
    public void Transfer_preserves_borderless_and_saved_maximized_placement_through_json()
    {
        WindowTransferPlacement placement = new(new WindowTransferRectangle(-1200, 80, 1100, 760), false);
        WindowTransferPlacement windowed = new(new WindowTransferRectangle(100, 140, 900, 620), true, 2, -1, -1, 20, 30);
        WindowTransferState state = new(true, new WindowTransferRectangle(-1920, 0, 1920, 1080), placement, windowed, 1920, 1080, 1920, 1080);
        WindowTransferState restored = JsonSerializer.Deserialize<WindowTransferState>(JsonSerializer.Serialize(state))!;

        Assert.Equal(state, restored);
    }

    [Fact]
    public void Restoring_transfer_updates_mode_policy_without_reentering_borderless()
    {
        FakePlatform platform = new();
        WindowModeController controller = new(platform);
        WindowTransferState state = CreateState(true);

        controller.PrepareTransferChild();
        controller.RestoreTransfer(state);
        controller.Apply(true);
        Assert.Same(state, controller.CaptureTransfer());
        controller.RevealTransferChild();
        controller.Apply(false);

        Assert.Equal(new[] { "hide", "restore", "capture", "reveal", "windowed" }, platform.Calls);
    }

    [Fact]
    public void Failed_transfer_does_not_change_the_controllers_mode()
    {
        FakePlatform platform = new() { FailRestore = true };
        WindowModeController controller = new(platform);

        Assert.Throws<InvalidDataException>(() => controller.RestoreTransfer(CreateState(true)));
        controller.Apply(true);

        Assert.Equal(new[] { "restore", "borderless" }, platform.Calls);
    }

    private static WindowTransferState CreateState(bool borderless)
    {
        WindowTransferPlacement placement = new(new WindowTransferRectangle(20, 40, 800, 600), false);
        return new WindowTransferState(borderless, placement.NormalBounds, placement, borderless ? placement : null, 800, 600, 800, 600);
    }

    private sealed class FakePlatform : IGameWindowPlatform
    {
        internal readonly List<string> Calls = [];
        internal bool FailRestore;
        private WindowTransferState? state;

        public void Maximize() => Calls.Add("maximize");
        public void EnterBorderless() => Calls.Add("borderless");
        public void RestoreWindowed() => Calls.Add("windowed");
        public void PrepareTransferChild() => Calls.Add("hide");
        public void RevealTransferChild() => Calls.Add("reveal");

        public WindowTransferState CaptureTransfer()
        {
            Calls.Add("capture");
            return state!;
        }

        public void RestoreTransfer(WindowTransferState value)
        {
            Calls.Add("restore");
            if (FailRestore)
            {
                throw new InvalidDataException();
            }
            state = value;
        }
    }
}
