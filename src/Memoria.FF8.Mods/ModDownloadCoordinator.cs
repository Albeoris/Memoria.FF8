using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.Mods;

public enum ModDownloadPhase { Queued, Resolving, Downloading, Verifying, ReadyToInstall, Paused, WaitingToRetry, NeedsUserAction, Failed, Cancelled }

public sealed record DownloadJobSnapshot(Guid JobId, RemoteModEntry Entry, string TargetProfile, bool InstallAfterDownload, ModDownloadPhase Phase)
{
    public int SchemaVersion { get; init; } = 1;
    public long CompletedBytes { get; init; }
    public long? TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public string? ArchivePath { get; init; }
    public string? Sha256 { get; init; }
    public string? Details { get; init; }
}

/// <summary>Application-lifetime queue. Snapshot reads and commands perform no filesystem or network work.</summary>
public sealed class ModDownloadCoordinator : IDisposable
{
    private sealed record PartialMetadata(string Link, string Validator, long? Total);
    private readonly string root;
    private readonly object commandGate = new();
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly ConcurrentDictionary<Guid, DownloadJobSnapshot> jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> running = new();
    private readonly ConcurrentDictionary<Guid, Task> tasks = new();
    private readonly SemaphoreSlim transferGate = new(1);
    private readonly SemaphoreSlim persistenceGate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private FileStream? instanceLock;
    private volatile bool initialized;
    private long revision;
    public long Revision => Interlocked.Read(ref revision);
    public IReadOnlyList<DownloadJobSnapshot> Snapshot => Array.AsReadOnly(jobs.Values.OrderBy(job => job.JobId).ToArray());

    public ModDownloadCoordinator(string downloadRoot, HttpClient? client = null)
    {
        root = Path.GetFullPath(downloadRoot);
        ownsClient = client == null;
        this.client = client ?? ModDownloadTransport.CreateClient();
    }

    public Task InitializeAsync(CancellationToken token = default) => Task.Run(async () =>
    {
        Directory.CreateDirectory(root);
        instanceLock = new FileStream(Path.Combine(root, "queue.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        foreach (string path in Directory.EnumerateDirectories(root).Where(directory => Guid.TryParseExact(Path.GetFileName(directory), "N", out _) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0).Select(directory => Path.Combine(directory, "job.json")).Where(File.Exists))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (new FileInfo(path).Length > 1024 * 1024)
                {
                    continue;
                }
                DownloadJobSnapshot? job = JsonSerializer.Deserialize<DownloadJobSnapshot>(await File.ReadAllTextAsync(path, token));
                if (job == null || job.SchemaVersion != 1 || Path.GetFullPath(Path.GetDirectoryName(path)!) != JobDirectory(job.JobId))
                {
                    continue;
                }
                jobs[job.JobId] = job with { Phase = job.Phase == ModDownloadPhase.Cancelled ? ModDownloadPhase.Cancelled : ModDownloadPhase.Paused, Message = "Recovered after restart. Resume to validate and continue." };
            }
            catch (Exception error) when (error is IOException or JsonException or ArgumentException)
            {
                // Unknown files never authorize deleting partials or user files.
            }
        }
        initialized = true;
        Interlocked.Increment(ref revision);
    }, token);

    public Guid Enqueue(RemoteModEntry entry, string targetProfile, bool installAfterDownload = false)
    {
        EnsureInitialized();
        lock (commandGate)
        {
            DownloadJobSnapshot? existing = jobs.Values.FirstOrDefault(job => job.Entry.SourceId == entry.SourceId && job.Entry.Id == entry.Id && (job.Entry.Version == entry.Version || RemoteModEntry.CompareRelease(job.Entry.Version, entry.Version) == 0) && job.TargetProfile == targetProfile && job.Phase != ModDownloadPhase.Cancelled);
            if (existing != null)
            {
                return existing.JobId;
            }
            Guid id = Guid.NewGuid();
            RemoteModEntry pinned = entry with { Links = Array.AsReadOnly(entry.Links.ToArray()) };
            jobs[id] = new(id, pinned, targetProfile, installAfterDownload, ModDownloadPhase.Queued);
            Start(id);
            return id;
        }
    }

