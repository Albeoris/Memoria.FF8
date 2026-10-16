using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public delegate int WorldGardenContactQuery(IReadOnlyList<WorldNativeGardenProbe> probes, int selectedEntity, int padding, bool forceCircle);
public sealed record WorldGardenPlacementResult(bool Accepted, NativeWorldPathPosition? Position, int LastContactObjectIndex, int Proposals, NativeWorldGroundIdentity? CenterGround);

public sealed partial class WorldAssets
{
    public WorldGardenPlacementResult ResolveNativeGardenPlacement(NativeWorldPathPosition body, short bodyYaw, int sourceEntity, int destinationEntity, short distance, int currentGroundHeight, uint currentPolygonFlags, NativeWorldGroundCacheState cache, IReadOnlyDictionary<int, int> replacements, WorldGardenContactQuery contact, bool collisionEnabled = true)
    {
        // NG-004: Garden's caller supplies
        // its retained2043D90 destination, correcting the original undefined
        // [ebp-8] validator read without substituting a fixed foot entity.
        ArgumentNullException.ThrowIfNull(contact);
        ValidateNativeGroundCache(cache, replacements);
        if (!NativeWorldTerrain.CanDisembark(currentPolygonFlags, sourceEntity, collisionEnabled))
        {
            return new WorldGardenPlacementResult(false, null, -1, 0, null);
        }
        WorldGardenPlacementResult? result = null;
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
        {
            WorldGardenPlacementResult candidate = Resolve(alternative.Entries);
            if (result is not null && result != candidate)
            {
                throw new NotSupportedException("Retained native ground-cache alternatives produce different Garden transfer outcomes.");
            }
            result = candidate;
        }
        return result!;

        WorldGardenPlacementResult Resolve(IReadOnlyList<NativeWorldGroundIdentity> entries)
        {
            int radius = destinationEntity == 50 ? 256 : destinationEntity is 48 or >= 32 and <= 40 or 132 ? 64 : 32;
            int lastContact = -1;
            for (int proposal = 0; proposal < 8; proposal++)
            {
                short yaw = unchecked((short)(bodyYaw + 1024 + proposal * 512));
                (int dx, int dz) = NativeGardenTransform.Offset(distance, yaw);
                int x = unchecked(body.X + dx);
                int graphicsZ = unchecked(body.Z + dz);
                IReadOnlyList<WorldNativeGardenProbe> probes = CreateNativeGroundFootprint(x, -graphicsZ, unchecked((short)body.Y), radius);
                WorldGroundSelection? center = null;
                int centerHeight = 0;
                bool rejected = false;
                for (int index = 0; index < probes.Count; index++)
                {
                    WorldNativeGardenProbe probe = probes[index];
                    WorldGroundSelection? selected = LookupNativeGround(probe.X, probe.Z, replacements, entries);
                    if (selected is null)
                    {
                        // 53EB80 null skips this probe. Only a later consumer
                        // of the unassigned center height must refuse.
                        continue;
                    }
                    int height = -(int)selected.Ground.Height;
                    WorldTriangle triangle = selected.Ground.Triangle;
                    uint flags = triangle.GroundType | ((uint)triangle.TextureFlags << 8) | ((uint)triangle.VertexFlags << 16);
                    if (!NativeGardenTransferState.PassesPlacementGate(sourceEntity, destinationEntity, flags, currentGroundHeight, height, collisionEnabled))
                    {
                        rejected = true;
                        break;
                    }
                    if (index == 0)
                    {
                        center = selected;
                        centerHeight = height;
                    }
                }
                if (rejected)
                {
                    continue;
                }
                if (center is null)
                {
                    throw new NotSupportedException("Native Garden transfer reached an unassigned center-height stack value after a null terrain lookup.");
                }
                NativeWorldPathPosition position = new(x, centerHeight, graphicsZ);
                IReadOnlyList<WorldNativeGardenProbe> shape = CreateNativeGardenShapeFootprint(position, destinationEntity, bodyYaw);
                int hit = contact(shape, destinationEntity, 0, true);
                if (hit >= 0)
                {
                    lastContact = hit;
                    continue;
                }
                return new WorldGardenPlacementResult(true, position, lastContact, proposal + 1, center.Identity);
            }
            return new WorldGardenPlacementResult(false, null, lastContact, 8, null);
        }
    }

    public IReadOnlyList<WorldNativeGardenProbe> CreateNativeGardenShapeFootprint(NativeWorldPathPosition center, int entity, short bodyYaw)
    {
        byte shapeId = NativeGardenEntityShape(entity);
        NativeWorldContactShape shape = ReadNativeContactShape(shapeId);
        int x = shape.Length >> 1;
        int z = shape.Width >> 1;
        (int X, int Z)[] corners = shape.Kind switch
        {
            1 or 4 => [(-x, z), (x, z), (x, -z), (-x, -z)],
            2 => [(-shape.Length, z), (0, z), (0, -z), (-shape.Length, -z)],
            3 => [(-(shape.Length >> 2), -z), (-x, z), (x, z), (shape.Length >> 2, -z)],
            _ => throw new NotSupportedException("Native Garden contact projection requires its original shape layout.")
        };
        short yaw = unchecked((short)(bodyYaw + NativeGardenShapeYawCorrection(shapeId)));
        List<WorldNativeGardenProbe> result = [CreateNativeGroundFootprint(center.X, -center.Z, unchecked((short)center.Y), 0)[0]];
        foreach ((int localX, int localZ) in corners)
        {
            (int dx, int dz) = NativeGardenTransform.Rotate(unchecked((short)localX), unchecked((short)localZ), yaw);
            result.Add(CreateNativeGroundFootprint(unchecked(center.X + dx), unchecked(-center.Z - dz), unchecked((short)center.Y), 0)[0]);
        }
        return result;
    }

    public static byte NativeGardenEntityShape(int entity)
    {
        return entity switch
        {
            0 or 3 or 4 or 5 or 7 or 8 or 9 => 0,
            1 => 4,
            2 => 5,
            6 => 6,
            32 => 74,
            33 => 73,
            >= 34 and <= 40 => checked((byte)(entity + 47)),
            48 => 64,
            49 => 2,
            50 => 1,
            _ => throw new NotSupportedException("Garden placement requires the original546E10 entity-to-shape mapping.")
        };
    }

    public static short NativeGardenShapeYawCorrection(byte shape)
    {
        int entity = NativeTrainWorldObject.ClassifyShape(shape);
        return entity is >= 0 and < 10 or 128 || shape is 2 or 3 ? (short)-2048 : entity is >= 32 and <= 40 or 132 ? (short)-1024 : entity == 50 ? (short)2048 : entity == 48 || shape == 79 ? (short)1024 : shape == 80 ? (short)2048 : (short)0;
    }
}
