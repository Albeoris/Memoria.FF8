using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Mods.Tests;

public sealed partial class ModPackageInstallerTests
{
    [Theory]
    [InlineData("archive-published")]
    [InlineData("receipt-published")]
    [InlineData("archive-retired")]
    public async Task RecoveringInterruptedArchiveRelocationPreservesTheSidecarAndReceipt(string boundary)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const string sourceProfile = "Classic/en/original";
        const string targetProfile = "Classic/en/new";
        string selected = Path.Combine(root, "Selected");
        string previousState = ModPackageInstaller.StateDirectoryFor(Mods);
        string selectedState = ModPackageInstaller.StateDirectoryFor(selected);
        ModPackageInstaller previous = new(previousState, Mods, sourceProfile);
        ModPackageInstaller destination = new(selectedState, selected, targetProfile);
        using ModDirectoryResourceLease previousLease = ModDirectoryResourceLease.Acquire(Mods, true);
        using ModDirectoryResourceLease destinationLease = ModDirectoryResourceLease.Acquire(selected, true);
        ModPackageInstallPlan first = await previous.InspectAsync(Iroj(), Entry(), cancellationToken);
        ModPackageInstallResult firstInstall = await previous.RequestInstallAsync(first, cancellationToken: cancellationToken);
        Assert.True(Assert.Single(previous.RecoverAndApply()).Installed);
        Assert.True(Assert.Single(previous.CompleteStartupApplications([first.PackageId])).Installed);
        const string playerSettings = "[Mod]\r\nenabled=0\r\n[Options]\r\nCustomMarker=17\r\n";
        File.WriteAllText(first.Destination + ".ini", playerSettings);

        RemoteModEntry update = Entry("2");
        ModPackageInstallPlan next = await previous.InspectAsync(Iroj(update.Version), update, cancellationToken);
        await previous.RequestInstallAsync(next, cancellationToken: cancellationToken, relocateTo: selected);
        Assert.True(Assert.Single(previous.RecoverAndApply()).Installed);
        Assert.True(Assert.Single(previous.CompleteStartupApplications([next.PackageId])).RestartRequired);

        string journalPath = Assert.Single(Directory.GetFiles(Path.Combine(previousState, "journals"), "*.json"));
        JsonObject journal = JsonNode.Parse(File.ReadAllText(journalPath))!.AsObject();
        Guid transactionId = Guid.Parse(journal["TransactionId"]!.GetValue<string>());
        JsonObject receipt = journal["Next"]!.DeepClone().AsObject();
        receipt["Target"] = targetProfile;
        string targetPackage = Path.Combine(selected, "Junction", Path.GetFileName(next.Destination));
        string stage = Path.Combine(selectedState, "incoming-payloads", transactionId.ToString("N"));
        string reservation = Path.Combine(selectedState, "incoming", Path.GetFileName(journalPath));
        string targetReceipt = Path.Combine(selectedState, "receipts", Path.GetFileName(journalPath));
        Directory.CreateDirectory(Path.GetDirectoryName(reservation)!);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPackage)!);
        Directory.CreateDirectory(stage);
        File.WriteAllText(reservation, new JsonObject
        {
            ["Schema"] = 1,
            ["TransactionId"] = transactionId,
            ["SourceRoot"] = Mods,
            ["Receipt"] = receipt.DeepClone(),
            ["SettingsHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(playerSettings))),
            ["Publishing"] = true
        }.ToJsonString());
        File.Copy(next.Destination, targetPackage);
        if (boundary == "archive-published")
        {
            File.Copy(next.Destination + ".ini", Path.Combine(stage, "content.iroj.ini"));
        }
        else
        {
            File.Copy(next.Destination + ".ini", targetPackage + ".ini");
            Directory.CreateDirectory(Path.GetDirectoryName(targetReceipt)!);
            File.WriteAllText(targetReceipt, receipt.ToJsonString());
        }
        if (boundary == "archive-retired")
        {
            File.Move(next.Destination, Path.Combine(previousState, "transactions", transactionId.ToString("N"), "relocated"));
        }

        ModPackageInstaller restarted = new(previousState, Mods, sourceProfile);
        ModPackageInstallResult moved = Assert.Single(restarted.RecoverRelocations([restarted, destination]));
        Assert.True(moved.Relocated, moved.Error);
        Assert.Null(moved.Error);
        Assert.Null(restarted.GetStatus(update));
        Assert.False(File.Exists(next.Destination));
        Assert.False(File.Exists(next.Destination + ".ini"));
        Assert.True(File.Exists(targetPackage));
        Assert.Equal(playerSettings, File.ReadAllText(targetPackage + ".ini"));
        ModPackageInstallResult installed = Assert.IsType<ModPackageInstallResult>(destination.GetStatus(update));
        Assert.True(installed.CanUninstall, installed.Error);
        Assert.Equal(firstInstall.InstallationId, installed.InstallationId);
        Assert.Equal(update.Version, installed.Version);
        Assert.Empty(restarted.RecoverRelocations([restarted, destination]));
        Assert.False(File.Exists(reservation));
        Assert.False(Directory.Exists(stage));
    }
}
