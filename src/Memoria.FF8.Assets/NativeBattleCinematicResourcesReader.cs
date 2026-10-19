using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCinematicResourcesReader
{
    public static NativeBattleCinematicResources ReadIfrit(byte[] executable, ImmutableArray<byte> initialFile0)
    {
        return Read(executable, initialFile0, NativeBattleCinematicProfile.ForEffect(201));
    }

    public static NativeBattleCinematicResources Read(byte[] executable, ImmutableArray<byte> initialFile0, NativeBattleCinematicProfile profile)
    {
        NativeExecutableMemory memory = new(executable);
        if (initialFile0.IsDefault || initialFile0.Length < 0x28)
        {
            throw new InvalidDataException("Native cinematic vibration requires its complete original first resource header.");
        }
        int vibrationStart = BinaryPrimitives.ReadInt32LittleEndian(initialFile0.AsSpan().Slice(0x24));
        if (vibrationStart < 0x28 || vibrationStart > initialFile0.Length - 4)
        {
            throw new InvalidDataException("Native cinematic vibration header leaves its actual resource file.");
        }
        int vibrationEnd = initialFile0.Length;
        for (int offset = 4; offset <= 0x24; offset += 4)
        {
            int next = BinaryPrimitives.ReadInt32LittleEndian(initialFile0.AsSpan().Slice(offset));
            if (next > vibrationStart && next < vibrationEnd)
            {
                vibrationEnd = next;
            }
        }
        int ownedEnd = checked(vibrationEnd + 2);
        if (ownedEnd > initialFile0.Length)
        {
            throw new InvalidDataException("Native cinematic vibration requires its actual adjacent lookahead bytes.");
        }
        ReadOnlySpan<byte> vibrationBytes = initialFile0.AsSpan().Slice(vibrationStart, ownedEnd - vibrationStart);
        int tableBytes = 0;
        for (int offset = 0; offset <= vibrationBytes.Length - 4; offset += 4)
        {
            int relative = BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes[offset..]);
            if (relative == 0)
            {
                continue;
            }
            if (relative < offset + 4 || (relative & 3) != 0 || relative > vibrationBytes.Length - 4)
            {
                throw new InvalidDataException("Native cinematic vibration payload does not bound its physical pointer table.");
            }
            tableBytes = relative;
            break;
        }
        if (tableBytes == 0)
        {
            throw new InvalidDataException("Native cinematic vibration has no retained non-null pattern pointer.");
        }
        ImmutableArray<int>.Builder offsets = ImmutableArray.CreateBuilder<int>(tableBytes / 4);
        for (int offset = 0; offset < tableBytes; offset += 4)
        {
            offsets.Add(BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes[offset..]));
        }
        NativeVibrationBank vibration = new(Convert.ToHexString(SHA256.HashData(initialFile0.AsSpan())), offsets.MoveToImmutable(), vibrationBytes.ToArray().ToImmutableArray());
        vibration.Validate();
        uint[] descriptor = Enumerable.Range(0, 4).Select(index => checked(profile.InitializationAddress + BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.InitializationAddress + (uint)(index * 4), 4)))).ToArray();
        NativeBattleCinematicResources result = new(profile.InitializationAddress, new("cinematic-static", memory.Read(profile.InitializationAddress, profile.StaticLength).ToImmutableArray()), new(profile.TextureCount, profile.InitializationAddress, profile.OwnerDirectoryAddress, descriptor[0], descriptor[1], descriptor[2], descriptor[3]), vibration);
        result.Validate();
        return result;
    }
}
