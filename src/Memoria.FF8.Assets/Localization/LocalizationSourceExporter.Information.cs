using System.Globalization;

namespace Memoria.FF8.Assets.Localization;

public static partial class LocalizationSourceExporter
{
    private sealed partial class Reader
    {
        private void InformationBody(byte[] body, string table, int ordinal, IReadOnlyDictionary<string, string?> metadata)
        {
            Dictionary<string, string?> values = new(metadata, StringComparer.Ordinal)
            {
                ["Structure"] = "information-body-v1",
                ["Role"] = "Body"
            };
            string text;
            try
            {
                text = NativeTutorialInformationText.Format(body, language == "jp" ? Albeoris.Games.FF8.TextEncoding.FF8Encoding.CreateJapanese() : encoding, language == "jp");
            }
            catch (Exception exception) when (ResourceError(exception))
            {
                text = "";
                values["Source Present"] = "false";
                values["Decode Error"] = exception.Message;
                gaps.Add(new("Information and SeeD exam", "menu/mngrp.bin/" + table + "/" + ordinal.ToString(CultureInfo.InvariantCulture), "Information body decoding failed: " + exception.Message));
            }
            Add("Information and SeeD exam", table, ordinal, text, "menu/mngrp.bin", values);
        }
    }
}
