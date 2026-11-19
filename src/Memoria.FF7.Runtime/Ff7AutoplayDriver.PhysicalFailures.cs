using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private const int PhysicalFailureRevision = 1;
    private Ff7AutoPlayPhysicalFailureReceipt[] physicalFailures = [];

    internal void SetPhysicalFailureReceipts(IEnumerable<Ff7AutoPlayPhysicalFailureReceipt> receipts)
    {
        physicalFailures = receipts.Where(receipt => receipt.Revision == PhysicalFailureRevision).ToArray();
    }

    internal static Ff7AutoPlayPhysicalFailureReceipt CreatePhysicalFailureReceipt(Ff7SessionSave native, string context, long expanded)
    {
        return new(PhysicalFailureRevision, ReadPhysicalFailureContract(context), ReadExactNativeGraphFingerprint(native, "physical-search-source:1"), expanded);
    }

    internal static bool MatchesPhysicalFailureReceipt(Ff7AutoPlayPhysicalFailureReceipt receipt, string nativeSource, string context)
    {
        return receipt.Revision == PhysicalFailureRevision && receipt.Contract == ReadPhysicalFailureContract(context) && receipt.NativeSource == nativeSource;
    }

    internal static string ReadPhysicalFailureGuideContext(string context, IReadOnlyList<int> routeDistance, IEnumerable<(int From, int To)> advice)
    {
        return $"{context}|distances:{string.Join(',', routeDistance)}|advice:{string.Join(';', advice.OrderBy(edge => edge.From).ThenBy(edge => edge.To).Select(edge => $"{edge.From}:{edge.To}"))}";
    }

    private bool TryReadPhysicalFailureReceipt(string context, out Ff7AutoPlayPhysicalFailureReceipt? receipt)
    {
        string contract = ReadPhysicalFailureContract(context);
        string? nativeSource = null;
        foreach (Ff7AutoPlayPhysicalFailureReceipt candidate in physicalFailures.Where(candidate => candidate.Contract == contract))
        {
            nativeSource ??= ReadExactNativeGraphFingerprint(session.CaptureAutoPlayNavigation(), "physical-search-source:1");
            if (candidate.NativeSource == nativeSource)
            {
                receipt = candidate;
                return true;
            }
        }
        receipt = null;
        return false;
    }

    private static string ReadPhysicalFailureContract(string context)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context)));
    }
}
