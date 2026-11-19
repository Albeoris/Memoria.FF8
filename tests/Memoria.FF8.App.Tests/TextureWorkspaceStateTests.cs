using Memoria.FF8.Resources;
using Memoria.FF8.Mods;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.App.Tests;

public sealed class TextureWorkspaceStateTests
{
    private static readonly TextureTarget Target = new("ff8", "battle/d0w001.dat", "texture/0");

    [Fact]
    public void Destination_baselines_are_independent_and_reverting_a_previous_import_restores_original()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string firstMod = Path.Combine(root, "mods", "first");
            string secondMod = Path.Combine(root, "mods", "second");
            Assert.True(TextureWorkspaceState.Evaluate(Target, "B", "A", null, null, null).Selected);
            TextureWorkspaceState.Write(root, firstMod, new Dictionary<string, TextureWorkingEntry> { ["repaint.png"] = new(Target, "B", "destination-B", true) });
            Dictionary<string, TextureWorkingEntry> other = TextureWorkspaceState.Read(root, secondMod);
            TextureWorkingEntry rememberedMapping = other["repaint.png"];
            Assert.Equal(Target, rememberedMapping.Target);
            Assert.Null(rememberedMapping.ImportedPixels);
            Assert.True(TextureWorkspaceState.Evaluate(Target, "B", "A", null, null, rememberedMapping).Selected);
            Assert.False(TextureWorkspaceState.Evaluate(Target, "A", "A", null, null, rememberedMapping).Selected);
            TextureWorkspaceState.Write(root, secondMod, new Dictionary<string, TextureWorkingEntry> { ["repaint.png"] = new(Target, "C", "destination-C", true) });
            TextureWorkingEntry first = TextureWorkspaceState.Read(root, firstMod)["repaint.png"];
            Assert.Equal("B", first.ImportedPixels);
            Assert.True(TextureWorkspaceState.Evaluate(Target, "A", "A", "destination-B", "B", first).Selected);
            Assert.Equal("C", TextureWorkspaceState.Read(root, secondMod)["repaint.png"].ImportedPixels);
            string state = File.ReadAllText(Path.Combine(root, TextureWorkspaceState.FileName));
            Assert.DoesNotContain(firstMod, state);
            Assert.DoesNotContain(secondMod, state);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Three_way_review_retains_destination_only_edits_and_leaves_both_sides_changed_unchecked()
    {
        TextureWorkingEntry previous = new(Target, "B", "destination-B");
        TextureImportDecision destinationOnly = TextureWorkspaceState.Evaluate(Target, "B", "A", "destination-C", "C", previous);
        Assert.False(destinationOnly.Selected);
        Assert.Contains("destination edit retained", destinationOnly.Status);
        TextureImportDecision both = TextureWorkspaceState.Evaluate(Target, "D", "A", "destination-C", "C", previous);
        Assert.False(both.Selected);
        Assert.Contains("Conflict", both.Status);
        TextureImportDecision recompressedSame = TextureWorkspaceState.Evaluate(Target, "D", "A", "recompressed-D", "D", previous);
        Assert.False(recompressedSame.Selected);
        Assert.Contains("Same decoded appearance", recompressedSame.Status);
    }

