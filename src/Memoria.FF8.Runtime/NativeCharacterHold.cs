namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native51EA50 changes physical saved WORD148 bits2/4 without rebuilding stats.</summary>
    public NativeCharacterVitals WithHoldFlags(int rawCharacter, bool enabled)
    {
        Validate();
        if (rawCharacter < 0)
        {
            throw new NotSupportedException("Native HOLD negative DWORD addresses an unowned saved row.");
        }
        int row = rawCharacter < 8 ? rawCharacter : FindPhysicalRow(unchecked((byte)rawCharacter));
        if (row == 255)
        {
            return this;
        }
        NativeCharacterVital[] records = Records.ToArray();
        ushort flags = records[row].MemberFlags;
        records[row] = records[row] with { MemberFlags = enabled ? (ushort)(flags | 6) : (ushort)(flags & 0xFFF9) };
        return (this with { Records = records }).DeepCopy();
    }
}
