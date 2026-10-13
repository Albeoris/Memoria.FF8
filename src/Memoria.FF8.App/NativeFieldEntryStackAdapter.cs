using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeFieldEntryStackAdapter
{
    // TODO(TD-005): reuse the proved entry-loop stack contract without a field132 admission gate
    public static bool Supports(FieldAssets field, FieldTransition arrival)
    {
        return field.WalkmeshHash == NativeFieldEntryStackContract.WalkmeshHash && NativeFieldEntryStackContract.Supports(field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), arrival);
    }
}
