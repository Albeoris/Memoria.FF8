using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledGuardianFormProfileTests
{
    private static string Payload => Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Guardian Forms", GuardianFormMods.PayloadPath);

    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR")]
    public void InstalledPackageResolvesProfileRowsAndSnapshots(string installedDirectoryVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(installedDirectoryVariable);
        Assert.SkipUnless(Directory.Exists(directory), $"Select installed resources with {installedDirectoryVariable}.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!, game.Resources);
        GuardianFormMods forms = InstalledGuardianForms.Read(game);
        Assert.Equal(new[] { "Memoria/memoria-guardian-forms" }, forms.PackageIds);
        Assert.Equal(new[] { "ifrit", "minotaur", "sacred" }, forms.Resources.Select(resource => resource.VariantId).Order(StringComparer.Ordinal));
        BattleGuardianFormResources ifrit = forms.Resources.Single(resource => resource.VariantId == "ifrit");
        BattleGuardianFormActionBinding breath = Assert.Single(ifrit.InnateBindings, action => action.Action == BattleGuardianFormAction.FireBreath);
        Assert.Equal((15, 9, 1, 70, 230), NativeRow(breath));
        Assert.True(breath.OncePerInvocation);
        Assert.Contains(breath.Action, ifrit.Profile!.TierAtLevel(100).Actions);
        Assert.DoesNotContain(breath.Action, ifrit.Profile.TierAtLevel(99).Actions);

        BattleGuardianFormResources sacred = forms.Resources.Single(resource => resource.VariantId == "sacred");
        BattleGuardianFormResources minotaur = forms.Resources.Single(resource => resource.VariantId == "minotaur");
        Assert.Equal(63, sacred.Profile!.ModelId);
        Assert.Equal(62, minotaur.Profile!.ModelId);
        Assert.Equal(new[] { BattleGuardianFormAction.SacredAttack, BattleGuardianFormAction.SacredMend, BattleGuardianFormAction.BrotherlyLove }, sacred.Profile.TierAtLevel(1).Actions);
        Assert.Equal(new[] { BattleGuardianFormAction.MinotaurAttack, BattleGuardianFormAction.MinotaurMower, BattleGuardianFormAction.BrotherlyLove }, minotaur.Profile.TierAtLevel(1).Actions);
        Assert.Equal(BattleGuardianFormAction.SacredMend, sacred.Profile.TierAtLevel(30).HealBeforeAttack);
        Assert.Equal(BattleGuardianFormAction.MinotaurMend, minotaur.Profile.TierAtLevel(30).HealBeforeAttack);
        Assert.Equal(24, Assert.Single(sacred.InnateBindings, action => action.Action == BattleGuardianFormAction.SacredMasterStrike).Power);
        Assert.Equal(14, Assert.Single(minotaur.InnateBindings, action => action.Action == BattleGuardianFormAction.MinotaurHeavyMower).Power);

        foreach (BattleGuardianFormResources resource in new[] { sacred, minotaur })
        {
            Assert.Equal(4, resource.GuardianForceId);
            Assert.Equal((254, 68, 0, 46, 205), NativeRow(resource.Profile!.Finisher));
            BattleGuardianFormActionBinding madCow = Assert.Single(resource.InnateBindings, action => action.Action == BattleGuardianFormAction.MadCowSpecial);
            Assert.Equal((8, 213, 0, 32, 258), NativeRow(madCow));
            Assert.Equal(new[] { "sacred", "minotaur" }, madCow.PresentedModels);
            Assert.True(madCow.RequiresFullAtb);
            Assert.True(madCow.OncePerInvocation);
            BattleGuardianFormResources restored = JsonSerializer.Deserialize<BattleGuardianFormResources>(JsonSerializer.Serialize(resource))!;
            Assert.Equal(resource.VariantId, restored.VariantId);
            Assert.Equal(resource.Profile.ProfileId, restored.Profile!.ProfileId);
            Assert.Equal(resource.Profile.TierAtLevel(100).Actions, restored.Profile.TierAtLevel(100).Actions);
            Assert.Equal(madCow.PresentedModels, restored.InnateBindings.Single(binding => binding.Action == madCow.Action).PresentedModels);
        }

        (int Command, int Action, int Crisis, int Power, int Effect) NativeRow(BattleGuardianFormActionBinding binding)
        {
            BattleNativeAction action = binding.ResolveNativeAction(assets.NativeActions);
            return (action.Command, action.ActionId, action.CrisisLevel ?? 0, action.Power, action.MagicId);
        }
    }

    [Fact]
    public void RemasteredCatalogPreservesSettingsAcrossBundledAndInstalledPackageRoots()
    {
        const string packageId = "Memoria/memoria-guardian-forms";
        DirectoryInfo temporary = Directory.CreateTempSubdirectory("memoria-guardian-forms-");
        try
        {
            string application = Path.Combine(temporary.FullName, "app");
            string mods = Path.Combine(temporary.FullName, "mods");
            string bundled = Path.Combine(application, "BundledMods", "Memoria", "Guardian Forms");
            string installed = Path.Combine(mods, "Memoria", "Guardian Forms Upgrade");
            string mixed = Path.Combine(application, "BundledMods", "Memoria", "Unsupported Mix");
            WritePackage(bundled);
            WritePackage(mixed);
            File.WriteAllText(Path.Combine(mixed, "mod.json"), File.ReadAllText(Path.Combine(mixed, "mod.json")).Replace("memoria-guardian-forms", "unsupported-guardian-forms", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(mixed, "gameplay", "additional.json"), "{}");
            using (ModRuntime first = Open())
            {
                ModPackage package = Assert.Single(first.Catalog.Packages, package => package.Id == packageId);
                Assert.True(package.IsBundled);
                Assert.True(package.GuardianFormContentOnly);
                Assert.Equal(ModEffectiveStatus.Active, package.Status);
                Assert.Contains(first.Catalog.Packages, package => package.Id == "Memoria/unsupported-guardian-forms" && package.Status == ModEffectiveStatus.IncompatibleEdition);
                BehaviorModSetting setting = Assert.Single(first.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == "memoria-guardian-forms");
                Assert.True(first.SetBehaviorSetting(ModRuntime.MemoriaPackageId, setting.Behavior, setting.Parameter, 0).Success);
                Commit(first);
                Assert.Empty(first.Catalog.FindAll(ModFormat.Memoria, GuardianFormMods.PayloadPath));
            }
            WritePackage(installed);
            string replacement = Path.Combine(installed, GuardianFormMods.PayloadPath);
            File.WriteAllText(replacement, File.ReadAllText(replacement).Replace("\"lifetimeTicks\": 900", "\"lifetimeTicks\": 600", StringComparison.Ordinal));
            using ModRuntime restarted = Open();
            ModPackage disabled = Assert.Single(restarted.Catalog.Packages, package => package.Id == packageId);
            Assert.Equal(ModEffectiveStatus.Disabled, disabled.Status);
            Assert.Equal(installed, disabled.RootDirectory);
            Assert.True(restarted.SetEnabled(packageId, true).Success);
            Commit(restarted);
            ModFile payload = Assert.Single(restarted.Catalog.FindAll(ModFormat.Memoria, GuardianFormMods.PayloadPath));
            Assert.All(GuardianFormMods.Parse(payload.ReadBytes()).Variants, variant => Assert.Equal(600, variant.Profile.LifetimeTicks));

            ModRuntime Open()
            {
                ModRuntime runtime = new(application, isCompatible: _ => false, watch: false, modsDirectory: mods, edition: "Remastered");
                runtime.RequestRefresh();
                Commit(runtime);
                return runtime;
            }

            static void Commit(ModRuntime runtime)
            {
                Assert.True(runtime.PreparePending(force: true));
                Assert.True(runtime.CommitPending());
            }

            static void WritePackage(string root)
            {
                Directory.CreateDirectory(Path.Combine(root, "gameplay"));
                File.Copy(Payload, Path.Combine(root, GuardianFormMods.PayloadPath));
                File.Copy(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Payload))!, "mod.json"), Path.Combine(root, "mod.json"));
            }
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    [Fact]
    public void PackageAcceptsIndependentNumericActionIdsAndReportsOverlappingVariantsBeforeResourceReads()
    {
        JsonObject document = JsonNode.Parse(File.ReadAllBytes(Payload))!.AsObject();
        JsonArray variants = document["variants"]!.AsArray();
        JsonObject independent = variants[0]!.DeepClone().AsObject();
        independent["profile"]!["variantId"] = "ember";
        independent["profile"]!["lifetimeTicks"] = 600;
        independent["profile"]!["tiers"]![0]!["actions"]![0] = 42;
        independent["profile"]!["tiers"]![0]!["compelledPhysical"] = 42;
        independent["actions"]![0]!["action"] = 42;
        document["variants"] = new JsonArray(independent);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        GuardianFormModVariant variant = Assert.Single(GuardianFormMods.Parse(bytes).Variants);
        Assert.Equal(600, variant.Profile.LifetimeTicks);
        Assert.Equal((BattleGuardianFormAction)42, variant.Profile.TierAtLevel(1).CompelledPhysical);

        ModCatalogSnapshot overlapping = new(1, [], [
            new("Memoria/first", ModFormat.Memoria, GuardianFormMods.PayloadPath, 1, () => bytes),
            new("Memoria/second", ModFormat.Memoria, GuardianFormMods.PayloadPath, 2, () => bytes)
        ]);
        Assert.False(GuardianFormMods.TryPrepare(overlapping, () => throw new InvalidOperationException("Conflicts must fail before reading resources."), out GuardianFormMods rejected, out IReadOnlyList<ModDiagnostic> diagnostics));
        Assert.Empty(rejected.Resources);
        Assert.Equal(new[] { "Memoria/first", "Memoria/second" }, diagnostics.Select(diagnostic => diagnostic.PackageId));

        independent["profile"]!["variantId"] = "second-ember";
        independent["profile"]!["profileId"] = "other-profile";
        byte[] mismatched = JsonSerializer.SerializeToUtf8Bytes(document);
        ModCatalogSnapshot mixedProfiles = new(2, [], [
            new("Memoria/first", ModFormat.Memoria, GuardianFormMods.PayloadPath, 1, () => bytes),
            new("Memoria/second", ModFormat.Memoria, GuardianFormMods.PayloadPath, 2, () => mismatched)
        ]);
        Assert.False(GuardianFormMods.TryPrepare(mixedProfiles, () => throw new InvalidOperationException("Profile identity must be checked before resource loading."), out _, out diagnostics));
        Assert.Equal(new[] { "Memoria/first", "Memoria/second" }, diagnostics.Select(diagnostic => diagnostic.PackageId));
    }
}
