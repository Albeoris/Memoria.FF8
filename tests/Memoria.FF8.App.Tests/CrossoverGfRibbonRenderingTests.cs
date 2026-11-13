using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
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
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")!);
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        using CrossoverCompositeActionTests.Fixture fixture = new("Quezacotl", 0, 0);
        HashSet<(int Width, int Height)> verified = [];
        int sampledFrames = 0;
        int maximumChanged = 0;
        int maximumPackets = 0;
        int eligibleRibbonFrames = 0;
        int maximumFullTriangles = 0;
        int maximumControlTriangles = 0;
        int maximumRemovedTriangles = 0;
        int orderingFrames = 0;
        int typedPackets = 0;
        int mainIdentityPackets = 0;
        int materialPackets = 0;
        int maximumViewportOverlap = 0;
        bool ribbonOnlyChecked = false;
        Dictionary<string, int> packetGroups = [];
        string? firstRibbonGeometry = null;
        string? firstViewportRibbon = null;
        fixture.Execute(queuedRoundTrip: true, observe: Observe);
        output.WriteLine($"Quezacotl ribbon diagnostics: {orderingFrames} ordering frames, {typedPackets} typed packets, {mainIdentityPackets} main-pool identities, {materialPackets} native outline-material matches, {eligibleRibbonFrames} eligible ribbon frames, {sampledFrames} GPU-sampled frames, maximum {maximumPackets} selected packets, maximum {maximumChanged} changed RGB pixels, {verified.Count}/2 verified display sizes. Triangle maxima: full {maximumFullTriangles}, control {maximumControlTriangles}, removed {maximumRemovedTriangles}.");
        output.WriteLine($"Packed native SXY: maximum {maximumViewportOverlap} selected ribbon bounds overlap 320x240; ribbon-only control executed {ribbonOnlyChecked}.");
        foreach ((string group, int count) in packetGroups)
        {
            output.WriteLine($"Observed {count}: {group}");
        }
        if (firstRibbonGeometry is not null)
        {
            output.WriteLine(firstRibbonGeometry);
        }
        if (firstViewportRibbon is not null)
        {
            output.WriteLine(firstViewportRibbon);
        }
        Assert.Equal(2, verified.Count);
        Assert.True(maximumPackets > 0);
        output.WriteLine($"Quezacotl main ribbons: up to {maximumPackets} native GT4 packets, {maximumChanged} changed RGB pixels; 320x240 and 640x360 checked in {sampledFrames} bounded in-memory frames.");

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (verified.Count == 2 || provider.EffectVisual is not { Ordering: { } ordering, Memory: { } memory } visual)
            {
                return;
            }
            orderingFrames++;
            foreach (NativeBattleLogicalPacket packet in ordering.Packets)
            {
                if (packet.PilotGeometry is not { } geometry)
                {
                    continue;
                }
                typedPackets++;
                mainIdentityPackets += geometry.Identity.Bank == 0x2521728 ? 1 : 0;
                materialPackets += HasOutlineMaterial(packet.Words) ? 1 : 0;
                string group = $"pool {geometry.Identity.Bank:X8}, packet bank {packet.Reference.Bank}, words {packet.Words.Length}, opcode {Word(packet.Words, 1) >> 24:X2}, material {Word(packet.Words, 3):X8}/{Word(packet.Words, 6):X8}/{Word(packet.Words, 9) & 0xFFFF:X4}/{Word(packet.Words, 12) & 0xFFFF:X4}, offsets {geometry.ScreenOffsets.Length}, source depths {geometry.UseSourceDepths}/{geometry.SourceDepths.HasValue}";
                if (packetGroups.TryGetValue(group, out int count))
                {
                    packetGroups[group] = count + 1;
                }
                else if (packetGroups.Count < 24)
                {
                    packetGroups.Add(group, 1);
                }
            }
            NativeBattleLogicalPacket[] ribbons = ordering.Packets.Where(IsMainRibbon).ToArray();
            if (ribbons.Length == 0)
            {
                return;
            }
            eligibleRibbonFrames++;
            if (sampledFrames == 64 || (eligibleRibbonFrames - 1) % 24 != 0)
            {
                return;
            }
            sampledFrames++;
            maximumPackets = Math.Max(maximumPackets, ribbons.Length);
            NativeBattlePilotVisualGeometry firstGeometry = ribbons[0].PilotGeometry!;
            firstRibbonGeometry ??= $"First selected ribbon at host tick {host.Tick}: A {firstGeometry.A}, C {firstGeometry.C}, offsets {string.Join(';', firstGeometry.ScreenOffsets)}, depths {firstGeometry.SourceDepths}, color words {Word(ribbons[0].Words, 1):X8}/{Word(ribbons[0].Words, 4):X8}/{Word(ribbons[0].Words, 7):X8}/{Word(ribbons[0].Words, 10):X8}.";
            Assert.All(ribbons, packet =>
            {
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
                Assert.Equal(4, geometry.CornerCount);
                Assert.Equal(4, geometry.ScreenOffsets.Length);
                Assert.True(geometry.UseSourceDepths);
                Assert.NotNull(geometry.SourceDepths);
            });
            HashSet<NativeBattlePrimitiveReference> removed = ribbons.Select(packet => NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)).ToHashSet();
            NativeBattleLogicalPacket[] inside = ribbons.Where(OverlapsViewport).ToArray();
            maximumViewportOverlap = Math.Max(maximumViewportOverlap, inside.Length);
            if (firstViewportRibbon is null)
            {
                NativeBattleLogicalPacket described = inside.FirstOrDefault() ?? ribbons[0];
                NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 0, ScaleV = 0, SpriteTileCacheControl1CCFD90 = 0 };
                var executed = NativeBattleLogicalPasses.Execute(ordering, tables, new(0) { TextureWindowBits = 0 }, 1, cache, ReadPhysicalPacket);
                NativeBattlePrimitiveReference reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(described.Reference);
                NativeBattleLogicalResolvedCommand[] commands = executed.Commands.Where(command => NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Command.Packet) == reference).ToArray();
                string points = string.Join(';', new[] { 2, 5, 8, 11 }.Select(index => $"({unchecked((short)described.Words[index]) / 8f},{unchecked((short)(described.Words[index] >> 16)) / 8f})"));
                firstViewportRibbon = $"Packed ribbon at tick {host.Tick}, viewport overlap {OverlapsViewport(described)}: SXY/8 {points}; {commands.Length} resolved commands: {string.Join(';', commands.Select(command => $"pass {command.Pass}, flags {command.Command.Flags20:X4}, tpage {command.DrawMode.TexturePage:X4}, ABR {command.DrawMode.BlendMode}, UV scale {command.DrawMode.TextureSelection?.ScaleU}/{command.DrawMode.TextureSelection?.ScaleV}, viewport {command.DrawMode.NormalViewport}, viewport inputs {command.DrawMode.ViewportInputs}, depths [{string.Join(',', command.Command.VertexPayload4)}]"))}.";
            }
            NativeBattleLogicalOrdering control = new(ordering.Table.ToImmutableArray(), ordering.Proxies.ToImmutableArray(), ordering.Packets.Select(packet => removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)) ? packet with { Words = RemoveCommands(packet.Words) } : packet).ToImmutableArray(), ordering.ProxyCursor, ordering.TableBuffer, ordering.OtherTable?.ToImmutableArray());
            string state = JsonSerializer.Serialize(host.Capture());
            string random = JsonSerializer.Serialize(provider.CurrentCrtRandom);
            CrossoverGuestFireHostState? invocation = provider.ActiveAbility;
            bool checkRibbonOnly = !ribbonOnlyChecked && inside.Length > 0;
            NativeBattleLogicalOrdering? ribbonOnly = checkRibbonOnly ? new(ordering.Table.ToImmutableArray(), ordering.Proxies.ToImmutableArray(), ordering.Packets.Select(packet => packet with { Words = FilterOtherGeometry(packet.Words, removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)), tables.WordLengths) }).ToImmutableArray(), ordering.ProxyCursor, ordering.TableBuffer, ordering.OtherTable?.ToImmutableArray()) : null;
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
                maximumFullTriangles = Math.Max(maximumFullTriangles, fullTriangles);
                maximumControlTriangles = Math.Max(maximumControlTriangles, controlTriangles);
                maximumRemovedTriangles = Math.Max(maximumRemovedTriangles, fullTriangles - controlTriangles);
                maximumChanged = Math.Max(maximumChanged, changed);
                if (changed > 0)
                {
                    verified.Add((width, height));
                    output.WriteLine($"{width}x{height}: {changed} changed RGB pixels with only {ribbons.Length} reached main-ribbon GT4 packets removed at host tick {host.Tick}.");
                }
                if (checkRibbonOnly)
                {
                    gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                    ProbeTriangleRenderer probe = new(gpu, width, height, output);
                    int triangles = visual.Renderer.DrawOrdering(probe, ribbonOnly!, memory.Textures!, width, height, visual.Scale, ReadRibbonOnlyPacket);
                    gpu.End();
                    int pixels = GpuTestDevice.Read(gpu.Target).Count(pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                    output.WriteLine($"Ribbon-only control at tick {host.Tick}, {width}x{height}: {inside.Length} overlapping native bounds, {triangles} triangles, {pixels} nonblack RGB pixels; retained original nongeometry controls.");
                    probe.Report();
                }
            }
            ribbonOnlyChecked |= checkRibbonOnly;
            Assert.Equal(state, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(random, JsonSerializer.Serialize(provider.CurrentCrtRandom));
            Assert.Same(invocation, provider.ActiveAbility);

            byte[] ReadControlPacket(NativeBattlePrimitiveReference reference, int count)
            {
                byte[] bytes = ReadPhysicalPacket(reference, count);
                if (count > 4 && removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference)))
                {
                    uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                    Array.Clear(bytes, 4, bytes.Length - 4);
                    Assert.Equal(header, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
                }
                return bytes;
            }

            byte[] ReadPhysicalPacket(NativeBattlePrimitiveReference reference, int count) => reference.Bank == "20DFAB8" ? memory.Arena!.Read(reference.ByteOffset, count) : (memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);

            byte[] ReadRibbonOnlyPacket(NativeBattlePrimitiveReference reference, int count)
            {
                byte[] bytes = ReadPhysicalPacket(reference, count);
                if (count > 4)
                {
                    ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
                    ImmutableArray<uint> filtered = FilterOtherGeometry(words, removed.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference)), tables.WordLengths);
                    for (int index = 1; index < filtered.Length; index++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), filtered[index]);
                    }
                }
                return bytes;
            }
        }
    }

    private static bool IsMainRibbon(NativeBattleLogicalPacket packet)
    {
        ImmutableArray<uint> words = packet.Words;
        return packet.PilotGeometry is { Identity.Bank: 0x2521728 } && packet.Reference.Bank is "off_B6D070" or "off_B6D094" && HasOutlineMaterial(words);
    }

    private static bool HasOutlineMaterial(ImmutableArray<uint> words) => words.Length == 13 && ((words[1] >> 24) & 0xFC) == 0x3C && words[3] == 0x3E94C008 && words[6] == 0x00B6C038 && (words[9] & 0xFFFF) == 0xC008 && (words[12] & 0xFFFF) == 0xC038;

    private static uint Word(ImmutableArray<uint> words, int index) => words.Length > index ? words[index] : 0;

    private static bool OverlapsViewport(NativeBattleLogicalPacket packet)
    {
        (float X, float Y)[] points = new[] { 2, 5, 8, 11 }.Select(index => (unchecked((short)packet.Words[index]) / 8f, unchecked((short)(packet.Words[index] >> 16)) / 8f)).ToArray();
        return points.Max(point => point.X) >= 0 && points.Min(point => point.X) < 320 && points.Max(point => point.Y) >= 0 && points.Min(point => point.Y) < 240;
    }

    private static ImmutableArray<uint> FilterOtherGeometry(ImmutableArray<uint> words, bool keepRibbon, ImmutableArray<byte> lengths)
    {
        if (keepRibbon)
        {
            return words;
        }
        uint[] filtered = words.ToArray();
        int cursor = 1;
        while (cursor < words.Length)
        {
            byte opcode = (byte)(words[cursor] >> 24);
            int length = opcode == 0 ? 1 : Math.Max(1, (int)lengths[opcode]);
            Assert.True(cursor + length <= words.Length);
            if (opcode is 2 or >= 0x20 and < 0x80)
            {
                Array.Clear(filtered, cursor, length);
            }
            cursor += length;
        }
        return filtered.ToImmutableArray();
    }

    private static ImmutableArray<uint> RemoveCommands(ImmutableArray<uint> words)
    {
        uint[] removed = new uint[words.Length];
        removed[0] = words[0];
        return removed.ToImmutableArray();
    }

    private sealed class ProbeTriangleRenderer(ITriangleRenderer target, int width, int height, ITestOutputHelper output) : ITriangleRenderer
    {
        private readonly HashSet<PixelImage> textures = [];
        private readonly List<string> details = [];
        private Vector2 minimumPosition = new(float.PositiveInfinity);
        private Vector2 maximumPosition = new(float.NegativeInfinity);
        private Vector2 minimumUv = new(float.PositiveInfinity);
        private Vector2 maximumUv = new(float.NegativeInfinity);
        private float minimumDepth = float.PositiveInfinity;
        private float maximumDepth = float.NegativeInfinity;
        private float minimumW = float.PositiveInfinity;
        private float maximumW = float.NegativeInfinity;
        private int triangles;
        private int viewportOverlaps;

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            triangles++;
            TriangleVertex[] vertices = [a, b, c];
            foreach (TriangleVertex vertex in vertices)
            {
                minimumPosition = Vector2.Min(minimumPosition, vertex.Position);
                maximumPosition = Vector2.Max(maximumPosition, vertex.Position);
                minimumUv = Vector2.Min(minimumUv, vertex.Uv);
                maximumUv = Vector2.Max(maximumUv, vertex.Uv);
                minimumDepth = Math.Min(minimumDepth, vertex.Depth);
                maximumDepth = Math.Max(maximumDepth, vertex.Depth);
                minimumW = Math.Min(minimumW, vertex.ReciprocalW);
                maximumW = Math.Max(maximumW, vertex.ReciprocalW);
            }
            bool overlaps = vertices.Max(vertex => vertex.Position.X) >= 0 && vertices.Min(vertex => vertex.Position.X) < width && vertices.Max(vertex => vertex.Position.Y) >= 0 && vertices.Min(vertex => vertex.Position.Y) < height;
            viewportOverlaps += overlaps ? 1 : 0;
            if (details.Count < 4)
            {
                Vector2 ab = b.Position - a.Position;
                Vector2 ac = c.Position - a.Position;
                float area = ab.X * ac.Y - ab.Y * ac.X;
                details.Add($"Triangle {triangles}: overlap {overlaps}, doubled area {area}, vertices {string.Join(';', vertices.Select(vertex => $"XY {vertex.Position}, depth {vertex.Depth}, W {vertex.ReciprocalW}, UV {vertex.Uv}, RGBA {vertex.Color}"))}; material blend {material.Blend}, depth {material.DepthComparison}/{material.DepthMapping}, scale {material.DepthScale}, write {material.DepthWrite}/{material.ColorWrite}, clip {material.ClipRectangle}, opacity {material.ActorOpacity}, replace alpha {material.ActorOpacityReplacesAlpha}, min/max alpha {material.MinimumAlpha}/{material.MaximumAlpha}, alpha multiplier {material.AlphaMultiplier}, texture alpha {material.TextureSelectsAlpha}, sampler {material.Sampler}.");
            }
            if (material.Texture is { } texture && textures.Add(texture) && textures.Count <= 3)
            {
                int nonblack = texture.Pixels.Count(pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
                int nonzeroAlpha = texture.Pixels.Count(pixel => pixel.A != 0);
                int visible = texture.Pixels.Count(pixel => pixel.A != 0 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0));
                Vector2 center = (a.Uv + b.Uv + c.Uv) / 3;
                TriangleSampler sampler = material.Sampler;
                string samples = string.Join(';', vertices.Select(vertex => vertex.Uv).Append(center).Select(uv =>
                {
                    bool wrap = sampler is TriangleSampler.PointWrap or TriangleSampler.LinearWrap;
                    int x = (int)MathF.Floor(uv.X * texture.Width);
                    int y = (int)MathF.Floor(uv.Y * texture.Height);
                    x = wrap ? (x % texture.Width + texture.Width) % texture.Width : Math.Clamp(x, 0, texture.Width - 1);
                    y = wrap ? (y % texture.Height + texture.Height) % texture.Height : Math.Clamp(y, 0, texture.Height - 1);
                    Rgba32 pixel = texture[x, y];
                    return $"UV {uv} -> ({x},{y}) RGBA {pixel.R},{pixel.G},{pixel.B},{pixel.A}";
                }));
                output.WriteLine($"Ribbon texture {textures.Count}: {texture.Width}x{texture.Height}, nonblack {nonblack}, nonzero alpha {nonzeroAlpha}, both {visible}; first triangle corner/centroid nearest texels: {samples}.");
            }
            target.Draw(a, b, c, material);
        }

        public void Report()
        {
            output.WriteLine($"Forwarded ribbon GPU triangles: {triangles}, viewport-overlapping {viewportOverlaps}, screen {minimumPosition}..{maximumPosition}, depth {minimumDepth}..{maximumDepth}, reciprocal W {minimumW}..{maximumW}, UV {minimumUv}..{maximumUv}, textures {textures.Count}.");
            foreach (string detail in details)
            {
                output.WriteLine(detail);
            }
        }
    }
}
