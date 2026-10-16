using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldNativeRagnarokGroundResult(WorldGroundSelection? Center, short GroundHeight, short BodyHeight, short FlightHeight, NativeWorldGroundCacheState Cache, int X, int Z, int GroundYaw, int PrimaryContact, int BroadContact, bool PositionCommitted, bool GroundCommitted, bool Blocked, bool ResetSpeed);

public sealed partial class WorldAssets
{
    /// <summary>53BB90/53E2A0/53ECE0 controlled50 ground/cache/contact publication, ordinary modes0/13.</summary>
    public WorldNativeRagnarokGroundResult ResolveNativeRagnarokGround(int beforeX, int beforeZ, short moveX, short graphicsMoveZ, short bodyHeight, short flightHeight, int? selectedGroup, NativeWorldGroundCacheState prior, IReadOnlyDictionary<int, int> replacements, Func<IReadOnlyList<WorldNativeGardenProbe>, int, int> contact, byte scriptMode = 0, bool commitSuppressed = false, bool selectionMayRetainGroup = false)
    {
        if (selectedGroup is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedGroup));
        }
        if (scriptMode is not (0 or 5 or 6 or 13 or 14))
        {
            throw new NotSupportedException("Ragnarok ground query requires ordinary0/13 height authority; transition5/6 consumes a native probe pointer WORD and needs its actual transition owner.");
        }
        ArgumentNullException.ThrowIfNull(contact);
        ValidateNativeGroundCache(prior, replacements);
        var alternatives = new List<NativeWorldGroundAlternative>();
        WorldNativeRagnarokGroundResult? physical = null;
        foreach (NativeWorldGroundAlternative alternative in prior.Alternatives)
        {
            List<NativeWorldGroundIdentity> cache = [.. alternative.Entries];
            NativeGroundProbe[][][] groups = CreateNativeGroundGroups(beforeX, beforeZ, moveX, graphicsMoveZ, 256);
            ResolveNativeGroundCells(groups, alternative.PreferredGroup, probe => ResolveNativeRagnarokGroundProbe(probe, cache, replacements));
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
                    int accepted = FindNativeGardenCandidate(groups[chosen]);
                    bool terrainBlocked = accepted < 0;
                    NativeGroundProbe[] candidate = terrainBlocked ? groups[selection][0] : groups[chosen][accepted];
                    NativeGroundProbe center = candidate[0];
                    bool scripted = scriptMode is 5 or 6 or 14;
                    short nextFlight = terrainBlocked || scripted ? flightHeight : unchecked((short)NativeWorldTerrain.ResolveHeight(50, center.GroundHeight, center.Selected!.Ground.Triangle.GroundType, bodyHeight, scriptMode, flightHeight: flightHeight).FlightHeight);
                    //53ECE0 returns the input probe pointer's low WORD in every nonzero mode except13.
                    short contactHeight = terrainBlocked ? bodyHeight : scripted ? unchecked((short)(0x2035D08 + chosen * 1760 + accepted * 220)) : nextFlight;
                    int primary = contact(PublishNativeGardenProbes(candidate, contactHeight), 0);
                    bool primaryBlocked = primary >= 0 && scriptMode != 13;
                    int broad = primaryBlocked ? primary : contact(PublishNativeGardenProbes(candidate, contactHeight), 200);
                    if (primaryBlocked)
                    {
                        //53BB90's blocked contact restores203FE48 high WORD from
                        //the previous body height and clears20409E8 for entity50.
                        nextFlight = bodyHeight;
                    }
                    bool blocked = terrainBlocked || primaryBlocked;
                    bool groundCommitted = !blocked && !commitSuppressed;
                    bool positionCommitted = groundCommitted && !scripted;
                    WorldGroundSelection? committedCenter = groundCommitted ? center.Selected : null;
                    var outcome = new WorldNativeRagnarokGroundResult(committedCenter, groundCommitted ? center.GroundHeight : (short)0, positionCommitted ? contactHeight : bodyHeight, nextFlight, prior, positionCommitted ? center.RawX : beforeX, positionCommitted ? center.RawZ : beforeZ, groundCommitted ? accepted * (chosen == 0 ? -160 : 160) : 0, primary, broad, positionCommitted, groundCommitted, blocked, primaryBlocked);
                    if (physical is not null && (physical with { Cache = prior }) != outcome)
                    {
                        throw new NotSupportedException("Retained Ragnarok cache/input alternatives disagree on actual ground, flight, pose or contacts.");
                    }
                    physical ??= outcome;
                    int? failures = terrainBlocked ? !primaryBlocked && alternative.FailedSelections is int failed ? unchecked(failed + 1) : alternative.FailedSelections : 0;
                    var next = new NativeWorldGroundAlternative(blocked ? selection : chosen, cache.ToArray(), committedCenter?.Identity ?? alternative.CurrentGround, groundCommitted ? alternative.CurrentGround : alternative.PreviousGround) { FailedSelections = failures };
                    if (!alternatives.Any(existing => existing.PreferredGroup == next.PreferredGroup && existing.FailedSelections == next.FailedSelections && existing.CurrentGround == next.CurrentGround && existing.PreviousGround == next.PreviousGround && existing.Entries.SequenceEqual(next.Entries)))
                    {
                        alternatives.Add(next);
                    }
                }
            }
            if (alternatives.Count > 64)
            {
                throw new NotSupportedException("Ragnarok retained ground alternatives exceed the bounded current query representation.");
            }
        }
        NativeWorldGroundCacheState result = prior with { Alternatives = alternatives.ToArray() };
        result.Validate();
        return physical! with { Cache = result };
    }

    private void ResolveNativeRagnarokGroundProbe(NativeGroundProbe probe, List<NativeWorldGroundIdentity> cache, IReadOnlyDictionary<int, int> replacements)
    {
        int minimum = int.MaxValue;
        foreach (NativeWorldGroundIdentity identity in cache)
        {
            if (identity.Cell == probe.Cell && TrySelected(identity, probe.X, probe.Z, replacements, out WorldGroundSelection? candidate) && -(int)candidate!.Ground.Height < minimum)
            {
                minimum = -(int)candidate.Ground.Height;
                probe.Selected = candidate;
            }
        }
        if (probe.Selected is null)
        {
            foreach (WorldGroundSelection candidate in ContainingNativeGround(probe.Cell, probe.X, probe.Z, replacements))
            {
                int height = -(int)candidate.Ground.Height;
                if (height < minimum)
                {
                    minimum = height;
                    probe.Selected = candidate;
                }
            }
            if (probe.Selected is not null)
            {
                cache.Insert(0, probe.Selected.Identity);
                if (cache.Count > 12)
                {
                    cache.RemoveAt(12);
                }
            }
        }
        probe.GroundHeight = unchecked((short)minimum);
        probe.Flags = probe.Selected is null ? (ushort)0 : NativeGardenFlags(probe.Selected);
        probe.Status = probe.Selected is null ? 1 : 0;
    }
}
