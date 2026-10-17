using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private static readonly ConditionalWeakTable<ResourceService, Dictionary<string, NativeActorInfoArenaDefinition>> NativeActorInfoCertificates = new();
    private NativeActorInfoArenaDefinition? nativeActorInfoArena;

    public NativeActorInfoArenaDefinition ReadNativeActorInfoArena()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } profile)
        {
            throw new NotSupportedException("Native actor information requires the authenticated original executable.");
        }
        if (overrides is null && nativeActorInfoArena is not null)
        {
            return nativeActorInfoArena;
        }
        Dictionary<string, NativeActorInfoArenaDefinition>? shared = resources is not null && overrides is null ? NativeActorInfoCertificates.GetValue(resources, _ => new Dictionary<string, NativeActorInfoArenaDefinition>(StringComparer.OrdinalIgnoreCase)) : null;
        string key = GameDirectory + "\n" + profile.ExecutableHash;
        if (shared is not null)
        {
            lock (shared)
            {
                if (shared.TryGetValue(key, out NativeActorInfoArenaDefinition? cached))
                {
                    return nativeActorInfoArena = cached;
                }
            }
        }
        HashSet<string> mapped = fieldNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] candidates = mapped.Concat(outer.Entries.Select(entry => ActorInfoBasename(entry.RelativePath)).Where(name => name.EndsWith(".fl", StringComparison.OrdinalIgnoreCase)).Select(name => name[..^3])).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        int maximum = 0;
        foreach (string name in candidates)
        {
            ResourcePrefix? prefix = ReadActorInfoHeader(name);
            if (prefix is null)
            {
                continue;
            }
            int count;
            try
            {
                if (prefix.Length is not long length)
                {
                    throw new InvalidDataException("Field JSM certification requires the actual resource payload length.");
                }
                count = NativeActorInfoArenaReader.ReadDeclaredRecords(profile, prefix.Bytes, length);
            }
            catch (InvalidDataException) when (!mapped.Contains(name))
            {
                continue;
            }
            maximum = Math.Max(maximum, count);
        }
        using IFlArchive main = archiveSource.OpenArchive("main");
        archiveIdentities[main] = "main";
        NativeActorInfoArenaDefinition result;
        try
        {
            result = new NativeActorInfoArenaDefinition(1, profile.ExecutableHash, maximum, Read(main, "kernel.bin").Length, Read(main, "wm2field.tbl").Length);
            result.Validate();
        }
        finally
        {
            archiveIdentities.Remove(main);
        }
        if (overrides is null)
        {
            nativeActorInfoArena = result;
            if (shared is not null)
            {
                lock (shared)
                {
                    shared[key] = result;
                }
            }
        }
        return result;
    }

    private ResourcePrefix? ReadActorInfoHeader(string name)
    {
        IFlArchiveEntry? listingEntry = outer.Entries.SingleOrDefault(entry => ActorInfoBasename(entry.RelativePath).Equals(name + ".fl", StringComparison.OrdinalIgnoreCase));
        if (listingEntry is null)
        {
            return null;
        }
        byte[] listing = Read(outer, name + ".fl");
        string? logicalPath = FlArchive.PathEncoding.GetString(listing).Replace("\r", string.Empty).Split('\n').Where(path => ActorInfoBasename(path).Equals(name + ".jsm", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
        if (logicalPath is null)
        {
            return null;
        }
        if (logicalPath.StartsWith(FlArchive.InternalPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logicalPath = logicalPath[FlArchive.InternalPathPrefix.Length..];
        }
        if (overrides?.TryRead(logicalPath.Replace('\\', '/').TrimStart('/'), out byte[]? replacement) == true)
        {
            return new ResourcePrefix(replacement.Take(8).ToArray(), replacement.LongLength);
        }
        if (resources is not null)
        {
            ResourcePrefix prefix = resources.ReadPrefix(new ResourceKey("field/" + name + "/" + name + ".jsm"), 8);
            return prefix.Length == 0 ? null : prefix;
        }
        byte[] metrics = Read(outer, name + ".fi");
        IFlArchiveEntry contentEntry = outer.Entries.Single(entry => ActorInfoBasename(entry.RelativePath).Equals(name + ".fs", StringComparison.OrdinalIgnoreCase));
        Stream content;
        long contentLength;
        if (overrides?.TryRead(contentEntry.RelativePath.Replace('\\', '/').TrimStart('/'), out byte[]? contentReplacement) == true)
        {
            content = new MemoryStream(contentReplacement, false);
            contentLength = contentReplacement.LongLength;
        }
        else
        {
            content = contentEntry.OpenForRead();
            contentLength = contentEntry.Size;
        }
        using IFlArchive nested = NativeActorInfoArenaReader.OpenHeaderArchive(listing, metrics, content, contentLength);
        IFlArchiveEntry jsm = nested.Entries.Single(entry => ActorInfoBasename(entry.RelativePath).Equals(name + ".jsm", StringComparison.OrdinalIgnoreCase));
        if (jsm.Size == 0)
        {
            return null;
        }
        byte[] header = new byte[(int)Math.Min(8, jsm.Size)];
        using Stream stream = jsm.OpenForRead();
        stream.ReadExactly(header);
        return new ResourcePrefix(header, jsm.Size);
    }

    private static string ActorInfoBasename(string path)
    {
        return path.Replace('\\', '/').Split('/')[^1];
    }
}
