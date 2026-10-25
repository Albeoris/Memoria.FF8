using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticJsonPublicationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-diagnostic-publication-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Immutable_publication_preserves_the_original_bytes_when_replacement_is_rejected()
    {
        string path = Path.Combine(directory, "candidate.json");
        Write(path, new { Sequence = 1 }, replace: false);
        byte[] original = File.ReadAllBytes(path);

        TargetInvocationException same = Assert.Throws<TargetInvocationException>(() => Write(path, new { Sequence = 1 }, replace: false));
        Assert.IsAssignableFrom<IOException>(same.InnerException);
        TargetInvocationException different = Assert.Throws<TargetInvocationException>(() => Write(path, new { Sequence = 2 }, replace: false));
        Assert.IsAssignableFrom<IOException>(different.InnerException);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Atomic_replacement_publishes_new_bytes_while_an_open_reader_retains_the_old_record()
    {
        string path = Path.Combine(directory, "progress.json");
        Write(path, new { Sequence = 1 }, replace: true);
        byte[] original = File.ReadAllBytes(path);
        using FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        Write(path, new { Sequence = 2 }, replace: true);

        byte[] retained = new byte[checked((int)reader.Length)];
        reader.ReadExactly(retained);
        Assert.Equal(original, retained);
        using JsonDocument published = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal(2, published.RootElement.GetProperty("sequence").GetInt32());
    }

    private static void Write(string path, object value, bool replace)
    {
        MethodInfo writer = typeof(RecreateGame).GetMethod("WriteDiagnosticJson", BindingFlags.Static | BindingFlags.NonPublic)!;
        writer.Invoke(null, [path, value, replace]);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
