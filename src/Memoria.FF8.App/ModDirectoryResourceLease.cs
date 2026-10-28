using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8;

/// <summary>Pins a mod directory for all lazy resource consumers, with an exclusive startup update boundary.</summary>
internal sealed class ModDirectoryResourceLease : IDisposable
{
    private readonly object gate = new();
    private readonly string runtimePath;
    private FileStream? startup;
    private FileStream? runtime;
    private bool disposed;

    private ModDirectoryResourceLease(string stateDirectory)
    {
        runtimePath = Path.Combine(stateDirectory, "runtime.lock");
    }

    public bool IsWriter { get; private set; }

    public static string StateDirectory(string modsRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsRoot));
        string parent = Path.GetDirectoryName(root) ?? throw new ArgumentException("A managed mod directory must be below the volume root.", nameof(modsRoot));
        string identity = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16].ToLowerInvariant();
        return Path.Combine(parent, ".memoria-mod-library-" + hash);
    }

    public static ModDirectoryResourceLease Acquire(string modsRoot, bool requestWriter)
    {
        string state = StateDirectory(modsRoot);
        Directory.CreateDirectory(state);
        RejectLinks(state);
        ModDirectoryResourceLease lease = new(state);
        try
        {
            lease.startup = AcquireStartup(Path.Combine(state, "startup.lock"));
            RejectLinks(lease.runtimePath);
            if (!File.Exists(lease.runtimePath))
            {
                using FileStream initialize = new(lease.runtimePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                initialize.Flush(true);
            }
            if (requestWriter)
            {
                try
                {
                    lease.runtime = new FileStream(lease.runtimePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    lease.IsWriter = true;
                    return lease;
                }
                catch (IOException exception) when (IsSharingViolation(exception))
                {
                    // Existing readers retain their backing paths; this startup becomes another reader.
                }
            }
            lease.runtime = OpenReader(lease.runtimePath);
            lease.startup.Dispose();
            lease.startup = null;
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>Downgrades an exclusive startup lease without opening a gap for another writer.</summary>
    public void FinishStartup()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsWriter)
            {
                return;
            }
            try
            {
                runtime!.Dispose();
                runtime = null;
                runtime = OpenReader(runtimePath);
                IsWriter = false;
                startup!.Dispose();
                startup = null;
            }
            catch
            {
                DisposeHandles();
                disposed = true;
                throw;
            }
        }
    }

    private static FileStream OpenReader(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    private static FileStream AcquireStartup(string path)
    {
        RejectLinks(path);
        Stopwatch elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(15))
            {
                Thread.Sleep(25);
            }
        }
    }

    private static bool IsSharingViolation(IOException exception)
    {
        int code = exception.HResult & 0xffff;
        return code is 32 or 33 || !OperatingSystem.IsWindows() && code is 11 or 13;
    }

    private static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Mod resource lease paths must not contain links or reparse points.");
            }
        }
    }

    private void DisposeHandles()
    {
        runtime?.Dispose();
        runtime = null;
        startup?.Dispose();
        startup = null;
        IsWriter = false;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (!disposed)
            {
                DisposeHandles();
                disposed = true;
            }
        }
    }
}
