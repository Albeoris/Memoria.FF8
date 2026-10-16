using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeGardenWorldAllocationReader
{
    public static NativeGardenWorldAllocation Read(ReadOnlySpan<byte> program, ReadOnlySpan<byte> coordinates, WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, byte guardianForce13Flags, NativeWorldPathPosition cactuarLoadPosition, NativeSavedWorldPose loadPose, ushort cameraYaw, byte variant, byte mapDisplay, NativeSavedWorldPose? parkedGardenPose = null)
    {
        entry.Validate();
        saved.Validate();
        memory.Validate();
        if (unchecked((byte)packet.Mode) is not (0 or 48) || unchecked((byte)packet.Mode) == 0 && parkedGardenPose is null || unchecked((sbyte)packet.Extra) != -1 || memory.Read(269, 1).RequireExact() != 0 || memory.Read(270, 1).RequireExact() != 0 || (entry.ReadBytes(264, 1) & 0x10) != 0 || saved.Slot4.ShapeId is not (0 or 255) || saved.Slot5.ShapeId != 0)
        {
            throw new NotSupportedException("Garden allocation requires ordinary48, inactive auxiliary path channels and the actual unoccupied saved car slots.");
        }
        if (program.Length < 8 || program.Length % 4 != 0 || coordinates.Length < 16 || coordinates[..16].IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("Garden allocation lacks the original aligned program and complete zero coordinate0.");
        }
        int tableBytes = BinaryPrimitives.ReadInt32LittleEndian(program);
        if (tableBytes < 8 || tableBytes % 4 != 0 || tableBytes > program.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(program[(tableBytes - 4)..]) != 0)
        {
            throw new InvalidDataException("Garden section9 clause pointer table is invalid.");
        }
        ushort story = entry.StoryWord;
        byte flags265 = checked((byte)entry.ReadBytes(265, 1));
        byte chocoboFlags = checked((byte)memory.Read(1536, 1).RequireExact());
        for (int index = 0; index < tableBytes / 4 - 1; index++)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(program[(index * 4)..]);
            int end = BinaryPrimitives.ReadInt32LittleEndian(program[((index + 1) * 4)..]);
            if (end == 0)
            {
                end = program.Length;
            }
            if (start < tableBytes || start % 4 != 0 || end <= start || end > program.Length)
            {
                throw new InvalidDataException("Garden section9 clause pointer leaves its original resource.");
            }
            ReadOnlySpan<byte> clause = program[start..end];
            if (clause.Length < 16 || Word(clause, 0) != 0xFF01 || Word(clause, 2) != 0 || Word(clause, 4) != 0xFF02 || Word(clause, 8) != 0xFF03 || Word(clause, 12) != 0xFF04 || Word(clause, 14) != 0)
            {
                throw new NotSupportedException("Garden allocation selected an unrepresented conditional story-clause header.");
            }
            ushort minimum = Word(clause, 6);
            ushort maximum = Word(clause, 10);
            if (story < minimum || story >= maximum)
            {
                continue;
            }
            ushort[] expected = [0xFF01, 0, 0xFF02, minimum, 0xFF03, maximum, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF05, 0];
            if (clause.Length != expected.Length * 2)
            {
                throw new NotSupportedException("Garden's reached selected clause has additional objects or branches outside the complete ordinary allocation contract.");
            }
            for (int word = 0; word < expected.Length; word++)
            {
                if (Word(clause, word * 2) != expected[word])
                {
                    throw new NotSupportedException("Garden's complete selected object instructions differ from the admitted ordinary clause.");
                }
            }
            NativeWorldPathPosition body = new(loadPose.X, loadPose.Height, -loadPose.RawZ);
            NativeSavedWorldPose gardenPose = parkedGardenPose ?? loadPose;
            NativeWorldPathPosition gardenPosition = new(gardenPose.X, gardenPose.Height, -gardenPose.RawZ);
            var objects = ImmutableArray.CreateBuilder<NativeGardenWorldObject>();
            objects.Add(new NativeGardenWorldObject(0, 0, body, 0, unchecked((short)loadPose.Yaw), 0));
            objects.Add(new NativeGardenWorldObject(2, 0, default, 0, 0, 0));
            if ((chocoboFlags & 3) == 1)
            {
                objects.Add(new NativeGardenWorldObject(3, 0, default, 0, 0, 0));
            }
            int garden64 = objects.Count;
            objects.Add(new NativeGardenWorldObject(64, 0, gardenPosition, 0, unchecked((short)gardenPose.Yaw), 0));
            objects.Add(new NativeGardenWorldObject(65, 2, gardenPosition, 0, unchecked((short)gardenPose.Yaw), 0));
            if ((guardianForce13Flags & 1) == 0)
            {
                objects.Add(new NativeGardenWorldObject(94, 0, cactuarLoadPosition, 0, 0, 0));
            }
            byte replacements = entry.ReplacementFlags264;
            for (int group = 0; group < 3; group++)
            {
                if ((replacements & (1 << group)) == 0)
                {
                    continue;
                }
                int offset = (31 + group) * 16;
                if (offset > coordinates.Length - 16)
                {
                    throw new InvalidDataException("Native264-controlled shape79/80 pair has no original coordinate row.");
                }
                ReadOnlySpan<byte> raw = coordinates.Slice(offset, 16);
                NativeWorldPathPosition position = new(BinaryPrimitives.ReadInt32LittleEndian(raw), BinaryPrimitives.ReadInt32LittleEndian(raw[4..]), BinaryPrimitives.ReadInt32LittleEndian(raw[8..]));
                int orientation = BinaryPrimitives.ReadInt32LittleEndian(raw[12..]);
                objects.Add(new NativeGardenWorldObject(79, 0, position, 0, unchecked((short)orientation), unchecked((short)(orientation >> 16))));
                objects.Add(new NativeGardenWorldObject(80, 4, position, 0, unchecked((short)orientation), unchecked((short)(orientation >> 16))));
            }
            var allocation = new NativeGardenWorldAllocation(1, NativeGardenWorldAllocation.AllocationRule, Hash(program), Hash(coordinates), Hash(clause), index, minimum, maximum, story, flags265, chocoboFlags, expected.ToImmutableArray(), objects.ToImmutable(), 0, garden64, garden64 + 1, packet, loadPose, gardenPose, cameraYaw, variant, mapDisplay) { ReplacementFlags264 = replacements, GuardianForce13Flags = guardianForce13Flags, CactuarLoadPosition = cactuarLoadPosition };
            allocation.Validate();
            return allocation;
        }
        throw new NotSupportedException("Original section9 has no admitted ordinary Garden allocation for the current story.");
    }

    public static NativeGardenCameraDefaults ReadCameraDefaults(byte[] executable)
    {
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("Garden camera constants require the authenticated original English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        short ReadWord(uint address)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 2 <= section.VirtualAddress + section.SizeOfRawData);
            return BinaryPrimitives.ReadInt16LittleEndian(executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), 2));
        }
        var defaults = new NativeGardenCameraDefaults(hash, ReadWord(0xC76D28), ReadWord(0xC76D40), ReadWord(0xC76D42), ReadWord(0xC76D44));
        defaults.Validate();
        return defaults;
    }

    public static NativeWorldPathPosition ReadCactuarLoadPosition(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new InvalidDataException("Garden Cactuar pose requires the authenticated original English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        long rva = 0xC76630 - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 12 <= section.VirtualAddress + section.SizeOfRawData);
        ReadOnlySpan<byte> bytes = executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), 12);
        return new NativeWorldPathPosition(BinaryPrimitives.ReadInt32LittleEndian(bytes), BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]));
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed partial class WorldAssets
{
    public NativeGardenWorldAllocation ReadGardenAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals)
    {
        int spawn = unchecked((sbyte)packet.EntryIndex);
        NativeSavedWorldPose pose;
        ushort cameraYaw;
        byte variant;
        byte mapDisplay = saved.ReadMapDisplayModeForFreshFootLoader();
        if (spawn == 62)
        {
            pose = saved.GardenPose3 ?? throw new NotSupportedException("Garden entry62 requires its executed native saved pose3; no old car or spawn pose substitutes.");
            cameraYaw = saved.GardenReturn?.CameraYaw96 ?? saved.FootReturn?.CameraYaw96 ?? throw new NotSupportedException("Garden entry62 requires the actual last saved camera WORD96.");
            variant = saved.GardenReturn?.AnimationVariant108 ?? saved.FootReturn?.AnimationVariant108 ?? throw new NotSupportedException("Garden entry62 requires the actual saved animation variant BYTE108.");
        }
        else
        {
            if ((uint)spawn >= Spawns.Count)
            {
                throw new InvalidDataException("The native ordinary Garden spawn is absent.");
            }
            WorldSpawn source = Spawns[spawn];
            pose = new NativeSavedWorldPose(source.X, source.Z, source.NativeHeight, (ushort)((source.Unknown >> 8) * 16));
            cameraYaw = (ushort)((source.Unknown & 255) * 16);
            variant = 0;
        }
        //544860's Garden pose3 reads are dead in this ordinary fresh/reload path:
        //auxiliary channels are proved inactive, indices inspect only shape bytes,
        //then548080/548300 writes foot and both Garden poses before first use.
        vitals.Validate();
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        return NativeGardenWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, pose, cameraYaw, variant, mapDisplay);
    }

    public NativeGardenCameraDefaults ReadGardenCameraDefaults() => NativeGardenWorldAllocationReader.ReadCameraDefaults(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));

    public NativeGardenWorldAllocation ReadParkedGardenAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals)
    {
        if (unchecked((byte)packet.Mode) != 0)
        {
            throw new NotSupportedException("Parked Garden allocation requires the actual normal-foot return packet.");
        }
        NativeSavedWorldPose parked = saved.GardenPose3 ?? throw new NotSupportedException("Foot return requires its actual previously parked Garden pose3.");
        int spawn = unchecked((sbyte)packet.EntryIndex);
        NativeSavedWorldPose foot;
        ushort camera;
        byte variant;
        if (spawn == 62)
        {
            NativeSavedWorldFootReturn receipt = saved.FootReturn ?? throw new NotSupportedException("Foot entry62 requires its executed foot return receipt.");
            receipt.RequireCompleteFootReload();
            foot = receipt.Pose0;
            camera = receipt.CameraYaw96;
            variant = receipt.AnimationVariant108!.Value;
        }
        else
        {
            if ((uint)spawn >= Spawns.Count)
            {
                throw new InvalidDataException("Original parked-Garden foot spawn is absent.");
            }
            WorldSpawn source = Spawns[spawn];
            foot = new NativeSavedWorldPose(source.X, source.Z, source.NativeHeight, (ushort)((source.Unknown >> 8) * 16));
            camera = (ushort)((source.Unknown & 255) * 16);
            variant = 0;
        }
        vitals.Validate();
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        return NativeGardenWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, foot, camera, variant, saved.ReadMapDisplayModeForFreshFootLoader(), parked);
    }
}
