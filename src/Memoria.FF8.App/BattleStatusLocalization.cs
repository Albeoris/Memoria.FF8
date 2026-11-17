using System.Collections.Frozen;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class BattleStatusLocalization
{
    private static readonly FrozenDictionary<NativeStatusId, string> SemanticIds = Enum.GetValues<NativeStatusId>().ToFrozenDictionary(status => status, status => "battle.status." + (status == NativeStatusId.Vit0 ? "vit-0" : JsonNamingPolicy.KebabCaseLower.ConvertName(status.ToString())));

    public static string SemanticId(NativeStatusId status)
    {
        return SemanticIds[status];
    }
}
