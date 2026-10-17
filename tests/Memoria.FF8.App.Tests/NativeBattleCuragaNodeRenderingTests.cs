using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCuragaNodeRenderingTests
{
    [Fact]
    public void Actual_initializer_and_private_node_cycle_render_original_sprites_FT4_GT4_G3_G4_and_restore_independently()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var fixture = Fixture(game);
        NativeBattleCuragaFrame frame = fixture.Frame;
        NativeCrtRandomState random = new(1);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess();
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors ?? throw new InvalidDataException("Curaga requires original color interpolation constants.");
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }, 216, 160, 108);
        HashSet<byte> opcodes = [];
        int peak = 0;
        int completed = -1;
        bool restoredPixels = false;
        for (ushort counter = 0; counter < 200; counter++)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(fixture.Task).WriteWord(0x34 + 0x18, counter);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(fixture.Task, task).Pools }, State = frame.State with { PrimaryDrawCursor = frame.State.SourceArenaOffset + ((counter & 1) != 0 ? 0 : 0x6000), SecondaryDrawCursor = frame.State.SourceArenaOffset + ((counter & 1) != 0 ? 0x6000 : 0xC000) } };
            NativeBattleCuragaNodePresentationStep step = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(frame, fixture.Task, random, _ => throw new InvalidOperationException(), projection, 0, NativeBattleLogicalOrdering.Clear(), colors);
            frame = step.Frame;
            random = step.Random;
            Assert.Equal(frame.State.PrimaryDrawCursor, frame.State.SourceArenaOffset + ((counter & 1) != 0 ? 0 : 0x6000) + step.Batch.AllocationBytes);
            foreach (NativeBattleGfGeometryAttempt attempt in step.Batch.Attempts.Where(attempt => attempt.Primitive is not null))
            {
                opcodes.Add((byte)(attempt.Primitive!.Words[1] >> 24 & 0xFC));
            }
            var pass = NativeBattleLogicalPasses.Execute(step.Ordering, tables, new(process.TexturePage) { TextureWindowBits = process.TextureWindowBits }, 1, process.TextureWindowCache);
            process = process with { TexturePage = pass.Final.TexturePage, TextureWindowBits = pass.Final.TextureWindowBits, TextureWindowCache = pass.Cache };
            NativeBattlePrimitiveFrame primitive = new(NativeBattleLogicalOrdering.Clear(), pass.Commands, []) { LastDescriptorPerspective = false, LastDepthState = process.Depth, LastColorState = process.Color };
            BattleRenderer renderer = new(320, 216);
            byte[] pixels = renderer.RenderNativePrimitiveFrame(primitive, frame.Memory.Textures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), textureWindowCache: process.TextureWindowCache).ToArray();
            int changed = Enumerable.Range(0, 320 * 216).Count(index => pixels[index * 4] != 0 || pixels[index * 4 + 1] != 0 || pixels[index * 4 + 2] != 0);
            peak = Math.Max(peak, changed);
            if (counter >= 12 && changed > 0 && !restoredPixels)
            {
                NativeBattlePrimitiveFrame clone = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(primitive))!;
                NativeBattleEffectTextureState textureClone = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(frame.Memory.Textures))!;
                NativeBattlePcTextureWindowCache cacheClone = JsonSerializer.Deserialize<NativeBattlePcTextureWindowCache>(JsonSerializer.Serialize(process.TextureWindowCache))!;
                byte[] restored = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(clone, textureClone, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), textureWindowCache: cacheClone).ToArray();
                Assert.Equal(pixels, restored);
                NativeBattleCuragaFrame independent = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(frame))!;
                NativeBattleCuragaNodeDrawStep first = NativeBattleCuragaNodeKinematics.DrawNodes880390And87ED10(frame, fixture.Task, projection, 0, NativeBattleLogicalOrdering.Clear(), colors);
                NativeBattleCuragaNodeDrawStep second = NativeBattleCuragaNodeKinematics.DrawNodes880390And87ED10(independent, fixture.Task, projection, 0, NativeBattleLogicalOrdering.Clear(), colors);
                Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
                restoredPixels = true;
            }
            task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Task);
            if (counter >= 45 && task.ReadWord(0x34 + 0x14) == 0 && task.ReadWord(0x34 + 0x16) == 0)
            {
                completed = counter;
                break;
            }
        }
        Assert.InRange(completed, 45, 199);
        Assert.True(restoredPixels);
        Assert.True(peak > 0, "Original Curaga packets produced no RGB pixels.");
        Assert.Contains((byte)0x2C, opcodes);
        Assert.Contains((byte)0x3C, opcodes);
        Assert.Contains((byte)0x30, opcodes);
        Assert.Contains((byte)0x38, opcodes);
        Console.WriteLine($"Original Curaga private node cycle completed at{completed}; peak{peak} nonblack pixels; commands{string.Join(',', opcodes.Order())}.");
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture(GameAssets game)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, int header) = packets.Prepare(new(0, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(0, 1, 0, 1, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(header, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleCuragaResources resources = game.ReadNativeCuragaResources();
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(28, game.ReadNativeEffectCatalogForEffects([28])) with { Parameters = new(0, 0, 1, header, 0, 0) };
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(initialized.Memory, initialized.Effect, packets, resources);
        NativeBattleEffectTextureDrain drain = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena);
        frame = frame with { Memory = frame.Memory with { Textures = drain.State, CuragaExecutable = frame.Memory.CuragaExecutable!.Initialize87EA20(resources.Executable!) } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(3, 0x87E1A0, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 0));
        //This isolates the actual private node engine with explicitly declared zero anchors and camera; it does not admit Session callbacks.
        NativeBattleModulePoolTask task = generic.Hierarchy.Read(generic.Address)
            .WriteDword(0x34 + 0x224, NativeBattleCuragaExecutableState.Address + 4)
            .WriteDword(0x34 + 0x228, NativeBattleCuragaExecutableState.Address + 0x44)
            .WriteDword(0x34 + 0x22C, NativeBattleCuragaExecutableState.Address + 0x84)
            .WriteDword(0x34 + 0x230, NativeBattleCuragaExecutableState.Address + 0xC4)
            .WriteWord(0x34 + 0x1C, 1);
        int first = frame.State.AuxiliaryCursor;
        frame = frame with { Effect = frame.Effect with { Pools = generic.Hierarchy.Write(generic.Address, task).Pools }, Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(first, new byte[0x25F8 + 0x16F80]) }, State = frame.State with { Auxiliary25F8 = first, Auxiliary16F80 = first + 0x25F8, AuxiliaryCursor = first + 0x25F8 + 0x16F80, Counter26D76C0 = 0, Counter26D82E8 = 0, CopiedCamera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 } } };
        return (frame, generic.Address);
    }
}
