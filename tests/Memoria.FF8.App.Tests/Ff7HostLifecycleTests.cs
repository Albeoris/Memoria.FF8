using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7HostLifecycleTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-native-handoff-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Native_handoff_preserves_opaque_bytes_and_pauses_until_cancelled()
    {
        byte[] payload = "native-game-continuation"u8.ToArray();
        int permissions = 0;
        UpdateSaveHandoff handoff = new(() => permissions++, path =>
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, payload);
        });
        string first = Path.Combine(directory, "first.save");
        handoff.Save(first, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.True(handoff.Paused);
        Assert.Equal(payload, File.ReadAllBytes(first));
        Assert.Throws<InvalidOperationException>(() => handoff.Save(Path.Combine(directory, "blocked.save"), DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.Equal(1, permissions);
        handoff.Cancel();
        handoff.Save(Path.Combine(directory, "second.save"), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(2, permissions);
        Assert.True(handoff.Paused);
    }

    [Fact]
    public void Failed_native_validation_never_publishes_or_pauses()
    {
        UpdateSaveHandoff handoff = new(() => { }, path =>
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "incomplete");
            throw new NotSupportedException("Unsupported native continuation");
        });
        Assert.Throws<NotSupportedException>(() => handoff.Save(Path.Combine(directory, "failed.save"), DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.False(handoff.Paused);
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public void Failed_combined_preparation_keeps_current_host_and_requested_state()
    {
        Ff7Game game = (Ff7Game)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Game));
        CombinedSessionSave candidate = new();
        Func<CombinedSessionSave, PreparedCrossover> prepare = saved =>
        {
            Assert.Same(candidate, saved);
            throw new NotSupportedException("Unsupported inactive world");
        };
        typeof(Ff7Game).GetField("prepareCrossover", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, prepare);
        MethodInfo request = typeof(Ff7Game).GetMethod("RequestCrossover", BindingFlags.NonPublic | BindingFlags.Instance)!;
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => request.Invoke(game, [candidate]));
        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Null(game.RequestedCrossover);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
