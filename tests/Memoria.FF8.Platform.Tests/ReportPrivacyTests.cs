using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class ReportPrivacyTests
{
    [Theory]
    [InlineData(@"at Example.Run() in C:\private\checkout\recreate\product\src\Memoria.FF8.Runtime\Example.cs:line 42", "<external>/<path>:line 42")]
    [InlineData("at Example.Run() in /home/example/checkout/src/Memoria.FF8.Runtime/Example.cs:line 42", "<external>/<path>:line 42")]
    [InlineData(@"Cannot open 'D:\Personal Folder\private-file.dat'", "<external>/<path>")]
    [InlineData(@"Cannot open '\\private-server\share\private-file.dat'", "<external>/<path>")]
    [InlineData("Cannot open '/Users/example/Personal Folder/private-file.dat'", "<external>/<path>")]
    [InlineData("Cannot open '/private-file.dat'", "<external>/<path>")]
    [InlineData(@"Cannot open '\private-file.dat'", "<external>/<path>")]
    [InlineData("Cannot open '../../private-file.dat'", "<external>/<path>")]
    public void SourceCoordinatesRemainUsefulWithoutExposingAbsolutePaths(string input, string expected)
    {
        string result = ReportPrivacy.Text(input);
        Assert.Contains(expected, result);
        Assert.DoesNotContain("private-server", result);
        Assert.DoesNotContain("private-file", result);
        Assert.DoesNotContain("checkout", result);
        Assert.DoesNotContain("/home/example", result);
    }

    [Fact]
    public void EscapedJsonStringsAndPropertyNamesAreSanitizedStructurally()
    {
        JsonElement data = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            [@"C:\private\personal.txt"] = new { File = @"../fixtures/external-fixture.txt", User = "account-alias", Profile = "personal-profile", SteamId = "12345678901234567", Names = new Dictionary<string, string> { ["0"] = "personal-name" } },
            ["version"] = "1.0.0.0"
        });
        string result = ReportPrivacy.Json(data).GetRawText();
        Assert.DoesNotContain("personal", result);
        Assert.DoesNotContain("account-alias", result);
        Assert.DoesNotContain("12345678901234567", result);
        Assert.DoesNotContain("private", result);
        Assert.Contains("1.0.0.0", result);
        using JsonDocument parsed = JsonDocument.Parse(result);
        Assert.Equal("Player", parsed.RootElement.EnumerateObject().First().Value.GetProperty("Names").GetProperty("0").GetString());
    }

    [Fact]
    public void CommonCredentialsAndAccountIdentifiersAreRemoved()
    {
        string result = ReportPrivacy.Text("mail@example.invalid token=hidden-value Bearer hidden-auth https://example.invalid/?key=hidden-query 12345678901234567");
        Assert.DoesNotContain("example.invalid", result);
        Assert.DoesNotContain("hidden", result);
        Assert.DoesNotContain("12345678901234567", result);
    }

    [Fact]
    public void ExecutionPathsUseAnAnonymousRoot()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "recreate", "product", "src", "Example.cs");
        Assert.Equal("<app>/recreate/product/src/Example.cs:line 42", ReportPrivacy.Text(path + ":line 42"));
    }

    [Fact]
    public async Task EveryPublicSurfaceIsSanitizedBeforeTruncationAndPackaging()
    {
        string root = Path.Combine(Path.GetTempPath(), "report-privacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            ErrorBreadcrumbs events = new();
            events.Add("test", @"Missing ../fixtures/external-fixture.txt");
            ErrorIncident incident = ErrorIncident.Capture(new IOException(@"Missing ../fixtures/external-fixture.txt"), "test", new { Path = @"\\private-server\share\private-file.txt", UserName = "private-account" }, breadcrumbs: events, launchId: "private-launch-alias");
            using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
            Assert.DoesNotContain("private-file", report.Description);
            Assert.DoesNotContain("private-account", report.Description);
            Assert.DoesNotContain("private-file", Uri.UnescapeDataString(report.IssueUrl));
            using ZipArchive zip = ZipFile.OpenRead(report.ArchivePath);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                using StreamReader reader = new(entry.Open(), Encoding.UTF8);
                string content = reader.ReadToEnd();
                Assert.DoesNotContain("private-file", content);
                Assert.DoesNotContain("private-server", content);
                Assert.DoesNotContain("private-account", content);
                Assert.DoesNotContain("private-launch-alias", content);
            }
            using JsonDocument completed = JsonDocument.Parse(zip.GetEntry("completed.json")!.Open());
            Assert.Equal(report.IssueUrl, completed.RootElement.GetProperty("issueUrl").GetString());
            using JsonDocument manifest = JsonDocument.Parse(zip.GetEntry("manifest.json")!.Open());
            foreach (JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                Assert.Equal(zip.GetEntry(file.GetProperty("name").GetString()!)!.Length, file.GetProperty("bytes").GetInt64());
            }
        }
        finally
        {
            foreach (string directory in new[] { root, root + ".pending" })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }

    [Fact]
    public async Task CachedArchiveCannotBypassTheCurrentPrivacyBoundary()
    {
        string root = Path.Combine(Path.GetTempPath(), "report-cache-privacy-" + Guid.NewGuid().ToString("N"));
        try
        {
            ErrorIncident incident = ErrorIncident.Capture(new Exception("test"), "test");
            string path;
            using (PreparedErrorReport initial = await ErrorReport.PrepareAsync(incident, root))
            {
                path = initial.ArchivePath;
            }
            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                zip.GetEntry("report.md")!.Delete();
                using StreamWriter writer = new(zip.CreateEntry("report.md").Open());
                writer.Write(@"../fixtures/external-fixture.txt");
            }
            using PreparedErrorReport regenerated = await ErrorReport.PrepareAsync(incident, root);
            using ZipArchive result = ZipFile.OpenRead(regenerated.ArchivePath);
            using StreamReader reader = new(result.GetEntry("report.md")!.Open());
            Assert.DoesNotContain("private-file", reader.ReadToEnd());
            Assert.DoesNotContain("private-file", regenerated.Description);
        }
        finally
        {
            foreach (string directory in new[] { root, root + ".pending" })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
