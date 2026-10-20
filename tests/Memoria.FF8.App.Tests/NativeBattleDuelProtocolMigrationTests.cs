using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleDuel112CastTests
{
    [Fact]
    public void Prepare_named_Duel_selection_protocol_standalone_migration()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_EFFECT_OUTPUT");
        Assert.SkipUnless(input is not null && File.Exists(input) && output is not null, "Select the standalone before the next Duel input and a separate migration output directory.");
        byte[] original = File.ReadAllBytes(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        BattleNativeLimitState owner = Assert.Single(source.NativeLimits);
        BattleNativeDuelState duel = owner.Duel!;
        Assert.NotNull(duel);
        Assert.Null(duel.Selection1D28DEF);
        Assert.Equal(BattleNativeDuelPhase.Ready, duel.Phase);
        Assert.True(duel.RemainingTimer > 0);
        Assert.Equal((byte)241, source.ActiveAction!.NativeQueuedCommand);
        Assert.Equal(4, source.ActiveAction.ActionId);
        Assert.Equal((ushort)274, source.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal((byte)6, source.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x25084A8).Slots[0].ReadByte(15));
        NativeBattleDescriptorState descriptors = source.NativeGraphics.Descriptors;
        int packetIndex = Assert.Single(descriptors.Queue.Where(index => descriptors.Packets[index].Command == 10 && descriptors.Packets[index].Argument == 0x47DE70));
        NativeBattleDescriptorPacket packet = descriptors.Packets[packetIndex];
        Assert.Equal((ushort)0, packet.UnknownByteMask);
        byte[] bytes = packet.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x4852B0);
        descriptors = descriptors with { Packets = descriptors.Packets.SetItem(packetIndex, packet with { Bytes = bytes.ToImmutableArray() }) };
        BattleSnapshot migrated = source with { NativeLimits = [owner with { Duel = duel with { Selection1D28DEF = 254 } }], NativeGraphics = source.NativeGraphics with { Descriptors = descriptors } };
        Directory.CreateDirectory(output!);
        string destination = Path.Combine(output!, "migrated-duel-selection-protocol.battle.json");
        File.WriteAllText(destination, JsonSerializer.Serialize(migrated));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(destination))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(restored.Capture()));
        BattleSession independent = BattleSession.Restore(migrated);
        restored.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(independent.Capture()));
        File.WriteAllText(Path.Combine(output!, "validation-next.battle.json"), JsonSerializer.Serialize(restored.Capture()));
        File.WriteAllText(Path.Combine(output!, "migration-receipt.json"), JsonSerializer.Serialize(new { Status = "restore-and-one-native-tick-validated", Input = input, Output = destination, OriginalSha256 = Convert.ToHexString(SHA256.HashData(original)), MigratedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))), source.Frame, NativeCallback = source.NativeGraphics.Callback, Changes = new[] { "NativeLimits[0].Duel.Selection1D28DEF: absent to native acknowledged selection FE", $"NativeGraphics.Descriptors.Packets[{packetIndex}].Bytes[4..8]: pending final47DE70 replaced by active-Duel poll4852B0" }, Continuation = "Installed_Duel112_completes_original_action_and_independent_standalones(true) with the migrated output as the selected standalone input" }));
    }
}
