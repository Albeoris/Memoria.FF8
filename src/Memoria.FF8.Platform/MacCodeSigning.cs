using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Memoria.FF8.Platform;

public static class MacCodeSigning
{
    public const string ResourceEnvelope = "Contents/_CodeSignature/CodeResources";
    public const string Launcher = "Contents/MacOS/Memoria.Launcher";
    public const string UnsignedHashKind = "macos-unsigned-sha256";

    public static bool IsMachO(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> bytes = stackalloc byte[4];
        return stream.Read(bytes) == 4 && BinaryPrimitives.ReadUInt32BigEndian(bytes) is 0xfeedface or 0xcefaedfe or 0xfeedfacf or 0xcffaedfe or 0xcafebabe or 0xbebafeca or 0xcafebabf or 0xbfbafeca;
    }

    public static void Sign(string path)
    {
        Run("--force", "--sign", "-", "--timestamp=none", "--preserve-metadata=entitlements", path);
    }

    public static string[] BundleFiles(string root)
    {
        List<string> files = [];
        void Visit(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Signed application contents cannot be symbolic links: " + path);
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string child in Directory.EnumerateFileSystemEntries(path))
                {
                    Visit(child);
                }
            }
            else
            {
                files.Add(path);
            }
        }
        Visit(root);
        return files.ToArray();
    }

    // Bundle signing changes the launcher's signature, which seals the embedded
    // inventory. Hash its unsigned representation to avoid a circular dependency.
    // Native signature verification remains mandatory for the complete bundle.
    public static (long Size, string Sha256) UnsignedHash(string path)
    {
        RequireMacOS();
        string temporary = Directory.CreateTempSubdirectory("memoria-unsigned-hash-").FullName;
        try
        {
            string copy = Path.Combine(temporary, Path.GetFileName(path));
            File.Copy(path, copy);
            Run("--remove-signature", copy);
            using FileStream input = File.OpenRead(copy);
            return (input.Length, Convert.ToHexStringLower(SHA256.HashData(input)));
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    public static void VerifyBundle(string root)
    {
        RequireMacOS();
        if (!File.Exists(Path.Combine(root, ResourceEnvelope)))
        {
            throw new InvalidDataException("The macOS application has no bundle resource signature.");
        }
        if (OperatingSystem.IsMacOS() && (int)File.GetUnixFileMode(Path.Combine(root, ResourceEnvelope)) != 420)
        {
            throw new InvalidDataException("The macOS bundle resource signature has an invalid file mode.");
        }
        foreach (string file in BundleFiles(root))
        {
            if (IsMachO(file))
            {
                Run("--verify", "--strict", "--verbose=2", file);
            }
        }
        Run("--verify", "--deep", "--strict", "--verbose=2", root);
    }

    private static void RequireMacOS()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("macOS application signing and signature validation require a native macOS host.");
        }
    }

    private static void Run(params string[] arguments)
    {
        RequireMacOS();
        ProcessStartInfo start = new("/usr/bin/codesign") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start macOS codesign.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string standardOutput = output.GetAwaiter().GetResult();
        string standardError = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException($"macOS codesign failed ({arguments[0]}, {arguments[^1]}): {standardOutput}{standardError}");
        }
    }
}
