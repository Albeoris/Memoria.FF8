using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets;

/// <summary>Selects the native text language and codepage for a logical resource source.</summary>
public static class NativeTextEncoding
{
    public static FF8Encoding Create(string language)
    {
        FF8TextLanguage nativeLanguage = FF8TextLanguages.Parse(language);
        return nativeLanguage == FF8TextLanguage.JP ? FF8Encoding.CreateJapanese(nativeLanguage) : FF8Encoding.CreateEuropean(nativeLanguage);
    }

    public static FF8Encoding CreateField(string language, string field)
    {
        FF8TextLanguage nativeLanguage = FF8TextLanguages.Parse(language);
        return nativeLanguage == FF8TextLanguage.JP ? FF8Encoding.CreateJapanese(nativeLanguage, new DefaultJapaneseFieldCharacters(), field) : FF8Encoding.CreateEuropean(nativeLanguage);
    }
}

