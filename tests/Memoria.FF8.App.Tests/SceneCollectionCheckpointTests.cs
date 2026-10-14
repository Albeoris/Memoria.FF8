using System.Text.Json;
using Memoria.FF8.Events;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed class SceneCollectionCheckpointTests
{
    [Fact]
    public void BitmapMutationAndRestorationKeepCanonicalStateWithoutReorderingParty()
    {
        SceneEventState original = new();
        foreach (SortedSet<int> bitmap in new[] { original.LockedTriangles, original.Members, original.UnlockedTutorials, original.AcquiredGuardianForces })
        {
            bitmap.UnionWith([7, 2, 5, 1]);
            bitmap.Remove(2);
            bitmap.Remove(7);
            bitmap.Add(7);
            bitmap.Add(2);
        }
        original.Party.AddRange([5, 0, 1]);
        string captured = JsonSerializer.Serialize(original);
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(captured)!;
        Assert.Equal(captured, JsonSerializer.Serialize(restored));
        foreach (SortedSet<int> bitmap in new[] { restored.LockedTriangles, restored.Members, restored.UnlockedTutorials, restored.AcquiredGuardianForces })
        {
            Assert.Equal(new[] { 1, 2, 5, 7 }, bitmap);
        }
        Assert.Equal(new[] { 5, 0, 1 }, restored.Party);
        restored.LockedTriangles.Remove(5);
        restored.LockedTriangles.Add(5);
        Assert.Equal(captured, JsonSerializer.Serialize(restored));
    }
}
