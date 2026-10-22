using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgamesh327PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_original_Zantetsuken_immune_Ifrit_uses_stage_fallback()
    {
        RunSelected(true, "MEMORIA_FF8_GILGAMESH327_IMMUNE");
    }

    [Fact]
    public void External_original_Zantetsuken_eligible_target_uses_actor_clipping()
    {
        RunSelected(false, "MEMORIA_FF8_GILGAMESH327_ELIGIBLE");
    }

    private static void RunSelected(bool immune, string variablePrefix)
    {
        const ushort selectedEffect = 327;
        const int selectedAction = 10;
        uint attackCallback = NativeBattleGilgameshProfile.ForEffect(selectedEffect).AttackCallback;
        string? sourcePath = Environment.GetEnvironmentVariable(variablePrefix + "_SOURCE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Select an external standalone idle battle fixture.");
        string output = Environment.GetEnvironmentVariable(variablePrefix + "_OUTPUT") ?? throw new InvalidDataException("Select a distinct empty output directory.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        string? configurationSha256 = null;
        byte messageSpeed;
        if (immune)
        {
            messageSpeed = source.Definition.NativeMessageSpeed ?? throw new InvalidDataException("The immune fixture requires its actual saved message-speed BYTE.");
        }
        else
        {
            string configurationPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH327_ELIGIBLE_CONFIG_SOURCE") ?? throw new InvalidDataException("Select an explicit external configuration source for the controlled eligible component.");
            BattleSnapshot configuration = NativeBattleEffectFamilyRunner.LoadSnapshot(configurationPath);
            messageSpeed = configuration.Definition.NativeMessageSpeed ?? throw new InvalidDataException("The explicitly selected configuration requires its known native message-speed BYTE.");
            configurationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(configurationPath)));
        }
        Assert.Equal(immune ? 94 : 368, source.Definition.EncounterId);
        Assert.Empty(source.QueuedActions);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.Definition.GuardianForces);
        Assert.Empty(source.GuardianForces);
        Assert.False(source.NativeGraphics!.Descriptors.Queue.Any(index => source.NativeGraphics.Descriptors.Packets[index].Command == 104));
        BattleNativeAction kernel = Assert.Single(assets.NativeActions, action => action.Command == 245 && action.ActionId == selectedAction);
        Assert.Equal(selectedEffect, kernel.MagicId);
        Assert.Equal((byte)11, kernel.AttackType);
        Assert.Equal((byte)0, kernel.Power);
        Assert.Equal((byte)250, kernel.StatusAttack);
        Assert.Equal(0x10000u, kernel.Statuses1);
        Assert.Equal((byte)35, kernel.AttackFlags);
        Assert.Equal((byte)0, kernel.Element);
        Assert.Equal((byte?)1, kernel.PowerModifier);
        Assert.Equal((byte?)1, kernel.LevelModifier);
        byte randomIndex = Enumerable.Range(0, 256).Select(index => (byte)index).First(index =>
        {
            NativeBattleRandomState candidate = new(Enumerable.Repeat(index, 8).ToImmutableArray(), 0);
            candidate = candidate.Draw(out byte first);
            _ = candidate.Draw(out byte second);
            return first <= 12 && second / 64 + 7 == selectedAction;
        });
        NativeBattleRandomState random = new(Enumerable.Repeat(randomIndex, 8).ToImmutableArray(), 0);
        NativeBattleRandomState afterChance = random.Draw(out byte chance);
        _ = afterChance.Draw(out byte selector);
        Assert.True(chance <= 12);
        Assert.Equal(selectedAction, selector / 64 + 7);
        BattleGuardianForceVital[] guardianProfile = [new(0, 1000, 1000, true)];
        BattleDefinition definition = source.Definition with
        {
            NativeGraphics = source.Definition.NativeGraphics! with { PrimitiveCapabilities = source.Definition.NativeGraphics.PrimitiveCapabilities! with { PcFramebufferCaptureProfile = NativeBattlePcFramebufferCaptureProfile.PrimaryRgba8() } },
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce && action.Id == 0 ? action with { Level = 10 } : action).ToArray(),
            GuardianForces = guardianProfile,
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([selectedEffect]),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeSupportRules = assets.ReadNativeSupportRules(),
            NativeMessageSpeed = messageSpeed,
            NativeSavedBattleFlags = 8,
            NativeSavedBattleFlagsKnownMask = 255
        };
        NativeBattleEffectMemory memory = source.NativeEffectMemory ?? throw new InvalidDataException("The fixture requires its existing effect process.");
        NativeBattleGilgameshProcess retained = Assert.IsType<NativeBattleGilgameshProcess>(memory.Gilgamesh);
        retained.Validate();
        Assert.Null(retained.Entry);
        Assert.Null(retained.Variant);
        Assert.IsType<NativeBattlePcRenderRequest>(memory.PcRenderRequest);
        Assert.NotNull(definition.NativeBattleSpeed);
        BattleSnapshot prepared = source with
        {
            Definition = definition,
            NativeSupport = new(true, 0, 0),
            GuardianForces = guardianProfile,
            NativeClock = source.NativeClock ?? throw new InvalidDataException("The fixture requires its actual battle clock owners."),
            Combatants = source.Combatants.Select(combatant =>
            {
                NativeBattleAtbState gauge = combatant.NativeAtb ?? throw new InvalidDataException("The fixture requires its current physical ATB gauge.");
                Assert.Equal(combatant.Atb, gauge.Display);
                return combatant with { NativeAtb = gauge };
            }).ToArray(),
            Random = random,
            NativeEffectMemory = memory
        };
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-support", prepared, null);
        File.WriteAllText(Path.Combine(output, "fixture-changes.json"), JsonSerializer.Serialize(new
        {
            SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))),
            Changes = new[] { "Installed native kernel actions and action definitions", "Explicit controlled acquired GF0 HP1000/1000 and level10 profile, shared by definition and snapshot; same profile as IndependentGuardianItemTests", $"Selected original effect{selectedEffect} catalog resources", "Explicit portable RGB555/no-alpha 640x480 PC framebuffer capture device profile", "Installed kernel ATB speed and support cooldown rules", "Explicit active battle clock with original UI admission and action-pause owners", "Existing physical ATB gauges and HUD values preserved", immune ? "Existing saved message-speed BYTE required and preserved" : "Explicit controlled message-speed configuration selected from an external standalone snapshot; no historical recovery claim", "Known saved support flags8", "Explicit periodic support entry initialized, cooldown0, GilgameshUsed0", "Explicit battle RNG indexes/lane0 selected by actual chance and quartile table", "Preserved existing authenticated Gilgamesh model/flare/trail and PC request owners without replacing them" },
            MessageConfiguration = new { ConfigurationSha256 = configurationSha256, Value = messageSpeed, OriginalSourceValue = source.Definition.NativeMessageSpeed, ExplicitControlledSelection = !immune, HistoricalRecovery = false },
            SourceGilgameshSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(memory.Gilgamesh))),
            PreparedGilgameshSha256 = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(prepared.NativeEffectMemory!.Gilgamesh))),
            SourcePcRenderRequest = memory.PcRenderRequest,
            PreparedPcRenderRequest = prepared.NativeEffectMemory.PcRenderRequest,
            SourceGuardianForces = source.GuardianForces,
            PreparedGuardianForces = guardianProfile,
            AcquiredGuardianLevels = new[] { new { Id = 0, Level = 10 } },
            SourceClock = source.NativeClock,
            PreparedClock = prepared.NativeClock,
            AtbGauges = source.Combatants.Select((combatant, index) => new { combatant.Id, Hud = combatant.Atb, Before = combatant.NativeAtb, After = prepared.Combatants[index].NativeAtb }),
            RandomIndex = randomIndex, Chance = chance, Selector = selector,
            Kernel = kernel
        }, new JsonSerializerOptions { WriteIndented = true }));
        BattleSession battle = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "prepared-support.battle.json")));
        BattleScenePresentation scene = new(assets, definition, true);
        int initialEventCount = battle.Events.Count;
        string targetId = source.NativeGraphics.ActorIds[3]!;
        var originalTarget = source.Combatants.Single(actor => actor.Id == targetId);
        int initialTargetHp = originalTarget.CurrentHp;
        Assert.True(initialTargetHp > 1);
        Assert.NotNull(originalTarget.NativeStatus);
        Assert.Equal((ushort)0, (ushort)(originalTarget.NativeStatus!.Saved & 5));
        Assert.Equal(0u, originalTarget.NativeStatus.Battle & 0x200800u);
        byte[] actualResistances = originalTarget.NativeStatus.Resistances ?? throw new InvalidDataException("Zantetsuken requires the selected target's original status resistance owner.");
        BattleCombatantDefinition originalDefinition = source.Definition.Combatants.Single(actor => actor.Id == targetId);
        Assert.Equal(immune ? 61 : 3, originalDefinition.ModelId);
        Assert.Equal(assets.ReadEnemyNativeStatusData(originalDefinition.ModelId).Resistances.ToArray(), actualResistances);
        Assert.Equal(immune ? (byte)255 : (byte)120, actualResistances[24]);
        Assert.Equal(0u, originalTarget.NativeStatus.Battle & 0x10000);
        string branchBoundary = immune ? "fallback-stage" : "custom-actor";
        NativeBattleContactHalf? formulaHalf = null;
        int? expectedDamage = null;
        byte? formulaRandom = null;
        byte? lastTickFirstRandom = null;
        NativeBattleRandomState? lastTickRandomAfterOneDraw = null;
        bool sawFormulaHeader = false;
        bool sawContactPresentation = false;
        bool sawVibrationStart = false;
        bool sawVibrationStop = false;
        bool sawStageClear = false;
        bool sawStageSet = false;
        int damageTextPublications = 0;
        Dictionary<string, string> phasePixelHashes = [];
        List<object> phaseFrames = [];
        int peakEffectRgb = 0;
        BattleSnapshot? peakSnapshot = null;
        BattleSnapshot? peakWithout = null;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        ImmutableHashSet<NativeBattlePrimitiveReference>? peakPackets = null;
        List<object> contactsPresented = [];
        List<object> audioReceipts = [];
        int ticks = 0;
        bool produced = false;
        bool initialized = false;
        bool completed = false;
        int callbacks = 0;
        int handoffCallbacks = 0;
        HashSet<uint> observed = [];
        HashSet<string> pending = ["entry", "opening", "swords", "attack", branchBoundary, "contact", "module-retired", "exit"];
        List<string> boundaries = [];
        List<object> supportProducerTicks = [];
        try
        {
            for (; ticks < 8 && !HasSupportProducer(); ticks++)
            {
                BattleSnapshot beforeProducer = battle.Capture();
                NativeBattleRandomState prefix = beforeProducer.Random ?? throw new InvalidDataException("The support producer requires its retained battle RNG.");
                byte? producerChance = null;
                byte? producerSelector = null;
                if (beforeProducer.NativeSupport!.GilgameshUsed == 0)
                {
                    prefix = prefix.Draw(out byte actualChance);
                    prefix = prefix.Draw(out byte actualSelector);
                    Assert.True(actualChance <= 12);
                    Assert.Equal(selectedAction, actualSelector / 64 + 7);
                    producerChance = actualChance;
                    producerSelector = actualSelector;
                }
                lastTickRandomAfterOneDraw = prefix.Draw(out byte nextFormulaRandom);
                lastTickFirstRandom = nextFormulaRandom;
                string producerBoundary = $"producer-before-{ticks:D3}";
                NativeBattleRestorationPresentationTests.Publish(output, producerBoundary, beforeProducer, null);
                BattleSession independentProducer = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, producerBoundary + ".battle.json")));
                battle.Tick();
                independentProducer.Tick();
                BattleSnapshot afterProducer = battle.Capture();
                Assert.Equal(JsonSerializer.Serialize(afterProducer), JsonSerializer.Serialize(independentProducer.Capture()));
                Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(independentProducer, 320, 224));
                ObserveFormula(afterProducer);
                produced |= sawFormulaHeader;
                supportProducerTicks.Add(new { beforeProducer.Frame, BeforeRandom = beforeProducer.Random, Chance = producerChance, Selector = producerSelector, FormulaCandidate = nextFormulaRandom, AfterRandom = afterProducer.Random, afterProducer.ActiveAction, FormulaProduced = sawFormulaHeader });
            }
            Assert.True(HasSupportProducer(), "The original support producer may queue callback7 or immediately activate its selected native command245/action.");
            Assert.Equal((byte)selectedAction, battle.Capture().NativeSupport!.SelectedAction);
            Assert.Equal((byte)1, battle.Capture().NativeSupport!.GilgameshUsed);
            NativeBattleRestorationPresentationTests.Publish(output, "callback7-produced", battle.Capture(), null);
            for (; callbacks < 2048; callbacks++)
            {
                BattleSnapshot before = battle.Capture();
                NativeBattleOriginalEffectState? effect = before.NativeGraphics?.OriginalEffect;
                NativeBattleModulePoolTask[] tasks = effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0).ToArray() ?? [];
                NativeBattleModulePoolTask? contactTask = tasks.SingleOrDefault(task => immune ? task.Callback == attackCallback && task.ReadWord(12) == 16 : task.Callback == 0x58E570 && unchecked((short)task.ReadWord(0x16)) <= 0 && task.ReadWord(12) == 40);
                NativeBattleModulePoolTask? directorTask = tasks.SingleOrDefault(task => task.Callback == 0x592300);
                observed.UnionWith(tasks.Select(task => task.Callback));
                string? boundary = new[]
                {
                    ("entry", effect?.Binding.Effect == selectedEffect && effect.ReturnedDescriptor > 0),
                    ("opening", tasks.Any(task => task.Callback == 0x592E90)),
                    (branchBoundary, tasks.Any(task => task.Callback == (immune ? 0x58F940u : 0x58E570u) && task.ReadWord(0x0C) > 0)),
                    ("swords", tasks.Any(task => task.Callback == 0x594FD0)),
                    ("attack", tasks.Any(task => task.Callback == attackCallback)),
                    ("contact", contactTask is not null),
                    ("module-retired", effect?.Binding.Effect == selectedEffect && initialized && effect.ReturnedDescriptor == 0)
                }.Where(item => item.Item2 && pending.Contains(item.Item1)).Select(item => item.Item1).FirstOrDefault();
                BattleSession? cold = null;
                if (boundary is not null)
                {
                    pending.Remove(boundary);
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    string pixelHash = Convert.ToHexString(SHA256.HashData(pixels));
                    phasePixelHashes.Add(boundary, pixelHash);
                    phaseFrames.Add(new { Boundary = boundary, Callback = before.NativeGraphics!.Callback, RgbaSha256 = pixelHash, Commands = before.NativeGraphics.PrimitiveFrame!.LastCommands.Length });
                    NativeBattleRestorationPresentationTests.Publish(output, boundary + "-before", before, pixels);
                    cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, boundary + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(pixels, new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224));
                }
                if (!produced)
                {
                    lastTickRandomAfterOneDraw = (before.Random ?? throw new InvalidDataException("The native formula requires its retained battle RNG.")).Draw(out byte nextFormulaRandom);
                    lastTickFirstRandom = nextFormulaRandom;
                    battle.Tick();
                    cold?.Tick();
                    ObserveFormula(battle.Capture());
                    produced |= sawFormulaHeader;
                }
                battle.AdvanceNativeGraphicsPresentation(Callback);
                cold?.AdvanceNativeGraphicsPresentation(Callback);
                BattleSnapshot after = battle.Capture();
                ObserveFormula(after);
                if (tasks.Any(task => task.Callback is 0x58E120 or 0x58E570 or 0x58F940))
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = NativeBattleAquaPresentationTests.AquaPacketReferences(before, after);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = after.NativeGraphics!.PrimitiveFrame!;
                        BattleSnapshot without = after with { NativeGraphics = after.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        byte[] plain = new BattleScenePresentation(assets, definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        int changed = 0;
                        for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                        {
                            if (!pixels.AsSpan(pixel, 3).SequenceEqual(plain.AsSpan(pixel, 3)))
                            {
                                changed++;
                            }
                        }
                        if (changed > peakEffectRgb)
                        {
                            peakEffectRgb = changed;
                            peakSnapshot = after;
                            peakWithout = without;
                            peakPixels = pixels;
                            peakPlain = plain;
                            peakPackets = owned;
                        }
                    }
                }
                int createdText = 0;
                for (int slot = 0; slot < after.NativeGraphics!.Effects.Slots.Length; slot++)
                {
                    NativeBattleEffectTask current = after.NativeGraphics.Effects.Slots[slot];
                    NativeBattleEffectTask previous = before.NativeGraphics!.Effects.Slots[slot];
                    if ((current.Flags0 & 1) != 0 && current.Kind == NativeBattleEffectKind.DamageText5069B0 && current.Frame == 1 && !((previous.Flags0 & 1) != 0 && previous.Kind == current.Kind && previous.Frame == 1))
                    {
                        Assert.Equal((byte)3, current.ModelIndex);
                        Assert.True(immune, "The successful status-only contact must not create numeric damage text.");
                        Assert.Equal(new byte[] { 16, 0 }, current.Glyphs.ToArray());
                        Assert.Equal((short)8, current.HalfWidth);
                        Assert.Equal(0x808080u, current.Color);
                        createdText++;
                    }
                }
                damageTextPublications += createdText;
                if (contactTask is not null && (before.NativeGraphics!.Flags & 0x201) == 0)
                {
                    Assert.False(sawContactPresentation);
                    NativeBattleModulePoolTask advancedContact = Assert.Single(after.NativeGraphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => task.Callback == contactTask.Callback);
                    Assert.Equal((ushort)(immune ? 17 : 41), advancedContact.ReadWord(12));
                    NativeBattleContactHalf half = formulaHalf ?? throw new InvalidDataException("Presentation contact preceded the actual formula packet.");
                    Assert.Equal(immune ? 1 : 0, createdText);
                    Assert.Equal(immune ? initialTargetHp : 0, after.Combatants.Single(actor => actor.Id == targetId).CurrentHp);
                    contactsPresented.Add(new { CounterBefore = contactTask.ReadWord(12), CounterAfter = advancedContact.ReadWord(12), half.ActorIndex, half.Sequence, half.Amount6, half.Flags2, half.HitFlags3, DamageTextPublications = createdText });
                    sawContactPresentation = true;
                }
                if (after.NativeEffectMemory?.Gilgamesh?.VibrationHandle is int currentHandle && currentHandle > 0 && after.NativeVibrationProcess!.IsPlaying(currentHandle))
                {
                    sawVibrationStart = true;
                }
                if (directorTask?.ReadWord(12) == 324 && (before.NativeGraphics!.Flags & 0x201) == 0)
                {
                    int handle = before.NativeEffectMemory!.Gilgamesh!.VibrationHandle!.Value;
                    Assert.True(before.NativeVibrationProcess!.IsPlaying(handle));
                    Assert.False(after.NativeVibrationProcess!.IsPlaying(handle));
                    sawVibrationStop = true;
                }
                if (directorTask?.ReadWord(12) is 8 or 76 && (before.NativeGraphics!.Flags & 0x201) == 0)
                {
                    bool set = directorTask.ReadWord(12) == 76;
                    for (int group = 0; group < 4; group++)
                    {
                        byte previous = before.NativeStage!.Groups[group].Flags;
                        Assert.Equal(set ? (byte)(previous | 2) : (byte)(previous & ~2), after.NativeStage!.Groups[group].Flags);
                    }
                    sawStageSet |= set;
                    sawStageClear |= !set;
                }
                if (after.ActiveAction?.IndependentGuardian is { } invocation)
                {
                    Assert.Equal((byte)10, invocation.Level);
                }
                produced |= after.NativeGraphics!.Descriptors.Queue.Any(index => after.NativeGraphics.Descriptors.Packets[index].Command == 104);
                initialized |= after.NativeGraphics.OriginalEffect is { Binding.Effect: var initializedEffect, ReturnedDescriptor: > 0 } && initializedEffect == selectedEffect;
                if (cold is not null)
                {
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224));
                    NativeBattleRestorationPresentationTests.Publish(output, boundary + "-after", after, scene.Render(battle, 320, 224));
                    boundaries.Add(boundary!);
                }
                if (initialized && after.NativeGraphics.OriginalEffect is { Binding.Effect: var finishedEffect, ReturnedDescriptor: 0 } && finishedEffect == selectedEffect && !after.NativeGraphics.Descriptors.Queue.Any(index => after.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    NativeBattleRestorationPresentationTests.Publish(output, "completed-before", after, pixels);
                    BattleSession independent = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(pixels, new BattleScenePresentation(assets, definition, true).Render(independent, 320, 224));
                    battle.AdvanceNativeGraphicsPresentation(Callback);
                    independent.AdvanceNativeGraphicsPresentation(Callback);
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(independent, 320, 224));
                    int damageBeforeHandoff = battle.Events.Count(item => item.Kind == "Damage");
                    string? originalActor = battle.ActiveAction?.ActorId;
                    for (; handoffCallbacks < 4 && battle.ActiveAction is not null; handoffCallbacks++)
                    {
                        Assert.Equal(originalActor, battle.ActiveAction.ActorId);
                        Assert.Equal((byte)245, battle.ActiveAction.NativeQueuedCommand);
                        Assert.Equal(selectedAction, battle.ActiveAction.ActionId);
                        battle.Tick();
                        independent.Tick();
                        battle.AdvanceNativeGraphicsPresentation(Callback);
                        independent.AdvanceNativeGraphicsPresentation(Callback);
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                        Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(independent, 320, 224));
                        Assert.Equal(damageBeforeHandoff, battle.Events.Count(item => item.Kind == "Damage"));
                    }
                    Assert.Null(battle.ActiveAction);
                    Assert.Empty(battle.QueuedActions);
                    Assert.Equal(0u, battle.Capture().NativeClock!.ActionPause);
                    foreach (var player in battle.Combatants.Where(actor => source.Definition.Combatants.Single(definition => definition.Id == actor.Id).IsPlayer))
                    {
                        Assert.False(player.IsBusy);
                    }
                    completed = true;
                    pending.Remove("exit");
                    boundaries.Add("exit");
                    break;
                }
            }
            Assert.True(completed, "The finite diagnostic budget is not native completion.");
            Assert.Empty(pending);
            Assert.Equal(boundaries.Count, boundaries.Distinct().Count());
            Assert.True(sawFormulaHeader);
            Assert.True(sawContactPresentation);
            Assert.Contains(0x58E120u, observed);
            Assert.Contains(immune ? 0x58F940u : 0x58E570u, observed);
            Assert.DoesNotContain(immune ? 0x58E570u : 0x58F940u, observed);
            if (!immune)
            {
                Assert.Contains(0x58E3E0u, observed);
            }
            Assert.Equal(immune ? 1 : 0, damageTextPublications);
            Assert.True(sawVibrationStart);
            Assert.True(sawVibrationStop);
            Assert.True(sawStageClear);
            Assert.True(sawStageSet);
            Assert.Contains(new[] { "swords", "attack", branchBoundary }, phase => phasePixelHashes[phase] != phasePixelHashes["entry"]);
            NativeBattleGilgameshEffectResources originalResources = game.ReadNativeGilgameshEffectResources(selectedEffect);
            uint Stream(ushort id) => unchecked((uint)(690000 + BinaryPrimitives.ReadInt32LittleEndian(originalResources.Files.Files.Single(file => file.FileId == id).Bytes.AsSpan())));
            uint[] expectedAudio = [originalResources.Director.SoundInitial, unchecked((uint)(originalResources.InitialStreamNumber + 690000)), originalResources.Director.SoundMiddle, originalResources.Director.SoundLate, Stream(750), Stream(752), originalResources.Director.SoundClosing, Stream(753)];
            NativeBattleModuleSound[] audio = battle.Events.Skip(initialEventCount).Select(item => item.NativeModuleSound).OfType<NativeBattleModuleSound>().Where(sound => expectedAudio.Contains(sound.Sound)).ToArray();
            Assert.Equal(expectedAudio, audio.Select(sound => sound.Sound));
            Assert.Equal(new uint[] { 127, 96, 127, 127, 96, 96, 127, 96 }, audio.Select(sound => sound.Volume));
            for (int index = 0; index < audio.Length; index++)
            {
                NativeBattleModuleSound sound = audio[index];
                Assert.Equal(0u, sound.Flags);
                Assert.Equal(128u, sound.RawPan);
                Assert.NotNull(sound.Allocation);
                _ = sound.ReadCommittedVoice();
                if (index > 0)
                {
                    Assert.Equal(audio[index - 1].Allocation!.AfterRotation, sound.Allocation!.BeforeRotation);
                }
                audioReceipts.Add(new { sound.Sound, sound.Flags, sound.RawPan, sound.Volume, sound.Allocation });
            }
            Assert.False(battle.NativeVibrationProcess!.IsPlaying(battle.NativeEffectMemory!.Gilgamesh!.VibrationHandle!.Value));
            for (int group = 0; group < 4; group++)
            {
                Assert.Equal((byte)(source.NativeStage!.Groups[group].Flags | 2), battle.NativeStage!.Groups[group].Flags);
                Assert.Equal((short)0, battle.NativeStage.Groups[group].ColorDepth);
                Assert.Equal(0u, battle.NativeStage.Groups[group].ColorFlags);
            }
            BattleEvent damage = Assert.Single(battle.Events.Skip(initialEventCount), item => item.Kind == "Damage");
            Assert.Equal(targetId, damage.TargetId);
            Assert.Equal(expectedDamage!.Value, damage.Amount);
            Assert.Equal(immune ? initialTargetHp : 0, battle.Combatants.Single(actor => actor.Id == targetId).CurrentHp);
            foreach (var original in source.Combatants.Where(actor => source.Definition.Combatants.Single(definition => definition.Id == actor.Id).IsPlayer))
            {
                Assert.Equal(original.CurrentHp, battle.Combatants.Single(actor => actor.Id == original.Id).CurrentHp);
            }
            Assert.All(battle.NativeGraphics!.OriginalEffect!.Pools, pool => Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0));
            {
                Assert.True(peakEffectRgb > 0, "No attributed Zantetsuken polygons changed the actual framebuffer.");
                Assert.NotNull(peakSnapshot);
                Assert.NotNull(peakWithout);
                Assert.NotNull(peakPixels);
                Assert.NotNull(peakPlain);
                NativeBattleRestorationPresentationTests.Publish(output, "visible", peakSnapshot, peakPixels);
                NativeBattleRestorationPresentationTests.Publish(output, "without-effect", peakWithout, peakPlain);
                File.WriteAllText(Path.Combine(output, "visible-packet-ownership.json"), JsonSerializer.Serialize(new { Effect = selectedEffect, peakSnapshot.Frame, NativeCallback = peakSnapshot.NativeGraphics!.Callback, ChangedRgb = peakEffectRgb, Packets = peakPackets!.OrderBy(packet => packet.Bank).ThenBy(packet => packet.Buffer).ThenBy(packet => packet.ByteOffset) }));
                BattleSession directPeak = BattleSession.Restore(peakSnapshot);
                BattleSession coldPeak = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "visible.battle.json")));
                Assert.Equal(JsonSerializer.Serialize(peakSnapshot), JsonSerializer.Serialize(coldPeak.Capture()));
                directPeak.AdvanceNativeGraphicsPresentation(Callback);
                coldPeak.AdvanceNativeGraphicsPresentation(Callback);
                Assert.Equal(JsonSerializer.Serialize(directPeak.Capture()), JsonSerializer.Serialize(coldPeak.Capture()));
                Assert.Equal(new BattleScenePresentation(assets, definition, true).Render(directPeak, 320, 224), new BattleScenePresentation(assets, definition, true).Render(coldPeak, 320, 224));
                NativeBattleRestorationPresentationTests.Publish(output, "peak-after", coldPeak.Capture(), new BattleScenePresentation(assets, definition, true).Render(coldPeak, 320, 224));
                boundaries.Add("peak");
            }
            Assert.Equal(9, boundaries.Count);
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
        }
        finally
        {
            NativeBattleRestorationPresentationTests.Publish(output, "last", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { SupportProducerTicks = supportProducerTicks, Immune = immune, EffectIsolatedRgb = peakEffectRgb, EffectPixelOwnership = "Complete matched first ordinary actor prefix; only preceding COMMON effect polygon references removed", FormulaContact = formulaHalf, ExpectedDamage = expectedDamage, FormulaRandom = formulaRandom, Kernel = kernel, Completed = completed, Produced = produced, Initialized = initialized, FormulaHeader = sawFormulaHeader, ContactPresentation = sawContactPresentation, DamageTextPublications = damageTextPublications, Contacts = contactsPresented, Audio = audioReceipts, VibrationStarted = sawVibrationStart, VibrationStopped = sawVibrationStop, StageFlagsCleared = sawStageClear, StageFlagsSet = sawStageSet, PhaseFrames = phaseFrames, Ticks = ticks, Callbacks = callbacks, HandoffCallbacks = handoffCallbacks, Observed = observed.Order(), Boundaries = boundaries, Pending = pending }, new JsonSerializerOptions { WriteIndented = true }));
        }

        bool HasSupportProducer()
        {
            return battle.QueuedActions.Any(action => action.NativeCallback?.Stage == 7) || battle.ActiveAction is { NativeQueuedCommand: 245, ActionId: var activeAction } && activeAction == selectedAction;
        }

        void ObserveFormula(BattleSnapshot after)
        {
            NativeBattleActionHeaderSlot? producedHeader = after.NativeActionPackets!.Headers.FirstOrDefault(header => header.ReadByte(1) == 245 && NativeBattleActionGraphics.Word(header, 4) == selectedAction && NativeBattleActionGraphics.Word(header, 6) == selectedEffect);
            if (producedHeader?.FirstContactIndex is int contactIndex)
            {
                NativeBattleContactPacket contact = after.NativeActionPackets.ReadContact(contactIndex);
                if (!sawFormulaHeader)
                {
                    Assert.Equal(JsonSerializer.Serialize(lastTickRandomAfterOneDraw), JsonSerializer.Serialize(after.Random));
                    formulaRandom = lastTickFirstRandom ?? throw new InvalidDataException("The actual formula producer requires its pre-Tick battle RNG owner.");
                    int spirit = (originalTarget.NativeStatus.Battle & 0x1000000) != 0 ? 0 : battle.Combatants.Single(actor => actor.Id == targetId).Spirit;
                    int rawDamage = NativeBattleDamage.GuardianForce(spirit, kernel.Power, 10, kernel.LevelModifier!.Value, kernel.PowerModifier!.Value, 0, formulaRandom.Value);
                    expectedDamage = Math.Clamp(rawDamage, -9999, 9999);
                    Assert.Equal(0, expectedDamage);
                }
                Assert.Equal((byte)3, contact.Primary.ActorIndex);
                Assert.Equal((ushort)expectedDamage!.Value, contact.Primary.Amount6);
                Assert.Equal((byte)255, contact.Secondary.ActorIndex);
                Assert.Equal(immune ? initialTargetHp : 0, after.Combatants.Single(actor => actor.Id == targetId).CurrentHp);
                Assert.Equal((ushort)(originalTarget.NativeStatus.Saved | (immune ? 0 : 1)), after.Combatants.Single(actor => actor.Id == targetId).NativeStatus!.Saved);
                uint expectedStatus = originalTarget.NativeStatus.Battle | (immune ? 0u : 0x10000u);
                Assert.Equal(expectedStatus, after.Combatants.Single(actor => actor.Id == targetId).NativeStatus!.Battle);
                Assert.Equal(immune, (contact.Primary.HitFlags3 & 4) != 0);
                Assert.Equal(!immune, (contact.Primary.Flags2 & 1) != 0);
                Assert.Equal(expectedStatus, contact.Primary.BattleStatuses8);
                formulaHalf = contact.Primary;
                sawFormulaHeader = true;
            }
        }

    }
}
