using System.Buffers.Binary;
using System.Text;

static class MacNativeClosure
{
    internal sealed record Image(string Path, string[] Imports, string[] SearchPaths, uint MinimumVersion);

    public static Image Read(string path)
    {
        using FileStream input = File.OpenRead(path);
        using BinaryReader reader = new(input, Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        long slice = 0;
        if (magic is 0xbebafeca or 0xbfbafeca)
        {
            int count = checked((int)BinaryPrimitives.ReverseEndianness(reader.ReadUInt32()));
            if (count > 64)
            {
                throw new InvalidDataException("Invalid Mach-O slice inventory.");
            }
            bool wide = magic == 0xbfbafeca;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                uint cpu = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());
                _ = reader.ReadUInt32();
                ulong offset = wide ? BinaryPrimitives.ReverseEndianness(reader.ReadUInt64()) : BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());
                _ = wide ? reader.ReadUInt64() : reader.ReadUInt32();
                _ = reader.ReadUInt32();
                if (wide)
                {
                    _ = reader.ReadUInt32();
                }
                if (cpu == 0x01000007)
                {
                    slice = checked((long)offset);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                throw new BadImageFormatException("Mach-O has no Intel x64 slice.");
            }
            input.Position = slice;
            magic = reader.ReadUInt32();
        }
        if (magic != 0xfeedfacf || reader.ReadUInt32() != 0x01000007)
        {
            throw new BadImageFormatException("Expected a little-endian Intel x64 Mach-O slice.");
        }
        input.Position = slice + 16;
        uint commands = reader.ReadUInt32();
        uint commandBytes = reader.ReadUInt32();
        if (commands > 65536 || commandBytes > 16 * 1024 * 1024 || slice + 32 + commandBytes > input.Length)
        {
            throw new InvalidDataException("Invalid Mach-O load commands.");
        }
        input.Position = slice + 32;
        List<string> imports = [];
        List<string> search = [];
        uint minimum = 0;
        for (int i = 0; i < commands; i++)
        {
            long start = input.Position;
            uint command = reader.ReadUInt32();
            uint length = reader.ReadUInt32();
            if (length < 8 || start + length > slice + 32 + commandBytes)
            {
                throw new InvalidDataException("Invalid Mach-O command length.");
            }
            if (command is 0xc or 0x80000018 or 0x8000001f or 0x8000001c)
            {
                uint offset = reader.ReadUInt32();
                if (offset < 12 || offset >= length)
                {
                    throw new InvalidDataException("Invalid Mach-O command string.");
                }
                input.Position = start + offset;
                byte[] bytes = reader.ReadBytes(checked((int)(length - offset)));
                int end = Array.IndexOf(bytes, (byte)0);
                if (end < 0)
                {
                    throw new InvalidDataException("Unterminated Mach-O command string.");
                }
                string value = Encoding.UTF8.GetString(bytes, 0, end);
                (command == 0x8000001c ? search : imports).Add(value);
            }
            else if (command == 0x32)
            {
                if (reader.ReadUInt32() != 1)
                {
                    throw new InvalidDataException("Mach-O target is not desktop macOS.");
                }
                minimum = reader.ReadUInt32();
            }
            else if (command == 0x24)
            {
                minimum = reader.ReadUInt32();
            }
            input.Position = start + length;
        }
        return new Image(path, imports.ToArray(), search.ToArray(), minimum);
    }

    public static void Validate(string bundle)
    {
        string[] paths = Directory.EnumerateFiles(bundle, "*", SearchOption.AllDirectories).Where(path => path.EndsWith(".dylib", StringComparison.Ordinal) || Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music").ToArray();
        foreach (string path in paths)
        {
            Image image = Read(path);
            if (image.MinimumVersion > 0x001a0000)
            {
                throw new InvalidDataException("The bundled native library requires a newer macOS version than the bundle declares: " + path);
            }
            foreach (string search in image.SearchPaths)
            {
                if (!search.StartsWith("@loader_path", StringComparison.Ordinal) && !search.StartsWith("@executable_path", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Non-relocatable Mach-O search path: " + search);
                }
            }
            foreach (string import in image.Imports)
            {
                if (import.StartsWith("/usr/lib/", StringComparison.Ordinal) || import.StartsWith("/System/Library/", StringComparison.Ordinal))
                {
                    continue;
                }
                string directory = Path.GetDirectoryName(path)!;
                string executable = Path.Combine(bundle, "Contents", "MacOS");
                IEnumerable<string> candidates;
                if (import.StartsWith("@loader_path/", StringComparison.Ordinal))
                {
                    candidates = [Path.Combine(directory, import[13..])];
                }
                else if (import.StartsWith("@executable_path/", StringComparison.Ordinal))
                {
                    // Helpers live beside their own native runtime closure.
                    candidates = [Path.Combine(directory, import[17..]), Path.Combine(executable, import[17..])];
                }
                else if (import.StartsWith("@rpath/", StringComparison.Ordinal))
                {
                    string[] rpaths = image.SearchPaths.Length == 0 ? ["@loader_path"] : image.SearchPaths;
                    candidates = rpaths.Select(search => search.Replace("@loader_path", directory, StringComparison.Ordinal).Replace("@executable_path", directory, StringComparison.Ordinal)).Select(search => Path.Combine(search, import[7..]));
                }
                else
                {
                    throw new InvalidDataException("Non-relocatable Mach-O import: " + import);
                }
                if (!candidates.Any(candidate => Path.GetFullPath(candidate).StartsWith(Path.GetFullPath(bundle) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(candidate)))
                {
                    throw new InvalidDataException("Missing bundled native import " + import + " required by " + path);
                }
            }
        }
    }
}
