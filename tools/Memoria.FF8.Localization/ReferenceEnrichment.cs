using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal static class ReferenceEnrichment
{
    public static ReferenceEnrichmentReport Run(string input, string gameDirectory, string language, string? reportPath, CancellationToken cancellationToken)
    {
        using TranslationReviewCommands.ReportDestination? reportDestination = reportPath is null ? null : TranslationReviewCommands.OpenReport(input, reportPath);

        language = FieldLocalizationKey.CanonicalLanguage(language);
        GameDataEdition edition = File.Exists(Path.Combine(gameDirectory, "main.zzz")) ? GameDataEdition.Remastered : GameDataEdition.Classic;
        LocalizationSourceCatalog catalog = LocalizationSourceExporter.ReadReference(gameDirectory, language, cancellationToken);
        ReferenceEnrichmentReport? report = null;
        LocalizationProjectIO.UpdateJsonReferences(input, project =>
        {
            string baseLanguage = FieldLocalizationKey.CanonicalLanguage(project.Manifest.FieldScriptBaseLanguage);
            LocalizationSourceCatalog? companion = edition == GameDataEdition.Remastered && baseLanguage != language
                ? LocalizationSourceExporter.ReadReference(gameDirectory, baseLanguage, cancellationToken)
                : null;
            LocalizationReferenceEnrichment enrichment = LocalizationProjectBuilder.EnrichReference(project, language, edition, catalog, companion, cancellationToken);
            report = new(enrichment.Language, enrichment.Edition.ToString(), enrichment.Project.Entries.Count, enrichment.Matched, enrichment.TextOnly, enrichment.Changed, enrichment.Coverage, enrichment.Skips, catalog.Gaps);
            reportDestination?.Stage(report);
            return enrichment.Project;
        }, cancellationToken);

        reportDestination?.Publish();
        return report ?? throw new InvalidOperationException("No localization references were processed.");
    }
}

internal sealed record ReferenceEnrichmentReport(string Language, string Edition, int Records, int Matched, int TextOnly, int Changed, IReadOnlyList<LocalizationReferenceCoverage> Coverage, IReadOnlyList<LocalizationReferenceSkip> Skips, IReadOnlyList<LocalizationSourceGap> ResourceGaps);
