using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NavigationControlTests
{
    [Fact]
    public void ObservationAndPlanningPreserveCompleteLoadedSession()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CONTROL_SAVE");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path), "Configure original resources and a field control fixture.");
        using MemoryStream source = new(File.ReadAllBytes(path!));
        Ff7SessionSave saved = CombinedSessionCodec.Read(source).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root!), saved);
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        typeof(Ff7Scene).GetProperty(nameof(Ff7Scene.Session))!.SetValue(scene, session);
        string before = JsonSerializer.Serialize(session.Capture());
        string observation = JsonSerializer.Serialize(scene.ObserveDiagnostic());
        Assert.Contains("StoryProgress", observation);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        string point = FormattableString.Invariant($"point:{player.X4096 / 4096f},{player.Y4096 / 4096f},{player.Z4096 / 4096f}:{player.Triangle}");
        scene.PlanDiagnostic(point);
        scene.PlanDiagnostic(point);
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic("actor:2147483647"));
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic("line:2147483647"));
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic("gateway:2147483647"));
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic("point:NaN,0,0"));
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic("point:0,0,0:2147483647"));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Same(session, typeof(Ff7Scene).GetField("navigationSession", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scene));
    }

    [Theory]
    [InlineData("actor:0:1")]
    [InlineData("line:0:6")]
    [InlineData("point:0,0")]
    [InlineData("unsupported:0")]
    public void MalformedTargetsFailBeforeDriverOrSessionAccess(string target)
    {
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        typeof(Ff7Scene).GetProperty(nameof(Ff7Scene.Session))!.SetValue(scene, session);
        Assert.Throws<ArgumentException>(() => scene.PlanDiagnostic(target));
        Assert.Null(typeof(Ff7Scene).GetField("navigationDriver", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scene));
    }
}
