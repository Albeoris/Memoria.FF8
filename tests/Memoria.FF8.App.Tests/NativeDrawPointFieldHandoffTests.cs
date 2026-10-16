using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDrawPointFieldHandoffTests
{
    [Fact]
    public void ActualPending857KeepsKnownParticleOwnerThroughDormant849Restore()
    {
        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeDrawPointFieldHandoffTests.1")));
        Assert.True(session.State.DepartingFieldState!.Value.GetProperty("Scene").TryGetProperty("NativeDrawPointParticles", out _));
        using GameWorld world = new(session, TestResources.GameDirectory);
        List<string> trace = [];
        world.Events.OperationCompleted += () => trace.Add($"field={world.Events.FieldId},owner={world.Events.Scene.NativeDrawPointParticles is not null}");
        world.EnsureField();
        Assert.True(world.Events.Scene.NativeDrawPointParticles is not null, string.Join(";", trace));
        NativeDrawPointParticles initialized = world.Events.Scene.NativeDrawPointParticles!;
        Assert.Equal(8, initialized.Sparks.Length);
        Assert.All(initialized.Sparks, spark => Assert.Equal(new NativeSparkPoint(-35, 707, -1264), spark.Anchor));
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-particle-handoff-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            // Exact headless pre-action save path: capture current VM before saving.
            session.SetEventState(world.Events.Capture());
            session.Save(snapshot);
            GameSession coldSession = new(GameSession.Load(snapshot));
            using GameWorld cold = new(coldSession, TestResources.GameDirectory);
            cold.EnsureField();
            coldSession.SetEventState(cold.Events.Capture());
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            NativeDrawPointParticles configured = world.Events.Scene.NativeDrawPointParticles!;
            Assert.NotNull(world.Events.Scene.DrawPointReset);
            Assert.Equal(962, world.Events.Scene.DrawPointReset!.Operation);
            for (int index = 0; index < initialized.Sparks.Length; index++)
            {
                Assert.Equal(initialized.Sparks[index].Control, configured.Sparks[index].Control);
                Assert.Equal(initialized.Sparks[index].Counter, configured.Sparks[index].Counter);
                Assert.Equal(initialized.Sparks[index].FirstEdge, configured.Sparks[index].FirstEdge);
            }
            Assert.True(cold.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(System.Text.Json.JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            session.SetEventState(world.Events.Capture());
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.NotNull(restored.Events.Scene.NativeDrawPointParticles);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(System.Text.Json.JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}
