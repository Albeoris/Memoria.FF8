using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class DiagnosticFreshEntryHost
{
    private static readonly Guid instance = Guid.NewGuid();

    internal static DiagnosticFreshEntryHostReceipt Capture(string gameDirectory, DiagnosticFreshEntryDeclaration declaration, NativeBattleExecutionStack? executionStack, Guid battleInstanceId, Guid lifecycleId)
    {
        declaration.Validate();
        if (battleInstanceId == Guid.Empty || lifecycleId == Guid.Empty)
        {
            throw new InvalidDataException("Fresh-entry source observation requires its actual initialized battle and retained lifecycle owner.");
        }
        string root = Path.GetFullPath(gameDirectory);
        ImmutableArray<DiagnosticFreshEntrySourceFile>.Builder observed = ImmutableArray.CreateBuilder<DiagnosticFreshEntrySourceFile>();
        long total = 0;
        foreach (DiagnosticFreshEntrySourceFile file in declaration.SourceFiles)
        {
            string path = Resolve(root, file.RelativePath);
            FileInfo before = new(path);
            if (!before.Exists || before.Length is <= 0 or > 2147483648L || checked(total + before.Length) > 8589934592L)
            {
                throw new InvalidDataException("Fresh-entry selected installed files exceed their bounded size.");
            }
            total = checked(total + before.Length);
            long length = before.Length;
            long modified = before.LastWriteTimeUtc.Ticks;
            string hash = Hash(path);
            FileInfo after = new(path);
            if (after.Length != length || after.LastWriteTimeUtc.Ticks != modified || !string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Fresh-entry selected installed source changed or differs from its declared pin.");
            }
            observed.Add(new(file.RelativePath.Replace('\\', '/'), hash));
        }
        using Process process = Process.GetCurrentProcess();
        return new(battleInstanceId, lifecycleId, instance, Environment.ProcessId, process.StartTime.ToUniversalTime().Ticks, Hash(typeof(RecreateGame).Assembly.Location), Hash(typeof(BattleSession).Assembly.Location), Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(executionStack ?? throw new NotSupportedException("Fresh entry requires its actually installed execution-stack owner.")))).ToLowerInvariant(), observed.ToImmutable());
    }

    internal static string Resolve(string root, string relative)
    {
        new DiagnosticFreshEntrySourceFile(relative, new string('0', 64)).Validate();
        string path = Path.GetFullPath(relative, root);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Fresh-entry source leaves the installed game directory.");
        }
        for (FileSystemInfo? current = new FileInfo(path); current is not null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Fresh-entry source cannot pass through links or junctions.");
            }
        }
        return path;
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
