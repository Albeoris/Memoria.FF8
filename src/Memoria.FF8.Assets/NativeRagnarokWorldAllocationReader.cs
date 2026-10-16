using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeRagnarokWorldAllocationReader
{
    public static NativeRagnarokWorldAllocation Read(ReadOnlySpan<byte> program, ReadOnlySpan<byte> coordinates, WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, byte guardianForce13Flags, NativeWorldPathPosition cactuarLoadPosition, NativeSavedWorldPose loadPose, ushort cameraYaw, byte mapDisplay, NativeSavedWorldPose? parkedRagnarokPose2 = null, NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = null)
    {
        entry.Validate();
        memory.Validate();
        saved.Validate();
        if (unchecked((byte)packet.Mode) is not (0 or 50) || unchecked((byte)packet.Mode) == 0 && parkedRagnarokPose2 is null || unchecked((byte)packet.Mode) == 50 && unchecked((sbyte)packet.Extra) != -1 || (entry.ReadBytes(264, 1) & 0x10) != 0 || saved.Slot4.ShapeId is not (0 or 255) || saved.Slot5.ShapeId != 0)
        {
            throw new NotSupportedException("Ragnarok allocation requires ordinary controlled50 and the original unoccupied-car allocation branch.");
        }
        if (memory.Read(269, 1).RequireExact() != 0 || memory.Read(270, 1).RequireExact() != 0)
        {
            if (auxiliaryEntry is null)
            {
                throw new NotSupportedException("Ragnarok allocation requires its actual54D6A0 relocation receipt for pending event269/270.");
            }
            _ = auxiliaryEntry.ApplyTo(memory);
        }
        auxiliaryEntry?.RequireRagnarokPublication(packet, loadPose);
        if (program.Length < 8 || program.Length % 4 != 0 || coordinates.Length < 16 || coordinates[..16].IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("Ragnarok allocation lacks its aligned original program or zero coordinate row.");
        }
        ushort story = entry.StoryWord;
        if (story is < 3100 or >= 3900)
        {
            throw new NotSupportedException("Ragnarok allocation currently represents the complete original3100..3899 clause14 only.");
        }
        int tableBytes = BinaryPrimitives.ReadInt32LittleEndian(program);
        if (tableBytes < 64 || tableBytes % 4 != 0 || tableBytes > program.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(program[(tableBytes - 4)..]) != 0)
        {
            throw new InvalidDataException("Ragnarok section9 clause pointer table is invalid.");
        }
        // Earlier clauses have disjoint original story windows. Validate every
        // preceding header rather than silently jumping over changed predicates.
        int selectedStart = -1;
        int selectedEnd = -1;
        for (int index = 0; index <= 14; index++)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(program[(index * 4)..]);
            int end = BinaryPrimitives.ReadInt32LittleEndian(program[((index + 1) * 4)..]);
            if (start < tableBytes || start % 4 != 0 || end <= start || end > program.Length || end - start < 16)
            {
                throw new InvalidDataException("Ragnarok allocation clause pointer leaves its resource.");
            }
            ReadOnlySpan<byte> clause = program[start..end];
            if (Word(clause, 0) != 0xFF01 || Word(clause, 2) != 0 || Word(clause, 4) != 0xFF02 || Word(clause, 8) != 0xFF03 || Word(clause, 12) != 0xFF04 || Word(clause, 14) != 0)
            {
                throw new NotSupportedException("Ragnarok preceding original story-clause header differs from its authenticated selection contract.");
            }
            if (story >= Word(clause, 6) && story < Word(clause, 10))
            {
                if (index != 14)
                {
                    throw new NotSupportedException("An earlier section9 clause precedes the expected Ragnarok allocation.");
                }
                selectedStart = start;
                selectedEnd = end;
                break;
            }
        }
        if (selectedStart < 0)
        {
            throw new NotSupportedException("Original section9 has no reached Ragnarok clause for the current story.");
        }
        ReadOnlySpan<byte> selected = program[selectedStart..selectedEnd];
        ushort[] expected = [0xFF01, 0, 0xFF02, 3100, 0xFF03, 3900, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 1, 0xFF13, 64, 0xFF13, 65, 0xFF13, 7514, 0xFF13, 7515, 0xFF05, 0];
        if (selected.Length != expected.Length * 2)
        {
            throw new NotSupportedException("Ragnarok selected clause includes unrepresented original object commands.");
        }
        for (int index = 0; index < expected.Length; index++)
        {
            if (Word(selected, index * 2) != expected[index])
            {
                throw new NotSupportedException("Ragnarok selected clause differs from the complete original object commands.");
            }
        }
        NativeSavedWorldPose garden = saved.GardenPose3 ?? throw new NotSupportedException("Ragnarok allocation requires the actual saved Garden pose3; controlled50 cannot relocate the parked Garden.");
        NativeWorldPathPosition body = new(loadPose.X, loadPose.Height, -loadPose.RawZ);
        var objects = ImmutableArray.CreateBuilder<NativeRagnarokWorldObject>();
        byte chocobo = checked((byte)memory.Read(1536, 1).RequireExact());
        objects.Add(new(0, 0, body, 0, unchecked((short)loadPose.Yaw), 0));
        objects.Add(new(2, 0, default, 0, 0, 0));
        if ((chocobo & 3) == 1)
        {
            objects.Add(new(3, 0, body, 0, unchecked((short)loadPose.Yaw), 0));
        }
        int ragnarokIndex = objects.Count;
        NativeSavedWorldPose ship = parkedRagnarokPose2 ?? loadPose;
        objects.Add(new(1, 0, new(ship.X, ship.Height, -ship.RawZ), 0, unchecked((short)ship.Yaw), 0));
        NativeWorldPathPosition gardenPosition = new(garden.X, garden.Height, -garden.RawZ);
        objects.Add(new(64, 0, gardenPosition, 0, unchecked((short)garden.Yaw), 0));
        objects.Add(new(65, 2, gardenPosition, 0, unchecked((short)garden.Yaw), 0));
        AddCoordinate(objects, coordinates, 90, 29, 0);
        AddCoordinate(objects, coordinates, 91, 29, 0);
        if ((guardianForce13Flags & 1) == 0)
        {
            objects.Add(new(94, 0, cactuarLoadPosition, 0, 0, 0));
        }
        if (auxiliaryEntry is not null)
        {
            ImmutableArray<NativeRagnarokWorldObject> relocated = auxiliaryEntry.RelocateRagnarokObjects(objects.ToImmutable());
            objects.Clear();
            objects.AddRange(relocated);
            //54D6A0 precedes548080/548300. Foot and conditional chocobo always
            //receive the final load pose; controlled50 also republishes shape1.
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index].ShapeId is 0 or 3 || objects[index].ShapeId == 1 && unchecked((byte)packet.Mode) == 50)
                {
                    objects[index] = objects[index] with { Position = body, Pitch = 0, Yaw = unchecked((short)loadPose.Yaw), Roll = 0 };
                }
            }
        }
        for (int group = 0; group < 3; group++)
        {
            if ((entry.ReplacementFlags264 & (1 << group)) != 0)
            {
                AddCoordinate(objects, coordinates, 79, 31 + group, 0);
                AddCoordinate(objects, coordinates, 80, 31 + group, 4);
            }
        }
        var result = new NativeRagnarokWorldAllocation(Hash(program), Hash(coordinates), Hash(selected), story, chocobo, entry.ReplacementFlags264, guardianForce13Flags, expected.ToImmutableArray(), objects.ToImmutable(), ragnarokIndex, packet, loadPose, cameraYaw, mapDisplay) { ParkedRagnarokPose2 = parkedRagnarokPose2, AuxiliaryEntry = auxiliaryEntry };
        result.Validate();
        return result;
    }

    private static void AddCoordinate(ImmutableArray<NativeRagnarokWorldObject>.Builder objects, ReadOnlySpan<byte> coordinates, byte shape, int row, byte flags)
    {
        if (row * 16 > coordinates.Length - 16)
        {
            throw new InvalidDataException("Ragnarok allocated object has no original section10 coordinate row.");
        }
        ReadOnlySpan<byte> bytes = coordinates.Slice(row * 16, 16);
        var position = new NativeWorldPathPosition(BinaryPrimitives.ReadInt32LittleEndian(bytes), BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]));
        int orientation = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
        objects.Add(new(shape, flags, position, 0, unchecked((short)orientation), unchecked((short)(orientation >> 16))));
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public sealed partial class WorldAssets
{
    public NativeRagnarokWorldAllocation ReadRagnarokAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals, NativeSavedWorldPose? savedRagnarokPose2 = null, ushort? savedCameraYaw96 = null, byte? savedVariant108 = null, NativeWorldAuxiliaryRelocationReceipt? retainedAuxiliary = null)
    {
        int spawn = unchecked((sbyte)packet.EntryIndex);
        NativeSavedWorldPose pose;
        ushort cameraYaw;
        if (spawn == 62)
        {
            pose = savedRagnarokPose2 ?? throw new NotSupportedException("Ragnarok entry62 requires genuine pose2/+24; no car or Garden saved pose substitutes.");
            cameraYaw = savedCameraYaw96 ?? throw new NotSupportedException("Ragnarok entry62 requires the actual saved camera WORD96.");
            if (savedVariant108 != 0)
            {
                throw new NotSupportedException("Ragnarok entry62 requires the actual saved variant108 and its supported ordinary variant0 camera/motor.");
            }
        }
        else
        {
            if ((uint)spawn >= Spawns.Count)
            {
                throw new InvalidDataException("Ragnarok ordinary spawn is absent from the original resource.");
            }
            WorldSpawn source = Spawns[spawn];
            pose = new(source.X, source.Z, source.NativeHeight, (ushort)((source.Unknown >> 8) * 16));
            cameraYaw = (ushort)((source.Unknown & 255) * 16);
        }
        vitals.Validate();
        if (retainedAuxiliary is not null)
        {
            if (memory.Read(269, 1).RequireExact() != 0 || memory.Read(270, 1).RequireExact() != 0)
            {
                throw new InvalidDataException("Restored Ragnarok cannot consume pending auxiliary relocation selectors again.");
            }
            ValidateWorldAuxiliaryRelocations(retainedAuxiliary);
        }
        NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = retainedAuxiliary ?? ReadWorldAuxiliaryRelocations(packet, memory, pose);
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        return NativeRagnarokWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, pose, cameraYaw, saved.ReadMapDisplayModeForFreshFootLoader(), auxiliaryEntry: auxiliaryEntry);
    }
}
