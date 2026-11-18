namespace Memoria.FF8.BattleEffects.Tests;

public sealed class WindowsGpuSessionValidityTests
{
    [Theory]
    [InlineData(1, 1, 1, 0, 1, true, true, "valid", "windows-session-interactive")]
    [InlineData(1, 1, 1, 0, 0, true, true, "invalid", "windows-session-locked")]
    [InlineData(1, 1, 1, 0, 0, false, false, "invalid", "windows-session-locked")]
    [InlineData(1, 1, 1, 4, 1, true, true, "invalid", "windows-session-not-active")]
    [InlineData(1, 1, 1, 0, 1, false, true, "invalid", "windows-input-desktop-unavailable")]
    [InlineData(1, 1, 1, 0, 1, true, false, "invalid", "windows-input-desktop-not-current")]
    [InlineData(1, 1, 1, 0, -1, true, true, "unknown", "windows-session-lock-state-unavailable")]
    [InlineData(1, 2, 1, 0, 1, true, true, "unknown", "windows-session-state-unavailable")]
    [InlineData(1, null, 1, 0, 1, true, true, "unknown", "windows-session-state-unavailable")]
    [InlineData(1, 1, 2, 0, 1, true, true, "unknown", "windows-session-state-unavailable")]
    [InlineData(1, 1, 1, null, 1, true, true, "unknown", "windows-session-state-unavailable")]
    [InlineData(1, 1, 1, 0, null, true, true, "unknown", "windows-session-state-unavailable")]
    public void Performance_requires_an_authenticated_unlocked_active_session_and_the_available_current_input_desktop(int currentSession, int? reportedSession, int? level, int? state, int? flags, bool desktopAvailable, bool desktopMatches, string status, string reason)
    {
        WindowsGpuSessionValidity validity = WindowsGpuSessionProbe.Classify(currentSession, reportedSession, level, state, flags, desktopAvailable, desktopMatches);
        Assert.Equal(status, validity.Status);
        Assert.Equal(reason, validity.Reason);
    }
}
