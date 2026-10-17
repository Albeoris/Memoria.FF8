namespace Memoria.FF8.Runtime;

/// <summary>Shared reconstructed native code profile, independent of individual field data.</summary>
public sealed record NativeFieldRuntimeProfile(int Version, string ExecutableHash, ushort FieldClock)
{
    public const string FF8EnExecutableHash = "064D466B5FE2BA901FD44ABF19F37C0FD6A2DB40AABD95C9E5959195B6589570";
    public const string RemasteredEnImageHash = "C52C42DBFE0EB3BBE17E3AA5A1E0607921B5A23CB9B2906D81844905106EA26F";
    public static bool IsSupportedImage(string? hash) => hash is FF8EnExecutableHash or RemasteredEnImageHash;
    public static NativeFieldRuntimeProfile RemasteredEn { get; } = new(1, RemasteredEnImageHash, 20);
    public static NativeFieldRuntimeProfile FF8En { get; } = new(1, FF8EnExecutableHash, 20);
    public static NativeFieldRuntimeProfile? ForImage(string? hash) => hash switch
    {
        FF8EnExecutableHash => FF8En,
        RemasteredEnImageHash => RemasteredEn,
        _ => null
    };
    public bool IsVerified => Version == 1 && IsSupportedImage(ExecutableHash) && FieldClock == 20;
    public int AllocatedActorSpeed => IsVerified ? 203000 * FieldClock >> 9 : throw new NotSupportedException("Unknown native field allocation profile.");
    // Native471F70 initializes BYTE1CE490E on each ordinary field allocation.
    public byte AllocatedLadderSpeed => IsVerified ? (byte)28 : throw new NotSupportedException("Unknown native field allocation profile.");
}

public enum NativeFieldInitializationStage
{
    Allocated,
    Initializing,
    Completed
}

/// <summary>Executed loader provenance. Restoring a checkpoint preserves this fact; absent legacy provenance is never inferred.</summary>
public sealed record NativeFieldInitializationProof(int Version, string ExecutableHash, int FieldId, string ProgramHash, ushort FieldClock, NativeFieldInitializationStage Stage, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? TimerClockCallsAtAllocation = null)
{
    public bool IsFrom(NativeFieldRuntimeProfile? profile, int fieldId, string programHash)
    {
        return profile is { IsVerified: true } && Version == 1 && ExecutableHash == profile.ExecutableHash && FieldClock == profile.FieldClock && FieldId == fieldId && ProgramHash == programHash && Stage is NativeFieldInitializationStage.Allocated or NativeFieldInitializationStage.Initializing or NativeFieldInitializationStage.Completed;
    }
}
