using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedBattlePresentationProcessEntryTests
{
    private const int EntryBudget = 1200;

    [Theory]
    [InlineData(4, 5, 4)]
    [InlineData(11, 14, 4)]
    [InlineData(4, 3, 2)]
    public void Installed_entry_drains_physical_descriptors_and_continues_idle_on_the_same_owner(int allyCount, int enemyCount, int authoredCount)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = CreateDefinitions(game, assets, allyCount, enemyCount, authoredCount, expanded: true);
        BattleSession logical = new(logicalDefinition, BattleAtbMode.TurnBased);
        string before = LogicalState(logical);
        AssertUnselectedStageCamera(logical);
        ExpandedBattlePresentationProcess process = new(logical, selected);
        NativeBattleGraphicsRoster roster = logical.ReadExpandedPresentationRoster();

        Assert.Equal(roster.Rows.ToArray(), process.Graphics.PresentationRoster!.Rows.ToArray());
        Assert.Equal(allyCount, roster.PartyRows.Count());
        Assert.Equal(enemyCount, roster.EnemyRows.Count());
        if (authoredCount == 2)
        {
            Assert.False(roster.ContainsRow(5));
            Assert.False(roster.ContainsRow(6));
            Assert.True(roster.ContainsRow(15));
        }
        Assert.False(process.EntryReady);
        Assert.All(process.Graphics.Models, model => Assert.Equal(0, model.Flags0 & 2));

        NativeExamTimerState? timer = allyCount == 4 && authoredCount == 4 ? NativeExamTimerState.FromColdProcess().SetRemaining(300).Display(4, 5) : null;
        uint duration = selected.NativeGraphics!.Rules.UiPublicationDuration!.Value;
        if (timer is not null)
        {
            process.Advance(logical, timer);
            NativeExamTimerState reset = Assert.IsType<NativeExamTimerState>(process.Timer);
            Assert.True(reset.HasSameClock(timer));
            Assert.False(reset.DisplayEnabled);
            Assert.Equal((ushort?)0, reset.DisplayIntensity814);
            timer = reset.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1);
        }
        EntryObservations observed = AdvanceToCase3(process, logical, ref timer, duration);

        Assert.Equal((byte)3, process.Graphics.EntryLifecycle);
        Assert.False(process.EntryReady);
        Assert.Equal((byte)2, process.Graphics.EntryGatePhase);
        Assert.Equal((byte)255, process.Graphics.EntryBarrierCompletion);
        Assert.NotNull(process.Graphics.EntryBarrierPacket);
        Assert.Equal(0, process.Graphics.ModelLoadCount);
        Assert.Equal(selected.StageId, process.Stage.ResourceStageId);
        Assert.NotNull(process.Stage.Task);
        Assert.NotNull(process.Camera.Projection);
        Assert.True(observed.WaveTask);
        Assert.Contains(process.Memory.Textures!.LiveTextureSources ?? [], region => region.Origin.SourceOwner == "b0wave.dat");
        Assert.True(observed.AppearanceTask);
        Assert.True(observed.AppearanceCompleted);
        Assert.True(observed.BarrierTask);
        Assert.Equal(roster.EnemyRows.Concat(roster.PartyRows).ToArray(), observed.CompletedRows.ToArray());
        Assert.InRange(observed.PeakQueuedDescriptors, 1, 29);
        if (allyCount == 11)
        {
            Assert.True(observed.AppearanceDeferredWhileLoadsWereQueued);
        }
        AssertCompletedModelOwners(process, roster, selected.NativeGraphics!);

        long beforeReady = process.Graphics.Callback;
        Advance(process, logical, ref timer, duration);
        Assert.True(process.EntryReady);
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal((byte?)1, process.Memory.ActionPhaseDEA);
        Assert.Equal(beforeReady + 1, process.Graphics.Callback);
        NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(process.PreparedPresentation);
        Assert.Equal(logical.Frame, prepared.Frame);
        Assert.Equal(process.Graphics.Callback, prepared.Callback);
        Assert.Equal(process.Stage.ResourceStageId, prepared.StageResourceId);
        Assert.NotEmpty(prepared.Primitives.LastCommands);
        Assert.Equal(roster.Rows.OfType<NativeBattleGraphicsActor>().Select(actor => actor.ActorId), prepared.Actors.Select(actor => actor.Id));
        foreach (int row in roster.PartyRows.Concat(roster.EnemyRows))
        {
            NativeBattlePreparedActor actor = Assert.Single(prepared.Actors, item => item.Id == roster[row].ActorId);
            NativeBattleGraphicsModel model = process.Graphics.Models[row];
            Assert.Same(model.BodyTextureSlots, actor.BodyTextures);
            Assert.Same(model.LinkedTextureSlots, actor.LinkedTextures);
            Assert.Equal(selected.NativeGraphics!.Bindings.Single(binding => binding.ActorId == actor.Id).InitialPosition, model.Transform?.Initial20);
        }

        var states = roster.PartyRows.Concat(roster.EnemyRows).ToDictionary(row => row, row => IdleState(process.Graphics.Models[row]));
        HashSet<int> advanced = [];
        for (int callback = 0; callback < 12; callback++)
        {
            Advance(process, logical, ref timer, duration);
            foreach ((int row, var initial) in states)
            {
                if (IdleState(process.Graphics.Models[row]) != initial)
                {
                    advanced.Add(row);
                }
            }
        }
        Assert.Equal(states.Keys.Order().ToArray(), advanced.Order().ToArray());
        Assert.True(process.PreparedPresentation!.Version > prepared.Version);
        Assert.Equal(prepared.Frame, process.PreparedPresentation.Frame);
        AssertCompletedModelOwners(process, roster, selected.NativeGraphics!);
        AssertRandomExchange(logical, process);
        Assert.True(process.GlobalRandomCalls > 0);
        Assert.True(string.Equals(before, LogicalState(logical), StringComparison.Ordinal), "Presentation entry or idle changed the logical battle.");
    }

    [Fact]
    public void Installed_seven_row_control_matches_shared_entry_order_and_global_random_consumption()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logicalDefinition, selected) = CreateDefinitions(game, assets, 3, 4, 4, expanded: false);
        BattleSession native = new(selected, BattleAtbMode.TurnBased);
        BattleSession logical = new(logicalDefinition with { UsesExpandedRoster = true }, BattleAtbMode.TurnBased);
        AssertUnselectedStageCamera(logical);
        Assert.Equal(native.NativeEntryAdvantage, logical.NativeEntryAdvantage);
        var process = new ExpandedBattlePresentationProcess(logical, selected);
        EntryObservations nativeEntry = new();
        for (int callback = 0; callback < EntryBudget && native.NativeGraphics!.EntryLifecycle < 3; callback++)
        {
            native.Tick();
            nativeEntry.Observe(native.NativeGraphics!);
        }
        NativeExamTimerState? timer = null;
        EntryObservations hostedEntry = AdvanceToCase3(process, logical, ref timer, 0);

        NativeBattleGraphicsState nativeGraphics = Assert.IsType<NativeBattleGraphicsState>(native.NativeGraphics);
        Assert.Equal((byte)3, nativeGraphics.EntryLifecycle);
        Assert.False(native.NativeEntryReady);
        Assert.False(process.EntryReady);
        Assert.Equal(nativeEntry.CompletedRows.ToArray(), hostedEntry.CompletedRows.ToArray());
        Assert.Equal(new[] { 3, 4, 5, 6, 0, 1, 2 }, hostedEntry.CompletedRows.ToArray());
        Assert.Equal(nativeGraphics.EntryBarrierCompletion, process.Graphics.EntryBarrierCompletion);
        Assert.Equal(native.NativeStage!.ResourceStageId, process.Stage.ResourceStageId);
        Assert.Equal(nativeGraphics.Models.Select(row => (row.ModelId4, row.Transform?.Initial20, row.SequenceState?.Default2)).ToArray(),
            process.Graphics.Models.Select(row => (row.ModelId4, row.Transform?.Initial20, row.SequenceState?.Default2)).ToArray());
        Assert.Equal(native.NativeGlobalRandomChange, process.GlobalRandomChange);
        AssertRandomExchange(logical, process);
        Assert.Equal(0, native.Frame);

        native.Tick();
        process.Advance(logical);
        Assert.True(native.NativeEntryReady);
        Assert.True(process.EntryReady);
        Assert.Equal((byte)4, process.Graphics.EntryLifecycle);
        Assert.Equal(native.NativeEffectMemory!.ActionPhaseDEA, process.Memory.ActionPhaseDEA);
        Assert.Equal(native.NativeGlobalRandomChange, process.GlobalRandomChange);
        AssertRandomExchange(logical, process);
        Assert.Equal(0, native.Frame);
        Assert.Equal(0, logical.Frame);
    }

    private static void Advance(ExpandedBattlePresentationProcess process, BattleSession logical, ref NativeExamTimerState? timer, uint publicationDuration)
    {
        if (timer is not { } canonical)
        {
            process.Advance(logical);
            Assert.Null(process.Timer);
            return;
        }
        process.Advance(logical, canonical);
        NativeExamTimerState observed = Assert.IsType<NativeExamTimerState>(process.Timer);
        Assert.True(observed.HasSameClock(canonical));
        Assert.Equal(canonical.DisplayEnabled, observed.DisplayEnabled);
        Assert.Equal(canonical.DisplayX, observed.DisplayX);
        Assert.Equal(canonical.DisplayY, observed.DisplayY);
        NativeExamTimerState expectedDisplay = canonical;
        uint count = (process.Graphics.Flags & 4) != 0 ? publicationDuration : 0;
        for (uint frame = 0; frame < count; frame++)
        {
            expectedDisplay = expectedDisplay.AdvanceDisplay4A6D00();
        }
        Assert.Equal(expectedDisplay.Alternatives, observed.Alternatives);
        timer = observed.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1);
    }

    private static EntryObservations AdvanceToCase3(ExpandedBattlePresentationProcess process, BattleSession logical, ref NativeExamTimerState? timer, uint publicationDuration)
    {
        EntryObservations observed = new();
        for (int callback = 0; callback < EntryBudget && process.Graphics.EntryLifecycle < 3; callback++)
        {
            Advance(process, logical, ref timer, publicationDuration);
            observed.Observe(process.Graphics);
            if (!observed.AppearanceCompleted && process.Graphics.EntryGatePhase == 2)
            {
                foreach (int row in process.Graphics.PresentationRoster!.PartyRows)
                {
                    NativeBattleGraphicsModel model = process.Graphics.Models[row];
                    Assert.True((model.Flags0 & 2) != 0, $"Appearance completed before party row {row} was loaded.");
                    Assert.NotNull(model.OriginalResourceRow104);
                    Assert.NotNull(model.SequenceState);
                    Assert.NotEqual((byte)0, model.SequenceState.Default2);
                }
                observed.AppearanceCompleted = true;
            }
        }
        Assert.True(process.Graphics.EntryLifecycle == 3, $"Entry stalled at lifecycle {process.Graphics.EntryLifecycle}, callback {process.Graphics.Callback}.");
        return observed;
    }

    private static void AssertCompletedModelOwners(ExpandedBattlePresentationProcess process, NativeBattleGraphicsRoster roster, NativeBattleGraphicsDefinition resources)
    {
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleModelTextureCache cache = process.ModelTextureCache;
        NativeBattleModelCpuResources cpu = process.ModelCpuResources;
        var bindings = resources.Bindings.ToDictionary(binding => binding.ActorId, StringComparer.Ordinal);
        for (int row = 0; row < roster.ActorCount; row++)
        {
            NativeBattleGraphicsModel model = graphics.Models[row];
            if (roster.Rows[row] is not { } actor)
            {
                Assert.Null(graphics.ActorIds[row]);
                Assert.Equal(0, model.Flags0 & 2);
                continue;
            }
            NativeBattleGraphicsBinding binding = bindings[actor.ActorId];
            Assert.Equal(actor.ActorId, graphics.ActorIds[row]);
            Assert.True((model.Flags0 & 2) != 0);
            Assert.Equal((byte?)2, model.EffectCounter97);
            Assert.Equal((byte?)binding.ModelId, model.ModelId4);
            Assert.Equal(binding.InitialPosition, model.Transform?.Initial20);
            int body = Assert.IsType<int>(model.ActiveResourceRow100);
            Assert.Equal(body, model.OriginalResourceRow104);
            Assert.Equal(body, model.TextureResourceIndex);
            Assert.Equal(row, cache.PresentationActors![body]);
            Assert.Equal((byte)(actor.IsParty ? 1 : 3), cache.Rows[body].Kind);
            Assert.NotNull(cpu.Rows[body].Require(12));
            Assert.NotNull(model.BodyTextureSlots);
            if (actor.IsParty && binding.ModelId != 7)
            {
                Assert.NotNull(model.LinkedTextureSlots);
                Assert.Equal((byte?)row, cpu.Rows[body].LinkedPhysicalIndex36);
                Assert.NotNull(cpu.Rows[body].Require(24));
                if (model.LinkedTextureResourceIndex is int linked)
                {
                    Assert.Equal(row, cache.PresentationActors![linked]);
                    Assert.Equal((byte)2, cache.Rows[linked].Kind);
                    Assert.NotNull(cpu.Rows[linked].Require(12));
                }
                if (binding.ModelId == 0)
                {
                    Assert.NotNull(model.LinkedTextureResourceIndex);
                }
            }
            Assert.NotNull(process.Memory.Textures!.RequirePresentationTexture(actor.ActorId));
        }
    }

    private static (byte? Sequence, int Callback, ushort? Offset, byte? PoseFrame) IdleState(NativeBattleGraphicsModel model)
    {
        return (model.Sequence, model.SequenceFrame, model.SequenceState?.Offset46, model.SequenceState?.PoseFrame114);
    }

    internal static string LogicalState(BattleSession logical)
    {
        BattleSnapshot data = logical.Capture();
        return JsonSerializer.Serialize(new
        {
            data.Frame, data.Combatants, data.NativeItemInventory, data.NativeGil, data.Events, data.ScriptedRewards, data.NativeRewardItems,
            data.Random, data.NativeBattleUiRandom, data.NativeAiVariables, data.PersistentAiVariables,
            data.QueuedActions, data.ActiveAction, data.Outcome, data.NativeScriptState
        });
    }

    internal static void AssertRandomExchange(BattleSession logical, ExpandedBattlePresentationProcess process)
    {
        NativeGlobalRandomChange owner = Assert.IsType<NativeGlobalRandomChange>(logical.NativeGlobalRandomChange);
        owner.Validate();
        Assert.Equal(owner, process.GlobalRandomChange);
        Assert.Equal(logical.CurrentCrtRandom, process.CurrentCrtRandom);
    }

    private static void AssertUnselectedStageCamera(BattleSession logical)
    {
        NativeGlobalRandomChange initial = Assert.IsType<NativeGlobalRandomChange>(logical.NativeGlobalRandomChange);
        Assert.Equal(0UL, initial.Calls);
        Assert.Equal(initial.Before, initial.After);
        Assert.Null(Assert.IsType<NativeBattleCameraState>(logical.NativeCamera).ControlCursor);
    }

    internal static (BattleDefinition Logical, BattleDefinition Selected) CreateDefinitions(GameAssets game, BattleAssets assets, int allyCount, int enemyCount, int authoredCount, bool expanded)
    {
        BattleEncounter encounter = assets.LoadEncounter(7);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleEnemyPlacement[] authored = encounter.Enemies.Where(enemy => enemy.Enabled).OrderBy(enemy => enemy.Slot).Take(authoredCount).ToArray();
        Assert.Equal(authoredCount, authored.Length);
        BattleEnemyPlacement donor = Assert.Single(assets.LoadEncounter(136).Enemies, enemy => enemy.Enabled);
        int addedCount = enemyCount - authoredCount;
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> added = Enumerable.Range(ExpandedBattleFormation.OriginalEnemyPrototypeCount, addedCount)
            .ToImmutableSortedDictionary(slot => slot, _ => new BattleEnemyRosterSource(136, donor.Slot, assets.FormationSourceHash(136)));
        int[] partyModels = [0, 3, 4, 1, 2, 5, 6, 7, 8, 9, 10];
        BattleCombatantDefinition[] party = partyModels.Take(allyCount).Select(character => new BattleCombatantDefinition
        {
            Id = $"party-{character}",
            Name = GameNames.DefaultName(GameNames.CharacterSubject(character)),
            IsPlayer = true,
            ModelId = character,
            WeaponId = assets.WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == character)?.Id,
            MaximumHp = 500,
            CurrentHp = 371,
            InitialAtb = BattleSession.AtbMaximum,
            NativeDeathCount = 0
        }).ToArray();
        BattleCombatantDefinition[] enemies = [.. authored.Select(placement => Enemy(placement, placement.Slot, expanded)), .. added.Keys.Select(slot => Enemy(donor, slot, false))];
        BattleCombatantDefinition[] actors = [.. party, .. enemies];
        BattleDefinition logical = new()
        {
            Name = "Installed roster entry",
            EncounterId = encounter.Id,
            StageId = encounter.StageId,
            UsesExpandedRoster = expanded,
            UsesOriginalEnemySlots = expanded && addedCount != 0,
            AddedEnemySources = addedCount == 0 ? null : added,
            Combatants = actors,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl,
            PersistentAiVariables = [1, 0, 0x1234, 0, 0, 0, 0, 0],
            Items = [new(1, 3)]
        };
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition([.. party.Take(Math.Min(3, allyCount)), .. enemies.Take(authoredCount)], rules, encounter);
        if (allyCount > 3 || addedCount > 0)
        {
            var bindings = graphics.Bindings.ToBuilder();
            for (int i = 3; i < party.Length; i++)
            {
                BattleCombatantDefinition actor = party[i];
                bindings.Add(new(actor.Id, checked((byte)actor.ModelId), assets.ReadNativeGraphicsResource(actor)) { InitialPosition = ExpandedBattleFormation.Position(i, party.Length, isAlly: true) });
            }
            if (addedCount != 0)
            {
                NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(donor.ModelId);
                foreach (int slot in added.Keys)
                {
                    bindings.Add(new($"enemy-{slot}", checked((byte)donor.NativeModelId), resource) { InitialPosition = ExpandedBattleFormation.AddedEnemyPosition(slot, addedCount) });
                }
            }
            graphics = graphics with { Bindings = bindings.ToImmutable(), InitialLoadOrder = [.. enemies.Select(actor => actor.Id), .. party.Select(actor => actor.Id)] };
        }
        NativeBattleMusicEntryPreparation music = NativeBattleMusicFixture.Prepare(game, encounter.Id);
        BattleDefinition selected = logical with
        {
            NativeGraphics = graphics,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = music.MusicProcess,
            NativeGlobalSoundProcess = music.SoundProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeStageSource = stage.NativeSource,
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            LimitProgress = game.ReadNewGameLimitProgress(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources()
        };
        return (logical, selected);

        BattleCombatantDefinition Enemy(BattleEnemyPlacement placement, int slot, bool original)
        {
            byte level = checked((byte)Math.Clamp(placement.Level, 1, 100));
            NativeBattleEnemyTemplate? template = original ? assets.ReadEnemyTemplate(placement.ModelId, level) with { NativeDrawTables = null } : null;
            return new()
            {
                Id = $"enemy-{slot}",
                Name = assets.ReadEnemyName(placement.ModelId),
                ModelId = placement.ModelId,
                SceneSlot = slot,
                Level = level,
                MaximumHp = 500,
                CurrentHp = 443,
                Information246 = assets.ReadEnemyNativeInformation246(placement.ModelId),
                Information254 = assets.ReadEnemyNativeEntryFlags(placement.ModelId),
                NativeTemplate = template,
                InitialHidden = original && placement.Hidden
            };
        }
    }

    private sealed class EntryObservations
    {
        internal bool WaveTask { get; private set; }
        internal bool AppearanceTask { get; private set; }
        internal bool AppearanceCompleted { get; set; }
        internal bool BarrierTask { get; private set; }
        internal int PeakQueuedDescriptors { get; private set; }
        internal bool AppearanceDeferredWhileLoadsWereQueued { get; private set; }
        internal List<int> CompletedRows { get; } = [];

        internal void Observe(NativeBattleGraphicsState graphics)
        {
            WaveTask |= Reached(NativeBattleDescriptorTaskKind.Map500AC0);
            AppearanceTask |= Reached(NativeBattleDescriptorTaskKind.Appearance500B60);
            BarrierTask |= Reached(NativeBattleDescriptorTaskKind.Barrier5085F0);
            ImmutableArray<int> queued = graphics.Descriptors.Queue;
            PeakQueuedDescriptors = Math.Max(PeakQueuedDescriptors, queued.Length);
            AppearanceDeferredWhileLoadsWereQueued |= graphics.EntryLifecycle == 2 && !AppearanceTask &&
                queued.Select(slot => graphics.Descriptors.Packets[slot]).Any(packet => packet.Command == 102) &&
                queued.Select(slot => graphics.Descriptors.Packets[slot]).All(packet => packet.Command != 13);
            foreach (NativeBattleGraphicsModel model in graphics.Models)
            {
                if ((model.Flags0 & 2) != 0 && model.EffectCounter97 == 2 && !CompletedRows.Contains(model.Index))
                {
                    CompletedRows.Add(model.Index);
                }
            }

            bool Reached(NativeBattleDescriptorTaskKind kind) => graphics.Descriptors.Tasks.Any(task => task.Kind == kind);
        }
    }
}
