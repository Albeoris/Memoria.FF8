using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Reflection.PortableExecutable;
using Memoria.FF8.Platform;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length == 3 && args[0] == "--set-mac-bundle-version")
{
    File.WriteAllBytes(args[1], MacBundleVersion.Stamp(File.ReadAllBytes(args[1]), args[2]));
    return;
}

if (args.Length == 3 && args[0] == "--validate-mac-bundle-version")
{
    MacBundleVersion.Validate(File.ReadAllBytes(args[1]), args[2]);
    return;
}

if (args.Length == 2 && args[0] == "--mac-imports")
{
    Console.WriteLine(JsonSerializer.Serialize(MacNativeClosure.Read(args[1])));
    return;
}

if (args.Length == 4 && args[0] == "--set-version")
{
    ArchiveVersion.Set(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && args[0] == "--validate-archive")
{
    PackagedDistribution.ValidateArchive(args[1], args[2], args[3]);
    return;
}

if (args.Length == 3 && args[0] == "--create-tar")
{
    string root = Path.GetFullPath(args[1]);
    using FileStream output = File.Create(args[2]);
    using GZipStream gzip = new(output, CompressionLevel.Optimal);
    using TarWriter writer = new(gzip, TarEntryFormat.Pax);
    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Package creation cannot follow links.");
        }
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        using FileStream tarContent = File.OpenRead(path);
        bool executable = Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music";
        PaxTarEntry entry = new(TarEntryType.RegularFile, relative) { DataStream = tarContent, Mode = (UnixFileMode)(executable ? 493 : 420) };
        writer.WriteEntry(entry);
    }
    return;
}

if (args.Length is 4 or 5 && args[0] == "--graphics-payload")
{
    GraphicsPayload.Validate(Path.GetFullPath(args[1]), args[2], Path.GetFullPath(args[3]), args.Length == 5 ? args[4] : "win-x64");
    return;
}

if (args.Length >= 3 && args[0] == "--native-image")
{
    foreach (string path in args.Skip(2))
    {
        using FileStream stream = File.OpenRead(path);
        NativeImage.RequireX64(stream, args[1]);
        Console.WriteLine($"Native x64 ABI verified: {Path.GetFileName(path)} ({args[1]}).");
    }
    return;
}

if (args.Length is not (1 or 2))
{
    throw new ArgumentException("Supply the published Data directory to validate.");
}

string dataDirectory = Path.GetFullPath(args[0]);
PayloadImages.Validate(dataDirectory, args.Length == 2 ? args[1] : "win-x64");
using DistributionAssemblies assemblies = new(dataDirectory);
Assembly textAssembly = assemblies.LoadFromAssemblyName(new AssemblyName("Albeoris.Games.FF8.TextEncoding"));
Type encodingType = textAssembly.GetType("Albeoris.Games.FF8.TextEncoding.FF8Encoding", throwOnError: true)!;
Encoding? europeanEncoding = null;
PropertyInfo languageProperty = encodingType.GetProperty("Language") ?? throw new InvalidDataException("The published text encoder has no native language contract.");
Type languageType = Nullable.GetUnderlyingType(languageProperty.PropertyType) ?? throw new InvalidDataException("The published native language selection must represent an unset value.");
foreach (string factory in new[] { "CreateEuropean", "CreateRussian", "CreateJapanese" })
{
    MethodInfo factoryMethod = encodingType.GetMethod(factory, [languageProperty.PropertyType]) ?? throw new InvalidDataException("The published text encoder has no language-aware factory: " + factory);
    Encoding encoding = (Encoding)factoryMethod.Invoke(null, [null])!;
    bool rejectedUnsetLanguage = false;
    try
    {
        encoding.GetBytes("Memoria");
    }
    catch (InvalidOperationException)
    {
        rejectedUnsetLanguage = true;
    }
    if (!rejectedUnsetLanguage)
    {
        throw new InvalidDataException("The published text encoder accepted an unset native language.");
    }
    languageProperty.SetValue(encoding, Enum.Parse(languageType, factory == "CreateJapanese" ? "JP" : "EN"));
    if (factory == "CreateEuropean")
    {
        europeanEncoding = encoding;
        if (encoding.GetString(encoding.GetBytes("Memoria")) != "Memoria")
        {
            throw new InvalidDataException("The published European codepage failed a text round trip.");
        }
    }
}

Type charactersType = textAssembly.GetType("Albeoris.Games.FF8.TextEncoding.DefaultJapaneseFieldCharacters", throwOnError: true)!;
object characters = Activator.CreateInstance(charactersType)!;
_ = charactersType.GetMethod("Get", [typeof(string)])!.Invoke(characters, ["start"]);

