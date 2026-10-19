using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>505F00/5060E0's original five frequency rows atB8B810/B8B824.</summary>
public sealed record NativeBattleActionCameraRules(string ExecutableSha256, ImmutableArray<ushort> Increment, ImmutableArray<ushort> RandomRange, ImmutableArray<ushort> EnemyThreshold)
{
    public void Validate()
    {
        if ((ExecutableSha256 != NativeFieldRuntimeProfile.FF8EnExecutableHash && ExecutableSha256 != NativeFieldRuntimeProfile.RemasteredEnImageHash) || Increment.IsDefault || Increment.Length != 5 || RandomRange.IsDefault || RandomRange.Length != 5 || EnemyThreshold.IsDefault || EnemyThreshold.Length != 5)
        {
            throw new InvalidDataException("Native action camera rules require the authenticated five original frequency rows.");
        }
    }

    public static byte Frequency(uint savedOptions)
    {
        byte index = unchecked((byte)(4 - (byte)(savedOptions >> 16)));
        if (index >= 5)
        {
            throw new NotSupportedException("Native camera frequency is outside its original five settings rows.");
        }
        return index;
    }
}
