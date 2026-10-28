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

    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1175)]
    [InlineData(1176)]
    public void ContendedProgressKeepsThePreviousRecordAndCoalescesOnTheNextPublication(int error)
    {
        string path = Path.Combine(directory, "progress.json");
        byte[] original = "{\"sequence\":1}"u8.ToArray();
        Assert.True(DiagnosticJsonPublication.Publish(path, original, progress: true));
        int moves = 0;
        bool published = DiagnosticJsonPublication.Publish(path, "{\"sequence\":2}"u8.ToArray(), progress: true, move: (source, destination, overwrite) =>
        {
            moves++;
            throw new PublicationIOException(error);
        }, delay: _ => throw new InvalidOperationException("Progress must not block."));

        Assert.False(published);
        Assert.Equal(1, moves);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.True(DiagnosticJsonPublication.Publish(path, "{\"sequence\":3}"u8.ToArray(), progress: true));
        using JsonDocument current = JsonDocument.Parse(File.ReadAllBytes(path));
        Assert.Equal(3, current.RootElement.GetProperty("sequence").GetInt32());
    }

    [Fact]
    public void ContendedTemporaryProgressFileDoesNotBlockOrReplaceTheCurrentRecord()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows sharing conflicts require Windows file handles.");
        string path = Path.Combine(directory, "progress.json");
        byte[] original = "{\"sequence\":1}"u8.ToArray();
        Assert.True(DiagnosticJsonPublication.Publish(path, original, progress: true));
        using FileStream competingReader = new(path + ".tmp", FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);

        Assert.False(DiagnosticJsonPublication.Publish(path, "{\"sequence\":2}"u8.ToArray(), progress: true, delay: _ => throw new InvalidOperationException("Progress must not block.")));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void RequiredReportsRetryTransientConflictsAndPublishAllBytes()
    {
        string path = Path.Combine(directory, "candidate.json");
        byte[] bytes = "{\"status\":\"complete\"}"u8.ToArray();
        int attempts = 0;
        List<TimeSpan> delays = [];
        Assert.True(DiagnosticJsonPublication.Publish(path, bytes, progress: false, move: (source, destination, overwrite) =>
        {
            Assert.False(overwrite);
            if (++attempts < 3)
            {
                throw new PublicationIOException(32);
            }
            File.Move(source, destination, overwrite);
        }, delay: delays.Add));

        Assert.Equal(3, attempts);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20) }, delays);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(true, 5, 1)]
    [InlineData(false, 5, 1)]
    [InlineData(false, 32, 5)]
    public void PermanentErrorsAndExhaustedRequiredRetriesRemainFailures(bool progress, int error, int expectedAttempts)
    {
        string path = Path.Combine(directory, "report.json");
        int attempts = 0;
        PublicationIOException failure = Assert.Throws<PublicationIOException>(() => DiagnosticJsonPublication.Publish(path, "{}"u8.ToArray(), progress, move: (source, destination, overwrite) =>
        {
            attempts++;
            throw new PublicationIOException(error);
        }, delay: _ =>
        {
        }));

        Assert.Equal(error, failure.HResult & 0xFFFF);
        Assert.Equal(expectedAttempts, attempts);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PermissionDeniedIsNotTreatedAsTransientPublicationContention(bool progress)
    {
        string path = Path.Combine(directory, "report.json");
        int attempts = 0;

        Assert.Throws<UnauthorizedAccessException>(() => DiagnosticJsonPublication.Publish(path, "{}"u8.ToArray(), progress, move: (source, destination, overwrite) =>
        {
            attempts++;
            throw new UnauthorizedAccessException("Injected permanent publication permission failure.");
        }, delay: _ => throw new InvalidOperationException("Permission failures must not retry.")));

        Assert.Equal(1, attempts);
        Assert.False(File.Exists(path));
    }

    private sealed class PublicationIOException : IOException
    {
        internal PublicationIOException(int error) : base("Injected publication failure.", unchecked((int)0x80070000) | error)
        {
        }
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
