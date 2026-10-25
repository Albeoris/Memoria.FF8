namespace Memoria.FF8.BattleEffects;

public sealed class CampaignPlanning(EvidenceStore store)
{
    private static readonly string[] PlanInputs = ["inventory.json", "build.json", "profile.json", "resources.json"];

    public void Create(string campaignId, int revision, string? parent, InventoryRecord inventory, BuildRecord build, PerformanceProfile profile, ResourceRecord resources, CaseRecipe[] cases)
    {
        EvidenceStore.Id(campaignId);
        if (revision < 1 || (revision > 1 && string.IsNullOrWhiteSpace(parent)))
        {
            throw new InvalidDataException("A corrected revision requires a parent campaign reference.");
        }

        if (store.ListPaths("").Count != 0)
        {
            throw new IOException("Create each campaign revision in a new empty store directory.");
        }

        foreach (CaseRecipe recipe in cases)
        {
            EvidenceStore.Id(recipe.EffectKey);
            EvidenceStore.Id(recipe.CaseId);
        }

        store.WriteNew("inventory.json", inventory);
        store.WriteNew("build.json", build);
        store.WriteNew("profile.json", profile);
        store.WriteNew("resources.json", resources);
        foreach (IGrouping<string, CaseRecipe> group in cases.GroupBy(c => c.EffectKey).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            store.WriteNew($"effects/{group.Key}/plan.json", new EffectPlan(1, group.Key, group.OrderBy(c => c.CaseId, StringComparer.Ordinal).ToArray()));
        }

        store.WriteNew("candidate.json", new CampaignRecord(1, campaignId, revision, parent, ComputePlanHash(), EvidenceStore.HashRecord(build), EvidenceStore.HashRecord(profile), EvidenceStore.HashRecord(resources), profile.InstrumentationVersion, DateTimeOffset.UtcNow));
    }

    public CaseRecipe[] ReadCases()
    {
        List<CaseRecipe> cases = [];
        foreach (string relative in store.ListPaths("effects/").Where(path => path.EndsWith("/plan.json", StringComparison.Ordinal)))
        {
            SchemaValidation.ValidateBytes(store.ReadBytes(relative), "effect-plan");
            EffectPlan plan = store.Read<EffectPlan>(relative);
            if (relative != $"effects/{plan.EffectKey}/plan.json" || plan.Cases.Any(c => c.EffectKey != plan.EffectKey))
            {
                throw new InvalidDataException("Effect plan path/identity mismatch.");
            }

            cases.AddRange(plan.Cases);
        }

        return cases.ToArray();
    }

