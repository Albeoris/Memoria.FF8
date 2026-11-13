using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaCaptureOrderingTests
{
    private sealed class Material : INativeBattleForeignMaterial
    {
    }

    [Theory]
    [InlineData(0x24, 2)]
    [InlineData(0x26, 3)]
    public void Captured_image_consumers_keep_native_passes_page_blend_and_depth_without_vram_selection(byte opcode, int passes)
    {
        NativeBattleForeignSurface original = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattleArenaGeometry arena = new([new(new(0, 0, 0), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, original)]);
        NativeBattleArenaCaptureMaterial capture = new(arena, new(NativeBattleForeignModelGeometry.Identity, 320, 160, 108), 256, 256) { NativeX = 640, NativeY = 256 };
        NativeBattleForeignSurface surface = original with { Material = capture };
        NativeBattlePilotVisualGeometry geometry = new(new(1, 1, 0, 1, 0), default, default, default, default, 3) { ForeignSurface = surface };
        ImmutableArray<uint> packet = [0x07000000, (uint)opcode << 24 | 0x808080, 0, 0, 80, 0x017A0000, 80u << 16, 0];
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(20, new("surface-image", 0, 0), packet, 0, [201, 202, 203, 0], pilotGeometry: geometry);
        Context context = new(Tables(opcode));
        var executed = NativeBattleLogicalPasses.Execute(ordering, Tables(opcode), new(0x100) { TextureWindowBits = 0 }, context);
        Assert.Equal(passes, executed.Commands.Length);
        Assert.Equal(Enumerable.Range(0, passes).Select(value => (byte)value), executed.Commands.Select(command => command.Pass));
        Assert.Equal(passes, context.NormalCalls);
        Assert.Equal((ushort)0x17A, executed.Final.TexturePage);
        Assert.All(executed.Commands, command =>
        {
            Assert.Equal((ushort)0x17A, command.DrawMode.TexturePage);
            Assert.Equal(3, command.DrawMode.BlendMode);
            Assert.Null(command.DrawMode.TextureSelection);
            Assert.Null(command.DrawMode.TexturePixelSnapshot);
            Assert.Equal(new[] { 201, 202, 203, 0 }, command.Command.VertexPayload4);
            Assert.Same(surface, command.Command.PilotGeometry!.ForeignSurface);
        });
    }

    private static NativeBattlePrimitiveTables Tables(byte opcode)
    {
        byte[] lengths = new byte[256];
        byte[] flagged = new byte[256];
        uint[] handlers = new uint[256];
        uint[] second = new uint[256];
        lengths[opcode] = 7;
        flagged[opcode] = 1;
        handlers[opcode] = 1;
        second[opcode] = (opcode & 2) != 0 ? 1u : 0;
        return new(lengths.ToImmutableArray(), flagged.ToImmutableArray(), handlers.ToImmutableArray(), handlers.ToImmutableArray(), second.ToImmutableArray());
    }

    private sealed class Context(NativeBattlePrimitiveTables tables) : INativeBattleLogicalExecutionContext
    {
        public uint LiveMode => 3;
        public uint LiveDebug => 1;
        public bool HardwareEnabled => true;
        public NativeSoftwarePrimitiveTable SoftwareTable { get; } = new(tables.PreprocessingHandlers);
        public NativeBattlePcTextureWindowCache? Cache { get; set; } = NativeBattlePrimitiveProcess.FromColdProcess().TextureWindowCache! with { ScaleU = 1, ScaleV = 1 };
        public int NormalCalls { get; private set; }
        private ushort page = 0x100;

        public void ExecuteSoftware(NativeBattleLogicalCommand command)
        {
            throw new InvalidOperationException("A deferred host surface image has no software VRAM polygon.");
        }

        public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass)
        {
            NormalCalls++;
            page = mode.TexturePage!.Value;
        }

        public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode) => mode with { TexturePage = page };

        public void EndBatch()
        {
        }
    }
}
