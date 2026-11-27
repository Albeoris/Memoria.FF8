using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeRagnarokWorldAllocationReader
{
    public static NativeRagnarokWorldAllocation Read(ReadOnlySpan<byte> program, ReadOnlySpan<byte> coordinates, WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, byte guardianForce13Flags, NativeWorldPathPosition cactuarLoadPosition, NativeSavedWorldPose loadPose, ushort cameraYaw, byte mapDisplay, NativeSavedWorldPose? parkedRagnarokPose2 = null, NativeWorldAuxiliaryRelocationReceipt? auxiliaryEntry = null, NativeSavedWorldFootReturn? battleReload = null)
    {
        byte controlledType = battleReload?.EntityType105 ?? unchecked((byte)packet.Mode);
        entry.Validate();
        memory.Validate();
        saved.Validate();
        if (!NativeWorldEntityTypes.IsSupportedFoot(controlledType) && controlledType != 50 && !NativeCarWorldRuntimeState.IsSupportedEntity(controlledType) || controlledType != 50 && parkedRagnarokPose2 is null || (entry.ReadBytes(264, 1) & 0x10) != 0 || saved.Slot4.ShapeId is not (0 or 255) && !NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId)))
        {
            throw new NotSupportedException("Ragnarok allocation requires its ordinary packet and represented active saved vehicle branch.");
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
        NativeWorldAllocationClause clause = NativeWorldAllocationClauseReader.Read(program, story);
        ReadOnlySpan<ushort> records = NativeGardenWorldAllocation.ReadObjectInstructions(clause.Words, clause.MinimumStory, clause.MaximumStory, mounted: true);
        if (!clause.ContainsShape(1) || !clause.ContainsShape(2) || NativeWorldEntityTypes.IsSupportedFoot(controlledType) && clause.HasRequestedFootObject(controlledType) != true)
        {
            throw new NotSupportedException("The selected world allocation has no Ragnarok, mount or requested controlled rider.");
        }
        NativeWorldPathPosition body = new(loadPose.X, loadPose.Height, -loadPose.RawZ);
        var objects = ImmutableArray.CreateBuilder<NativeRagnarokWorldObject>();
        byte chocobo = checked((byte)memory.Read(1536, 1).RequireExact());
        int ragnarokIndex = -1;
        for (int index = 0; index < records.Length; index += 2)
        {
            ushort operand = records[index + 1];
            byte shape = unchecked((byte)operand);
            switch (shape)
            {
                case 3 when (chocobo & 3) != 1:
                    break;
                case 0 or 4 or 5 or 6 or 3:
                    objects.Add(new(shape, 0, body, 0, unchecked((short)loadPose.Yaw), 0));
                    break;
                case 2:
                    objects.Add(new(shape, 0, default, 0, 0, 0));
                    break;
                case 1:
                    ragnarokIndex = objects.Count;
                    NativeSavedWorldPose ship = parkedRagnarokPose2 ?? loadPose;
                    objects.Add(new(shape, 0, new(ship.X, ship.Height, -ship.RawZ), 0, unchecked((short)ship.Yaw), 0));
                    break;
                case 64 or 65:
                    NativeSavedWorldPose garden = saved.GardenPose3 ?? throw new NotSupportedException("The selected world allocation needs the parked Garden pose3.");
                    objects.Add(new(shape, shape == 65 ? (byte)2 : (byte)0, new(garden.X, garden.Height, -garden.RawZ), 0, unchecked((short)garden.Yaw), 0));
                    break;
                default:
                    AddCoordinate(objects, coordinates, shape, operand >> 8, shape == 80 ? (byte)4 : (byte)0);
                    break;
            }
        }
        NativeSavedWorldVehicleSlot? savedCarSlot = null;
        if (!clause.ContainsShape(92) && NativeCarWorldRuntimeState.IsSupportedEntity(NativeTrainWorldObject.ClassifyShape(saved.Slot4.ShapeId)))
        {
            savedCarSlot = saved.Slot4;
            NativeSavedWorldPose car = NativeCarWorldRuntimeState.IsSupportedEntity(controlledType) ? loadPose : savedCarSlot.Pose;
            if (NativeCarWorldRuntimeState.IsSupportedEntity(controlledType) && savedCarSlot.ShapeId != NativeCarWorldRuntimeState.ShapeForEntity(controlledType))
            {
                throw new InvalidDataException("Ragnarok-allocation car loader lost its selected saved shape.");
            }
            objects.Add(new(savedCarSlot.ShapeId, 0, new(car.X, car.Height, -car.RawZ), 0, unchecked((short)car.Yaw), 0));
        }
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
                if (index == 0 || objects[index].ShapeId == 3 || objects[index].ShapeId == 1 && controlledType == 50 || NativeCarWorldRuntimeState.IsSupportedEntity(controlledType) && objects[index].ShapeId == savedCarSlot?.ShapeId)
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
        var result = new NativeRagnarokWorldAllocation(Hash(program), Hash(coordinates), Hash(program.Slice(clause.Offset, clause.Length)), story, chocobo, entry.ReplacementFlags264, guardianForce13Flags, clause.Words, objects.ToImmutable(), ragnarokIndex, packet, loadPose, cameraYaw, mapDisplay) { ParkedRagnarokPose2 = parkedRagnarokPose2, AuxiliaryEntry = auxiliaryEntry, BattleReload = battleReload, SavedCarSlot = savedCarSlot };
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

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public sealed partial class WorldAssets
{
    public NativeRagnarokWorldAllocation ReadRagnarokCarAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals, NativeSavedWorldPose loadPose, ushort cameraYaw, byte mapDisplay)
    {
        if (!NativeCarWorldRuntimeState.IsSupportedEntity(unchecked((byte)packet.Mode)))
        {
            throw new InvalidDataException("Ragnarok car allocation requires its actual original car packet.");
        }
        vitals.Validate();
        NativeSavedWorldPose ship = saved.RagnarokPose2 ?? throw new NotSupportedException("Car allocation requires the actual separately saved Ragnarok pose2.");
        NativeWorldAuxiliaryRelocationReceipt? auxiliary = ReadWorldAuxiliaryRelocations(packet, memory, loadPose);
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(NativeWorldMemory);
        return NativeRagnarokWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, loadPose, cameraYaw, mapDisplay, parkedRagnarokPose2: ship, auxiliaryEntry: auxiliary);
    }

    public void ValidateRagnarokAllocationResources(NativeRagnarokWorldAllocation allocation)
    {
        allocation.Validate();
        string programHash = Convert.ToHexString(SHA256.HashData(wmset.AsSpan(sections[9], sections[10] - sections[9])));
        string coordinateHash = Convert.ToHexString(SHA256.HashData(wmset.AsSpan(sections[10], sections[11] - sections[10])));
        NativeWorldAllocationClause clause = ReadWorldAllocationClause(allocation.Story);
        if (allocation.ProgramHash != programHash || allocation.CoordinateHash != coordinateHash || !allocation.ClauseWords.SequenceEqual(clause.Words))
        {
            throw new InvalidDataException("Retained Ragnarok allocation differs from its actual current native resources.");
        }
        NativeRagnarokWorldObject Coordinate(byte shape, byte flags, int row)
        {
            ReadOnlySpan<byte> bytes = wmset.AsSpan(sections[10] + row * 16, 16);
            int orientation = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
            return new(shape, flags, new(BinaryPrimitives.ReadInt32LittleEndian(bytes), BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[8..])), 0, unchecked((short)orientation), unchecked((short)(orientation >> 16)));
        }
        int denseIndex = 0;
        ReadOnlySpan<ushort> records = NativeGardenWorldAllocation.ReadObjectInstructions(clause.Words, clause.MinimumStory, clause.MaximumStory, mounted: true);
        for (int index = 0; index < records.Length; index += 2)
        {
            ushort operand = records[index + 1];
            byte shape = unchecked((byte)operand);
            if (shape == 3 && allocation.CompanionIndex < 0)
            {
                continue;
            }
            if (shape > 65 && allocation.Objects[denseIndex] != Coordinate(shape, shape == 80 ? (byte)4 : (byte)0, operand >> 8))
            {
                throw new InvalidDataException("Retained Ragnarok fixed object differs from its original section10 coordinate.");
            }
            denseIndex++;
        }
        int objectIndex = allocation.Objects.Length;
        for (int group = 2; group >= 0; group--)
        {
            if ((allocation.ReplacementFlags264 & (1 << group)) != 0)
            {
                objectIndex -= 2;
                if (allocation.Objects[objectIndex] != Coordinate(79, 0, 31 + group) || allocation.Objects[objectIndex + 1] != Coordinate(80, 4, 31 + group))
                {
                    throw new InvalidDataException("Retained Ragnarok auxiliary objects differ from their actual original section10 coordinates.");
                }
            }
        }
    }

    public NativeRagnarokWorldAllocation ReadRagnarokAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals, NativeSavedWorldPose? savedRagnarokPose2 = null, ushort? savedCameraYaw96 = null, byte? savedVariant108 = null, NativeWorldAuxiliaryRelocationReceipt? retainedAuxiliary = null)
    {
        int spawn = unchecked((sbyte)packet.EntryIndex);
        NativeSavedWorldPose pose;
        ushort cameraYaw;
        if (spawn == 62)
        {
            pose = savedRagnarokPose2 ?? throw new NotSupportedException("Ragnarok entry62 requires genuine pose2/+24; no car or Garden saved pose substitutes.");
            cameraYaw = savedCameraYaw96 ?? throw new NotSupportedException("Ragnarok entry62 requires the actual saved camera WORD96.");
            if (savedVariant108 is null or > 1)
            {
                throw new NotSupportedException("Ragnarok entry62 requires the actual saved variant108 and its supported ordinary camera/motor.");
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
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(NativeWorldMemory);
        return NativeRagnarokWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, pose, cameraYaw, saved.ReadMapDisplayModeForFreshFootLoader(), auxiliaryEntry: auxiliaryEntry);
    }
}
