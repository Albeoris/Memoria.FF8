using System.Buffers.Binary;
using System.Collections.Immutable;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Holy175CapturePresentationTests
{
    [Fact]
    public void External_declared_Holy_capture_component_executes_actual_driver_and_independent_cold_next()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_CAPTURE_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Supply a complete declared stage snapshot for the independent Holy capture component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOLY175_CAPTURE_OUTPUT") ?? throw new InvalidDataException("A distinct component output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(gameDirectory);
        NativeBattleHoly175Resources resources = game.ReadNativeHoly175Resources();
        BattleSnapshot snapshot = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        NativeBattlePrimitiveProcess retained = snapshot.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleSoftwarePrimitiveState software = retained.Software! with { ViewportInputs = new(640, 480, 0, 0) };
        NativeBattlePrimitiveProcess process = (retained with { Software = software }).Reset500900(4);
        NativeBattleCameraProjection projection = snapshot.NativeCamera!.Projection!;
        NativeBattleGraphicsDefinition graphics = snapshot.Definition.NativeGraphics!;
        Component initial = new(process, snapshot.NativeStage!, snapshot.NativeEffectMemory.Textures!, new(resources.CaptureBlackPackets!.Data, 0, 0), resources, snapshot.Definition.NativeStageSource!.Geometry ?? throw new InvalidDataException("Native Holy capture requires its retained stage geometry."), projection.View!, projection, graphics.Rules.SummonColors!, graphics.PrimitiveTables!, graphics.SoftwarePrimitiveTable!, graphics.PrimitiveCapabilities!);
        Prepared? prepared = null;
        Executed? executed = null;
        int changed = 0;
        int nonzero = 0;
        try
        {
            Write("component-initial.json", initial);
            prepared = Prepare(initial);
            Write("capture-produced.json", prepared);
            Prepared independent = JsonSerializer.Deserialize<Prepared>(File.ReadAllBytes(Path.Combine(output, "capture-produced.json")))!;
            Assert.Equal(JsonSerializer.Serialize(prepared), JsonSerializer.Serialize(independent));
            executed = Execute(prepared);
            Executed cold = Execute(independent);
            Assert.Equal(JsonSerializer.Serialize(executed), JsonSerializer.Serialize(cold));
            byte[] pixels = Render(executed);
            Assert.Equal(pixels, Render(cold));
            File.WriteAllBytes(Path.Combine(output, "capture.rgba"), pixels);
            WritePng(Path.Combine(output, "capture.png"), pixels);
            Write("component-after.json", executed.State);
            Component afterCold = JsonSerializer.Deserialize<Component>(File.ReadAllBytes(Path.Combine(output, "component-after.json")))!;
            Prepared next = Prepare(executed.State);
            Prepared coldNext = Prepare(afterCold);
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
            Executed nextExecuted = Execute(next);
            Executed coldNextExecuted = Execute(coldNext);
            Assert.Equal(JsonSerializer.Serialize(nextExecuted), JsonSerializer.Serialize(coldNextExecuted));
            Assert.Equal(Render(nextExecuted), Render(coldNextExecuted));
            Write("component-next.json", nextExecuted.State);
            byte[] beforeVram = initial.Textures.ReadVram();
            byte[] afterVram = executed.State.Textures.ReadVram();
            for (int y = 256; y < 488; y++)
            {
                for (int x = 576; x < 896; x++)
                {
                    int offset = (y * 1024 + x) * 2;
                    ushort before = BinaryPrimitives.ReadUInt16LittleEndian(beforeVram.AsSpan(offset));
                    ushort after = BinaryPrimitives.ReadUInt16LittleEndian(afterVram.AsSpan(offset));
                    changed += before != after ? 1 : 0;
                    nonzero += after != 0 ? 1 : 0;
                }
            }
            Assert.True(prepared.StagePackets > 0);
            Assert.True(executed.Frame.LastCommands.Any(command => command.Command.Packet.Bank == "holy-capture"));
            Assert.True(executed.Frame.LastCommands.Any(command => command.Pass == 1 && command.DrawMode.NormalViewport is not null));
            Assert.True(changed > 0 && nonzero > 0);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!)));
            Report("checked", null, pixels);
        }
        catch (Exception error)
        {
            if (prepared is not null)
            {
                Write("before-failure-component.json", prepared);
            }
            if (executed is not null)
            {
                Write("failure-current-component.json", executed);
            }
            Report("blocked", error.ToString(), null);
            throw;
        }

        void Write<T>(string name, T value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value));
        void Report(string status, string? blocker, byte[]? rgba)
        {
            object report = new { Status = status, FirstBlocker = blocker, Scenario = "Independent Holy capture component cloned from declared stage snapshot with explicitly selected640x480 native viewport; no Holy175 dispatch or gameplay continuation", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), StagePackets = prepared?.StagePackets, TargetRectangle = new { X = 576, Y = 256, Width = 320, Height = 232 }, ChangedTargetPixels = changed, NonzeroTargetPixels = nonzero, ExecutedCommands = executed?.Frame.LastCommands.Length, NormalCommandsWithViewport = executed?.Frame.LastCommands.Count(command => command.Pass > 0 && command.DrawMode.NormalViewport is not null), SoftwareMode = executed?.State.Process.Software?.Environment.Derived?.Mode, Boundaries = new[] { "Produced complete component cold execution", "Post-capture complete component next-capture JSON/RGBA" }, RgbaSha256 = rgba is null ? null : Convert.ToHexString(SHA256.HashData(rgba)), BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), Pending = new[] { "Whole Holy175 entry/lifecycle", "Original native capture pixel differential", "Other source stages and viewport inputs" } };
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static Prepared Prepare(Component component)
    {
        NativeBattlePrimitiveProcess process = component.Process.Reset500900(4);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear(process.Buffer80!.Value);
        NativeBattleHoly175CaptureWrite written = NativeBattleHoly175CapturePackets.Write5D29E0(process, ordering, component.Stage, component.Geometry, component.View, component.Projection, component.Colors, component.Resources, component.Memory);
        return new(component with { Process = written.Process, Stage = written.Stage, Memory = written.Memory }, written.Ordering, written.StagePackets);
    }

    private static Executed Execute(Prepared prepared)
    {
        Component component = prepared.State;
        NativeBattlePrimitiveProcess process = component.Process;
        byte buffer = process.Buffer80!.Value;
        NativeBattleSoftwarePrimitiveExecution execution = new(process.Software!, component.Textures, component.SoftwareTable, component.Capabilities.HardwareEnabled ?? throw new NotSupportedException("The component requires its actual hardware execution capability."), process.TextureWindowCache!);
        execution.ApplyEnvironment45B660(buffer ^ 1);
        var result = NativeBattleLogicalPasses.Execute(prepared.Ordering.SelectTable(buffer), component.Tables, new(process.TexturePage) { TextureWindowBits = process.TextureWindowBits }, execution, ReadPhysical);
        NativeBattlePrimitiveFrame frame = new(prepared.Ordering.ResetAfterDraw45D080().ClearRoots45D550(), result.Commands, []) { LastDescriptorPerspective = process.TablePerspective[buffer], LastDepthState = process.Depth, LastColorState = process.Color, LastTextureSnapshots = execution.TextureSnapshots };
        process = process with { TexturePage = result.Final.TexturePage, TextureWindowBits = result.Final.TextureWindowBits, TextureWindowCache = result.Cache, Software = execution.State };
        return new(component with { Process = process.Reset500900(4), Textures = execution.Textures }, frame);

        byte[] ReadPhysical(NativeBattlePrimitiveReference reference, int count)
        {
            if (reference.Bank == "holy-capture")
            {
                return component.Memory.BlackPackets.AsSpan().Slice(reference.ByteOffset, count).ToArray();
            }
            return (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);
        }
    }

    private static void WritePng(string path, byte[] rgba)
    {
        using Bitmap bitmap = new(320, 216);
        for (int y = 0; y < 216; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.SavePng(path);
    }

    private static byte[] Render(Executed executed) => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(executed.Frame, executed.State.Textures, capabilities: executed.State.Capabilities, textureWindowCache: executed.State.Process.TextureWindowCache).ToArray();
    private sealed record Component(NativeBattlePrimitiveProcess Process, NativeBattleStageState Stage, NativeBattleEffectTextureState Textures, NativeBattleHoly175CaptureMemory Memory, NativeBattleHoly175Resources Resources, ImmutableArray<NativeBattleRawMesh> Geometry, NativeBattleActionMatrix View, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, NativeBattlePrimitiveTables Tables, NativeSoftwarePrimitiveTable SoftwareTable, NativeBattlePrimitiveCapabilities Capabilities);
    private sealed record Prepared(Component State, NativeBattleLogicalOrdering Ordering, int StagePackets);
    private sealed record Executed(Component State, NativeBattlePrimitiveFrame Frame);
}
