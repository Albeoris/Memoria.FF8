using System.Text.Json;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SaltLakeScriptMovementTests
{
    [Fact]
    public void Actual516Move405ContinuesThroughNativeContactGeometry()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        FieldNativeMovementRequest request = new(1, -399114, 6707494, -3149824, 126, 7929, 48, false, true, 0, true);
        FieldNativeMovementResult geometry = FieldNativeMovementProbe.Resolve(world.Field!.Walkmesh, request, world.Events.Scene.LockedTriangles, _ => false);
        Assert.Equal(new Vector3(-224, 1938, -811), world.Field.Walkmesh.Triangles[1].A);
        Assert.Equal(new Vector3(89, 1631, -779), world.Field.Walkmesh.Triangles[1].B);
        Assert.Equal(new Vector3(-224, 1631, -762), world.Field.Walkmesh.Triangles[1].C);
        Assert.Equal(FieldNativeMovementStatus.Accepted, geometry.Status);
        Assert.Equal(new FieldNativeSlopeFactors(-215, -636, -4041, 4090, 4047), geometry.Factors);
        Assert.Equal((-392951, 6832685, -3170304, 1, (byte)126), (geometry.FixedX, geometry.FixedY, geometry.FixedZ, geometry.Triangle, geometry.Direction));
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Salt-Lake-MOVE-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}
