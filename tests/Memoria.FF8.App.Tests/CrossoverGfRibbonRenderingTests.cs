using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverGfRibbonRenderingTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_Quezacotl_ribbons_change_actual_host_pixels_at_both_display_sizes_without_advancing_simulation()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverCompositeActionTests.Fixture fixture = new("Quezacotl", 0, 0);
        HashSet<(int Width, int Height)> verified = [];
        int eligibleFrames = 0;
        int sampledFrames = 0;
        int maximumPackets = 0;
        int maximumChanged = 0;
        int maximumRemovedTriangles = 0;
        fixture.Execute(queuedRoundTrip: true, observe: Observe);
        output.WriteLine($"Quezacotl ribbons: {verified.Count}/2 display sizes verified, {sampledFrames} sampled/{eligibleFrames} eligible frames, maximum {maximumPackets} native GT4 packets, {maximumRemovedTriangles} removed triangles and {maximumChanged} changed RGB pixels.");
        Assert.Equal(2, verified.Count);
        Assert.True(maximumPackets > 0);
        Assert.True(maximumRemovedTriangles > 0);

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (verified.Count == 2 || provider.EffectVisual is not { Ordering: { } ordering, Memory: { } memory } visual)
            {
                return;
            }
            NativeBattleLogicalPacket[] ribbons = ordering.Packets.Where(IsMainRibbon).ToArray();
            if (ribbons.Length == 0)
            {
                return;
            }
            eligibleFrames++;
            if (sampledFrames == 64 || (eligibleFrames - 1) % 24 != 0)
            {
                return;
            }
            sampledFrames++;
            maximumPackets = Math.Max(maximumPackets, ribbons.Length);
            Assert.All(ribbons, packet =>
            {
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
                Assert.Equal(4, geometry.CornerCount);
                Assert.Equal(4, geometry.ScreenOffsets.Length);
                Assert.True(geometry.UseSourceDepths);
                Assert.NotNull(geometry.SourceDepths);
            });
            HashSet<NativeBattlePrimitiveReference> removed = ribbons.Select(packet => NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)).ToHashSet();
            NativeBattleLogicalOrdering control = new(ordering.Table.ToImmutableArray(), ordering.Proxies.ToImmutableArray(), ordering.Packets.Select(packet => removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)) ? packet with { Words = RemoveCommands(packet.Words) } : packet).ToImmutableArray(), ordering.ProxyCursor, ordering.TableBuffer, ordering.OtherTable?.ToImmutableArray());
            string state = JsonSerializer.Serialize(host.Capture());
            string random = JsonSerializer.Serialize(provider.CurrentCrtRandom);
            CrossoverGuestFireHostState? invocation = provider.ActiveAbility;
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) }.Where(size => !verified.Contains(size)))
            {
                gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                int fullTriangles = visual.Draw(gpu, host.GuestRenderCameraOutput, width, height);
                gpu.End();
                Microsoft.Xna.Framework.Color[] full = GpuTestDevice.Read(gpu.Target);
                gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                int controlTriangles = visual.Renderer.DrawOrdering(gpu, control, memory.Textures!, width, height, visual.Scale, ReadControlPacket);
                controlTriangles += visual.Renderer.Draw(gpu, visual.Draws, visual.Textures, CrossoverNativeSpriteRenderer.Camera(host.GuestRenderCameraOutput, visual.OriginToHost), width, height, visual.Scale);
                gpu.End();
                Microsoft.Xna.Framework.Color[] without = GpuTestDevice.Read(gpu.Target);
                int changed = full.Where((pixel, index) => pixel.R != without[index].R || pixel.G != without[index].G || pixel.B != without[index].B).Count();
                maximumRemovedTriangles = Math.Max(maximumRemovedTriangles, fullTriangles - controlTriangles);
                maximumChanged = Math.Max(maximumChanged, changed);
                if (changed > 0)
                {
                    verified.Add((width, height));
                    output.WriteLine($"{width}x{height}: {changed} changed RGB pixels after removing {ribbons.Length} native main-ribbon GT4 packets at host tick {host.Tick}.");
                }
            }
            Assert.Equal(state, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(random, JsonSerializer.Serialize(provider.CurrentCrtRandom));
            Assert.Same(invocation, provider.ActiveAbility);

            byte[] ReadControlPacket(NativeBattlePrimitiveReference reference, int count)
            {
                byte[] bytes = reference.Bank == "20DFAB8" ? memory.Arena!.Read(reference.ByteOffset, count) : (memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);
                if (count > 4 && removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference)))
                {
                    uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                    Array.Clear(bytes, 4, bytes.Length - 4);
                    Assert.Equal(header, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
                }
                return bytes;
            }
        }
    }

    private static bool IsMainRibbon(NativeBattleLogicalPacket packet)
    {
        ImmutableArray<uint> words = packet.Words;
        return packet.PilotGeometry is { Identity.Bank: 0x2521728 } && packet.Reference.Bank is "off_B6D070" or "off_B6D094" && words.Length == 13 && ((words[1] >> 24) & 0xFC) == 0x3C && words[3] == 0x3E94C008 && words[6] == 0x00B6C038 && (words[9] & 0xFFFF) == 0xC008 && (words[12] & 0xFFFF) == 0xC038;
    }

    private static ImmutableArray<uint> RemoveCommands(ImmutableArray<uint> words)
    {
        uint[] removed = new uint[words.Length];
        removed[0] = words[0];
        return removed.ToImmutableArray();
    }
}
