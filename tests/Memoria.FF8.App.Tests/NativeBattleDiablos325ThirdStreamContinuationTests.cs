using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325ThirdStreamContinuationTests
{
    [Fact]
    public void Named_third_stream_waits_for_physical_IO_then_publishes_loaded_number_and_ack_before_playback()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-third-stream Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)171, Root(source).ReadWord(0xC));
        NativeBattleModulePoolTask sourceTask = Timeline(source);
        Assert.Equal((ushort)169, sourceTask.ReadWord(0xC));
        string output = Path.Combine(evidence!, "diablos-third-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos point continuation evidence: {output}");
        using BattleAssets assets = new(directory!);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(original)!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeCrtRandomState expectedRandom = source.NativeCrtRandom!;
        List<object> rows = [];
        BattleSnapshot before = source;
        try
        {
            for (ushort counter = 169; counter <= 172; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleRestorationPresentationTests.Publish(output, counter == 172 ? "before-third-stream-playback" : "timeline-" + counter, current, pixels);
                NativeBattleModulePoolTask task = Timeline(current);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal(unchecked((uint)-5792), task.ReadDword(0x164));
                Assert.Equal((byte)(((counter - 91) % 15) + 1), task.ReadByte(0x17E));
                Assert.Equal((byte)15, task.ReadByte(0x17F));
                Assert.Equal((byte)((((counter - 91) % 15) & 1) == 0 ? 4 : 0), task.ReadByte(0x179));
                Assert.Equal(sourceTask.ReadWord(0x15E), task.ReadWord(0x15E));
                Assert.Equal(new uint[] { 0x20DFAB8u + 0x21874, 0x20DFAB8u + 0x22574, 0x20DFAB8u + 0x2CE68, 0x20DFAB8u + 0x31B5C }, Enumerable.Range(0, 4).Select(index => task.ReadDword(0x244 + index * 4)));
                if (counter < 167 && ((counter - 92) & 7) == 7)
                {
                    expectedRandom = expectedRandom.Draw(out _).Draw(out _);
                }
                Assert.Equal(expectedRandom, current.NativeCrtRandom);
                Assert.Equal(before.NativeCamera!.CurrentPose, current.NativeCamera!.CurrentPose);
                Assert.DoesNotContain(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null);
                NativeBattleDiablos325Frame wingFrame = new(current.NativeEffectMemory!, current.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325ShapeDraw wing = NativeBattleDiablos325Shapes.Advance654899(wingFrame, task.WriteWord(0xC, counter), before.NativeCamera!.Projection!);
                uint privateStart = Root(current).ReadByte(0xE) != 0 ? 0xF85Cu : 0x885Cu;
                Assert.Equal(current.NativeEffectMemory!.Diablos325!.ModelBuffer5208 + privateStart + (uint)wing.Packets.AllocationBytes, current.NativeEffectMemory.Diablos325.PacketCursor51D0);
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
                    Assert.Contains(current.NativeGraphics.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, before.NativeEffectMemory!.PrimitiveProcess!.PacketBank!, address));
                }
                foreach (NativeBattleGfGeometryAttempt attempt in wing.Packets.Attempts.Where(item => item.Primitive is not null))
                {
                    int address = checked((int)(privateStart + (uint)attempt.ByteOffsetRelative));
                    Assert.Contains(current.NativeGraphics.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, "20DFAB8", address));
                }
                NativeBattleDiablos325Frame reference = new(current.NativeEffectMemory! with { PrimitiveProcess = wing.Memory.PrimitiveProcess }, current.NativeGraphics!.OriginalEffect!);
                NativeBattleEffectTaskPool previous = before.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                reference = reference with { Random = before.NativeCrtRandom, Effect = reference.Effect with { Pools = reference.Effect.Pools.Select(pool => pool.DescriptorAddress == previous.DescriptorAddress ? previous : pool).ToImmutableArray() } };
                NativeBattleDiablos325PointChoice? chosen = null;
                if (counter < 167 && ((counter - 92) & 7) == 7)
                {
                    chosen = NativeBattleDiablos325Point.Choose6557E5(reference, drawnTask);
                    reference = NativeBattleDiablos325Particles.Spawn659900(reference with { Random = chosen.Random }, chosen.Position, -128, chosen.Programme);
                    Assert.Equal(current.NativeCrtRandom, chosen.Random);
                }
                int childBytes = 0;
                reference = NativeBattleDiablos325Particles.Advance659960(reference, before.NativeCamera!.Projection!, source.Definition.NativeGraphics!.Rules.SummonColors!, (frame, batch) =>
                {
                    foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts.Where(item => item.Primitive is not null))
                    {
                        int address = checked(programme.Packets.AllocationBytes + body.Packets.AllocationBytes + childBytes + attempt.ByteOffsetRelative);
                        Assert.Contains(current.NativeGraphics.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, before.NativeEffectMemory!.PrimitiveProcess!.PacketBank!, address));
                    }
                    childBytes += batch.AllocationBytes;
                    return frame;
                });
                NativeBattleEffectTaskPool children = Assert.IsType<NativeBattleOriginalEffectState>(current.NativeGraphics?.OriginalEffect).Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                NativeBattleEffectTaskPool expectedChildren = reference.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                Assert.Equal(JsonSerializer.Serialize(expectedChildren), JsonSerializer.Serialize(children));
                foreach (int index in Enumerable.Range(0, children.Slots.Length))
                {
                    Assert.Equal(children.Slots[index].Payload, current.NativeEffectMemory.Arena!.Read(0x3505C + index * 0xE8 + 12, 0xDC).ToImmutableArray());
                }
                Assert.Equal((byte)0, current.NativeEffectMemory!.Diablos325!.Mode51F0);
                Assert.Equal(690163, current.NativeEffectMemory.SummonStream!.Number);
                Assert.Equal((byte)1, task.ReadByte(0xF));
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics!.Callback, Timeline = counter + 1, BodyAllocationBytes = body.Packets.AllocationBytes, ProgrammeAllocationBytes = programme.Packets.AllocationBytes, PrivateShapeBytes = wing.Packets.AllocationBytes, ChildCommonBytes = childBytes, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)) });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            NotSupportedException independent = Assert.Throws<NotSupportedException>(() => second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            Assert.Contains("65551D", error.Message);
            Assert.Equal(error.Message, independent.Message);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "third-stream-reset-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = error.Message }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
            throw;
        }
        Assert.Equal(original, File.ReadAllText(input!));

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