    [Fact]
    public void Legacy_unscoped_state_preserves_explicit_mapping_without_assigning_its_baseline_to_another_mod()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, TextureWorkspaceState.FileName), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, TextureWorkingEntry> { ["repaint.png"] = new(Target, "B", "destination-B", true) }));
            TextureWorkingEntry restored = TextureWorkspaceState.Read(root, Path.Combine(root, "any-mod"))["repaint.png"];
            Assert.Equal(Target, restored.Target);
            Assert.True(restored.ExplicitMapping);
            Assert.Null(restored.ImportedPixels);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Native_coverage_only_edit_is_a_semantic_change_and_stripped_metadata_uses_source_flags()
    {
        PixelImage native = new(1, 1, [new Rgba32(40, 80, 120, 128)]);
        TextureBuildContext source = new(Target.Key, new("synthetic", GameDataEdition.Classic, []), 1, "classic-field-tile", 1, 1, () => native);
        TextureTargetContract contract = TextureTargetContract.FromContext(source);
        PixelImage editable = TexturePixelExchange.ToEditable(native);
        TextureNativeCoverage before = new(1, 1, Convert.ToBase64String([128]));
        TextureNativeCoverage after = new(1, 1, Convert.ToBase64String([255]));
        string original = TextureImportSemantics.Fingerprint(editable, Target, contract, source, before);
        Assert.Equal(original, TextureImportSemantics.Fingerprint(editable, Target, contract, source));
        string changed = TextureImportSemantics.Fingerprint(editable, Target, contract, source, after);
        Assert.NotEqual(original, changed);
        TextureWorkingEntry previous = new(Target, original, "png-before", DestinationSemantic: original);
        Assert.True(TextureWorkspaceState.Evaluate(Target, changed, original, "png-before", original, previous).Selected);
        TextureImportDecision destinationOnly = TextureWorkspaceState.Evaluate(Target, original, original, "png-before", changed, previous);
        Assert.False(destinationOnly.Selected);
        Assert.Contains("destination edit retained", destinationOnly.Status);
    }

    [Fact]
    public void Original_provider_proof_distinguishes_remastered_hd_alpha_from_native_tim_fallback()
    {
        PixelImage native = new(1, 1, [new Rgba32(10, 20, 30, 128)]);
        TextureBuildContext source = new(Target.Key, new("synthetic-remastered", GameDataEdition.Remastered, []), 1, "classic-battle", 1, 1, () => native, new TextureSourceRegion(BitsPerPixel: 4));
        TextureValue timFallback = new(native, 1, 1, "native");
        TextureValue installedHd = new(new PixelImage(2, 2, Enumerable.Repeat(new Rgba32(10, 20, 30, 128), 4).ToArray()), 1, 1, "source-provider") { IsReplacement = true };
        Assert.True(TexturePixelExchange.UsesNativeFlags(source, timFallback));
        Assert.False(TexturePixelExchange.UsesNativeFlags(source, installedHd));
        TextureTargetContract contract = TextureTargetContract.FromContext(source);
        string hdSemantic = TextureImportSemantics.Fingerprint(installedHd.Image, Target, contract, source, installedOriginal: installedHd);
        Assert.Equal(TexturePixelExchange.SemanticFingerprint(installedHd.Image, Target, contract), hdSemantic);
    }

    [Fact]
    public void Recompressed_destination_is_not_a_conflict_when_its_semantics_are_unchanged()
    {
        TextureWorkingEntry previous = new(Target, "semantic-B", "encoded-B", DestinationSemantic: "semantic-B");
        TextureImportDecision decision = TextureWorkspaceState.Evaluate(Target, "semantic-C", "semantic-A", "recompressed-B", "semantic-B", previous);
        Assert.True(decision.Selected);
        Assert.DoesNotContain("Conflict", decision.Status);
    }

    [Fact]
    public void Pixel_only_version_two_baselines_are_reset_without_losing_explicit_assignments()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-v2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string destination = Path.Combine(root, "mod");
            byte[] document = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 2,
                destinations = new Dictionary<string, Dictionary<string, TextureWorkingEntry>> { [TextureWorkspaceState.DestinationKey(destination)] = new() { ["repaint.png"] = new(Target, "image-only-B", "encoded-B", true) } },
                mappings = new Dictionary<string, TextureTarget> { ["repaint.png"] = Target }
            });
            File.WriteAllBytes(Path.Combine(root, TextureWorkspaceState.FileName), document);
            TextureWorkingEntry restored = TextureWorkspaceState.Read(root, destination)["repaint.png"];
            Assert.True(restored.ExplicitMapping);
            Assert.Equal(Target, restored.Target);
            Assert.Null(restored.ImportedPixels);
            Assert.Null(restored.DestinationSemantic);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_new_metadata_claim_after_validation_prevents_publication()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string image = Path.Combine(root, "image.png");
            string metadata = image + ".texture.json";
            File.WriteAllBytes(image, [1, 2, 3, 4]);
            using TextureInputLeases leases = new([image, metadata], TestContext.Current.CancellationToken);
            leases.ValidateContents(TestContext.Current.CancellationToken);
            File.WriteAllText(metadata, "new target claim");
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => leases.ValidateContents(TestContext.Current.CancellationToken));
            Assert.Contains("changed after review", exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Input_read_leases_remain_valid_until_disposed_and_release_editor_writes_afterward()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-texture-lease-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string image = Path.Combine(root, "image.png");
            string metadata = image + ".texture.json";
            File.WriteAllBytes(image, [1, 2, 3, 4]);
            File.WriteAllText(metadata, "original metadata");
            using (TextureInputLeases leases = new([image, metadata], TestContext.Current.CancellationToken))
            {
                leases.ValidateContents(TestContext.Current.CancellationToken);
                if (OperatingSystem.IsWindows())
                {
                    Assert.ThrowsAny<IOException>(() => File.WriteAllText(metadata, "changed metadata"));
                }
                leases.ValidateStamps();
            }
            File.WriteAllText(metadata, "changed metadata");
            Assert.Equal("changed metadata", File.ReadAllText(metadata));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
