using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Current bytes and known masks of the original mutable Carbuncle skeleton section.</summary>
public sealed record NativeBattleCarbuncleExecutableMemory([property: JsonRequired] NativeBattleEffectArena Skeleton)
{
    public const uint Address = 0x10B6FAC;
    public const int Length = 0x5B0;

    public static NativeBattleCarbuncleExecutableMemory FromOriginal(NativeBattleEffectOwnedModelResource resource)
    {
        if (resource.Address != 0x10B6F94 || resource.Section(0) != 0x18 || resource.Section(1) != 0x5C8)
        {
            throw new InvalidDataException("Native Carbuncle skeleton requires its actual original model section.");
        }
        return new(NativeBattleEffectArena.Clear().Write(0, resource.Bytes.AsSpan().Slice(0x18, Length)));
    }

    public void Validate()
    {
        if (Skeleton is null)
        {
            throw new InvalidDataException("Native Carbuncle lost its current executable skeleton owner.");
        }
        Skeleton.Validate();
        if (Skeleton.Regions.Any(region => region.Offset > Length - region.Data.Length) || Skeleton.UnknownRegions.Any(region => region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Native Carbuncle executable skeleton leaves its original section.");
        }
    }

    public byte[] Read(uint address, int count)
    {
        return Skeleton.Read(Offset(address, count), count);
    }

    public NativeBattleCarbuncleExecutableMemory Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return this with { Skeleton = Skeleton.Write(Offset(address, bytes.Length), bytes) };
    }

    public NativeBattleCarbuncleExecutableMemory SelectFirstPose(NativeBattlePoseFirstFrame frame)
    {
        Validate();
        if (frame.Pose.Bones.IsDefault || frame.Pose.Bones.Length != Read(Address, 1)[0])
        {
            throw new InvalidDataException("Native Carbuncle pose does not match its current skeleton bone count.");
        }
        NativeBattleEffectArena skeleton = NativeBattleArenaPose.WritePose(Skeleton, 0, frame.Pose);
        return this with { Skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, frame.Pose) };
    }

    private static int Offset(uint address, int count)
    {
        if (address < Address || count < 0 || address - Address > Length || count > Length - (address - Address))
        {
            throw new InvalidDataException("Native Carbuncle executable read or write leaves its proved skeleton section.");
        }
        return checked((int)(address - Address));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCarbuncleExecutableMemory? CarbuncleExecutableMemory { get; init; }
}
