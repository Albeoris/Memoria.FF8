using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class BattleStatusLocalization
{
    public static string SemanticId(NativeStatusId status)
    {
        string name = status == NativeStatusId.Vit0 ? "vit-0" : JsonNamingPolicy.KebabCaseLower.ConvertName(status.ToString());
        return "battle.status." + name;
    }
}
