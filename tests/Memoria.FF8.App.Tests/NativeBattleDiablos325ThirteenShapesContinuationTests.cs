using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325ThirteenShapesContinuationTests
{
    [Fact]
    public void Named_thirteen_shape_phase_selects_pose_before_body_and_initializes_only_native_trail_fields()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-thirteen-shapes Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)240, Root(source).ReadWord(0xC));
        NativeBattleModulePoolTask sourceTask = Timeline(source);
        Assert.Equal((ushort)238, sourceTask.ReadWord(0xC));
        string output = Path.Combine(evidence!, "diablos-thirteen-shapes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos thirteen shape continuation evidence: {output}");
        Assert.Null(source.NativeEffectMemory!.Diablos325!.Trail);
        string migrated = original;
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
            for (ushort counter = 238; counter <= 245; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleRestorationPresentationTests.Publish(output, counter == 245 ? "before-attached-trails" : "timeline-" + counter, current, pixels);
                NativeBattleModulePoolTask task = Timeline(current);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal(unchecked((uint)-6272), task.ReadDword(0x164));
                Assert.Equal((byte)(counter - 236), task.ReadByte(0x17E));
                Assert.Equal((byte)9, task.ReadByte(0x17F));
                Assert.Equal((byte)3, task.ReadByte(0x178));
                Assert.Equal((byte)((task.ReadByte(0x17E) & 1) == 0 ? 0 : 4), task.ReadByte(0x179));
                Assert.Equal(Enumerable.Range(0, 13).Select(index => 0x20DFAB8u + 0x3505Cu + (uint)index * 0x1FF0), Enumerable.Range(0, 13).Select(index => task.ReadDword(0x14 + index * 4)));
                Assert.Equal(expectedRandom, current.NativeCrtRandom);
                Assert.Equal(before.NativeCamera!.CurrentPose, current.NativeCamera!.CurrentPose);
                if (counter == 238)
                {
                    NativeBattleModuleSound sound = Assert.Single(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null).NativeModuleSound!;
                    Assert.Equal(240254u, sound.Sound);
                    Assert.Equal(1u, sound.Flags);
                    Assert.Equal((byte?)128, sound.Pan);
                }
                else
                {
                    Assert.DoesNotContain(current.Events.Skip(before.Events.Length), item => item.NativeModuleSound is not null);
                }
                NativeBattleDiablos325Frame wingFrame = new(current.NativeEffectMemory!, current.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325ShapeDraw wing = NativeBattleDiablos325Shapes.Advance654AAC(wingFrame, task.WriteWord(0xC, counter), before.NativeCamera!.Projection!);
                uint privateStart = Root(current).ReadByte(0xE) != 0 ? 0xF85Cu : 0x885Cu;

                NativeBattleDiablos325Frame programmeFrame = new(before.NativeEffectMemory!, before.NativeGraphics!.OriginalEffect!);
                NativeBattleDiablos325ProgrammeDraw programme = NativeBattleDiablos325Programme.Advance654654(programmeFrame, Timeline(before), before.NativeCamera!.Projection!, source.Definition.NativeGraphics!.Rules.SummonColors!);
                foreach (NativeBattleGfGeometryAttempt attempt in programme.Packets.Attempts.Where(item => item.Primitive is not null))
                {
                    Assert.Contains(current.NativeGraphics!.PrimitiveFrame!.LastCommands, resolved => MatchesPacket(resolved.Command, attempt, before.NativeEffectMemory!.PrimitiveProcess!.PacketBank!, attempt.ByteOffsetRelative));
                }
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
                Assert.Equal(current.NativeEffectMemory!.Diablos325!.ModelBuffer5208 + privateStart + (uint)wing.Packets.AllocationBytes, current.NativeEffectMemory!.Diablos325!.PacketCursor51D0);
                NativeBattleDiablos325Frame trailFrame = new(before.NativeEffectMemory!, before.NativeGraphics!.OriginalEffect!);
                if (counter == 238)
                {
                    trailFrame = NativeBattleDiablos325Trail.Initialize657E70(trailFrame, 5, 0, unchecked((ushort)-32));
                }
                var expectedTrailBytes = trailFrame.Memory.Arena!.ReadSnapshot(0x4C5C, 0x3C00);
                var actualTrailBytes = current.NativeEffectMemory!.Arena!.ReadSnapshot(0x4C5C, 0x3C00);
                Assert.Equal(expectedTrailBytes.Data, actualTrailBytes.Data);
                Assert.Equal(expectedTrailBytes.Known, actualTrailBytes.Known);
                Assert.Equal(trailFrame.Memory.Diablos325!.Trail, current.NativeEffectMemory!.Diablos325!.Trail);
                Assert.NotNull(current.NativeEffectMemory!.Diablos325!.Trail);
                Assert.Equal(before.NativeEffectMemory!.Diablos325!.Control51E4, current.NativeEffectMemory!.Diablos325!.Control51E4);
                NativeBattleEffectTaskPool children = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                NativeBattleEffectTaskPool previousChildren = before.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
                Assert.Equal(JsonSerializer.Serialize(previousChildren), JsonSerializer.Serialize(children));
                Assert.Null(children.Head);
                Assert.Null(children.Tail);
                Assert.Equal((byte)0, current.NativeEffectMemory!.Diablos325!.Mode51F0);
                Assert.Equal(690164, current.NativeEffectMemory!.SummonStream!.Number);
                NativeBattleEffectFileAsset file = current.NativeGraphics!.OriginalEffect!.Binding.Diablos325Resources!.Files.Single(item => item.FileId == 734);
                Assert.Equal(file.Data, current.NativeEffectMemory!.Arena!.Read(0x3505C, file.Data.Length).ToImmutableArray());
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics!.Callback, Timeline = counter + 1, BodyAllocationBytes = body.Packets.AllocationBytes, ProgrammeAllocationBytes = programme.Packets.AllocationBytes, PrivateShapeBytes = wing.Packets.AllocationBytes, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)) });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            NotSupportedException independent = Assert.Throws<NotSupportedException>(() => second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            Assert.Contains("6559D3", error.Message);
            Assert.Equal(error.Message, independent.Message);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "thirteen-shapes-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = error.Message }));
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
