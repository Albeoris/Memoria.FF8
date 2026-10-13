using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeFieldEntryStackAdapter
{
    public static bool Supports(FieldAssets field, FieldTransition arrival)
    {
        return field.Id >= 72 && arrival.FieldId == field.Id && field.NativeExecutableHash == NativeFieldRuntimeProfile.FF8EnExecutableHash && field.WalkmeshHash is { Length: 64 } hash && hash.All(Uri.IsHexDigit);
    }
}
