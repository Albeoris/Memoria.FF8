using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterDonorFieldBundleTests(ITestOutputHelper output)
{
    private const int MaximumNestedMemberBytes = 32 * 1024 * 1024;
    private static readonly string[] BundleExtensions = [".rat", ".mrt", ".jsm", ".msd", ".sym", ".sfx", ".pcb"];
    private static readonly string[] ArchiveExtensions = [".fl", ".fi", ".fs"];

    [Fact]
    public void InstalledFieldBundleAuditReportsKnownFixtureBoundaries()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_FIELD_AUDIT") == "1", "Enable the optional Demaster field corpus audit for the audited fixture.");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? export = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_DATA_DIR");
        Assert.SkipUnless(Directory.Exists(game) && Directory.Exists(export), "Set the installed Remastered and optional Demaster fixture directories.");
        using RemasteredGameDataSource stock = new(game!);
        using RemasteredGameDataSource donor = new(game!);
        donor.PrepareLooseData(ArchiveExtensions.Select(extension => "data/field" + extension).Select(path =>
            new RemasteredLooseDataFile(path, path, 2, () => File.OpenRead(Path.Combine(export!, path)))));
        donor.SealLooseData();
        using IFlArchive stockOuter = stock.OpenArchive("field");
        using IFlArchive donorOuter = donor.OpenArchive("field");
        Dictionary<string, IFlArchiveEntry> originalMembers = IndexByName(stockOuter);
        Dictionary<string, IFlArchiveEntry> replacementMembers = IndexByName(donorOuter);
        string[] fields = replacementMembers.Keys.Where(name => name.EndsWith(".fl", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(field => field is not ("mapdata" or "main_chr"))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        Dictionary<string, int> changedMembers = BundleExtensions.ToDictionary(extension => extension, _ => 0);
        Dictionary<string, int> addedMembers = BundleExtensions.ToDictionary(extension => extension, _ => 0);
        List<string> failures = [];
        List<string> stockIssues = [];
        int changedBundles = 0;
        int supportedBundles = 0;
        int passedBundles = 0;
        int inheritedDiagnosticBundles = 0;
        int scriptBundles = 0;
        int encounterBundles = 0;
        int soundBundles = 0;
        int colorBundles = 0;
        int dynamicReferences = 0;
        foreach (string field in fields)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, byte[]?> source;
            Dictionary<string, byte[]?> modified;
            int errorsBefore = failures.Count;
            try
            {
                source = ReadBundle(originalMembers, field, "stock", failures);
                modified = ReadBundle(replacementMembers, field, "donor", failures);
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or KeyNotFoundException or NotSupportedException)
            {
                failures.Add($"{field}: nested archive: {error.GetType().Name}: {error.Message}");
                continue;
            }
            string[] changed = source.Keys.Union(modified.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(name => source.ContainsKey(name) != modified.ContainsKey(name) || IsChanged(source.GetValueOrDefault(name), modified.GetValueOrDefault(name))).ToArray();
            if (changed.Length == 0)
            {
                continue;
            }
            changedBundles++;
            foreach (string name in changed)
            {
                string extension = Path.GetExtension(name);
                if (!source.ContainsKey(name))
                {
                    addedMembers[extension]++;
                }
                else
                {
                    changedMembers[extension]++;
                }
            }
            if (changed.All(name => Path.GetExtension(name) == ".sym"))
            {
                continue;
            }
            supportedBundles++;
            int stockIssuesBefore = stockIssues.Count;
            try
            {
                if (changed.Any(name => Path.GetExtension(name) is ".rat" or ".mrt"))
                {
                    NativeFieldEncounterDefinition original = NativeEncounterTables.ReadField(Member(source, field + ".rat"), Member(source, field + ".mrt"));
                    NativeFieldEncounterDefinition selected = NativeEncounterTables.ReadField(Member(modified, field + ".rat"), Member(modified, field + ".mrt"));
                    if (original.Rate == selected.Rate && original.Formations.SequenceEqual(selected.Formations))
                    {
                        output.WriteLine($"{field}: RAT/MRT changes outside the encounter reader's first rate byte and four formation WORDs.");
                    }
                    encounterBundles++;
                }
                if (changed.Contains(field + "_en.jsm") || changed.Contains(field + "_en.msd"))
                {
                    JsmResourceValidationResult validation = Analyze(field, modified);
                    dynamicReferences += validation.DynamicMessageReferences;
                    if (!validation.IsValid)
                    {
                        IReadOnlyList<JsmResourceValidationIssue> stockErrors = Analyze(field, source).Errors;
                        foreach (JsmResourceValidationIssue issue in validation.Errors)
                        {
                            string context = $"{field}: {issue.Code}, script {issue.ScriptId}, operation {issue.Operation}: {issue.Message}";
                            (stockErrors.Contains(issue) ? stockIssues : failures).Add(context);
                        }
                    }
                    scriptBundles++;
                }
                foreach (string sound in changed.Where(name => Path.GetExtension(name) == ".sfx"))
                {
                    _ = FieldSoundEffectTable.Read(sound, Member(modified, sound));
                    soundBundles++;
                }
                if (changed.Contains(field + ".pcb") && modified.GetValueOrDefault(field + ".pcb") is not null)
                {
                    using GameAssets assets = new(game!, resources: new ResourceService(donor));
                    Assert.True(assets.NativeRuntimeProfile?.IsVerified);
                    Assert.True(assets.ReadFieldLogicResources(field).HasColorCorrections);
                    colorBundles++;
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or KeyNotFoundException or NotSupportedException)
            {
                failures.Add($"{field}: {error.GetType().Name}: {error.Message}");
            }
            if (errorsBefore == failures.Count && stockIssuesBefore == stockIssues.Count)
            {
                passedBundles++;
            }
            else if (errorsBefore == failures.Count)
            {
                inheritedDiagnosticBundles++;
            }
        }
        output.WriteLine($"Changed bundles: {changedBundles}; complete supported-reader passes: {passedBundles}/{supportedBundles}; inherited diagnostics only: {inheritedDiagnosticBundles}; SYM-only comparison: {changedBundles - supportedBundles}.");
        output.WriteLine($"Changed existing: {Counts(changedMembers)}. Added: {Counts(addedMembers)}.");
        output.WriteLine($"Encounter bundles: {encounterBundles}; script/text bundles: {scriptBundles}; sound bundles: {soundBundles}; color bundles: {colorBundles}; dynamic message references: {dynamicReferences}.");
        output.WriteLine($"Identical stock script diagnostics: {stockIssues.Count}. Changed diagnostics: {failures.Count}.");
        foreach (string issue in stockIssues.Concat(failures))
        {
            output.WriteLine(issue);
        }
        Assert.Equal((397, 392, 390, 0), (changedBundles, supportedBundles, passedBundles, inheritedDiagnosticBundles));
        Assert.Equal(new[] { 213, 203, 186, 125, 111, 7, 1 }, BundleExtensions.Select(extension => changedMembers[extension]).ToArray());
        Assert.Equal(3, addedMembers[".sfx"]);
        Assert.Equal((249, 190, 10, 0), (encounterBundles, scriptBundles, soundBundles, colorBundles));
        Assert.Empty(stockIssues);
        Assert.Equal(new[]
        {
            "ewpanel1: donor ewpanel1.mrt: Not enough capacity available: 5. Desired: 8",
            "ewpanel1: donor ewpanel1.pcb: Not enough capacity available: 3. Desired: 16",
            "ewpanel1: InvalidDataException: 'ewpanel1.mrt' is absent or unreadable.",
            "fhmin1: message-out-of-range, script 67, operation 738: Message 99 is outside the selected MSD table (99 messages).",
            "fhmin1: message-out-of-range, script 67, operation 743: Message 100 is outside the selected MSD table (99 messages).",
            "fhmin1: message-out-of-range, script 67, operation 748: Message 101 is outside the selected MSD table (99 messages)."
        }.Order(StringComparer.Ordinal), failures.Order(StringComparer.Ordinal));
    }

    private static JsmResourceValidationResult Analyze(string field, Dictionary<string, byte[]?> members)
    {
        int messageCount = MsdFile.Read(Member(members, field + "_en.msd"), NativeTextEncoding.CreateField("en", field)).Texts.Count();
        return JsmResourceValidation.Validate(Member(members, field + "_en.jsm"), messageCount);
    }

    private static byte[] Member(Dictionary<string, byte[]?> members, string name) => members.GetValueOrDefault(name) ?? throw new InvalidDataException($"'{name}' is absent or unreadable.");

    private static bool IsChanged(byte[]? stock, byte[]? donor)
    {
        return stock is null || donor is null ? !ReferenceEquals(stock, donor) : !stock.AsSpan().SequenceEqual(donor);
    }

    private static string Counts(Dictionary<string, int> counts) => string.Join(", ", counts.Where(pair => pair.Value > 0).Select(pair => $"{pair.Key}={pair.Value}"));

    private static Dictionary<string, IFlArchiveEntry> IndexByName(IFlArchive archive)
    {
        return archive.Entries.ToDictionary(entry => Path.GetFileName(entry.RelativePath.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, byte[]?> ReadBundle(Dictionary<string, IFlArchiveEntry> members, string field, string side, List<string> failures)
    {
        using MemoryStream listing = new(Read(members[field + ".fl"]), writable: false);
        using MemoryStream index = new(Read(members[field + ".fi"]), writable: false);
        using MemoryStream content = new(Read(members[field + ".fs"]), writable: false);
        using IFlArchive archive = FlArchive.Open(listing, index, content, leaveOpen: true, FlArchiveRepresentation.Files);
        Dictionary<string, byte[]?> result = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IFlArchiveEntry> nested = IndexByName(archive);
        foreach ((string name, IFlArchiveEntry entry) in nested.Where(pair => BundleExtensions.Contains(Path.GetExtension(pair.Key), StringComparer.OrdinalIgnoreCase)))
        {
            try
            {
                result.Add(name.ToLowerInvariant(), Read(entry));
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or NotSupportedException)
            {
                failures.Add($"{field}: {side} {name}: {error.Message}");
                result.Add(name.ToLowerInvariant(), null);
            }
        }
        return result;
    }

    private static byte[] Read(IFlArchiveEntry member)
    {
        if (member.Size > MaximumNestedMemberBytes)
        {
            throw new InvalidDataException($"'{member.RelativePath}' exceeds the bounded field verification size.");
        }
        byte[] data = new byte[checked((int)member.Size)];
        if (data.Length == 0)
        {
            return data;
        }
        using Stream stream = member.OpenForRead();
        stream.ReadExactly(data);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException($"'{member.RelativePath}' exceeded its decoded size.");
        }
        return data;
    }
}
