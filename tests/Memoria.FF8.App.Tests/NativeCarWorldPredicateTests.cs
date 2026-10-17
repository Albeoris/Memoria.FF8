using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarWorldPredicateTests
{
    [Theory]
    [InlineData(0xFF1A)]
    [InlineData(0xFF1B)]
    [InlineData(0xFF1C)]
    [InlineData(0xFF1D)]
    public void ShapeAbsentFromActualSelectedAllocationRejectsWithoutInventingAContactOwner(int opcode)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        using WorldAssets assets = new(directory);
        WorldEntryVariables inputs = WorldEntryVariables.Read(session.State);
        NativeWorldAllocationClause clause = assets.ReadWorldAllocationClause(inputs.StoryWord);
        Assert.False(clause.ContainsShape(92));
        Assert.NotEqual(92, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(0, inputs.ReplacementFlags & 7);
        GameState before = session.State;
        WorldState candidate = scene.PrepareCurrentCarPlacement(12390, -26709);
        Assert.False(scene.EvaluateWorldEventPredicate(candidate, 0, before.SavedWorld, (ushort)opcode, 92));
        Assert.Same(before, session.State);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(session.State)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(73)]
    [InlineData(94)]
    public void DeclaredAndAppendedShapesStillRequireTheirActualContactPublication(int shape)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.EvaluateWorldEventPredicate(scene.State, 0, before.SavedWorld!, 0xFF1B, (ushort)shape));
        Assert.Same(before, session.State);
    }
}