    public void Pause(Guid id) => Stop(id, ModDownloadPhase.Paused);
    public void Cancel(Guid id) => Stop(id, ModDownloadPhase.Cancelled);
    public void Resume(Guid id) => Retry(id);
    public void Retry(Guid id)
    {
        EnsureInitialized();
        if (jobs.TryGetValue(id, out DownloadJobSnapshot? job) && job.Phase is ModDownloadPhase.Paused or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction)
        {
            if (tasks.TryGetValue(id, out Task? task) && !task.IsCompleted)
            {
                _ = task.ContinueWith(_ => Retry(id), TaskScheduler.Default);
                return;
            }
            jobs[id] = job with { Phase = ModDownloadPhase.Queued, ErrorCode = null, Message = null };
            Start(id);
        }
    }

    public string? GetCompletedPath(Guid id) => jobs.TryGetValue(id, out DownloadJobSnapshot? job) && job.Phase == ModDownloadPhase.ReadyToInstall ? job.ArchivePath : null;

    private void Stop(Guid id, ModDownloadPhase phase)
    {
        if (!jobs.TryGetValue(id, out DownloadJobSnapshot? job) || job.Phase == ModDownloadPhase.ReadyToInstall && phase != ModDownloadPhase.Cancelled)
        {
            return;
        }
        jobs[id] = job with { Phase = phase, BytesPerSecond = 0 };
        Interlocked.Increment(ref revision);
        if (running.TryGetValue(id, out CancellationTokenSource? cancellation))
        {
            cancellation.Cancel();
        }
        _ = Task.Run(async () =>
        {
            if (phase == ModDownloadPhase.Cancelled && tasks.TryGetValue(id, out Task? pending))
            {
                await pending.ConfigureAwait(false);
            }
            await SaveAsync(id).ConfigureAwait(false);
            if (phase == ModDownloadPhase.Cancelled)
            {
                string directory = JobDirectory(id);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                {
                    foreach (string name in new[] { "package.download", "download.part", "partial.json" })
                    {
                        File.Delete(Path.Combine(directory, name));
                    }
                }
            }
        }).ContinueWith(completed =>
        {
            _ = completed.Exception;
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void Start(Guid id)
    {
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        running[id] = cancellation;
        Track(id, Task.Run(() => RunAsync(id, cancellation)));
    }

    private void Track(Guid id, Task task)
    {
        tasks[id] = task;
        Interlocked.Increment(ref revision);
        _ = task.ContinueWith(completed =>
        {
            _ = completed.Exception;
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task RunAsync(Guid id, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
        bool entered = false;
        try
        {
            await SaveAsync(id);
            await transferGate.WaitAsync(token);
            entered = true;
            token.ThrowIfCancellationRequested();
            DownloadJobSnapshot job = jobs[id];
            Directory.CreateDirectory(JobDirectory(id));
            string cachedPath = Path.Combine(JobDirectory(id), "package.download");
            if (job.ArchivePath == cachedPath && job.Sha256 != null && File.Exists(cachedPath))
            {
                Update(id, state => state with { Phase = ModDownloadPhase.Verifying }, token);
                using FileStream cached = new(cachedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
                string digest = Convert.ToHexString(await SHA256.HashDataAsync(cached, token)).ToLowerInvariant();
                if (cached.Length != job.CompletedBytes || !string.Equals(digest, job.Sha256, StringComparison.Ordinal))
                {
                    Update(id, state => state with { ArchivePath = null, Sha256 = null }, token);
                    throw new InvalidDataException("The downloaded cache changed. Retry to download a fresh copy.");
                }
                Update(id, state => state with { Phase = ModDownloadPhase.ReadyToInstall, Message = "Cached download verified. Package inspection is required before installation." }, token);
                return;
            }
            for (int mirror = 0; mirror < job.Entry.Links.Count; mirror++)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        Update(id, state => state with { Phase = ModDownloadPhase.Resolving }, token);
                        await TransferAsync(id, job.Entry.Links[mirror], token);
                        return;
                    }
                    catch (Exception error) when (IsTransient(error) && attempt < 2)
                    {
                        Update(id, state => state with { Phase = ModDownloadPhase.WaitingToRetry, Message = "Connection interrupted. Retrying the saved partial download." }, token);
                        await SaveAsync(id);
                        TimeSpan delay = error.Data["RetryAfter"] is TimeSpan retryAfter ? retryAfter : TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.NextDouble());
                        await Task.Delay(delay, token);
                    }
                    catch (Exception error) when (IsTransient(error) && mirror + 1 < job.Entry.Links.Count)
                    {
                        break;
                    }
                }
            }
            throw new NotSupportedException("No supported download locations are available.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (jobs[id].Phase is not (ModDownloadPhase.Cancelled or ModDownloadPhase.Paused))
            {
                jobs[id] = jobs[id] with { Phase = ModDownloadPhase.Paused, Message = "Download paused. The partial file has been saved." };
            }
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested)
            {
                jobs[id] = jobs[id] with
                {
                    Phase = error is ModDownloadActionException ? ModDownloadPhase.NeedsUserAction : ModDownloadPhase.Failed,
                    ErrorCode = error switch { ModDownloadActionException => "provider-action", InvalidDataException => "invalid-response", NotSupportedException => "unsupported-provider", IOException or UnauthorizedAccessException => "storage-or-transfer", _ => "connection" },
                    Details = $"Failed phase: {jobs[id].Phase}. {DescribeFailure(error)}",
                    Message = error switch { ModDownloadActionException => "The provider requires an action in your browser.", InvalidDataException => "The package response failed validation. Retry or choose another source.", NotSupportedException => "This download provider is unsupported.", IOException => "The download could not be saved or completed. Check storage and retry.", _ => "The connection stopped responding. Your partial download has been saved." }, BytesPerSecond = 0
                };
            }
        }
        finally
        {
            try
            {
                await SaveAsync(id);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                jobs[id] = jobs[id] with { Phase = ModDownloadPhase.Failed, ErrorCode = "queue-persistence", Message = "The queue state could not be saved. Check available storage." };
            }
            Interlocked.Increment(ref revision);
            running.TryRemove(id, out _);
            cancellation.Dispose();
            if (entered)
            {
                transferGate.Release();
            }
        }
    }

    private async Task TransferAsync(Guid id, string link, CancellationToken token)
    {
        (Uri uri, bool drive) = ModDownloadTransport.DecodeLink(link);
        string directory = JobDirectory(id);
        string partialPath = Path.Combine(directory, "download.part");
        string metadataPath = Path.Combine(directory, "partial.json");
        PartialMetadata? metadata = null;
        if (File.Exists(metadataPath))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<PartialMetadata>(await File.ReadAllTextAsync(metadataPath, token));
            }
            catch (JsonException)
            {
                metadata = null;
            }
        }
        long offset = metadata?.Link == link && !string.IsNullOrEmpty(metadata.Validator) && File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        for (int restart = 0; restart < 2; restart++)
        {
            using HttpResponseMessage response = await ModDownloadTransport.OpenAsync(client, uri, offset, metadata?.Validator, drive, token);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && offset > 0)
            {
                offset = 0;
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                HttpRequestException failure = new("The download source returned an HTTP error.", null, response.StatusCode);
                TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                if (retryAfter != null)
                {
                    failure.Data["RetryAfter"] = TimeSpan.FromSeconds(Math.Clamp(retryAfter.Value.TotalSeconds, 0, 60));
                }
                throw failure;
            }
            if (response.Content.Headers.ContentEncoding.Count > 0)
            {
                throw new InvalidDataException("Encoded responses cannot be safely resumed.");
            }
            string? validator = response.Headers.ETag is { IsWeak: false } etag ? etag.ToString() : null;
            long? total;
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var range = response.Content.Headers.ContentRange;
                if (offset == 0 || range == null || range.Unit != "bytes" || range.From != offset || range.To < range.From || range.Length == null || range.To != range.Length - 1 || validator == null || validator != metadata?.Validator || metadata.Total != null && range.Length != metadata.Total || response.Content.Headers.ContentLength != null && response.Content.Headers.ContentLength != range.To - range.From + 1)
                {
                    offset = 0;
                    File.Delete(metadataPath);
                    if (restart == 0)
                    {
                        continue;
                    }
                    throw new InvalidDataException("The server returned an inconsistent byte range.");
                }
                total = range.Length;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                if (offset > 0)
                {
                    Update(id, state => state with { Message = "This source requires restarting the download from the beginning." }, token);
                }
                offset = 0;
                total = response.Content.Headers.ContentLength;
            }
            else
            {
                throw new InvalidDataException("Unexpected package response status.");
            }
            if (total is { } expected)
            {
                long available = new DriveInfo(Path.GetPathRoot(directory)!).AvailableFreeSpace;
                if (expected - offset > available - 64L * 1024 * 1024)
                {
                    throw new IOException("There is not enough free space for the remaining download and storage reserve.");
                }
            }
            await WriteAtomicAsync(metadataPath, new PartialMetadata(link, validator ?? "", total), token);
            Update(id, state => state with { Phase = ModDownloadPhase.Downloading, CompletedBytes = offset, TotalBytes = total }, token);
            using Stream input = await response.Content.ReadAsStreamAsync(token);
            long written = offset;
            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastPublish = 0;
            using (FileStream output = new(partialPath, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 131072, true))
            {
                byte[] buffer = new byte[131072];
                while (true)
                {
                    using CancellationTokenSource inactivity = CancellationTokenSource.CreateLinkedTokenSource(token);
                    inactivity.CancelAfter(TimeSpan.FromSeconds(30));
                    int count = await input.ReadAsync(buffer, inactivity.Token);
                    if (count == 0)
                    {
                        break;
                    }
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    written += count;
                    if (total != null && written > total)
                    {
                        throw new InvalidDataException("The response exceeds its declared length.");
                    }
                    if (stopwatch.ElapsedMilliseconds - lastPublish >= 150)
                    {
                        double speed = (written - offset) / Math.Max(.001, stopwatch.Elapsed.TotalSeconds);
                        Update(id, state => state with { CompletedBytes = written, BytesPerSecond = speed }, token);
                        lastPublish = stopwatch.ElapsedMilliseconds;
                    }
                }
                await output.FlushAsync(token);
            }
            if (written == 0 || total != null && written != total)
            {
                throw new HttpRequestException("The response ended before its declared length.");
            }
            Update(id, state => state with { Phase = ModDownloadPhase.Verifying, CompletedBytes = written, BytesPerSecond = 0 }, token);
            using FileStream check = new(partialPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
            byte[] prefix = new byte[8];
            int prefixLength = await check.ReadAsync(prefix, token);
            if (prefixLength < 4 || !(prefix.AsSpan(0, 4).SequenceEqual("IROS"u8) || prefix.AsSpan(0, 4).SequenceEqual("PK\x03\x04"u8) || prefix.AsSpan(0, 4).SequenceEqual("PK\x05\x06"u8) || prefix.AsSpan(0, 4).SequenceEqual("Rar!"u8) || prefixLength >= 6 && prefix.AsSpan(0, 6).SequenceEqual(new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c })))
            {
                check.Close();
                File.Delete(metadataPath);
                throw new InvalidDataException("The response is not a recognized mod archive.");
            }
            check.Position = 0;
            string digest = Convert.ToHexString(await SHA256.HashDataAsync(check, token)).ToLowerInvariant();
            check.Close();
            token.ThrowIfCancellationRequested();
            string final = Path.Combine(directory, "package.download");
            File.Move(partialPath, final, true);
            Update(id, state => state with { Phase = ModDownloadPhase.ReadyToInstall, ArchivePath = final, Sha256 = digest, Message = "Downloaded. Package inspection is required before installation." }, token);
            return;
        }
        throw new InvalidDataException("The server could not safely restart this download.");
    }

    private void Update(Guid id, Func<DownloadJobSnapshot, DownloadJobSnapshot> update, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        jobs.AddOrUpdate(id, _ => throw new InvalidOperationException(), (_, state) => state.Phase is ModDownloadPhase.Paused or ModDownloadPhase.Cancelled ? state : update(state));
        Interlocked.Increment(ref revision);
    }

    private async Task SaveAsync(Guid id)
    {
        await persistenceGate.WaitAsync();
        try
        {
            Directory.CreateDirectory(JobDirectory(id));
            await WriteAtomicAsync(Path.Combine(JobDirectory(id), "job.json"), jobs[id], CancellationToken.None);
        }
        finally
        {
            persistenceGate.Release();
        }
    }

    private static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken token)
    {
        string temporary = path + ".tmp";
        using (FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(output, value, cancellationToken: token);
            await output.FlushAsync(token);
            output.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    private string JobDirectory(Guid id) => Path.Combine(root, id.ToString("N"));
    private static string DescribeFailure(Exception error)
    {
        string message = error.Message[..Math.Min(error.Message.Length, 4096)];
        message = System.Text.RegularExpressions.Regex.Replace(message, @"https?://[^\s]+", "[source address]", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        return error.GetType().Name + (error is HttpRequestException { StatusCode: { } status } ? $" (HTTP {(int)status})" : "") + ": " + message;
    }
    private static bool IsTransient(Exception error) => error is HttpRequestException request && (request.StatusCode == null || request.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout) || error is OperationCanceledException;
    private void EnsureInitialized()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("Initialize the download queue before issuing commands.");
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(tasks.Values.ToArray());
            }
            catch (Exception)
            {
                // Each worker records its own durable outcome.
            }
            instanceLock?.Dispose();
            if (ownsClient)
            {
                client.Dispose();
            }
        });
    }
}







