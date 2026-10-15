namespace Memoria.FF8.Runtime;

/// <summary>A semantic travel action resolved from current resources; it does not claim a physical contact or motor callback.</summary>
public sealed record NativeSemanticWorldTravel(int Version, int TargetFieldId, ushort StoryWord, byte EntityType, int WorldX, int WorldRawZ, short NativeHeight, ushort BodyYaw, int WmsetGroup, int WmsetRow, string WmsetHash, string Wm2FieldHash, string ExecutableHash)
{
    public const string ReturnRule = "semantic-current-resource-world-return-v1";

    public void Validate()
    {
        if (Version != 1 || TargetFieldId is < 0 or > ushort.MaxValue || EntityType is not (0 or 1 or 32 or 33) || WorldX is < -131072 or >= 131072 || WorldRawZ is < -98304 or >= 98304 || BodyYaw >= 4096 || WmsetGroup < 0 || WmsetRow < 0 || !IsHash(WmsetHash) || !IsHash(Wm2FieldHash) || !IsHash(ExecutableHash))
        {
            throw new InvalidDataException("Invalid current-resource semantic world travel proof.");
        }
    }

    public void ValidateReturn(ushort story, byte entity, NativeSavedWorldPose pose)
    {
        Validate();
        if (StoryWord != story || EntityType != entity || pose != new NativeSavedWorldPose(WorldX, WorldRawZ, NativeHeight, BodyYaw))
        {
            throw new InvalidDataException("Semantic return bytes differ from the authenticated current resource action.");
        }
    }

    private static bool IsHash(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}
