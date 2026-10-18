using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanRepeatedAcceptanceTests
{
    [Fact]
    public void Known_Scan_uses_alternate_primary_auto_finishes_pages_and_releases_with_disk_continuations()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(sourcePath is not null && File.Exists(sourcePath), "Select a standalone ready Scan component with MEMORIA_FF8_SCAN_SNAPSHOT.");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        byte[] originalBytes = File.ReadAllBytes(sourcePath!);
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, candidate => candidate.Ability == BattleAbility.Magic && candidate.NativeAction is { MagicId: 40, AttackType: 12 });
        Assert.Equal(50, action.Id);
        BattleCombatantDefinition caster = Assert.Single(original.Definition.Combatants, actor => actor.IsPlayer && original.Combatants.Single(saved => saved.Id == actor.Id).NativeMagicInventory!.Any(stock => stock.SpellId == action.Id && stock.Count >= 2));
        BattleCombatantDefinition target = original.Definition.Combatants.First(actor => !actor.IsPlayer && original.Combatants.Single(saved => saved.Id == actor.Id).CurrentHp > 0);
        Assert.Equal(61, target.ModelId);
        Assert.Equal(false, original.Definition.NativeScanAlwaysShow);
        NativeScannedModels history = original.NativeScannedModels!.Add(target.ModelId);
        BattleSnapshot source = original with
        {
            Definition = original.Definition with { NativeScannedModels = original.Definition.NativeScannedModels!.Add(target.ModelId) },
            NativeScannedModels = history
        };
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_EVIDENCE") ?? Path.GetTempPath(), "scan-repeated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "source-original.battle.json"), originalBytes);
        string controlledPath = Path.Combine(output, "controlled-known.battle.json");
        File.WriteAllBytes(controlledPath, JsonSerializer.SerializeToUtf8Bytes(source));
        File.WriteAllText(Path.Combine(output, "controlled-known.receipt.json"), JsonSerializer.Serialize(new { OriginalSha256 = Convert.ToHexString(SHA256.HashData(originalBytes)), ControlledSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(controlledPath))), TargetModel = target.ModelId, Changes = new[] { "Definition.NativeScannedModels: preserve all bits and add target model", "Snapshot.NativeScannedModels: preserve all bits and add target model" } }));
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(controlledPath))!);
        Assert.True(battle.NativeEntryReady);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.True(battle.Combatants.Single(actor => actor.Id == caster.Id).IsReady);
        Assert.Equal(2, Stock(source));
        int scansBefore = battle.Events.Count(entry => entry.Kind == "Scan");
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<BattleSession> continuations = [];
        HashSet<string> boundaries = [];
        HashSet<ushort> pages = [];
        HashSet<uint> callbacks = [];
        bool selected = false;
        bool contact = false;
        bool resultText = false;
        int spinnerPixels = 0;
        int boardPixels = 0;
        uint pageCount = 0;
        BattleSnapshot prior = source;
        Console.WriteLine($"Repeated Scan action50/effect40; controlled known model61; evidence: {output}");
        try
        {
            Boundary("controlled-ready");
            battle.QueueAction(caster.Id, target.Id, BattleAbility.Magic, action.Id);
            foreach (BattleSession restored in continuations)
            {
                restored.QueueAction(caster.Id, target.Id, BattleAbility.Magic, action.Id);
            }
            Boundary("queued");
            for (int callback = 0; callback < 4096; callback++)
            {
                prior = battle.Capture();
                battle.PublishNativeScanMappedButtons(0);
                foreach (BattleSession restored in continuations)
                {
                    restored.PublishNativeScanMappedButtons(0);
                }
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                foreach (BattleSession restored in continuations)
                {
                    restored.Tick();
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(restored.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224));
                }
                continuations.Clear();
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                if (graphics.OriginalEffect is { Binding.Effect: 40 } effect)
                {
                    selected = true;
                    NativeBattleModulePoolTask[] live = effect.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0).ToArray();
                    foreach (NativeBattleModulePoolTask task in live)
                    {
                        callbacks.Add(task.Callback);
                    }
                    Assert.DoesNotContain(live, task => task.Callback is 0x84D4B0 or 0x84F860 or 0x84ED90 or 0x8507F0);
                    if (live.Any(task => task.Callback == 0x850B40))
                    {
                        Assert.Equal((byte)2, current.NativeActionScratch!.Command);
                        Assert.Equal((ushort)50, current.NativeActionScratch.Action);
                        NativeBattleActionPacketsState packets = current.NativeActionPackets ?? throw new InvalidDataException("Repeated Scan requires its actual admitted contact packets.");
                        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters!.HeaderIndex7C];
                        NativeBattleContactPacket packet = packets.ReadContact(header.FirstContactIndex!.Value);
                        Assert.Equal((byte)3, (byte)(packet.Primary.Flags2 & 3));
                        contact = true;
                        Boundary("alternate-primary-contact");
                    }
                    if (live.Any(task => task.Callback == 0x850C70))
                    {
                        Boundary("alternate-spinner");
                        if (spinnerPixels == 0)
                        {
                            spinnerPixels = VisiblePacketChanges(current, command => command.Command.Packet.Bank == "20DFAB8" && command.Command.WordOffset == 10 && command.Command.Opcode == 0xE1 && command.Command.Words[0] == 0xE1000220);
                            if (spinnerPixels > 0)
                            {
                                Boundary("visible-alternate-spinner");
                            }
                        }
                    }
                    if (live.Any(task => task.Callback == 0x850EB0))
                    {
                        Boundary("alternate-board");
                        if (boardPixels == 0)
                        {
                            boardPixels = VisiblePacketChanges(current, IsBoardCommand);
                            if (boardPixels > 0)
                            {
                                Boundary("visible-alternate-board");
                            }
                        }
                    }
                    NativeBattleModulePoolTask? result = live.FirstOrDefault(task => task.Callback == 0x850FD0);
                    NativeBattleScanProcess process = current.NativeEffectMemory!.Scan!;
                    pageCount = Math.Max(pageCount, process.Results.Count);
                    if (result is not null && result.ReadByte(0x29) == 2 && pages.Add(result.ReadWord(0x6C)))
                    {
                        Boundary("page-" + result.ReadWord(0x6C));
                    }
                    IReadOnlyList<NativeScanWindowText> windows = NativeScanWindowTextProjection.ProjectForViewport(process.Text.ReadFrame(graphics.UiServices!.Flags942), battle.CreateNativeScanTextExpansionServices(), new(0, 0, 320, 224));
                    if (windows.Any(window => window.Index == 1 && window.Lines.Any(line => line.Line.Runs.Any(run => run.Text.Length > 0))))
                    {
                        resultText = true;
                        Boundary("result-prefix");
                    }
                }
                if (selected && Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(selected && contact && resultText);
            Assert.Contains(0x850B40u, callbacks);
            Assert.Contains(0x850FD0u, callbacks);
            Assert.Contains(0x850C70u, callbacks);
            Assert.Contains(0x850EB0u, callbacks);
            Assert.True(pageCount > 1);
            Assert.Equal((int)pageCount, pages.Count);
            Assert.True(spinnerPixels > 0, "The alternate spinner's actual44B packets must change rendered RGB.");
            Assert.True(boardPixels > 0, "The alternate board's actual material-matched packets must change rendered RGB.");
            Assert.Equal(scansBefore, battle.Events.Count(entry => entry.Kind == "Scan"));
            Assert.True(completed.NativeScannedModels!.Contains(61));
            Assert.Equal(1, Stock(completed));
            Assert.True(Released(completed));
            foreach (BattleCombatantSnapshot participant in source.Combatants)
            {
                Assert.Equal(participant.CurrentHp, completed.Combatants.Single(actor => actor.Id == participant.Id).CurrentHp);
            }
            Boundary("released");
            battle.PublishNativeScanMappedButtons(0);
            battle.Tick();
            foreach (BattleSession restored in continuations)
            {
                restored.PublishNativeScanMappedButtons(0);
                restored.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224));
            }
            NativeBattleRestorationPresentationTests.Publish(output, "released-next", battle.Capture(), scene.Render(battle, 320, 224));
            Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath!));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", completed.Frame, pages, boundaries, spinnerPixels, boardPixels, callbacks }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", Exception = error.ToString(), prior.Frame, Callback = prior.NativeGraphics?.Callback }));
            throw;
        }

        int Stock(BattleSnapshot snapshot) => Assert.Single(snapshot.Combatants.Single(actor => actor.Id == caster.Id).NativeMagicInventory!, stock => stock.SpellId == action.Id).Count;

        bool IsBoardCommand(NativeBattleLogicalResolvedCommand command)
        {
            NativeBattleStaticMeshResource mesh = game.ReadNativeScanVisualResources().MeshesByAddress[0x15CF614];
            int face = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 16;
            uint lastUv = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(face + 20));
            return command.Command.Packet.Bank == "20DFAB8" && (command.Command.Opcode & 0xFC) == 0x2C && command.Command.Words.Length == 9 && command.Command.Words[2] == BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(face + 12)) && command.Command.Words[4] == BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(face + 16)) && (command.Command.Words[6] & 0xFFFF) == (lastUv & 0xFFFF) && (command.Command.Words[8] & 0xFFFF) == lastUv >> 16;
        }

        int VisiblePacketChanges(BattleSnapshot snapshot, Func<NativeBattleLogicalResolvedCommand, bool> matches)
        {
            NativeBattlePrimitiveFrame primitive = snapshot.NativeGraphics!.PrimitiveFrame!;
            HashSet<NativeBattlePrimitiveReference> packets = primitive.LastCommands.Where(matches).Select(command => command.Command.Packet).ToHashSet();
            if (packets.Count == 0)
            {
                return 0;
            }
            NativeBattlePrimitiveFrame removed = primitive with { LastCommands = primitive.LastCommands.Where(command => !packets.Contains(command.Command.Packet)).ToImmutableArray() };
            BattleSession background = BattleSession.Restore(snapshot with { NativeGraphics = snapshot.NativeGraphics with { PrimitiveFrame = removed } });
            byte[] normal = scene.Render(battle, 320, 224);
            byte[] comparison = new BattleScenePresentation(assets, source.Definition, true).Render(background, 320, 224);
            return Enumerable.Range(0, normal.Length / 4).Count(pixel => !normal.AsSpan(pixel * 4, 3).SequenceEqual(comparison.AsSpan(pixel * 4, 3)));
        }

        void Boundary(string name)
        {
            if (!boundaries.Add(name))
            {
                return;
            }
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, scene.Render(battle, 320, 224));
            using FileStream stream = File.OpenRead(Path.Combine(output, name + ".battle.json"));
            BattleSnapshot disk = JsonSerializer.Deserialize<BattleSnapshot>(stream)!;
            BattleSession restored = BattleSession.Restore(disk);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224));
            continuations.Add(restored);
        }
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && snapshot.NativeGraphics!.Descriptors.Queue.IsEmpty && snapshot.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
}
