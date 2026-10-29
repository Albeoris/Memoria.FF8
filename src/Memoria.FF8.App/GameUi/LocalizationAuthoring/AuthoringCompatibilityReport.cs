using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.GameUi;

/// <summary>Rendering, byte representation and deployment are independent compatibility claims.</summary>
public sealed record AuthoringCompatibilityReport(string PresentationMode, string LayoutMode, string? ProfileId, string Role, string? Dataset, NativeTextContext Context, IReadOnlyList<NativeTextDiagnostic> PresentationDiagnostics, IReadOnlyList<NativeTextDiagnostic> EncodingDiagnostics, string Deployment)
{
    public bool CanPreview => PresentationDiagnostics.Count == 0;
    public bool NativeEncodingCompatible => EncodingDiagnostics.Count == 0;

    public static AuthoringCompatibilityReport Evaluate(bool nativeFonts, bool originalLayout, NativeFontProfile? profile, NativeTextContext context, string role, string? dataset, string body, IEnumerable<string> renderedRuns, IEnumerable<NativeTextDiagnostic>? presentationDiagnostics = null)
    {
        List<NativeTextDiagnostic> glyphs = [.. presentationDiagnostics ?? []];
        if (nativeFonts)
        {
            if (profile is null)
            {
                glyphs.Add(new("profile-unavailable", "No native font profile is selected for this resource context."));
            }
            else
            {
                glyphs.AddRange(renderedRuns.SelectMany(text => NativeFontLayout.Layout(profile, text, context).Diagnostics));
            }
        }
        IReadOnlyList<NativeTextDiagnostic> encoding = profile is null
            ? [new("profile-unavailable", "Native encoding cannot be checked without a profile for this role and resource context.")]
            : NativeStrictCodec.Encode(profile, body, context).Diagnostics;
        return new(nativeFonts ? "Native" : "System", originalLayout ? "Original" : "Modern", profile?.Id, role, dataset, context, glyphs, encoding, context.ResourceFamily == "System" ? "MemoriaOnly" : "Unverified");
    }
}
