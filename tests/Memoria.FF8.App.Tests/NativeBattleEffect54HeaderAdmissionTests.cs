using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect54HeaderAdmissionTests
{
    [Fact]
    public void Canonical_local_packet_preparation_retains_cold_group_zero_and_publishes_one_contact()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT54_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT54_HEADER_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new header-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        BattleCombatantDefinition caster = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[3]!);
        Assert.Equal(56, caster.ModelId);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 223 && row.MagicId == 54 && row.KernelSection == 3 && row.KernelIndex == 223);
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState original = source.NativeActionPackets ?? throw new NotSupportedException("The source requires its current action packet owners.");
        NativeBattleActionHeaderSlot retained = original.Headers[original.ActionCount];
        Assert.Equal((byte)0, retained.ReadByte(17));
        string targetId = source.NativeGraphics.ActorIds[0] ?? throw new NotSupportedException("The source requires its current physical target.");
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.Id == targetId).NativeStatus ?? throw new NotSupportedException("The target requires its actual saved and battle status owners.");
        (NativeBattleActionPacketsState packets, int headerIndex) = original.Prepare(new(3, 8, 11, unchecked((sbyte)action.RawRecord[4]), 223, 54, null));
        Assert.Equal(retained.ReadByte(17), packets.Headers[headerIndex].ReadByte(17));
        (packets, int contactIndex) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 0, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, int publishedIndex) = packets.Publish(1);
        Assert.Equal(original.ActionCount, headerIndex);
        Assert.Equal(headerIndex, publishedIndex);
        Assert.Equal(original.ContactCount, contactIndex);
        NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
        Assert.Equal((byte)1, header.ReadByte(0x10));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal(retained.ReadByte(17), header.ReadByte(17));
        Assert.Equal(retained.UnknownByteMask & (1u << 17), header.UnknownByteMask & (1u << 17));
        Assert.Equal(contactIndex, header.FirstContactIndex);
        Assert.Equal(0x1D28344u + 24u * (uint)contactIndex, BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan().Slice(8, 4)));
        Assert.NotNull(packets.Contacts[contactIndex]);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "header-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Canonical local postformula packet preparation only; no action execution or snapshot mutation", source.Frame, PhysicalCaster = 3, caster.ModelId, action.Command, action.ActionId, action.MagicId, Sequence = 11, HeaderIndex = headerIndex, ContactIndex = contactIndex, header.FirstContactIndex, TargetCount = header.ReadByte(0x10), RetainedGroup = header.ReadByte(17), RootMaximumGroup = 0, Header = header.Payload, Contact = packets.Contacts[contactIndex], SnapshotUnchanged = true }));
    }
}