    public string ComputePlanHash()
    {
        List<BuildFile> files = [];
        foreach (string relative in PlanInputs)
        {
            byte[] bytes = store.ReadBytes(relative);
            files.Add(new BuildFile(relative, bytes.LongLength, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))));
        }

        foreach (CaseRecipe recipe in ReadCases().OrderBy(c => c.EffectKey, StringComparer.Ordinal).ThenBy(c => c.CaseId, StringComparer.Ordinal))
        {
            string relative = $"effects/{recipe.EffectKey}/plan.json";
            if (!files.Any(f => f.Path == relative))
            {
                byte[] bytes = store.ReadBytes(relative);
                files.Add(new BuildFile(relative, bytes.LongLength, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))));
            }
        }

        return EvidenceStore.HashRecord(files);
    }

    public PlanValidation Validate()
    {
        List<PlanIssue> issues = [];
        foreach (string input in PlanInputs)
        {
            SchemaValidation.ValidateBytes(store.ReadBytes(input), Path.GetFileNameWithoutExtension(input));
        }

        InventoryRecord inventory = store.Read<InventoryRecord>("inventory.json");
        BuildRecord build = store.Read<BuildRecord>("build.json");
        ResourceRecord resources = store.Read<ResourceRecord>("resources.json");
        PerformanceProfile profile = store.Read<PerformanceProfile>("profile.json");
        CaseRecipe[] cases = ReadCases();
        Add(inventory.ResourceHash != EvidenceStore.HashRecord(resources), "inventory", "Resource identity differs from the declared manifest.");
        Add(build.Dirty && (build.PatchHash == null || !System.Text.RegularExpressions.Regex.IsMatch(build.PatchHash, "^[0-9a-f]{64}$")), "build", "Dirty builds require a patch SHA256.");
        Add(!resources.EnabledMods.SequenceEqual(profile.Mods), "profile", "Resource and performance profiles declare different mods or order.");
        Add(profile.HostFps < 30, "profile", "Host pacing cannot satisfy 30 FPS.");
        foreach (IGrouping<string, CaseRecipe> duplicate in cases.GroupBy(c => c.CaseId).Where(g => g.Count() > 1))
        {
            issues.Add(new(duplicate.Key, "Duplicate case identity."));
        }

        foreach (IGrouping<string, InventoryEntry> duplicate in inventory.Entries.GroupBy(e => e.EntryId).Where(g => g.Count() > 1))
        {
            issues.Add(new(duplicate.Key, "Duplicate inventory entry identity."));
        }

        foreach (InventoryEntry entry in inventory.Entries)
        {
            EvidenceStore.Id(entry.EffectKey);
            if (entry.Disposition is "unresolved" or "excluded_with_reason")
            {
                Add(string.IsNullOrWhiteSpace(entry.Reason), entry.EntryId, "A blocker/exclusion requires a reason.");
                Add(entry.Disposition == "unresolved", entry.EntryId, "Unresolved coverage: " + entry.Reason);
                Add(entry.CaseIds.Length != 0, entry.EntryId, "Blocked/excluded entries cannot claim case coverage.");
            }
            else
            {
                Add(entry.CaseIds.Length == 0, entry.EntryId, "Scheduled coverage needs at least one case.");
                foreach (string caseId in entry.CaseIds)
                {
                    CaseRecipe? recipe = cases.FirstOrDefault(c => c.CaseId == caseId);
                    Add(recipe == null, entry.EntryId, $"Missing case: {caseId}");
                    if (recipe != null)
                    {
                        Add(recipe.EffectKey != entry.EffectKey || recipe.RequestedEffectId != entry.NativeEffectId || recipe.ActionId != entry.ActionId, caseId, "Inventory action/effect mapping disagrees with case identity.");
                    }
                }
            }

            Add(entry.MappingEvidence.Length == 0, entry.EntryId, "Mapping evidence is required, including for aliases and exclusions.");
        }

        foreach (CaseRecipe recipe in cases)
        {
            Add(!inventory.Entries.Any(e => e.CaseIds.Contains(recipe.CaseId)), recipe.CaseId, "Case has no inventory coverage owner.");
            foreach (string blocker in recipe.Blockers)
            {
                issues.Add(new(recipe.CaseId, blocker));
            }

            Add(recipe.ScenarioKind == "player" && recipe.Scenario.Caster != "Squall", recipe.CaseId, "The canonical player case must originate from Squall.");
            CaseTimeouts t = recipe.Timeouts;
            Add(t.ProcessMs < t.PreparationMs + recipe.Executions * (t.ActionMs + t.CleanupMs) + t.TeardownMs, recipe.CaseId, "Process timeout is shorter than its declared phase budgets.");
            Add(recipe.Invocation.DiagnosticCapability && recipe.Invocation.AuxiliaryResources.Length == 0, recipe.CaseId, "Borrowed capability requires its original resources.");
        }

        Add(cases.Length == 0, "plan", "A plan needs cases; no synthetic sample is substituted for installed-resource discovery.");
        string campaignFile = store.Exists("campaign.json") ? "campaign.json" : "candidate.json";
        SchemaValidation.ValidateBytes(store.ReadBytes(campaignFile), "campaign");
        CampaignRecord campaign = store.Read<CampaignRecord>(campaignFile);
        Add(campaign.PlanHash != ComputePlanHash(), "campaign", "Plan content changed after its revision was created.");
        Add(campaign.BuildHash != EvidenceStore.HashRecord(build) || campaign.ProfileHash != EvidenceStore.HashRecord(profile) || campaign.ResourceHash != EvidenceStore.HashRecord(resources) || campaign.InstrumentationVersion != profile.InstrumentationVersion, "campaign", "Pinned input identities disagree.");
        return new(issues.Count == 0, issues.ToArray(), cases.Select(c => c.EffectKey).Distinct().Count(), cases.Length, inventory.Entries.Count(e => e.Disposition == "unresolved"));

        void Add(bool condition, string identity, string message)
        {
            if (condition)
            {
                issues.Add(new(identity, message));
            }
        }
    }

    public CampaignRecord Seal()
    {
        PlanValidation validation = Validate();
        if (!validation.Valid)
        {
            throw new InvalidDataException("Plan has blockers: " + string.Join("; ", validation.Issues.Select(i => $"{i.Identity}: {i.Message}")));
        }

        CampaignRecord campaign = store.Read<CampaignRecord>("candidate.json");
        store.WriteNew("campaign.json", campaign);
        return campaign;
    }

    public PassRecord CreatePass(string passId, string kind, BuildRecord build, string[]? selectedCases)
    {
        EvidenceStore.Id(passId);
        SchemaValidation.ValidateRecord(build, "build");
        if (build.Dirty != (build.PatchHash != null) || build.PatchHash is { } patch && !System.Text.RegularExpressions.Regex.IsMatch(patch, "^[0-9a-f]{64}$"))
        {
            throw new InvalidDataException("Dirty build identity requires an exact patch SHA256; clean builds cannot declare a patch.");
        }
        if (kind is not ("baseline" or "post_fix" or "diagnostic" or "final"))
        {
            throw new ArgumentException("Unknown execution pass kind.");
        }

        PlanValidation validation = Validate();
        if (!validation.Valid)
        {
            throw new InvalidDataException("The sealed plan no longer validates.");
        }

        CampaignRecord campaign = store.Read<CampaignRecord>("campaign.json");
        string[] required = ReadCases().Select(c => c.CaseId).Order(StringComparer.Ordinal).ToArray();
        string[] selected = selectedCases?.Order(StringComparer.Ordinal).ToArray() ?? required;
        if (selected.Length == 0 || selected.Distinct().Count() != selected.Length || selected.Except(required).Any() || kind == "final" && !selected.SequenceEqual(required))
        {
            throw new InvalidDataException("Unknown/duplicate cases, or an incomplete final pass.");
        }

        string buildHash = EvidenceStore.HashRecord(build);
        if (kind == "baseline" && buildHash != campaign.BuildHash)
        {
            throw new InvalidDataException("The baseline pass must use the sealed baseline build.");
        }

        string buildPath = $"builds/{buildHash}.json";
        if (!store.Exists(buildPath))
        {
            store.WriteNew(buildPath, build);
        }

        PassRecord pass = new(1, passId, campaign.CampaignId, campaign.PlanHash, kind, buildHash, campaign.ProfileHash, campaign.ResourceHash, campaign.InstrumentationVersion, selected, DateTimeOffset.UtcNow);
        store.WriteNew($"passes/{passId}.json", pass);
        return pass;
    }

    public PassRecord ReadPass(string id)
    {
        EvidenceStore.Id(id);
        SchemaValidation.ValidateBytes(store.ReadBytes($"passes/{id}.json"), "pass");
        PassRecord pass = store.Read<PassRecord>($"passes/{id}.json");
        CampaignRecord campaign = store.Read<CampaignRecord>("campaign.json");
        if (!Validate().Valid || pass.RunPassId != id || pass.CampaignId != campaign.CampaignId || pass.PlanHash != campaign.PlanHash || pass.ProfileHash != campaign.ProfileHash || pass.ResourceHash != campaign.ResourceHash || pass.InstrumentationVersion != campaign.InstrumentationVersion)
        {
            throw new InvalidDataException("Execution pass is incompatible with its sealed campaign.");
        }

        string buildPath = $"builds/{pass.BuildHash}.json";
        SchemaValidation.ValidateBytes(store.ReadBytes(buildPath), "build");
        BuildRecord build = store.Read<BuildRecord>(buildPath);
        if (EvidenceStore.HashRecord(build) != pass.BuildHash || pass.RunKind == "baseline" && pass.BuildHash != campaign.BuildHash || build.Dirty != (build.PatchHash != null) || build.PatchHash is { } patch && !System.Text.RegularExpressions.Regex.IsMatch(patch, "^[0-9a-f]{64}$"))
        {
            throw new InvalidDataException("Execution pass build identity is invalid.");
        }

        string[] required = ReadCases().Select(c => c.CaseId).Order(StringComparer.Ordinal).ToArray();
        if (pass.RequiredCases.Except(required).Any() || pass.RunKind == "final" && !pass.RequiredCases.Order(StringComparer.Ordinal).SequenceEqual(required))
        {
            throw new InvalidDataException("Execution pass case selection is invalid or final coverage is incomplete.");
        }

        return pass;
    }
}
