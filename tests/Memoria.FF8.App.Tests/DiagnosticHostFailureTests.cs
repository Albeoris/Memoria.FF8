namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticHostFailureTests
{
    [Fact]
    public void Finalization_stages_retain_clocks_before_a_later_host_failure()
    {
        string directory = Path.Combine(Path.GetTempPath(), "diagnostic-finalization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DiagnosticHostFailure.WriteStage(directory, "report-write-begin", 580, 900);
            using StringWriter errors = new();
            DiagnosticHostFailure.Write(new InvalidDataException("Publication failed"), directory, errors);
            string logged = File.ReadAllText(Path.Combine(directory, "host.log"));
            Assert.Contains("diagnostic-finalization=report-write-begin callback=580 frame=900", logged);
            Assert.Contains("Publication failed", logged);
            Assert.True(logged.IndexOf("report-write-begin", StringComparison.Ordinal) < logged.IndexOf("Publication failed", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(Path.Combine(directory, "host.log"));
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void Fatal_startup_error_reaches_the_existing_supervisor_log_channel()
    {
        string directory = Path.Combine(Path.GetTempPath(), "diagnostic-host-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            InvalidDataException failure = new("The original resource could not be loaded.");
            using StringWriter errors = new();
            DiagnosticHostFailure.Write(failure, directory, errors);
            string logged = File.ReadAllText(Path.Combine(directory, "host.log"));
            Assert.Contains(failure.ToString(), logged);
            Assert.Contains(failure.ToString(), errors.ToString());
        }
        finally
        {
            File.Delete(Path.Combine(directory, "host.log"));
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_unavailable_or_unselected_log_keeps_the_original_error(bool selectUnavailable)
    {
        string missing = Path.Combine(Path.GetTempPath(), "diagnostic-no-directory-" + Guid.NewGuid().ToString("N"));
        InvalidOperationException failure = new("Original host failure");
        using StringWriter errors = new();
        DiagnosticHostFailure.Write(failure, selectUnavailable ? missing : null, errors);
        Assert.Contains(failure.ToString(), errors.ToString());
        Assert.False(Directory.Exists(missing));
    }
}
