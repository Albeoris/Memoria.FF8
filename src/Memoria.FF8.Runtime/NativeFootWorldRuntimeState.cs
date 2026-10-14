using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Executed normal-foot loader authority for native 20409E4 and C75CF8; absence in historical world snapshots remains unknown.</summary>
public sealed record NativeFootWorldRuntimeState
{
    public const int CurrentProofVersion = 1;
    public const string FirstLoadRule = "ff8-en-first-foot-542b90-548080-save110-v1";
    public const string SavedReloadRule = "ff8-en-foot-entry62-548080-54a0d0-v1";

    [JsonRequired]
    public required int ProofVersion { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    /// <summary>Native world entity type selected by the original world-entry packet.</summary>
    [JsonRequired]
    public required byte EntityType { get; init; }
    /// <summary>Native 20409E4, selected separately from locomotion animation IDs and their phase.</summary>
    [JsonRequired]
    public required byte AnimationVariant { get; init; }
    /// <summary>Native C75CF8 controls the world map display; native 549E80 normalizes fullscreen mode3 to saved0.</summary>
    [JsonRequired]
    public required byte MapDisplayMode { get; init; }
    [JsonRequired]
    public required long? ReloadSourceWorldFrame { get; init; }

    public static NativeFootWorldRuntimeState CreateFirstNormalLoad(NativeSavedWorldState savedWorld, byte entityType = 0)
    {
        ArgumentNullException.ThrowIfNull(savedWorld);
        byte mapDisplayMode = savedWorld.ReadMapDisplayModeForFreshFootLoader();
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = FirstLoadRule,
            EntityType = entityType,
            AnimationVariant = 0,
            MapDisplayMode = mapDisplayMode,
            ReloadSourceWorldFrame = null
        };
        state.Validate();
        return state;
    }

    public static NativeFootWorldRuntimeState CreateSavedReload(NativeSavedWorldFootReturn receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        receipt.RequireCompleteFootReload();
        var state = new NativeFootWorldRuntimeState
        {
            ProofVersion = CurrentProofVersion,
            Rule = SavedReloadRule,
            EntityType = receipt.EntityType105,
            AnimationVariant = receipt.AnimationVariant108!.Value,
            MapDisplayMode = receipt.MovementMode110!.Value,
            ReloadSourceWorldFrame = receipt.SourceWorldFrame
        };
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (ProofVersion != CurrentProofVersion || Rule is not (FirstLoadRule or SavedReloadRule) || EntityType > 1 || AnimationVariant > 1 || MapDisplayMode > 3 || Rule == FirstLoadRule && (AnimationVariant != 0 || ReloadSourceWorldFrame is not null) || Rule == SavedReloadRule && (ReloadSourceWorldFrame is null or < 0))
        {
            throw new InvalidDataException("Invalid native foot-world loader or saved-reload authority.");
        }
    }

    public void RequireSupportedNormalFoot()
    {
        Validate();
        if (AnimationVariant != 0 || MapDisplayMode != 0)
        {
            throw new NotSupportedException("The native foot renderer/control path currently requires variant0 and map display mode0.");
        }
    }
}
