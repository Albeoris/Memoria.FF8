using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7FieldResources(string Name, PcFieldFile Container, PcFieldScripts Scripts, PcFieldModels Models, IReadOnlyList<PcFieldCamera> Cameras, IReadOnlyList<PcWalkmeshTriangle> Walkmesh, PcFieldTriggers Triggers, PcFieldPalettes Palettes, PcFieldBackground Background)
{
    public static Ff7FieldResources Load(Ff7Installation installation, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        return Load(archive, fieldName);
    }

    public static Ff7FieldResources Load(LgpArchive archive, string fieldName)
    {
        IReadOnlyList<LgpEntry> candidates = archive.Find(fieldName);
        if (candidates.Count != 1)
        {
            throw new InvalidDataException($"FF7 field '{fieldName}' resolves to {candidates.Count} archive members; exactly one is required.");
        }
        PcFieldFile file = PcFieldFile.ReadCompressed(archive.ReadEntry(candidates[0]));
        return new Ff7FieldResources(fieldName, file, PcFieldScripts.Read(file.GetSection(PcFieldSectionKind.ScriptAndText).Span), PcFieldModels.Read(file.GetSection(PcFieldSectionKind.Models).Span), PcFieldCameras.Read(file.GetSection(PcFieldSectionKind.Camera).Span), PcFieldWalkmesh.Read(file.GetSection(PcFieldSectionKind.Walkmesh).Span), PcFieldTriggers.Read(file.GetSection(PcFieldSectionKind.TriggersAndGateways).Span), PcFieldPalettes.Read(file.GetSection(PcFieldSectionKind.Palettes).Span), PcFieldBackground.Read(file.GetSection(PcFieldSectionKind.Background).Span));
    }
}