Assembly menuAssembly = assemblies.LoadFromAssemblyName(new AssemblyName("Albeoris.Games.FF8.MngrpBin"));
Type pairType = menuAssembly.GetType("Albeoris.Games.FF8.MngrpBin.MngrpFilePair", throwOnError: true)!;
Type archiveType = menuAssembly.GetType("Albeoris.Games.FF8.MngrpBin.MngrpArchive", throwOnError: true)!;
byte[] header = new byte[256 * 8];
BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(255 * 8), 1);
BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(255 * 8 + 4), 1);
object files = Activator.CreateInstance(pairType, new byte[] { 42 }, header)!;
object archive = archiveType.GetMethod("Read", [pairType, typeof(Encoding)])!.Invoke(null, [files, europeanEncoding])!;
object roundTrip = archiveType.GetMethod("Write")!.Invoke(archive, null)!;
byte[] content = (byte[])pairType.GetProperty("Content")!.GetValue(roundTrip)!;
if (content.Length == 0 || content[0] != 42)
{
    throw new InvalidDataException("The published menu catalog failed an archive round trip.");
}

Console.WriteLine("Published dependency resources: all codepages, Japanese field characters and menu catalog loaded from shipped product assemblies using the build-host framework. Target native ABI is validated separately; this resource exercise does not execute the target runtime.");

// Product assemblies come from the payload. Framework assemblies use the host's
// trusted runtime closure because another RID's framework can contain native R2R code.
sealed class DistributionAssemblies(string directory) : AssemblyLoadContext(isCollectible: true), IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> HostFramework = GetHostFramework();

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && HostFramework.TryGetValue(assemblyName.Name, out string? frameworkPath))
        {
            return Default.LoadFromAssemblyPath(frameworkPath);
        }
        string path = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }

    private static IReadOnlyDictionary<string, string> GetHostFramework()
    {
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? throw new InvalidOperationException("Cannot locate the build-host framework.");
        string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? throw new InvalidOperationException("The build-host trusted assembly closure is unavailable.");
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return trusted.Split(Path.PathSeparator).Where(path => string.Equals(Path.GetDirectoryName(path), runtimeDirectory, comparison)).DistinctBy(path => Path.GetFileNameWithoutExtension(path)!, StringComparer.OrdinalIgnoreCase).ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, path => path, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Unload();
    }
}

