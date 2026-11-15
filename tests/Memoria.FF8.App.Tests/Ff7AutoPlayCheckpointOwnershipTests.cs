namespace Memoria.FF8.App.Tests;

public sealed class Ff7AutoPlayCheckpointOwnershipTests
{
    [Fact]
    public void AResumedRunRetiresOnlyItsOwnGeneratedChoiceFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-ownership", "current", "choices");
        string filename = new string('A', 64) + ".ffsave";
        Assert.True(Ff7Scene.CanRetireAutoPlayCheckpoint(directory, Path.Combine(directory, filename)));
        Assert.False(Ff7Scene.CanRetireAutoPlayCheckpoint(directory, Path.Combine(directory, "..", "..", "previous", "choices", filename)));
        Assert.False(Ff7Scene.CanRetireAutoPlayCheckpoint(directory, Path.Combine(directory, "..", filename)));
        Assert.False(Ff7Scene.CanRetireAutoPlayCheckpoint(directory, Path.Combine(directory, "standalone.ffsave")));
        Assert.False(Ff7Scene.CanRetireAutoPlayCheckpoint(directory, Path.Combine(directory, new string('A', 64) + ".json")));
    }
}
