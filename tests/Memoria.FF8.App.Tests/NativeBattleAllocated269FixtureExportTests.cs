using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAllocated269FixtureExportTests
{
    [Fact]
    public void Export_original269_queued_component_from_actual_native94_allocation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ALLOCATED269_FIXTURE_OUTPUT");
        Assert.SkipUnless(directory is not null && output is not null, "Select original resources and a distinct allocation fixture output.");
        _ = Prepare(directory!, output!);
    }

    internal static BattleSnapshot Prepare(string directory, string output)
    {
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        NativeEffectFixtureRequest baseline = new("allocated269-baseline", 45, 8, 84, 0, 1200, 240) { EncounterId = 436, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };
        BattleSnapshot original = NativeBattleEffectFixtureExporter.Prepare(baseline, directory, Path.Combine(output, "fixture"), "before-entry").Snapshot;
        (BattleSnapshot ready, int caster) = NativeBattleEvilEye282FixturePreparation.Prepare(game, assets, original, output!);
        Assert.InRange(caster, 3, 6);
        NativeBattleGraphicsState graphics = ready.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("The allocation requires its actual graphics owner.");
        Assert.Null(graphics.OriginalEffect);
        Assert.Empty(graphics.Descriptors.Queue);
        NativeBattleEnemyAllocation allocation = Assert.Single(ready.NativeAllocations);
        Assert.Equal(3, allocation.SceneSlot);
        BattleCombatantSnapshot allocated = Assert.Single(ready.Combatants, actor => actor.Id == allocation.Id);
        Assert.Equal(caster, allocated.NativeParticipant!.ActorIndex);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 266 && row.MagicId == 269);
        byte sequence = NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(94, allocated.NativeParticipant!.ResolvedLevel ?? throw new InvalidDataException("The allocated original caster requires its resolved native level.")), action);
        Assert.Equal((byte)15, sequence);
        BattleCombatantSnapshot target = Assert.Single(ready.Combatants, actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The declared contact requires its actual target status owner.");
        target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattlePresentationStatusMemory presentation = (ready.NativePresentationStatusMemory ?? throw new InvalidDataException("The declared contact requires its physical presentation status owner.")).Observe(0, target.NativeStatus!.Presentation);
        ready = ready with { Combatants = ready.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray(), NativePresentationStatusMemory = presentation, Definition = ready.Definition with { NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([269]) } };
        NativeBattleActionPacketsState packets = ready.NativeActionPackets!;
        Assert.Equal((byte)0, packets.ActionCount);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(checked((byte)caster)), action.Command, sequence, -1, action.ActionId, action.MagicId, null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        BattleSnapshot queued = (ready with { NativeActionPackets = packets}).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        string readyPath = Save("after-entry", ready);
        string queuedPath = Save("queued-action104", queued);
        BattleSession cold = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(queued));
        Assert.Equal(JsonSerializer.Serialize(queued), JsonSerializer.Serialize(cold.Capture()));
        Assert.Null(cold.Presentation!.Graphics.OriginalEffect);
        Assert.Equal((byte)caster, ReadOriginalModelOperand(packets.ModelAliases, packets.ReadHeader(header).Source));
        Assert.Equal((byte)15, packets.ReadHeader(header).Sequence);
        Assert.Equal((byte)1, packets.ReadHeader(header).ReadContactCount());
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "prepared", Effect = 269, Encounter = 436, Stage = 125, OriginalModel = 94, NativeModel = ReadOriginalModelCell(graphics, caster).ModelId4, ActualCaster = caster, allocation.SceneSlot, Command = action.Command, Action = action.ActionId, Sequence = sequence, HeaderIndex = header.Ordinal, header = packets.ReadHeader(header), Contact = packets.ReadContact(new BattleContactOccurrence(0)), DeclaredContactValue = 7, EffectCallbacks = 0, ready.Frame, NativeCallback = graphics.Callback, ReadyPath = readyPath, ReadySha256 = Hash(readyPath), QueuedPath = queuedPath, QueuedSha256 = Hash(queuedPath), Scope = "Actual existing native prototype allocation102/activation113; no death-callback or gameplay eligibility claim; no269 callback executed" }));

        return queued;

        string Save(string name, BattleSnapshot snapshot)
        {
            string path = Path.Combine(output!, name + ".battle.json");
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
            return path;
        }

        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
