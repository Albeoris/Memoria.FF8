using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class TranslationReviewCommandTests
{
    [Fact]
    public void ApprovedGlossaryUsesAnExactSpeakerLineAndDefersLocalConflicts()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("memoria-speaker-glossary-");
        try
        {
            const string reference = "Reference [en]";
            const string untranslated = "Port Mechanic{Line}«Идите».{End}";
            string input = Path.Combine(directory.FullName, "package");
            string glossary = Path.Combine(directory.FullName, "speakers.json");
            string report = Path.Combine(directory.FullName, "review.json");
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            LocalizationRecord Row(string file, string ordinal, string english, string target) => new()
            {
                Category = "Fields",
                File = file,
                Key = $"$en_{file}_{ordinal}",
                SourcePresent = false,
                References = new Dictionary<string, string?> { [reference] = english },
                Target = target
            };
            LocalizationProject project = new()
            {
                Manifest = new()
                {
                    TargetLanguage = "ru",
                    ReferenceLanguages = new Dictionary<string, string> { [reference] = "en" }
                },
                Entries =
                [
                    Row("agreed", "0000", "Port Mechanic{Line}”Proceed.“{End}", untranslated),
                    Row("case", "0000", "port Mechanic{Line}”Proceed.“{End}", "port Mechanic{Line}«Идите».{End}"),
                    Row("conflict", "0000", "Port Mechanic{Line}”Wait.“{End}", "Техник{Line}«Подождите».{End}"),
                    Row("conflict", "0001", "Port Mechanic{Line}”Proceed.“{End}", untranslated),
                    Row("party", "0000", "Selphie{Line}”Selphie is ready!“{End}", "Селфи{Line}«Селфи готова!»{End}"),
                    Row("party", "0001", "Quistis{Line}”Wait!“{End}", "Селфи{Line}«Стой!»{End}"),
                    Row("party", "0002", "”Selphie, wait!“{End}", "«Селфи, подожди!»{End}")
                ]
            };
            LocalizationProjectIO.WriteNew(project, input, "json", "single", cancellation);
            File.WriteAllText(glossary, """{"Port Mechanic":"Механик","Селфи":"{Char Selphie}"}""");
            string expectedGlossary = File.ReadAllText(glossary);
            Assert.Throws<ArgumentException>(() => TranslationReviewCommands.Run("fix", input, "en", glossary, cancellation, glossary));
            Assert.Equal(1, TranslationReviewCommands.Run("fix", input, "en", report, cancellation, glossary));
            LocalizationProject fixedProject = LocalizationProjectIO.Read(input);
            Assert.Equal("Механик{Line}«Идите».{End}", fixedProject.Entries.Single(row => row.File == "agreed").Target);
            Assert.Equal("port Mechanic{Line}«Идите».{End}", fixedProject.Entries.Single(row => row.File == "case").Target);
            Assert.Equal(untranslated, fixedProject.Entries.Single(row => row.Key == "$en_conflict_0001").Target);
            Assert.Equal("{Char Selphie}{Line}«Селфи готова!»{End}", fixedProject.Entries.Single(row => row.Key == "$en_party_0000").Target);
            Assert.Equal("Селфи{Line}«Стой!»{End}", fixedProject.Entries.Single(row => row.Key == "$en_party_0001").Target);
            Assert.Equal("«Селфи, подожди!»{End}", fixedProject.Entries.Single(row => row.Key == "$en_party_0002").Target);
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(report)))
            {
                JsonElement issue = Assert.Single(document.RootElement.GetProperty("findings").EnumerateArray(), issue => issue.GetProperty("rule").GetString() == "speaker-glossary-conflict");
                Assert.Equal("$en_conflict_0000", issue.GetProperty("key").GetString());
                Assert.Contains(document.RootElement.GetProperty("findings").EnumerateArray(), finding =>
                    finding.GetProperty("rule").GetString() == "speaker-character" && finding.GetProperty("key").GetString() == "$en_party_0001" && !finding.GetProperty("fixable").GetBoolean());
            }
            Assert.Equal(expectedGlossary, File.ReadAllText(glossary));
            File.WriteAllText(glossary, """{"Rinoa":"Риноа"}""");
            Assert.Throws<InvalidDataException>(() => TranslationReviewCommands.Run("fix", input, "en", report, cancellation, glossary));
            File.WriteAllText(glossary, """{"Port Mechanic":"Механик","port mechanic":"Техник"}""");
            Assert.Throws<InvalidDataException>(() => TranslationReviewCommands.Run("fix", input, "en", report, cancellation, glossary));
            File.WriteAllText(glossary, """{"Селфи":"{Color Yellow}"}""");
            Assert.Throws<InvalidDataException>(() => TranslationReviewCommands.Run("fix", input, "en", report, cancellation, glossary));
            File.WriteAllText(glossary, """{"Селфи":"{Char Mog}"}""");
            Assert.Throws<InvalidDataException>(() => TranslationReviewCommands.Run("fix", input, "en", report, cancellation, glossary));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void FixRequiresTheCorrectEditionAndKeepsExistingTargetsOnAnotherPass()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("memoria-translation-review-");
        try
        {
            string input = Path.Combine(directory.FullName, "package");
            string manifestPath = Path.Combine(input, LocalizationProjectIO.ManifestRelativePath);
            string payload = Path.Combine(input, "Localization", "fields.json");
            string report = Path.Combine(directory.FullName, "review.json");
            Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
            string rawTermSource = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Say {TermEN SeeD}")));
            string readingHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Say SeeD{End}")));
            File.WriteAllText(manifestPath, $$"""
                {
                  "schemaVersion": 2,
                  "payloadKind": "localization",
                  "projectId": "translation-review-fixture",
                  "sourceLanguage": "en",
                  "targetLanguage": "ru-RU",
                  "fieldScriptBaseLanguage": "en",
                  "sourceColumn": "Source [en]",
                  "targetColumn": "Target",
                  "referenceLanguages": { "Reference [en]": "en" },
                  "mappingVersion": "source-ordinal-v1",
                  "compatibleVariants": ["Classic:en", "Remastered:en"],
                  "format": "json",
                  "layout": "single",
                  "payloads": [{ "path": "Localization/fields.json" }],
                  "sourceEdition": "Remastered",
                  "playerExport": true,
                  "referenceEditions": { "Reference [en]": "Classic" },
                  "{{LocalizationReferenceProvenance.ManifestMember}}": {
                    "Reference [en]": {
                      "$en_fixture_0002": ["{{rawTermSource}}", "{{readingHash}}"],
                      "$en_fixture_0003": ["{{rawTermSource}}", "{{readingHash}}"]
                    }
                  }
                }
                """);
            string sameSource = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Agreed")));
            string differentSource = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Press {Key Menu}.")));
            File.WriteAllText(payload, $$"""
                {
                  "Fields": {
                    "fixture": {
                      "$en_fixture_0000": {
                        "Target": "Договорились{End}{End}",
                        "Reference [en]": "Agreed{End}",
                        "ReferenceKey [en]": "$en_fixture_0000",
                        "Source Text SHA256": "{{sameSource}}"
                      },
                      "$en_fixture_0001": {
                        "Target": "Нажми {Key Menu}.{End}",
                        "Reference [en]": "Press {Key Cancel}.{End}",
                        "ReferenceKey [en]": "$en_fixture_0001",
                        "Source Text SHA256": "{{differentSource}}",
                        "Edition Targets": { "Classic:en": "Нажми {Key Cancel}.{End}{End}" }
                      },
                      "$en_fixture_0002": {
                        "Target": "Скажи SeeD{End}{End}",
                        "Reference [en]": "Say SeeD{End}",
                        "ReferenceKey [en]": "$en_fixture_0002",
                        "Source Text SHA256": "{{rawTermSource}}"
                      },
                      "$en_fixture_0003": {
                        "Target": "Скажи SeeD{End}{End}",
                        "Reference [en]": "Edited reading{End}",
                        "ReferenceKey [en]": "$en_fixture_0003",
                        "Source Text SHA256": "{{rawTermSource}}"
                      }
                    }
                  }
                }
                """);
            byte[] original = File.ReadAllBytes(payload);
            byte[] manifest = File.ReadAllBytes(manifestPath);
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            string blocked = Path.Combine(directory.FullName, "blocking-file");
            File.WriteAllText(blocked, "Existing file");
            Assert.Throws<IOException>(() => TranslationReviewCommands.Run("fix", input, "en", Path.Combine(blocked, "report.json"), cancellation));
            Assert.Equal(original, File.ReadAllBytes(payload));
            Assert.Equal(1, TranslationReviewCommands.Run("lint", input, "en", report, cancellation));
            Assert.Equal(original, File.ReadAllBytes(payload));
            Assert.Equal(1, TranslationReviewCommands.Run("fix", input, "en", report, cancellation));
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(report)))
            {
                JsonElement detail = document.RootElement;
                Assert.Equal(3, detail.GetProperty("fixedCount").GetInt32());
                JsonElement[] issues = detail.GetProperty("findings").EnumerateArray().Where(issue => !issue.GetProperty("fixed").GetBoolean()).ToArray();
                Assert.Equal(["$en_fixture_0001", "$en_fixture_0003"], issues.Select(issue => issue.GetProperty("key").GetString()));
                Assert.All(issues, issue =>
                {
                    Assert.Equal("reference-edition-mismatch", issue.GetProperty("rule").GetString());
                    Assert.Equal("Target", issue.GetProperty("column").GetString());
                });
            }
            var rows = LocalizationProjectIO.Read(input).Entries;
            Assert.Equal("Договорились{End}", rows[0].Target);
            Assert.Equal("Нажми {Key Menu}.{End}", rows[1].Target);
            Assert.Equal("Нажми {Key Cancel}.{End}", rows[1].ExtensionData[LocalizationProjectIO.EditionTargetsColumn].GetProperty("Classic:en").GetString());
            Assert.Equal("Скажи SeeD{End}", rows[2].Target);
            Assert.Equal("Скажи SeeD{End}{End}", rows[3].Target);
            Assert.Equal(manifest, File.ReadAllBytes(manifestPath));
            byte[] fixedBytes = File.ReadAllBytes(payload);
            Assert.Equal(1, TranslationReviewCommands.Run("fix", input, "en", report, cancellation));
            Assert.Equal(fixedBytes, File.ReadAllBytes(payload));
            Assert.Throws<ArgumentException>(() => TranslationReviewCommands.Run("lint", input, "en", payload, cancellation));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
