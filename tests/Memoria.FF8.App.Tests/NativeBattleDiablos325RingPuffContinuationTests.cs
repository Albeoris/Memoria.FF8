using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325RingPuffContinuationTests
{
    [Fact]
    public void Named_ring_phase_selects_real_poses_and_sprites_and_preserves_native_packet_and_random_order()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-third-stream-playback Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)175, Root(source).ReadWord(0xC));
        NativeBattleModulePoolTask sourceTask = Timeline(source);
        Assert.Equal((ushort)173, sourceTask.ReadWord(0xC));
        string output = Path.Combine(evidence!, "diablos-ring-puffs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos point continuation evidence: {output}");
        using GameAssets game = new(directory!);
        JsonNode installed = JsonSerializer.SerializeToNode(game.ReadNativeDiablos325Resources())!;
        JsonNode node = JsonNode.Parse(original)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.Equal(9, changes.Count);
        string migrated = node.ToJsonString();
        File.WriteAllText(Path.Combine(output, "ring-resources.battle.json"), migrated);
        File.WriteAllText(Path.Combine(output, "resource-migration.json"), JsonSerializer.Serialize(new { OriginalSha256 = Hash(original), MigratedSha256 = Hash(migrated), Changes = changes }));
        source = JsonSerializer.Deserialize<BattleSnapshot>(migrated)!;
        using BattleAssets assets = new(directory!);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(migrated)!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeCrtRandomState expectedRandom = source.NativeCrtRandom!;
        List<object> rows = [];
        BattleSnapshot before = source;
        try
        {
            for (ushort counter = 173; counter <= 201; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleRestorationPresentationTests.Publish(output, counter == 201 ? "before-ring-camera-reset" : "timeline-" + counter, current, pixels);
                NativeBattleModulePoolTask task = Timeline(current);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal(unchecked((uint)-5792), task.ReadDword(0x164));
                Assert.Equal(counter == 201 ? (byte)2 : (byte)(counter - 171), task.ReadByte(0x17E));
                Assert.Equal(counter == 201 ? (byte)15 : (byte)30, task.ReadByte(0x17F));
                Assert.Equal((byte)((task.ReadByte(0x17E) & 1) == 0 ? 0 : 4), task.ReadByte(0x179));
                Assert.Equal(counter == 201 ? (byte)2 : (byte)1, task.ReadByte(0x178));
                Assert.Equal(sourceTask.ReadWord(0x15E), task.ReadWord(0x15E));
                Assert.Equal(new uint[] { 0x20DFAB8u + 0x21874, 0x20DFAB8u + 0x22574, 0x20DFAB8u + 0x2CE68, 0x20DFAB8u + 0x31B5C }, Enumerable.Range(0, 4).Select(index => task.ReadDword(0x244 + index * 4)));
                if (counter is 177 or 178)
                {
                    for (int draw = 0; draw < 96; draw++)
                    {
                        expectedRandom = expectedRandom.Draw(out _);
                    }
                }
                Assert.Equal(expectedRandom, current.NativeCrtRandom);
                NativeBattleCameraState expectedCamera = counter is >= 175 and <= 189 ? NativeBattleDiablos325MidCamera.Advance656504(new(before.NativeEffectMemory!, before.NativeGraphics!.OriginalEffect!), before.NativeCamera!, counter).Camera : before.NativeCamera!;
                Assert.Equal(expectedCamera.CurrentPose, current.NativeCamera!.CurrentPose);
                if (counter == 173)
                {
                    NativeBattleModuleSound sound = Assert.Single(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null).NativeModuleSound!;
                    Assert.Equal(690163u, sound.Sound);
                    Assert.Equal<uint?>(128u, sound.RawPan);
                    Assert.Equal(127u, sound.Volume);
                }
                else
                {
                    Assert.DoesNotContain(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null);
                }
                NativeBattleDiablos325Frame wingFrame = new(current.NativeEffectMemory!, current.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325ShapeDraw wing = NativeBattleDiablos325Shapes.Advance654899(wingFrame, task.WriteWord(0xC, counter), before.NativeCamera!.Projection!);
                uint privateStart = Root(current).ReadByte(0xE) != 0 ? 0xF85Cu : 0x885Cu;

                NativeBattleDiablos325Frame programmeFrame = new(before.NativeEffectMemory!, before.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325ProgrammeDraw programme = NativeBattleDiablos325Programme.Advance654654(programmeFrame, Timeline(before), before.NativeCamera!.Projection!, source.Definition.NativeGraphics!.Rules.SummonColors!);
                NativeBattleModulePoolTask drawnTask = task;
                NativeBattlePrimitiveProcess bodyProcess = before.NativeEffectMemory!.PrimitiveProcess! with { ByteCursor = programme.Packets.AllocationBytes, ModelRendererRegisters = wing.Memory.PrimitiveProcess!.ModelRendererRegisters };
                NativeBattleEffectMemory bodyMemory = current.NativeEffectMemory! with { PrimitiveProcess = bodyProcess };
                bool hardware = Assert.IsType<bool>(source.Definition.NativeGraphics!.PrimitiveCapabilities!.HardwareEnabled);
                NativeBattleDiablos325BodyDraw body = NativeBattleDiablos325RawBodyDrawing.Draw6A79B0(bodyMemory, drawnTask, before.NativeCamera!.Projection!, hardware);
                foreach (NativeBattleGfGeometryAttempt attempt in body.Packets.Attempts.Where(item => item.Primitive is not null))
                {
                    int address = checked(programme.Packets.AllocationBytes + attempt.ByteOffsetRelative);
                    Assert.Contains(current.NativeGraphics!.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, before.NativeEffectMemory!.PrimitiveProcess!.PacketBank!, address));
                }
                foreach (NativeBattleGfGeometryAttempt attempt in wing.Packets.Attempts.Where(item => item.Primitive is not null))
                {
                    int address = checked((int)(privateStart + (uint)attempt.ByteOffsetRelative));
                    Assert.Contains(current.NativeGraphics!.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, "20DFAB8", address));
                }
                int puffBytes = 0;
                NativeBattleDiablos325Frame puffFrame = new(before.NativeEffectMemory!, before.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325Resources resources = puffFrame.Effect.Binding.Diablos325Resources!;
                if (puffFrame.Memory.Diablos325!.Mode51F0 == 3)
                {
                    for (int index = 0; index < NativeBattleDiablos325Puffs.Capacity; index++)
                    {
                        int offset = NativeBattleDiablos325Puffs.BaseOffset + index * 16;
                        byte life = puffFrame.Memory.Arena!.Read(offset + 15, 1)[0];
                        if (life == 0)
                        {
                            continue;
                        }
                        uint pointer = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(puffFrame.Memory.Arena.Read(offset, 4));
                        NativeBattleActionMatrix basis = NativeBattleDiablos325PuffPackets.Basis658970(puffFrame, index, before.NativeCamera!.Projection!.View!, out life);
                        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325PuffPackets.Emit6FC9E0(resources.ReadPuffSprite(pointer), life, basis, before.NativeCamera!.Projection!, 19);
                        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts.Where(item => item.Primitive is not null))
                        {
                            int address = checked((int)privateStart + wing.Packets.AllocationBytes + puffBytes + attempt.ByteOffsetRelative);
                            NativeBattleGfGeometryAttempt receipt = MaterializeReceipt(batch, attempt);
                            Assert.Contains(current.NativeGraphics!.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, receipt, "20DFAB8", address));
                        }
                        puffBytes += batch.AllocationBytes;
                    }
                }
                Assert.Equal(current.NativeEffectMemory!.Diablos325!.ModelBuffer5208 + privateStart + (uint)(wing.Packets.AllocationBytes + puffBytes), current.NativeEffectMemory!.Diablos325!.PacketCursor51D0);
                puffFrame = puffFrame with { Random = before.NativeCrtRandom };
                if (counter is 177 or 178)
                {
                    puffFrame = NativeBattleDiablos325Puffs.SpawnRing6558A4(puffFrame);
                }
                puffFrame = NativeBattleDiablos325Puffs.Drift658CE0(puffFrame);
                var expectedPuffs = puffFrame.Memory.Arena!.ReadSnapshot(NativeBattleDiablos325Puffs.BaseOffset, 160 * 16);
                var actualPuffs = current.NativeEffectMemory!.Arena!.ReadSnapshot(NativeBattleDiablos325Puffs.BaseOffset, 160 * 16);
                Assert.Equal(expectedPuffs.Data, actualPuffs.Data);
                Assert.Equal(expectedPuffs.Known, actualPuffs.Known);
                Assert.Equal(puffFrame.Memory.Diablos325!.Mode51F0, current.NativeEffectMemory!.Diablos325!.Mode51F0);
                Assert.Equal(puffFrame.Memory.Diablos325!.ParticleCursor51F4, current.NativeEffectMemory!.Diablos325!.ParticleCursor51F4);
                Assert.Equal(puffFrame.Random, current.NativeCrtRandom);
                NativeBattleDiablos325Frame reference = new(current.NativeEffectMemory! with { PrimitiveProcess = wing.Memory.PrimitiveProcess }, current.NativeGraphics!.OriginalEffect!);
                NativeBattleEffectTaskPool previous = before.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                reference = reference with { Random = before.NativeCrtRandom, Effect = reference.Effect with { Pools = reference.Effect.Pools.Select(pool => pool.DescriptorAddress == previous.DescriptorAddress ? previous : pool).ToImmutableArray() } };
                int childBytes = 0;
                reference = NativeBattleDiablos325Particles.Advance659960(reference, before.NativeCamera!.Projection!, source.Definition.NativeGraphics!.Rules.SummonColors!, (frame, batch) =>
                {
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts.Where(item => item.Primitive is not null))
                    {
                        int address = checked(programme.Packets.AllocationBytes + body.Packets.AllocationBytes + childBytes + attempt.ByteOffsetRelative);
                        Assert.Contains(current.NativeGraphics!.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, before.NativeEffectMemory!.PrimitiveProcess!.PacketBank!, address));
                    }
                    childBytes += batch.AllocationBytes;
                    return frame;
                });
                NativeBattleEffectTaskPool children = Assert.IsType<NativeBattleOriginalEffectState>(current.NativeGraphics?.OriginalEffect).Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                NativeBattleEffectTaskPool expectedChildren = reference.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                Assert.Equal(JsonSerializer.Serialize(expectedChildren), JsonSerializer.Serialize(children));
                foreach (int index in Enumerable.Range(0, children.Slots.Length))
                {
                    Assert.Equal(children.Slots[index].Payload, current.NativeEffectMemory!.Arena!.Read(0x3505C + index * 0xE8 + 12, 0xDC).ToImmutableArray());
                }
                Assert.Equal(counter < 177 ? (byte)0 : (byte)3, current.NativeEffectMemory!.Diablos325!.Mode51F0);
                Assert.Equal(690163, current.NativeEffectMemory!.SummonStream!.Number);
                Assert.Equal((byte)1, task.ReadByte(0xF));
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics!.Callback, Timeline = counter + 1, BodyAllocationBytes = body.Packets.AllocationBytes, ProgrammeAllocationBytes = programme.Packets.AllocationBytes, PrivateShapeBytes = wing.Packets.AllocationBytes, PrivatePuffBytes = puffBytes, ChildCommonBytes = childBytes, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)) });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "ring-puffs-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
            throw;
        }
        Assert.Equal(original, File.ReadAllText(input!));

        void AddResources(JsonNode current, string path)
        {
            if (current is JsonObject obj)
            {
                foreach (var entry in obj.ToArray())
                {
                    if (entry.Key == "Diablos325Resources" && entry.Value is JsonObject retained)
                    {
                        owners++;
                        foreach (var field in retained)
                        {
                            if (field.Key != "Files")
                            {
                                Assert.Equal(installed[field.Key]!.ToJsonString(), field.Value!.ToJsonString());
                            }
                        }
                        foreach (string name in new[] { "RingPuffSprite0", "RingPuffSprite1" })
                        {
                            Assert.False(retained.ContainsKey(name));
                            JsonNode added = installed[name]!.DeepClone();
                            retained[name] = added;
                            changes.Add(new { Path = path + "/" + entry.Key + "/" + name, Operation = "add", ValueSha256 = Hash(added.ToJsonString()) });
                        }
                        JsonArray files = retained["Files"]!.AsArray();
                        Assert.Equal(6, files.Count);
                        for (int index = 0; index < files.Count; index++)
                        {
                            Assert.Equal(installed["Files"]![index]!.ToJsonString(), files[index]!.ToJsonString());
                        }
                        JsonNode file = installed["Files"]![6]!.DeepClone();
                        Assert.Equal((short)733, file.Deserialize<NativeBattleEffectFileAsset>()!.FileId);
                        files.Add(file);
                        changes.Add(new { Path = path + "/" + entry.Key + "/Files/6", Operation = "add", ValueSha256 = Hash(file.ToJsonString()) });
                    }
                    else if (entry.Value is not null)
                    {
                        AddResources(entry.Value, path + "/" + entry.Key);
                    }
                }
            }
            else if (current is JsonArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonNode value)
                    {
                        AddResources(value, path + "/" + index);
                    }
                }
            }
        }

    }

    private static NativeBattleGfGeometryAttempt MaterializeReceipt(NativeBattleGfGeometryBatch batch, NativeBattleGfGeometryAttempt linked)
    {
        byte[] bytes = new byte[linked.Data.Length];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        foreach (NativeBattleGfGeometryAttempt store in batch.Attempts)
        {
            for (int index = 0; index < store.Data.Length; index++)
            {
                int target = store.ByteOffsetRelative + index - linked.ByteOffsetRelative;
                if ((uint)target < bytes.Length && store.WriteMask[index] != NativeBattleGfPacketByteState.Untouched)
                {
                    bytes[target] = store.Data[index];
                    mask[target] = store.WriteMask[index];
                }
            }
            if (ReferenceEquals(store, linked))
            {
                break;
            }
        }
        return linked with { Data = bytes.ToImmutableArray(), WriteMask = mask.ToImmutableArray() };
    }

    private static bool MatchesPacket(NativeBattleLogicalCommand command, NativeBattleGfGeometryAttempt attempt, string bank, int address)
    {
        if (command.Packet.Bank != bank || command.Packet.ByteOffset != address || command.WordOffset != 1 || command.Flags20 != 0 || !command.VertexPayload4.SequenceEqual(attempt.Primitive!.VertexPayload4) || command.Words.Length != attempt.Primitive!.Words.Length - 1 || attempt.WriteMask.Skip(attempt.Primitive!.Words.Length * 4).Any(state => state != NativeBattleGfPacketByteState.Untouched))
        {
            return false;
        }
        for (int index = 4; index < attempt.Primitive!.Words.Length * 4; index++)
        {
            if (attempt.WriteMask[index] == NativeBattleGfPacketByteState.Known)
            {
                byte actual = unchecked((byte)(command.Words[(index - 4) / 4] >> (((index - 4) & 3) * 8)));
                if (actual != attempt.Data[index])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static NativeBattleModulePoolTask Root(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051B0).Slots[0];
    private static NativeBattleModulePoolTask Timeline(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
