using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private string LocalizeUiText(string key, string original) => resourceHost?.Resources.Localize(key, original) ?? original;
    private string LocalizeAuthored(string id, string original) => LocalizeUiText(SystemTextCatalog.Key(id), original);
    private string FormatAuthored(string id, string original, params object?[] arguments) => SystemTextCatalog.Format(LocalizeAuthored(id, original), original, arguments);
}
