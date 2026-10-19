using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCinematicFilesReader
{
    public static ImmutableArray<NativeBattleArchiveFile> ReadIfrit(byte[] executable, Func<string, ImmutableArray<byte>> readBattleFile)
    {
        return Read(executable, NativeBattleCinematicProfile.ForEffect(201), readBattleFile);
    }

    public static ImmutableArray<NativeBattleArchiveFile> Read(byte[] executable, NativeBattleCinematicProfile profile, Func<string, ImmutableArray<byte>> readBattleFile)
    {
        ArgumentNullException.ThrowIfNull(readBattleFile);
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleArchiveFile>.Builder files = ImmutableArray.CreateBuilder<NativeBattleArchiveFile>();
        foreach (ushort id in Enumerable.Range(profile.SourceBase, profile.LastSource - profile.SourceBase + 1).Concat(Enumerable.Range(0x16B, 3)).Distinct().Select(id => checked((ushort)id)))
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * id), 4));
            byte[] nameBytes = memory.Read(address, 128);
            int end = Array.IndexOf(nameBytes, (byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native cinematic file directory lacks its complete original name.");
            }
            string name = Encoding.ASCII.GetString(nameBytes, 0, end).ToLowerInvariant();
            ImmutableArray<byte> bytes = readBattleFile(name);
            if (bytes.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native cinematic file input requires its complete original archive bytes.");
            }
            files.Add(new(id, name, bytes));
        }
        return files.ToImmutable();
    }
}
