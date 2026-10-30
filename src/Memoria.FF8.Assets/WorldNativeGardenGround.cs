using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>One original53D8A0 probe. X/Z retain the unwrapped world coordinates; GraphicsZ is their native rendering sign.</summary>
public sealed record WorldNativeGardenProbe(int X, short Y, int Z, int Cell, short LocalX, short LocalY, short LocalGraphicsZ, NativeWorldGroundIdentity? GroundIdentity, short GroundHeight, ushort Flags, int Status)
{
    public int GraphicsZ => -Z;
}

public sealed record WorldNativeGardenGroundResult(WorldGroundSelection? Center, short GroundHeight, ushort Flags, short BodyHeight, NativeWorldGroundCacheState Cache, int X, int Z, int GroundYaw, int PrimaryContact, int BroadContact, bool PositionCommitted, bool GroundCommitted, bool Blocked);

public sealed partial class WorldAssets
{
    public static IReadOnlyList<WorldNativeGardenProbe> CreateNativeGroundFootprint(int x, int z, short bodyHeight, int radius = 64)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }
        return PublishNativeGardenProbes(CreateNativeGroundProbes(x, z, radius), bodyHeight);
    }

    public static IReadOnlyList<WorldNativeGardenProbe> CreateNativeGroundFootprint(int x, int z, int radius)
    {
        return CreateNativeGroundFootprint(x, z, 0, radius);
    }

    public WorldNativeGardenGroundResult ResolveNativeGardenGround(int beforeX, int beforeZ, short moveX, short graphicsMoveZ, short bodyHeight, int? selectedGroup, NativeWorldGroundCacheState prior, IReadOnlyDictionary<int, int> replacements, Func<IReadOnlyList<WorldNativeGardenProbe>, int, int> contact, byte scriptMode = 0, bool commitSuppressed = false, bool collisionEnabled = true, bool selectionMayRetainGroup = false)
    {
        if (selectedGroup is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedGroup));
        }
        ArgumentNullException.ThrowIfNull(contact);
        ValidateNativeGroundCache(prior, replacements);
        List<NativeWorldGroundAlternative> alternatives = [];
        WorldNativeGardenGroundResult? physical = null;
        foreach (NativeWorldGroundAlternative alternative in prior.Alternatives)
        {
            List<NativeWorldGroundIdentity> cache = [.. alternative.Entries];
            NativeGroundProbe[][][] groups = CreateNativeGroundGroups(beforeX, beforeZ, moveX, graphicsMoveZ, 64);
            byte oldType = ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            ResolveNativeGroundCells(groups, alternative.PreferredGroup, probe => ResolveNativeGardenGroundProbe(probe, bodyHeight, oldType, cache, replacements, collisionEnabled));
            int[] selections = selectedGroup is int explicitGroup ? selectionMayRetainGroup && explicitGroup != alternative.PreferredGroup ? [explicitGroup, alternative.PreferredGroup] : [explicitGroup] : [alternative.PreferredGroup];
            foreach (int selection in selections)
            {
                int[] outcomes = [selection];
                if (FindNativeGardenCandidate(groups[selection]) < 0)
                {
                    outcomes = alternative.FailedSelections is null ? [selection, 1 - selection] : alternative.FailedSelections >= 4 ? [1 - selection] : [selection];
                }
                foreach (int chosen in outcomes)
                {
                    int acceptedIndex = FindNativeGardenCandidate(groups[chosen]);
                    bool terrainBlocked = acceptedIndex < 0;
                    NativeGroundProbe[] candidate = terrainBlocked ? groups[selection][0] : groups[chosen][acceptedIndex];
                    NativeGroundProbe center = candidate[0];
                    short initialHover = terrainBlocked ? bodyHeight : checked((short)NativeWorldTerrain.ResolveHeight(48, center.GroundHeight, center.Selected!.Ground.Triangle.GroundType, bodyHeight, scriptMode).Height);
                    int primary = contact(PublishNativeGardenProbes(candidate, initialHover), 0);
                    bool primaryBlocked = primary >= 0 && scriptMode is not (1 or 2 or 13);
                    int broad = primaryBlocked ? primary : contact(PublishNativeGardenProbes(candidate, initialHover), 200);
                    short finalHeight = initialHover;
                    if (!terrainBlocked && !primaryBlocked && scriptMode != 8)
                    {
                        //53BB90's second height attempt starts at the original body, not53ECE0's clamp.
                        int offset = center.Selected!.Ground.Triangle.GroundType is 33 or 34 ? 456 : 0;
                        short lower = unchecked((short)(center.GroundHeight - 256 + offset));
                        short upper = unchecked((short)(center.GroundHeight - 128 + offset));
                        finalHeight = bodyHeight;
                        if (lower > bodyHeight)
                        {
                            finalHeight = unchecked((short)(bodyHeight + 14));
                        }
                        if (upper < finalHeight)
                        {
                            finalHeight = unchecked((short)(finalHeight - 14));
                        }
                        int retryContact = contact(PublishNativeGardenProbes(candidate, finalHeight), 0);
                        if (retryContact >= 0 && scriptMode is not (1 or 2 or 13))
                        {
                            finalHeight = initialHover;
                        }
                    }
                    bool blocked = terrainBlocked || primaryBlocked;
                    bool groundCommitted = !blocked && !commitSuppressed;
                    bool positionCommitted = groundCommitted && scriptMode is 0 or 13;
                    int? failedSelections = terrainBlocked ? !primaryBlocked && alternative.FailedSelections is int failures ? unchecked(failures + 1) : alternative.FailedSelections : 0;
                    WorldGroundSelection? committedCenter = groundCommitted ? center.Selected : null;
                    WorldNativeGardenGroundResult outcome = new(committedCenter, groundCommitted ? center.GroundHeight : (short)0, groundCommitted ? center.Flags : (ushort)0, positionCommitted ? finalHeight : bodyHeight, prior, positionCommitted ? center.RawX : beforeX, positionCommitted ? center.RawZ : beforeZ, groundCommitted ? acceptedIndex * (chosen == 0 ? -160 : 160) : 0, primary, broad, positionCommitted, groundCommitted, blocked);
                    if (physical is not null && !SameNativeGardenGroundOutcome(physical, outcome))
                    {
                        throw new NotSupportedException("Retained native Garden cache/input alternatives disagree on published ground, pose or contact authority.");
                    }
                    physical ??= outcome;
                    NativeWorldGroundAlternative next = new(blocked ? selection : chosen, cache.ToArray(), committedCenter?.Identity ?? alternative.CurrentGround, groundCommitted ? alternative.CurrentGround : alternative.PreviousGround) { FailedSelections = failedSelections, TerrainFlag109Bit0 = groundCommitted ? (ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags & 8) != 0 : alternative.TerrainFlag109Bit0 };
                    if (!alternatives.Any(existing => existing.PreferredGroup == next.PreferredGroup && existing.FailedSelections == next.FailedSelections && existing.TerrainFlag109Bit0 == next.TerrainFlag109Bit0 && existing.CurrentGround == next.CurrentGround && existing.PreviousGround == next.PreviousGround && existing.Entries.SequenceEqual(next.Entries)))
                    {
                        alternatives.Add(next);
                    }
                }
            }
            if (alternatives.Count > 64)
            {
                throw new NotSupportedException("Native retained Garden ground alternatives exceed the bounded current query representation.");
            }
        }
        NativeWorldGroundCacheState result = prior with { Alternatives = alternatives.ToArray() };
        result.Validate();
        return physical! with { Cache = result };
    }

    private void ResolveNativeGardenGroundProbe(NativeGroundProbe probe, short bodyHeight, byte oldType, List<NativeWorldGroundIdentity> cache, IReadOnlyDictionary<int, int> replacements, bool collisionEnabled)
    {
        int minimum = int.MaxValue;
        int state = 0;
        foreach (NativeWorldGroundIdentity identity in cache)
        {
            if (identity.Cell != probe.Cell || !TrySelected(identity, probe.X, probe.Z, replacements, out WorldGroundSelection? candidate))
            {
                continue;
            }
            byte type = candidate!.Ground.Triangle.GroundType;
            if (type is not (>= 30 and <= 34) && bodyHeight > 0)
            {
                continue;
            }
            if (state == 0)
            {
                state = -1;
            }
            int height = -(int)candidate.Ground.Height;
            if (height < minimum)
            {
                minimum = height;
                probe.Flags = NativeGardenFlags(candidate);
                //53E410..53E48A publishes height/flags before permission. A denied lower
                //cached polygon leaves an earlier accepted pointer/state intact.
                if (!collisionEnabled || (candidate.Ground.Triangle.VertexFlags & 0x20) != 0)
                {
                    state = 1;
                    probe.Selected = candidate;
                }
            }
        }
        if (state == 0)
        {
            WorldGroundSelection? lowest = null;
            foreach (WorldGroundSelection candidate in ContainingNativeGround(probe.Cell, probe.X, probe.Z, replacements))
            {
                byte type = candidate.Ground.Triangle.GroundType;
                if (type is not (>= 30 and <= 34) && bodyHeight > 0 && type > 5 && oldType > 5)
                {
                    continue;
                }
                int height = -(int)candidate.Ground.Height;
                if (height < minimum)
                {
                    minimum = height;
                    lowest = candidate;
                    probe.Flags = NativeGardenFlags(candidate);
                }
            }
            if (lowest is not null)
            {
                //53E5C9 inserts before53E6B0, including a subsequently denied polygon.
                cache.Insert(0, lowest.Identity);
                if (cache.Count > 12)
                {
                    cache.RemoveAt(12);
                }
                if (!collisionEnabled || (lowest.Ground.Triangle.VertexFlags & 0x20) != 0)
                {
                    state = 1;
                    probe.Selected = lowest;
                }
            }
        }
        probe.GroundHeight = unchecked((short)minimum);
        probe.Status = state <= 0 ? 1 : 0;
    }

    private static ushort NativeGardenFlags(WorldGroundSelection selected)
    {
        return (ushort)(selected.Ground.Triangle.GroundType | selected.Ground.Triangle.TextureFlags << 8);
    }

    private static int FindNativeGardenCandidate(NativeGroundProbe[][] group)
    {
        return Array.FindIndex(group, candidate => candidate.All(probe => probe.Status == 0));
    }

    private static IReadOnlyList<WorldNativeGardenProbe> PublishNativeGardenProbes(NativeGroundProbe[] probes, short height)
    {
        return probes.Select(probe =>
        {
            int graphicsLocalZ = -probe.RawZ & 0x7FF;
            return new WorldNativeGardenProbe(probe.RawX, height, probe.RawZ, probe.Cell, (short)(probe.RawX & 0x7FF), height, (short)(graphicsLocalZ == 0 ? 0 : graphicsLocalZ - 2048), probe.Selected?.Identity, probe.GroundHeight, probe.Flags, probe.Status);
        }).ToArray();
    }

    private static bool SameNativeGardenGroundOutcome(WorldNativeGardenGroundResult left, WorldNativeGardenGroundResult right)
    {
        return left.Center?.Identity == right.Center?.Identity && left.GroundHeight == right.GroundHeight && left.Flags == right.Flags && left.BodyHeight == right.BodyHeight && left.X == right.X && left.Z == right.Z && left.GroundYaw == right.GroundYaw && left.PrimaryContact == right.PrimaryContact && left.BroadContact == right.BroadContact && left.PositionCommitted == right.PositionCommitted && left.GroundCommitted == right.GroundCommitted && left.Blocked == right.Blocked;
    }
}
