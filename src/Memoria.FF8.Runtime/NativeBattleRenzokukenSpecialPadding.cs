namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static bool ReadExtraPaddingUnknown(NativeBattleRenzokuken160Frame frame, int row)
    {
        if ((uint)row >= 100)
        {
            throw new InvalidDataException("Native special Renzokuken padding row exceeds its physical bank.");
        }
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        return row < 64 ? (state.ExtraStorageUnknownPaddingLow & (1UL << row)) != 0 : (state.ExtraStorageUnknownPaddingHigh & (1UL << (row - 64))) != 0;
    }

    public static NativeBattleRenzokuken160Frame SetExtraPaddingUnknown(NativeBattleRenzokuken160Frame frame, int row, bool unknown)
    {
        if ((uint)row >= 100)
        {
            throw new InvalidDataException("Native special Renzokuken padding write exceeds its physical bank.");
        }
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        if (row < 64)
        {
            ulong bit = 1UL << row;
            state = state with { ExtraStorageUnknownPaddingLow = unknown ? state.ExtraStorageUnknownPaddingLow | bit : state.ExtraStorageUnknownPaddingLow & ~bit };
        }
        else
        {
            ulong bit = 1UL << (row - 64);
            state = state with { ExtraStorageUnknownPaddingHigh = unknown ? state.ExtraStorageUnknownPaddingHigh | bit : state.ExtraStorageUnknownPaddingHigh & ~bit };
        }
        return Store(frame, state);
    }
}
