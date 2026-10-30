using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static partial class NativePcSaveConverter
{
    private static NativeSavedWorldState ReadWorldExact(byte[] block, NativeSavedWorldState defaults, bool active)
    {
        NativeSavedWorldPose Pose(int offset)
        {
            return new(unchecked((int)ReadDWord(block, World + offset)), unchecked((int)ReadDWord(block, World + offset + 4)), unchecked((short)ReadWord(block, World + offset + 8)), NativeSavedWorldAngleWords.Normalize(ReadWord(block, World + offset + 10)));
        }
        byte entity = block[World + 105];
        NativeSavedWorldState result = defaults with
        {
            Origin = new NativeSavedWorldOrigin(NativeSavedWorldState.CurrentProofVersion, NativeSavedWorldOriginKind.OriginalSave, NativeSavedWorldState.OriginalSaveInitializationRule),
            InitialMapDisplayMode110 = block[World + 110] == 3 ? (byte)0 : block[World + 110],
            Slot4 = new NativeSavedWorldVehicleSlot(block[World + 98], Pose(48)),
            Slot5 = new NativeSavedWorldVehicleSlot(block[World + 99], Pose(60)),
            GardenPose3 = Pose(36),
            RagnarokPose2 = Pose(24),
            FuelDistance112 = ReadDWord(block, World + 112),
            WorldEventFlags116 = new NativeWorldEventFlags(BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(World + 116)), ulong.MaxValue),
            WorldEventVariable124 = block[World + 124],
            WorldEventVariable125 = block[World + 125],
            TerrainFlags109 = NativeWorldTerrainFlags109.Exact(block[World + 109]),
            EntryMode104 = block[World + 104],
            OriginalAngleWords = new(Enumerable.Range(0, 8).Select(slot => ReadWord(block, World + 12 * slot + 10)).ToImmutableArray(), ReadWord(block, World + 96)),
            FootReturn = null,
            CarReturn = null,
            ScriptedReturn = null,
            GardenReturn = null,
            RagnarokReturn = null,
            UnsupportedEntry = null
        };
        ushort lastField = ReadWord(block, Event + 84);
        ushort story = ReadWord(block, Event + 256);
        ushort camera = NativeSavedWorldAngleWords.Normalize(ReadWord(block, World + 96));
        byte map = block[World + 110] == 3 ? (byte)0 : block[World + 110];
        short location = active ? unchecked((short)ReadWord(block, 388)) : (short)0;
        switch (entity)
        {
            case 0:
            case 1:
            case 2:
                return result with { FootReturn = new NativeSavedWorldFootReturn { ProofVersion = 1, Rule = NativeSavedWorldFootReturn.OriginalSaveRule, SourceWorldFrame = 0, LastFieldId = lastField, StoryWord = story, WorldMode = 0, Pose0 = Pose(0), CameraYaw96 = camera, EntityType105 = entity, Channel106 = 255, Channel107 = 255, AnimationVariant108 = block[World + 108], MapDisplayMode110 = map, MapLocationId = location } };
            case 32:
            case 33:
            case 35:
                return result with { CarReturn = new NativeSavedWorldCarReturn { Version = 2, Rule = NativeSavedWorldCarReturn.OriginalSaveRule, SourceWorldFrame = 0, LastFieldId = lastField, StoryWord = story, Pose = Pose(48), CameraYaw96 = camera, EntityType105 = entity, Channel106 = 255, Channel107 = 255, AnimationVariant108 = block[World + 108], MapDisplayMode110 = map, MapLocationId = location, RetireSelectedVehicle = result.Slot4.ShapeId == 255 } };
            case 48:
                return result with { GardenReturn = new NativeSavedWorldGardenReturn(1, NativeSavedWorldGardenReturn.OriginalSaveRule, 0, lastField, story, Pose(36), camera, entity, 255, 255, block[World + 108], map, location) };
            case 50:
                return result with { RagnarokReturn = new NativeSavedWorldRagnarokReturn(NativeSavedWorldRagnarokReturn.OriginalSaveRule, 0, lastField, story, Pose(24), camera, map, location) { AnimationVariant108 = block[World + 108] } };
            default:
                if (active)
                {
                    throw new NotSupportedException($"Original FF8 saved world entity {entity} requires its native transport loader before import.");
                }
                ImmutableArray<NativeSavedWorldPose> rawPoses = Enumerable.Range(0, 8).Select(slot => new NativeSavedWorldPose(unchecked((int)ReadDWord(block, World + 12 * slot)), unchecked((int)ReadDWord(block, World + 12 * slot + 4)), unchecked((short)ReadWord(block, World + 12 * slot + 8)), ReadWord(block, World + 12 * slot + 10))).ToImmutableArray();
                return result with { UnsupportedEntry = new(rawPoses, ReadWord(block, World + 96), block[World + 104], entity, block[World + 106], block[World + 107], block[World + 108], block[World + 110]) };
        }
    }
}
