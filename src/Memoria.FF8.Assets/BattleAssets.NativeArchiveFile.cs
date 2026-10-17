using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleArchiveFile ReadNativeBattleFile(ushort fileId)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        uint nameAddress = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0xB84CCC + (uint)fileId * 4), 4).AsSpan());
        byte[] nameBytes = NativeBattleEffectStaticResourcesReader.Read(executable, nameAddress, 128).ToArray();
        int end = Array.IndexOf(nameBytes, (byte)0);
        if (end <= 0)
        {
            throw new InvalidDataException("Native battle file-name table lacks a bounded original name.");
        }
        string name = Encoding.ASCII.GetString(nameBytes, 0, end).ToLowerInvariant();
        return new(fileId, name, ImmutableArray.CreateRange(Read(name)));
    }
}
