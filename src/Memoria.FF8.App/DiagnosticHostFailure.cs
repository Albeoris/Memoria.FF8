namespace Memoria.FF8;

internal static class DiagnosticHostFailure
{
    internal static void Write(Exception exception, string? outputDirectory, TextWriter errors)
    {
        errors.WriteLine(exception);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }
        try
        {
            using StreamWriter log = new(Path.Combine(outputDirectory, "host.log"), append: true);
            log.WriteLine(exception);
        }
        catch (Exception loggingFailure) when (loggingFailure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            errors.WriteLine($"Diagnostic host log could not be written: {loggingFailure.Message}");
        }
    }
}
