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
        NativeVibrationBank? vibration = ReadVibration(initialFile0);
        return ReadStatic(memory, profile, vibration);
    }

    private static NativeVibrationBank? ReadVibration(ImmutableArray<byte> initialFile0)
    {
        int vibrationStart = BinaryPrimitives.ReadInt32LittleEndian(initialFile0.AsSpan().Slice(0x24));
        if (vibrationStart == 0)
        {
            return null;
        }
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
        int requestedEnd = checked(vibrationEnd + 2);
        int ownedEnd = Math.Min(requestedEnd, initialFile0.Length);
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
        if (ownedEnd < requestedEnd)
        {
            foreach (int offset in vibration.Offsets.Where(offset => offset != 0))
            {
                int firstLength = vibration.ReadWord(offset);
                RequireReachableLookahead(offset + 4, firstLength);
                RequireReachableLookahead(offset + 4 + firstLength, vibration.ReadWord(offset + 2));
            }
        }
        return vibration;

        void RequireReachableLookahead(int start, int length)
        {
            for (int cursor = 0; cursor < length; cursor += 2)
            {
                int duration = checked(start + cursor + 1);
                if (duration >= vibration.Bytes.Length)
                {
                    throw new InvalidDataException("Native cinematic vibration requires its actual following duration byte.");
                }
                if (vibration.Bytes[duration] == 255)
                {
                    return;
                }
                if (duration + 1 >= vibration.Bytes.Length)
                {
                    throw new InvalidDataException("Native cinematic vibration requires its actual adjacent interpolation byte.");
                }
            }
        }
    }

    private static NativeBattleCinematicResources ReadStatic(NativeExecutableMemory memory, NativeBattleCinematicProfile profile, NativeVibrationBank? vibration)
    {
        uint[] descriptor = Enumerable.Range(0, 4).Select(index => checked(profile.InitializationAddress + BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(profile.InitializationAddress + (uint)(index * 4), 4)))).ToArray();
        NativeBattleCinematicResources result = new(profile.InitializationAddress, new("cinematic-static", memory.Read(profile.InitializationAddress, profile.StaticLength).ToImmutableArray()), new(profile.TextureCount, profile.InitializationAddress, profile.OwnerDirectoryAddress, descriptor[0], descriptor[1], descriptor[2], descriptor[3]), vibration);
        uint actorDirectory = profile.Effect switch
        {
            228 => 0x18821A0,
            261 => 0x187C20C,
            262 => 0x187A2A4,
            263 => 0x1877DAC,
            _ => 0
        };
        if (actorDirectory != 0)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(actorDirectory, 4));
            if (count is < 2 or > 3)
            {
                throw new InvalidDataException("Native cinematic actor resource has an invalid original directory.");
            }
            int length = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(actorDirectory + 4u + 4u * (uint)count), 4));
            ImmutableArray<byte> bytes = memory.Read(actorDirectory, length).ToImmutableArray();
            _ = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
            result = result with { AdditionalStaticData = result.AdditionalStaticData.Add(actorDirectory, bytes) };
        }
        result.Validate();
        return result;
    }
}
