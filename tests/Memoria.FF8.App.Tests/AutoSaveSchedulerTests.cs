using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class AutoSaveSchedulerTests
{
    [Fact]
    public void FirstRequestWaitsThroughLaterLocationAndIntervalTriggers()
    {
        var scheduler = new AutoSaveScheduler();
        var preferences = new UiPreferences();
        scheduler.Reset(new(10));
        scheduler.Update(preferences, new(11), TimeSpan.Zero);
        scheduler.TrySave(_ => throw new InvalidOperationException("The save boundary is blocked."));

        scheduler.Update(preferences, new(12), TimeSpan.FromHours(1));
        scheduler.Update(preferences, null, TimeSpan.FromHours(1));

        var saves = new List<AutoSaveKind>();
        scheduler.TrySave(saves.Add);
        scheduler.Update(preferences, new(12), TimeSpan.Zero);
        scheduler.TrySave(saves.Add);

        Assert.Equal([AutoSaveKind.Location], saves);
        Assert.Null(scheduler.Pending);
    }

    [Fact]
    public void IntervalWaitsForFullDelayAndRestartsAfterDeferredSave()
    {
        var scheduler = new AutoSaveScheduler();
        var preferences = new UiPreferences { AutoSaveOnLocationEntry = false };
        scheduler.Update(preferences, new(10), TimeSpan.FromMinutes(9));
        Assert.Null(scheduler.Pending);

        scheduler.Update(preferences, null, TimeSpan.FromMinutes(1));
        Assert.Equal(AutoSaveKind.Interval, scheduler.Pending);
        scheduler.Update(preferences, null, TimeSpan.FromMinutes(9));

        var saves = new List<AutoSaveKind>();
        scheduler.TrySave(saves.Add);
        scheduler.Update(preferences, new(11), TimeSpan.FromMinutes(9));
        Assert.Null(scheduler.Pending);
        scheduler.Update(preferences, new(11), TimeSpan.FromMinutes(1));
        scheduler.TrySave(saves.Add);

        Assert.Equal([AutoSaveKind.Interval, AutoSaveKind.Interval], saves);
    }

    [Fact]
    public void DisablingASourceClearsItsRequest()
    {
        var scheduler = new AutoSaveScheduler();
        var preferences = new UiPreferences();
        scheduler.Update(preferences, new(10), TimeSpan.Zero);
        Assert.Equal(AutoSaveKind.Location, scheduler.Pending);

        preferences = preferences with { AutoSaveOnLocationEntry = false };
        scheduler.Update(preferences, new(10), TimeSpan.FromMinutes(10));
        Assert.Equal(AutoSaveKind.Interval, scheduler.Pending);

        scheduler.Update(preferences with { AutoSaveIntervalMinutes = 0 }, new(10), TimeSpan.FromHours(1));
        Assert.Null(scheduler.Pending);
        scheduler.Update(preferences with { AutoSaveEnabled = false }, new(11), TimeSpan.FromHours(1));
        scheduler.Update(preferences with { AutoSaveOnLocationEntry = true }, new(11), TimeSpan.Zero);
        Assert.Null(scheduler.Pending);
    }

    [Fact]
    public void ResumedLocationIsBaselineAndSameFieldReloadIsAVisit()
    {
        var scheduler = new AutoSaveScheduler();
        var preferences = new UiPreferences();
        var restored = new AutoSaveLocation(10, FieldLoad: NativeFieldLoadContext.FromFieldRequest(10));
        scheduler.Reset(restored);
        scheduler.Update(preferences, null, TimeSpan.Zero);
        scheduler.Update(preferences, restored, TimeSpan.Zero);
        Assert.Null(scheduler.Pending);

        var reentered = new AutoSaveLocation(10, FieldLoad: NativeFieldLoadContext.FromFieldRequest(10));
        scheduler.Update(preferences, reentered, TimeSpan.Zero);
        Assert.Equal(AutoSaveKind.Location, scheduler.Pending);
    }
}
