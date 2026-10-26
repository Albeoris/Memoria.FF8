using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Reflection.PortableExecutable;
using Memoria.FF8.Platform;

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

if (args.Length != 1)
{
    throw new ArgumentException("Supply the published Data directory to validate.");
}

string dataDirectory = Path.GetFullPath(args[0]);
foreach (string path in Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories).Where(path => Path.GetExtension(path) is ".exe" or ".dll"))
{
    using FileStream stream = File.OpenRead(path);
    using (PEReader image = new(stream, PEStreamOptions.LeaveOpen))
    {
        if (image.PEHeaders.CorHeader is not null)
        {
            if ((image.PEHeaders.CorHeader.Flags & CorFlags.Requires32Bit) != 0)
            {
                throw new BadImageFormatException($"The x64 distribution contains a required x86 managed image: {Path.GetFileName(path)}.");
            }
            continue;
        }
    }
    stream.Position = 0;
    NativeImage.RequireX64(stream, "win-x64");
}
using DistributionAssemblies assemblies = new(dataDirectory);
Assembly textAssembly = assemblies.LoadFromAssemblyName(new AssemblyName("Albeoris.Games.FF8.TextEncoding"));
Type encodingType = textAssembly.GetType("Albeoris.Games.FF8.TextEncoding.FF8Encoding", throwOnError: true)!;
Encoding? europeanEncoding = null;
foreach (string factory in new[] { "CreateEuropean", "CreateRussian", "CreateJapanese" })
{
    Encoding encoding = (Encoding)encodingType.GetMethod(factory, Type.EmptyTypes)!.Invoke(null, null)!;
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

Console.WriteLine("Published dependency resources: all codepages, Japanese field characters and menu catalog loaded successfully.");

// No project references: resolve only the assemblies actually shipped in Data.
sealed class DistributionAssemblies(string directory) : AssemblyLoadContext(isCollectible: true), IDisposable
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string path = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }

    public void Dispose()
    {
        Unload();
    }
}
