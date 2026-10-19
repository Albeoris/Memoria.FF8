using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBreath235ComponentTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Controlled_sequence_free235_module_executes_draw32_and_releases_through_disk_continuations()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH_COMPONENT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH_COMPONENT_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(sourcePath is not null && output is not null && directory is not null, "Select an installed original235 resource snapshot and a distinct controlled component evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(sourcePath!))!;
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
        NativeBattleOriginalEffectState loaded = Assert.IsType<NativeBattleOriginalEffectState>(graphics.OriginalEffect);
        Assert.Equal(235, loaded.RequestedEffect);
        Assert.Null(loaded.ReturnedDescriptor);
        Assert.Empty(loaded.Pools);
        NativeBattleActionPacketsState packets = Assert.IsType<NativeBattleActionPacketsState>(source.NativeActionPackets);
        NativeBattleActionGraphicsState original = Assert.IsType<NativeBattleActionGraphicsState>(graphics.ActionGraphics);
        NativeBattleContactPacket contact = packets.ReadContact(original.FirstContactIndex);
        // A controlled component uses original50B190 with sequence0. It does not claim a reachable enemy bank action or alter the casting handshake.
        (packets, int header) = packets.Prepare(new(original.ActorIndex, 8, 0, -1, 94, 235, null));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(0x50B190u, NativeBattleActionGraphics.SelectService(packets.Headers[header]).Callback);
        (NativeBattleDescriptorState descriptors, _) = NativeBattleDescriptorState.Clear().Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot component = source with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors, ActionGraphics = null, OriginalEffect = null } };
        BattleSession battle = BattleSession.Restore(component);
        BattleScenePresentation scene = new(assets, component.Definition, true);
        Publish("controlled-before-entry", component);
        battle = Load("controlled-before-entry");
        BattleSnapshot prior = battle.Capture();
        bool entered = false;
        bool visible = false;
        bool restoredLive = false;
        bool completed = false;
        int callbacks = 0;
        int peakChangedRgb = 0;
        try
        {
            for (; callbacks < 4096; callbacks++)
            {
                prior = battle.Capture();
                battle.AdvanceNativeGraphicsPresentation(Callback);
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics!;
                entered |= current.NativeEffectMemory?.Cinematic?.Effect == 235 && currentGraphics.OriginalEffect?.ReturnedDescriptor is > 0;
                NativeBattlePrimitiveFrame? frame = currentGraphics.PrimitiveFrame;
                if (entered && frame is { LastCommands.IsEmpty: false })
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> references = NativeBattleTargetGuardianCastTests.GuardianPacketReferences(prior, current, 235, game);
                    if (references.Count != 0)
                    {
                        byte[] rendered = scene.Render(battle, 320, 224).ToArray();
                        BattleSession background = BattleSession.Restore(current with { NativeGraphics = currentGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !references.Contains(command.Command.Packet)).ToImmutableArray() } } });
                        byte[] plain = new BattleScenePresentation(assets, component.Definition, true).Render(background, 320, 224).ToArray();
                        int changed = 0;
                        for (int offset = 0; offset < rendered.Length; offset += 4)
                        {
                            if (!rendered.AsSpan(offset, 3).SequenceEqual(plain.AsSpan(offset, 3)))
                            {
                                changed++;
                            }
                        }
                        if (changed > peakChangedRgb)
                        {
                            peakChangedRgb = changed;
                            visible = true;
                            Publish($"peak-{callbacks:D4}", current);
                        }
                    }
                    if (!restoredLive && currentGraphics.OriginalEffect?.ReturnedDescriptor is > 0)
                    {
                        VerifyContinuation("live", current);
                        restoredLive = true;
                    }
                }
                if (entered && currentGraphics.OriginalEffect?.ReturnedDescriptor == 0 && !currentGraphics.Descriptors.Queue.Any(index => currentGraphics.Descriptors.Packets[index].Command == 104))
                {
                    completed = true;
                    VerifyContinuation("completed", current);
                    break;
                }
            }
            Assert.True(entered, "The controlled descriptor never invoked original235.");
            Assert.True(visible, "Original235 submitted no visible packet contribution.");
            Assert.True(restoredLive, "The controlled235 component did not check a live disk continuation.");
            Assert.True(completed, "Original235 did not release its module and controlled descriptor.");
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scope = "controlled-sequence-free-module235", callbacks, peakChangedRgb, restoredLive, battle.Frame, NativeCallback = battle.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            Publish("before-boundary", prior);
            Publish("failed-live", battle.Capture());
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Scope = "controlled-sequence-free-module235", callbacks, error = error.ToString() }));
            throw;
        }

        void Publish(string name, BattleSnapshot snapshot) => NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
        BattleSession Load(string name) => BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, name + ".battle.json")))!);
        void VerifyContinuation(string name, BattleSnapshot snapshot)
        {
            Publish(name, snapshot);
            BattleSession first = Load(name);
            BattleSession second = Load(name);
            first.AdvanceNativeGraphicsPresentation(Callback);
            second.AdvanceNativeGraphicsPresentation(Callback);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224), new BattleScenePresentation(assets, component.Definition, true).Render(second, 320, 224));
            Publish(name + "-next", first.Capture());
        }
    }
}

