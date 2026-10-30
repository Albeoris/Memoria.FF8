using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleDuel112CastTests
{
    [Fact]
    public void Prepare_named_cold_Duel_opening_standalone_migration()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select the original cold Duel opening input and a separate migration output directory.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input!))!;
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Null(source.ActiveAction);
        Assert.Equal(0, source.Frame);
        NativeBattleRetainedMemory retained = source.Definition.NativeRetainedMemory!;
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, retained.ColdExecutableSha256);
        NativeBattleEffectMemory process = RequireCold(source.NativeEffectMemory!);
        NativeBattleEffectMemory definitionMemory = RequireCold(retained.EffectMemory!);
        NativeBattleEffectBinding previous = source.Definition.NativeEffectCatalog[273];
        Assert.Equal((ushort)274, previous.Effect);
        Assert.Equal(0x6897E0u, previous.Entry);
        Assert.Equal(0x6897F0u, previous.Initializer);
        Assert.Null(previous.InitializerResource);
        Assert.Null(previous.InitializerAsset);
        Assert.Null(previous.Duel274Resources);
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectBinding current = game.ReadNativeEffectCatalogForEffects([274])[273];
        Assert.NotNull(current.InitializerResource);
        Assert.NotNull(current.InitializerAsset);
        current.Duel274Resources!.Validate();
        BattleSnapshot migrated = source with { NativeEffectMemory = process with { Duel274 = NativeBattleDuel274State.FromColdProcess() }, Definition = source.Definition with { NativeRetainedMemory = retained with { EffectMemory = definitionMemory with { Duel274 = NativeBattleDuel274State.FromColdProcess() } }, NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(273, current) } };
        Directory.CreateDirectory(output!);
        string destination = Path.Combine(output!, "migrated-cold-duel-opening.battle.json");
        File.WriteAllText(destination, JsonSerializer.Serialize(migrated));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(destination))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(restored.Capture()));
        File.WriteAllText(Path.Combine(output!, "migration-receipt.json"), JsonSerializer.Serialize(new { Status = "restore-validated", Input = input, Output = destination, OriginalSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input!))), MigratedSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(destination))), source.Frame, NativeCallback = source.NativeGraphics.Callback, Changes = new[] { "NativeEffectMemory.Duel274: absent to explicit cold process owner", "Definition.NativeRetainedMemory.EffectMemory.Duel274: absent to explicit cold process owner", "Definition.NativeEffectCatalog[273]: original authenticated initializer asset and Duel274 resources" }, Continuation = "Installed_Duel112_completes_original_action_and_independent_standalones(true) with MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT set to the migrated output" }));

        static NativeBattleEffectMemory RequireCold(NativeBattleEffectMemory memory)
        {
            Assert.Null(memory.Duel274);
            Assert.Null(memory.Arena);
            Assert.Equal(0, memory.Active);
            Assert.Equal(0, memory.Cursor);
            Assert.Equal(0, memory.SelectedIndex);
            Assert.Equal(0, memory.AllocationCount);
            Assert.All(memory.Allocations, Assert.Null);
            Assert.Null(memory.Entry21DFEC4);
            Assert.Equal(0u, memory.ReturnedTaskPointer1D96AAC);
            return memory;
        }
    }

    [Fact]
    public void Capture_named_Duel112_standalone_at_target_phase()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select the standalone input and diagnostic output directory.");
        int phase = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_TARGET_PHASE") ?? "45");
        Assert.InRange(phase, 1, 151);
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(input!))!;
        ushort selectedEffectId = source.NativeGraphics?.OriginalEffect?.Binding.Effect ?? 112;
        Assert.Contains(selectedEffectId, new ushort[] { 112, 212, 215, 216 });
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        Directory.CreateDirectory(output!);
        BattleSnapshot before = source;
        try
        {
            for (int callback = 0; callback < 512; callback++)
            {
                if (battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } effect && effect.Binding.Effect == selectedEffectId && effect.Pools[1].Slots[0].ReadWord(12) >= phase)
                {
                    BattleSnapshot selected = battle.Capture();
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected", selected, scene.Render(battle, 320, 224).ToArray());
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, "selected.battle.json")))!);
                    clone.AttachNativeEffectResourceResolver(effectId => game.ReadNativeEffectCatalogForEffects([effectId])[effectId - 1]);
                    Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(clone.Capture()));
                    before = selected;
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, source.Definition, true).Render(clone, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "selected-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Input = input, TargetPhase = phase, ActualPhase = selected.NativeGraphics!.OriginalEffect!.Pools[1].Slots[0].ReadWord(12), selected.Frame, selected.NativeGraphics.Callback }));
                    return;
                }
                before = battle.Capture();
                battle.Tick();
            }
            Assert.Fail("The selected standalone did not reach the requested active Duel target phase.");
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            throw;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Duel112_completes_original_action_and_independent_standalones(bool duelCommandRoute)
    {
        RunOriginalDuelEffect(112, duelCommandRoute);
    }

    [Fact]
    public void Installed_direct_Duel112_component_completes_original_action_and_independent_standalones()
    {
        RunOriginalDuelEffect(112, false);
    }

    internal static void RunOriginalDuelEffect(ushort effectId, bool duelCommandRoute, byte? requiredBasicAction = null)
    {
        if (requiredBasicAction is byte basicAction)
        {
            Assert.InRange(basicAction, (byte)0, (byte)5);
            Assert.True(duelCommandRoute);
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select the installed original game resources.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        string? namedInput = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        Assert.SkipUnless(namedInput is null || duelCommandRoute, "A selected Duel opening standalone requires the actual command route.");
        BattleSnapshot? namedSource = namedInput is null ? null : JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(namedInput));
        BattleNativeAction action = assets.NativeActions.First(action => action.Command == (duelCommandRoute ? 241 : 239) && action.MagicId == effectId);
        BattleDefinition source = namedSource?.Definition ?? NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 1, effectId);
        BattleCombatantDefinition actor = source.Combatants[0] with { Name = "Zell" };
        if (!duelCommandRoute)
        {
            actor = actor with { NativeCommands = actor.NativeCommands.Add(new(239, action.Name ?? "Duel", action.TargetFlags!.Value, checked((byte)action.ActionId))) };
        }
        BattleDefinition definition = namedSource?.Definition ?? source with { NativeVictoryFile = assets.ReadNativeBattleFile(766), Combatants = [actor, source.Combatants[1]] };
        BattleSession battle = namedSource is null ? new(definition, BattleAtbMode.TurnBased) : BattleSession.Restore(namedSource);
        battle.AttachNativeEffectResourceResolver(requestedEffect => game.ReadNativeEffectCatalogForEffects([requestedEffect])[requestedEffect - 1]);
        BattleScenePresentation scene = new(assets, definition, true);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_EVIDENCE") ?? Path.GetTempPath(), "duel-" + effectId + "-" + (duelCommandRoute ? "command-" : "raw-") + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine("Duel evidence: " + output);
        BattleSnapshot before = battle.Capture();
        BattleNativeDuelState? savedDuel = namedSource?.NativeLimits.SingleOrDefault(owner => owner.ActorId == "party")?.Duel;
        bool queuedChoice = savedDuel is { Phase: BattleNativeDuelPhase.Finisher } && NativeBattleDuelRules.ActionId(definition.Combatants[0].NativeLimit!, savedDuel.MoveSetId) == action.ActionId;
        bool selected = false;
        bool drawn = false;
        bool midpoint = false;
        int captured112Frames = 0;
        int duelInputs = 0;
        bool basicQueued = false;
        bool basicObserved = false;
        bool basicMidpoint = false;
        try
        {
            if (namedSource is null)
            {
                for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
                {
                    before = battle.Capture();
                    battle.Tick();
                }
                Assert.True(battle.CanChoosePlayerCommand("party"));
                if (duelCommandRoute)
                {
                    battle = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(battle.Capture() with { NativeLimits = [new("party", 1)] }));
                    battle.AttachNativeEffectResourceResolver(requestedEffect => game.ReadNativeEffectCatalogForEffects([requestedEffect])[requestedEffect - 1]);
                }
                Publish("before-command");
                battle.QueueNativePlayerCommand("party", "enemy", duelCommandRoute ? (byte)11 : (byte)239, duelCommandRoute ? (ushort)0 : action.ActionId);
                CompareNext("queued");
            }
            else
            {
                CompareNext("resumed");
            }
            for (int callback = 0; callback < 1024; callback++)
            {
                before = battle.Capture();
                battle.Tick();
                if (duelCommandRoute && !queuedChoice && battle.GetNativeDuel("party") is { Phase: BattleNativeDuelPhase.Ready })
                {
                    BattleNativeDuelChoice choice = NextChoice();
                    Publish("before-input-" + duelInputs);
                    foreach (ushort button in choice.Buttons)
                    {
                        battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1), 0);
                        battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1), button);
                    }
                    Assert.Equal(choice.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect, battle.GetNativeDuel("party")!.Phase);
                    queuedChoice = choice.ActionId == action.ActionId;
                    if (requiredBasicAction == choice.ActionId && !choice.Finisher)
                    {
                        basicQueued = true;
                    }
                    File.WriteAllText(Path.Combine(output, "duel-input-" + duelInputs + ".json"), JsonSerializer.Serialize(choice));
                    CompareNext("duel-input-" + duelInputs++);
                }
                NativeBattleGraphicsState graphics = battle.NativeGraphics!;
                if (basicQueued && !basicMidpoint && graphics.OriginalEffect is { Binding.Effect: 274, ReturnedDescriptor: > 0 } opening)
                {
                    NativeBattleEffectTaskPool? targetPool = opening.Pools.SingleOrDefault(pool => pool.DescriptorAddress == 0x25084A8);
                    NativeBattleModulePoolTask? target = targetPool?.Slots.Single();
                    if (target is not null && (target.Flags & 1) != 0 && target.ReadByte(15) == requiredBasicAction + 2 && target.ReadWord(12) > 0)
                    {
                        basicObserved = true;
                        if (target.ReadWord(12) >= Math.Max(1, target.ReadByte(17) / 2))
                        {
                            Assert.NotEmpty(graphics.PrimitiveFrame!.LastCommands);
                            CompareNext("basic-midpoint-" + requiredBasicAction);
                            basicMidpoint = true;
                        }
                    }
                }
                if (effectId == 112 && graphics.OriginalEffect is { Binding.Effect: 112 } captureEffect && (captureEffect.ReturnedDescriptor is > 0 || before.NativeGraphics?.OriginalEffect?.ReturnedDescriptor is > 0))
                {
                    if (NativeBattleExplicitComponentCapture.AssertFrame(battle.Capture(), 112) > 0)
                    {
                        captured112Frames++;
                    }
                }
                bool active = graphics.OriginalEffect is { ReturnedDescriptor: > 0 } selectedEffect && selectedEffect.Binding.Effect == effectId && (!duelCommandRoute || queuedChoice);
                if (active)
                {
                    selected = true;
                    if (!drawn && graphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        BattleSnapshot backgroundSource = battle.Capture();
                        NativeBattleGraphicsState backgroundGraphics = backgroundSource.NativeGraphics!;
                        NativeBattlePrimitiveFrame withoutEffect = backgroundGraphics.PrimitiveFrame! with { LastCommands = backgroundGraphics.PrimitiveFrame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(backgroundSource with { NativeGraphics = backgroundGraphics with { PrimitiveFrame = withoutEffect } });
                        byte[] backgroundPixels = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                        drawn = Enumerable.Range(0, pixels.Length / 4).Any(index => !pixels.AsSpan(index * 4, 3).SequenceEqual(backgroundPixels.AsSpan(index * 4, 3)));
                    }
                    int expectedPools = effectId switch { 112 or 216 => 5, 212 => 4, 215 => 3, _ => throw new NotSupportedException("The selected Duel finisher has no original task-bank contract in this fixture.") };
                    if (!midpoint && graphics.OriginalEffect!.Pools.Length == expectedPools && graphics.OriginalEffect.Pools[1].Slots[0].ReadWord(12) >= 70)
                    {
                        CompareNext("midpoint");
                        midpoint = true;
                    }
                }
                if (selected && Released() && (!duelCommandRoute || battle.GetNativeDuel("party") is null))
                {
                    if (effectId == 112)
                    {
                        Assert.True(captured112Frames > 1);
                    }
                    Assert.True(drawn);
                    Assert.True(midpoint);
                    Assert.True(requiredBasicAction is null || basicObserved);
                    Assert.True(requiredBasicAction is null || basicMidpoint);
                    Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == "enemy");
                    battle.SetMode(BattleAtbMode.Normal);
                    CompareNext("completed");
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", duelCommandRoute, requiredBasicAction, basicQueued, basicObserved, basicMidpoint, duelInputs, queuedChoice, selected, drawn, midpoint, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback, Mode = battle.Mode.ToString() }));
                    return;
                }
            }
            Assert.Fail("The selected Duel effect did not release its actual module/action owners within the diagnostic bound; the saved boundary identifies any remaining command-control producer.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", duelCommandRoute, requiredBasicAction, basicQueued, basicObserved, basicMidpoint, duelInputs, queuedChoice, selected, drawn, midpoint, before.Frame, NativeCallback = before.NativeGraphics?.Callback, Error = error.ToString() }));
            throw;
        }

        BattleNativeDuelChoice NextChoice()
        {
            BattleNativeLimitDefinition rules = definition.Combatants[0].NativeLimit!;
            BattleNativeDuelState state = battle.GetNativeDuel("party")!;
            ushort desiredAction = requiredBasicAction is byte required && !basicQueued ? required : action.ActionId;
            Queue<(byte MoveSet, byte First)> pending = new();
            HashSet<byte> visited = [state.MoveSetId];
            pending.Enqueue((state.MoveSetId, byte.MaxValue));
            while (pending.TryDequeue(out var current))
            {
                for (int index = 1; index <= 3; index++)
                {
                    byte next = rules.DuelMoveSets[4 * current.MoveSet + index];
                    if (next == byte.MaxValue || !visited.Add(next))
                    {
                        continue;
                    }
                    byte first = current.First == byte.MaxValue ? next : current.First;
                    if (NativeBattleDuelRules.ActionId(rules, next) == desiredAction)
                    {
                        //Manual native input compares button edges even when the learned flag hides the choice label.
                        return battle.GetNativeDuelChoices("party").Single(choice => choice.MoveSetId == first);
                    }
                    pending.Enqueue((next, first));
                }
            }
            throw new InvalidDataException("The original Duel move graph has no route from the current move set to the selected finisher.");
        }
        bool Released()
        {
            NativeBattleGraphicsState graphics = battle.NativeGraphics!;
            return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Capture().NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
        }
        void Publish(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224).ToArray());
        }
        void CompareNext(string name)
        {
            Publish(name);
            BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output, name + ".battle.json")))!);
            clone.AttachNativeEffectResourceResolver(requestedEffect => game.ReadNativeEffectCatalogForEffects([requestedEffect])[requestedEffect - 1]);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            before = battle.Capture();
            battle.Tick();
            clone.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
            Publish(name + "-next");
        }
    }
}
