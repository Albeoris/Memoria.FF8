namespace Memoria.FF8.Resources.Localization;

public sealed record LocalizationAuthoringRequest(string SourceLanguage, string FieldScriptBaseLanguage, string TargetLanguage, IReadOnlyList<string> ReferenceLanguages, string PackageName);
