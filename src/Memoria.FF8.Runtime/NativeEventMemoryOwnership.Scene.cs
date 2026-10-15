using System.Text.Json;

namespace Memoria.FF8.Runtime;

[Flags]
public enum NativeEventSceneMemoryOwners
{
    None = 0,
    PreviousField = 1 << 0,
    PreviousWorldEntity = 1 << 1,
    PrimaryChannel = 1 << 2,
    SecondaryChannel = 1 << 3,
    PartyBindings = 1 << 4,
    MusicVolume = 1 << 5,
    SoundVolume = 1 << 6,
    MusicProcess = 1 << 7,
    WorldMusicSelection = 1 << 8,
    DrawPointDisplay = 1 << 9,
    DrawPointIndex = 1 << 10
}

public static partial class NativeEventMemoryOwnership
{
    /// <summary>Canonical scene owner spans shared by live execution and persisted-state validation.</summary>
    public static IReadOnlyList<NativeEventMemoryRegion> SceneRegions(NativeEventSceneMemoryOwners owners)
    {
        const NativeEventSceneMemoryOwners all = NativeEventSceneMemoryOwners.PreviousField | NativeEventSceneMemoryOwners.PreviousWorldEntity | NativeEventSceneMemoryOwners.PrimaryChannel | NativeEventSceneMemoryOwners.SecondaryChannel | NativeEventSceneMemoryOwners.PartyBindings | NativeEventSceneMemoryOwners.MusicVolume | NativeEventSceneMemoryOwners.SoundVolume | NativeEventSceneMemoryOwners.MusicProcess | NativeEventSceneMemoryOwners.WorldMusicSelection | NativeEventSceneMemoryOwners.DrawPointDisplay | NativeEventSceneMemoryOwners.DrawPointIndex;
        if ((owners & ~all) != NativeEventSceneMemoryOwners.None)
        {
            throw new InvalidDataException("Unrecognized native event scene memory owner flags.");
        }
        List<NativeEventMemoryRegion> regions = [];
        if (owners.HasFlag(NativeEventSceneMemoryOwners.PreviousField))
        {
            regions.Add(new(84, 2, NativeEventMemoryOwner.PreviousField));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.PreviousWorldEntity))
        {
            regions.Add(new(86, 1, NativeEventMemoryOwner.PreviousWorldEntity));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.PrimaryChannel))
        {
            regions.Add(new(108, 4, NativeEventMemoryOwner.MusicChannels));
            regions.Add(new(199, 1, NativeEventMemoryOwner.MusicChannels));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.SecondaryChannel))
        {
            regions.Add(new(112, 4, NativeEventMemoryOwner.MusicChannels));
            regions.Add(new(200, 1, NativeEventMemoryOwner.MusicChannels));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.PartyBindings))
        {
            regions.Add(new(194, 3, NativeEventMemoryOwner.PartyBindings));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.MusicVolume))
        {
            regions.Add(new(197, 1, NativeEventMemoryOwner.SoundVolume));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.SoundVolume))
        {
            regions.Add(new(198, 1, NativeEventMemoryOwner.SoundVolume));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.MusicProcess))
        {
            regions.Add(new(201, 2, NativeEventMemoryOwner.MusicProcess));
            regions.Add(new(213, 2, NativeEventMemoryOwner.MusicProcess));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.WorldMusicSelection))
        {
            regions.Add(new(268, 1, NativeEventMemoryOwner.WorldMusicSelection));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.DrawPointDisplay))
        {
            regions.Add(new(240, 2, NativeEventMemoryOwner.DrawPoints));
        }
        if (owners.HasFlag(NativeEventSceneMemoryOwners.DrawPointIndex))
        {
            regions.Add(new(242, 1, NativeEventMemoryOwner.DrawPoints));
        }
        return regions.AsReadOnly();
    }

    /// <summary>Reads only the presence of canonical owners in a captured Scene; it never reconstructs field scalar values.</summary>
    public static NativeEventSceneMemoryOwners SceneOwners(JsonElement scene)
    {
        if (scene.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Native event scene owner registrations require a captured Scene object.");
        }
        NativeEventSceneMemoryOwners owners = NativeEventSceneMemoryOwners.None;
        void Include(string property, NativeEventSceneMemoryOwners owner)
        {
            if (scene.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null)
            {
                owners |= owner;
            }
        }
        Include("PreviousFieldWord84", NativeEventSceneMemoryOwners.PreviousField);
        Include("PreviousWorldEntityByte86", NativeEventSceneMemoryOwners.PreviousWorldEntity);
        Include("MusicChannel108", NativeEventSceneMemoryOwners.PrimaryChannel);
        Include("MusicChannel112", NativeEventSceneMemoryOwners.SecondaryChannel);
        Include("PartyBindingMemory", NativeEventSceneMemoryOwners.PartyBindings);
        Include("SoundMusicVolume197", NativeEventSceneMemoryOwners.MusicVolume);
        Include("SoundMusicVolume198", NativeEventSceneMemoryOwners.SoundVolume);
        Include("MusicProcess", NativeEventSceneMemoryOwners.MusicProcess);
        Include("WorldMusicSelection", NativeEventSceneMemoryOwners.WorldMusicSelection);
        Include("DrawPointIndex", NativeEventSceneMemoryOwners.DrawPointIndex);
        if (scene.TryGetProperty("DrawPointFieldStateVersion", out JsonElement displayVersion))
        {
            if (displayVersion.ValueKind != JsonValueKind.Number || !displayVersion.TryGetInt32(out int version) || version is not (0 or 1))
            {
                throw new InvalidDataException("Captured draw-point scene ownership has an unsupported version.");
            }
            if (version == 1)
            {
                owners |= NativeEventSceneMemoryOwners.DrawPointDisplay;
            }
        }
        return owners;
    }
}
