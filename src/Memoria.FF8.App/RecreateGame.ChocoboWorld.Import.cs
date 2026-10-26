using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ChocoboImportPreview? chocoboTransferPreview;
    private string? chocoboTransferPath;
    private bool promptNewGameChocoboClaim;
    private ChocoboPresentationText? lastChocoboResultText;

    private void InitializeChocoboWorldIntegration()
    {
        gameMenus!.ConnectChocoboWorldAsync(DiscoverChocoboWorldSaves, BrowseChocoboWorldSave, path => PreviewChocoboWorldFile(path, false), CommitChocoboWorldFile, PreviewChocoboWorldClaim, CommitChocoboWorldClaim, () => PreviewChocoboWorldFile(chocoboTransferPath!, true), () => lastChocoboResultText);
    }

    private IReadOnlyList<ChocoboImportSource> DiscoverChocoboWorldSaves()
    {
        List<ChocoboImportSource> sources = [];
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] folders = [Path.Combine(options.GameDirectory, "save"), Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam"), Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII")];
        foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                foreach (string file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true }))
                {
                    string name = Path.GetFileName(file);
                    if (name.Equals("chocorpg", StringComparison.OrdinalIgnoreCase) || name.Equals("chocorpg.ff8", StringComparison.OrdinalIgnoreCase))
                    {
                        ChocoboPresentationText label = ChocoboText.Create("chocobo.source.pc", name, Path.GetDirectoryName(file));
                        sources.Add(new(file, label.Fallback) { LabelText = label });
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                // Discovery is best effort; Browse remains available for inaccessible roots.
            }
        }
        return sources.DistinctBy(source => source.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static byte[] ReadChocoboWorldFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > 0xE080)
        {
            throw ChocoboText.Create("chocobo.error.file-too-large").InvalidData();
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private ChocoboImportReview PreviewChocoboWorldFile(string path, bool reviewed)
    {
        chocoboTransferPath = path;
        chocoboTransferPreview = null;
        try
        {
            NativeChocoboWorldImport imported = NativeChocoboWorldReader.Read(ReadChocoboWorldFile(path));
            using GameAssets assets = new(options.GameDirectory, resources: resourceHost?.Resources);
            ChocoboImportPreview preview = session.PreviewChocoboWorldImport(imported, assets.ReadNativeChocoboWorldTransferTable(), reviewed);
            chocoboTransferPreview = preview;
            EnsureLiveMenuCatalog();
            return ChocoboWorldPresentation.Import(path, session.State, imported, preview, reviewed, ChocoboWorldBoundaryUnavailableText, itemId => liveMenuCatalog?.ItemName(itemId), itemId => liveMenuCatalog?.ItemIcon(itemId));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            ChocoboPresentationText reason = ChocoboPresentationText.FromException(exception);
            return new([Path.GetFileName(path)], [], false, reason.Fallback) { HeaderText = [ChocoboWorldPresentation.Literal(Path.GetFileName(path))], ReasonText = reason };
        }
    }

    private string CommitChocoboWorldFile()
    {
        if (ChocoboWorldBoundaryUnavailableText is { } reason)
        {
            lastChocoboResultText = reason;
            return reason.Fallback;
        }
        try
        {
            ChocoboImportPreview preview = chocoboTransferPreview ?? throw ChocoboText.Create("chocobo.import.refresh-required").InvalidOperation();
            string hash = Convert.ToHexString(SHA256.HashData(ReadChocoboWorldFile(chocoboTransferPath!)));
            string result = session.CommitChocoboWorldImport(preview, hash);
            lastChocoboResultText = preview.MessageText ?? ChocoboWorldPresentation.Literal(result);
            chocoboTransferPreview = null;
            gameMenus?.InvalidateLiveData();
            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            lastChocoboResultText = ChocoboPresentationText.FromException(exception);
            return lastChocoboResultText.Fallback;
        }
    }

    private ChocoboClaimReview PreviewChocoboWorldClaim()
    {
        EnsureLiveMenuCatalog();
        ChocoboRewardGrantResult preview = ChocoboRewardTransactions.ClaimRemaining(session.State);
        return ChocoboWorldPresentation.Claim(session.State, preview, ChocoboWorldBoundaryUnavailableText, itemId => liveMenuCatalog?.ItemName(itemId), itemId => liveMenuCatalog?.ItemDescription(itemId), itemId => liveMenuCatalog?.ItemIcon(itemId));
    }

    private string CommitChocoboWorldClaim()
    {
        if (ChocoboWorldBoundaryUnavailableText is { } reason)
        {
            lastChocoboResultText = reason;
            return reason.Fallback;
        }
        ChocoboRewardGrantResult result = session.ClaimRemainingChocoboRewards();
        gameMenus?.InvalidateLiveData();
        lastChocoboResultText = result.MessageText ?? ChocoboWorldPresentation.Literal(result.Message);
        return result.Message;
    }

    private void ShowPendingNewGameChocoboClaim()
    {
        if (promptNewGameChocoboClaim && session.State.Scene is SceneKind.Field or SceneKind.WorldMap && session.State.Economy is not null && ChocoboWorldBoundaryUnavailableReason is null && gameMenus?.IsOpen != true)
        {
            promptNewGameChocoboClaim = false;
            gameMenus?.ShowChocoboClaim();
        }
    }
}
