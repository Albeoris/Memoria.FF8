using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        byte[] unchanged = File.ReadAllBytes(path!);
        using JsonDocument document = JsonDocument.Parse(unchanged);
        JsonElement source = document.RootElement;
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsState>(source, "NativeGraphics"));
        Assert.True(graphics.EntryReady);
        Assert.Equal(0, source.GetProperty("Frame").GetInt64());
        Assert.Null(graphics.OriginalEffect);
        string casterId = Assert.IsType<string>(ReadOriginalModelActorId(graphics, 3));
        BattleCombatantDefinition[] actors = Assert.IsType<BattleCombatantDefinition[]>(NativeBattleFixtureState.ReadArchived<BattleCombatantDefinition[]>(source.GetProperty("Definition"), "Combatants"));
        BattleCombatantDefinition caster = Assert.Single(actors, actor => actor.Id == casterId);
        Assert.Equal(56, caster.ModelId);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 223 && row.MagicId == 54 && row.KernelSection == 3 && row.KernelIndex == 223);
        Assert.Equal((byte)11, NativeBattleComponentInputs.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeBattleActionPacketsState original = Assert.IsType<NativeBattleActionPacketsState>(NativeBattleFixtureState.ReadArchived<NativeBattleActionPacketsState>(source, "NativeActionPackets"));
        NativeBattleRetainedActionHeaderSlot retained = original.ExportRetained().Headers[original.ActionCount];
        Assert.Equal((byte)0, retained.ReadOriginalByte(17));
        string targetId = Assert.IsType<string>(ReadOriginalModelActorId(graphics, 0));
        JsonElement target = Assert.Single(source.GetProperty("Combatants").EnumerateArray(), actor => actor.GetProperty("Id").GetString() == targetId);
        NativeBattleStatusSnapshot status = Assert.IsType<NativeBattleStatusSnapshot>(NativeBattleFixtureState.ReadArchived<NativeBattleStatusSnapshot>(target, "NativeStatus"));
        (NativeBattleActionPacketsState packets, BattleActionHeaderReference headerIndex) = original.Prepare(new(original.ModelAliases.DecodeLiteral(3), 8, 11, unchecked((sbyte)action.RawRecord[4]), 223, 54, null));
        Assert.Equal(retained.ReadOriginalByte(17), packets.ReadHeader(headerIndex).ReadRepeatModeMinusOne());
        (packets, BattleContactOccurrence contactIndex) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(0), action.DamagePresentation, 0, 0, status.Saved, 0, status.Battle), null));
        (packets, BattleActionHeaderReference publishedIndex) = packets.Publish(1);
        Assert.Equal(original.ActionCount, headerIndex.Ordinal);
        Assert.Equal(headerIndex.Ordinal, publishedIndex.Ordinal);
        Assert.Equal(original.ContactCount, contactIndex.Ordinal);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(headerIndex);
        Assert.Equal((byte)1, header.ReadContactCount());
        Assert.Equal((byte)0, header.ReadRepeatModeMinusOne());
        Assert.Equal(retained.ReadOriginalByte(17), header.ReadRepeatModeMinusOne());
        NativeBattleRetainedActionHeaderSlot rawHeader = packets.ExportRetained().Headers[headerIndex.Ordinal];
        Assert.Equal(retained.UnknownByteMask & (1u << 17), rawHeader.UnknownByteMask & (1u << 17));
        Assert.Equal(contactIndex.AsCursor(), header.FirstContact);
        Assert.Equal(0x1D28344u + 24u * (uint)contactIndex.Ordinal, BinaryPrimitives.ReadUInt32LittleEndian(rawHeader.Payload.AsSpan().Slice(8, 4)));
        NativeBattleContactPacket contact = packets.ReadContact(contactIndex);
        Assert.NotNull(contact);
        Assert.Equal(unchanged, File.ReadAllBytes(path!));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "header-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Local packet preparation over archived facts", Frame = source.GetProperty("Frame").GetInt64(), PhysicalCaster = 3, caster.ModelId, action.Command, action.ActionId, action.MagicId, Sequence = 11, HeaderIndex = headerIndex.Ordinal, ContactIndex = contactIndex.Ordinal, rawHeader.FirstContactIndex, TargetCount = header.ReadContactCount(), RetainedGroup = header.ReadRepeatModeMinusOne(), RootMaximumGroup = 0, Header = rawHeader.Payload, Contact = contact, SnapshotUnchanged = true }));
    }
}