static class GraphicsPayload
{
    public static void Validate(string directory, string backend, string manifestPath, string rid)
    {
        bool vulkan = backend switch
        {
            "Direct3D11" => false,
            "Vulkan" => true,
            "Direct3D12" => true,
            _ => throw new ArgumentException("Graphics backend must be Direct3D11, Direct3D12 or Vulkan.")
        };
        string frameworkPackage = vulkan ? "MonoGame.Framework.Native" : "MonoGame.Framework.WindowsDX";
        using JsonDocument dependencies = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "Memoria.FF8.deps.json")));
        if (!dependencies.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!.EndsWith("/" + rid, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Graphics payload RID does not match the requested target.");
        }
        string[] packages = dependencies.RootElement.GetProperty("libraries").EnumerateObject().Select(property => property.Name).Where(name => name.StartsWith("MonoGame.", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] expectedPackages = vulkan ? [frameworkPackage + "/3.8.5.1", "MonoGame.Runtime." + (rid == "win-x64" ? (backend == "Direct3D12" ? "Windows.DX12" : "Windows.Vulkan") : rid == "linux-x64" ? "Linux.Vulkan" : "Mac.Vulkan") + "/3.8.5.1"] : [frameworkPackage + "/3.8.5.1"];
        if (!packages.Order(StringComparer.Ordinal).SequenceEqual(expectedPackages.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Mixed, missing or unexpected MonoGame packages in {backend} host: {string.Join(", ", packages)}.");
        }
        using (FileStream frameworkStream = File.OpenRead(Path.Combine(directory, "MonoGame.Framework.dll")))
        using (PEReader framework = new(frameworkStream))
        {
            MetadataReader metadata = framework.GetMetadataReader();
            HashSet<string> types = metadata.TypeDefinitions.Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name)).ToHashSet(StringComparer.Ordinal);
            if (!types.Contains(vulkan ? "NativeGamePlatform" : "WinFormsGamePlatform") || types.Contains(vulkan ? "WinFormsGamePlatform" : "NativeGamePlatform"))
            {
                throw new InvalidDataException($"The shipped MonoGame.Framework.dll does not match the {backend} dependency family.");
            }
        }
        string runtime = rid == "win-x64" ? "mgruntime.dll" : rid == "linux-x64" ? "libmgruntime.so" : "libmgruntime.dylib";
        if (vulkan != File.Exists(Path.Combine(directory, runtime)))
        {
            throw new InvalidDataException("The native MonoGame runtime payload does not match the host backend.");
        }
        PayloadImages.Validate(directory, rid);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("sourceNormalization").GetString() != "utf8-lf" || root.GetProperty("compiler").GetProperty("package").GetString() != "dotnet-mgfxc" || root.GetProperty("compiler").GetProperty("version").GetString() != "3.8.5.1" || root.GetProperty("effects").GetArrayLength() != 9)
        {
            throw new InvalidDataException("Shader manifest schema, compiler or complete variant inventory is invalid.");
        }
        (string Assembly, string Resource, string Source)[] shaders =
        [
            ("Memoria.FF8.dll", "Memoria.FF8.Shaders.Triangles.mgfxo", "src/Memoria.FF8.App/Shaders/Triangles.fx"),
            ("Memoria.FF8.dll", "Memoria.FF8.Shaders.FrameDifference.mgfxo", "src/Memoria.FF8.App/Shaders/FrameDifference.fx"),
            ("Memoria.FF8.TitleScene.dll", "Memoria.FF8.TitleScene.TitleScene.mgfxo", "src/Memoria.FF8.TitleScene/Shaders/TitleScene.fx")
        ];
        foreach (var shader in shaders)
        {
            string? sourceHash = null;
            foreach (string profile in new[] { "DirectX_11", "Vulkan", "DirectX_12" })
            {
                JsonElement[] entries = root.GetProperty("effects").EnumerateArray().Where(entry => entry.GetProperty("source").GetString() == shader.Source && entry.GetProperty("profile").GetString() == profile).ToArray();
                if (entries.Length != 1)
                {
                    throw new InvalidDataException($"Shader manifest must contain exactly one {profile} variant of {shader.Source}.");
                }
                JsonElement entry = entries[0];
                string expectedOutput = shader.Source[..^3] + (profile == "Vulkan" ? ".vulkan.mgfxo" : profile == "DirectX_12" ? ".dx12.mgfxo" : ".mgfxo");
                string hash = entry.GetProperty("sourceSha256").GetString()!;
                string outputHash = entry.GetProperty("outputSha256").GetString()!;
                if (entry.GetProperty("output").GetString() != expectedOutput || !IsHash(hash) || !IsHash(outputHash) || (sourceHash is not null && sourceHash != hash))
                {
                    throw new InvalidDataException($"Shader manifest source/output correspondence is invalid: {shader.Source}.");
                }
                sourceHash = hash;
                string candidateSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, "..", shader.Source));
                if (File.Exists(candidateSource) && Sha256(Encoding.UTF8.GetBytes(File.ReadAllText(candidateSource).Replace("\r\n", "\n"))) != hash)
                {
                    throw new InvalidDataException($"Shader manifest source hash is stale: {shader.Source}.");
                }
                if (profile != (backend == "Direct3D12" ? "DirectX_12" : vulkan ? "Vulkan" : "DirectX_11"))
                {
                    continue;
                }
                byte[] payload = ReadResource(Path.Combine(directory, shader.Assembly), shader.Resource);
                if (payload.Length < 10 || !payload.AsSpan(0, 4).SequenceEqual("MGFX"u8) || payload[4] != 11 || payload[5] != (backend == "Direct3D12" ? 2 : vulkan ? 80 : 1) || Sha256(payload) != outputHash)
                {
                    throw new InvalidDataException($"Embedded shader profile or artifact hash does not match {backend}: {shader.Resource}.");
                }
            }
        }
        Console.WriteLine($"Graphics payload verified: {backend}, isolated MonoGame family, x64 native ABI, three embedded shader profiles and manifest hashes.");
    }

    private static bool IsHash(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static byte[] ReadResource(string assemblyPath, string name)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader image = new(stream);
        MetadataReader metadata = image.GetMetadataReader();
        ManifestResource[] resources = metadata.ManifestResources.Select(metadata.GetManifestResource).Where(resource => metadata.GetString(resource.Name) == name).ToArray();
        if (resources.Length != 1 || !resources[0].Implementation.IsNil)
        {
            throw new InvalidDataException($"Expected one embedded shader resource: {name}.");
        }
        int offset = checked((int)resources[0].Offset);
        var content = image.GetSectionData(image.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);
        int length = BinaryPrimitives.ReadInt32LittleEndian(content.GetContent(offset, 4).AsSpan());
        return content.GetContent(checked(offset + 4), length).ToArray();
    }
}

static class PayloadImages
{
    public static void Validate(string directory, string rid)
    {
        Span<byte> magic = stackalloc byte[4];
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Read(magic) != 4)
            {
                continue;
            }
            stream.Position = 0;
            bool pe = magic[0] == 'M' && magic[1] == 'Z';
            bool elf = magic.SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46 });
            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(magic);
            bool mach = signature is 0xfeedfacf or 0xcffaedfe or 0xbebafeca or 0xcafebabe or 0xbfbafeca;
            if (!pe && !elf && !mach)
            {
                continue;
            }
            if (pe)
            {
                using PEReader image = new(stream, PEStreamOptions.LeaveOpen);
                if (image.PEHeaders.CorHeader is not null)
                {
                    if ((image.PEHeaders.CorHeader.Flags & CorFlags.Requires32Bit) != 0)
                    {
                        throw new BadImageFormatException("Required x86 managed image: " + path);
                    }
                    continue;
                }
                stream.Position = 0;
            }
            NativeImage.RequireX64(stream, rid);
        }
    }
}
