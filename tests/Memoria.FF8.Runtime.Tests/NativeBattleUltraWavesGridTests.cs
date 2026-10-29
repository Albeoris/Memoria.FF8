using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWavesGridTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void All_authored_cells_capture_exact_final_SXY_in_both_native_buffers_without_write_changes(byte buffer)
    {
        NativeBattlePilotVisualIdentity owner = new(154, 1, 2, 3, 4);
        NativeBattleUltraWavesGridDraw Run(NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattleUltraWavesGridInitialization initialized = Initialize(true);
            return NativeBattleUltraWavesGrid.Draw(Memory(), NativeBattleUltraWavesGrid.Wave(initialized.Grid, 11), initialized.Quads, GridAddress, QuadsAddress, NativeBattleLogicalOrdering.Clear(), Projection, 512, buffer, 0x123456, identity);
        }
        NativeBattleUltraWavesGridDraw native = Run(null);
        NativeBattleUltraWavesGridDraw captured = Run(owner);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(1080, captured.Ordering.Packets.Length);
        HashSet<NativeBattlePilotVisualIdentity> identities = [];
        foreach (NativeBattleLogicalPacket packet in captured.Ordering.Packets)
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
            Assert.True(identities.Add(geometry.Identity));
            Assert.True(geometry.ScreenSpace);
            Assert.Equal(1, geometry.ScreenScale);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
            for (int corner = 0; corner < 4; corner++)
            {
                uint xy = packet.Words[2 + corner * 2];
                Assert.Equal(unchecked((short)xy), geometry.Corner(corner).X);
                Assert.Equal(unchecked((short)(xy >> 16)), geometry.Corner(corner).Y);
            }
        }
        Assert.All(captured.Ordering.Proxies, proxy => Assert.Equal((ushort)0, proxy.Flags20));
    }

    [Fact]
    public void Initialize_writes_actual_pointers_UVs_and_border_words_without_inventing_screen_or_CLUT()
    {
        NativeBattleUltraWavesGridInitialization initialized = Initialize(false);
        Assert.Throws<NotSupportedException>(() => initialized.Grid.Read(0, 4));
        Assert.Equal((short)-164, Word(initialized.Grid, 4));
        Assert.Equal((short)-112, Word(initialized.Grid, 6));
        Assert.Equal((short)3, Word(initialized.Grid, 10));
        Assert.Equal(GridAddress, Dword(initialized.Quads, 0));
        Assert.Equal(GridAddress + 12, Dword(initialized.Quads, 4));
        Assert.Equal(GridAddress + 492, Dword(initialized.Quads, 8));
        Assert.Equal(0x09000000u, Dword(initialized.Quads, 16));
        Assert.Throws<NotSupportedException>(() => initialized.Quads.Read(30, 2));
        int last = (27 * 40 - 1) * 96 + 16;
        Assert.Equal(unchecked((short)0xD73F), Word(initialized.Quads, last + 36));
    }

    [Fact]
    public void Wave_uses_actual_retained_border_flag_instead_of_reconstructing_grid_position()
    {
        NativeBattleUltraWavesGridInitialization initialized = Initialize(false);
        int at = (8 * 41 + 9) * 12;
        NativeBattleEffectArena grid = initialized.Grid.Write(at + 8, BitConverter.GetBytes((short)123)).Write(at + 10, BitConverter.GetBytes((short)1));
        NativeBattleEffectArena waved = NativeBattleUltraWavesGrid.Wave(grid, 17);
        Assert.Equal((short)123, Word(waved, at + 8));
        Assert.Equal(Word(grid, 8), Word(waved, 8));
        Assert.Throws<NotSupportedException>(() => NativeBattleUltraWavesGrid.Wave(grid.MarkUnknown(at + 10, 1), 17));
    }

    [Fact]
    public void Draw_uses_signed_focal_shadow_and_retained_quad_pointers_and_publishes_current_GTE()
    {
        NativeBattleUltraWavesGridInitialization initialized = Initialize(true);
        NativeBattleUltraWavesGridDraw drawn = Draw(Memory(), initialized.Grid, initialized.Quads);
        Assert.Equal(new int?[] { 4, 4, 512 }, drawn.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Null(drawn.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(drawn.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(drawn.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Equal(1080, drawn.Ordering.Packets.Length);
        Assert.Equal(0x2C123456u, Dword(drawn.Quads, 20));
        NativeBattleGeometryRegisterStep expected = new(Memory().GeometryRegisters!, Memory().ScriptedColorRegisters!);
        expected = expected with { Geometry = NativeBattleGeometry.LoadMatrix(expected.Geometry, NativeBattleCuragaMatrices.Identity8DD770() with { X = 4, Y = 4, Z = 512 }, true, true, null) };
        expected = NativeBattleGeometry.LoadVector(expected, 0, new(-164, -112, 0), 3);
        expected = NativeBattleGeometry.ProjectSingle(expected, Projection, Memory().ScriptedGraphics!.Dqa, Memory().ScriptedGraphics!.Dqb);
        Assert.Equal(expected.Geometry.Sxy[2]!.Value, Dword(drawn.Grid, 0));
        NativeBattleEffectArena bad = initialized.Quads.Write(0, BitConverter.GetBytes(GridAddress + 1));
        Assert.Throws<InvalidDataException>(() => Draw(Memory(), initialized.Grid, bad));
    }

    [Fact]
    public void Grid_and_registers_roundtrip_continue_exactly_and_missing_controls_fail()
    {
        NativeBattleUltraWavesGridInitialization initialized = Initialize(true);
        NativeBattleUltraWavesGridDraw drawn = Draw(Memory(), initialized.Grid, initialized.Quads);
        NativeBattleUltraWavesGridDraw restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleUltraWavesGridDraw>(NativeBattleTestSnapshots.Json(drawn))!;
        NativeBattleUltraWavesGridDraw Next(NativeBattleUltraWavesGridDraw current) => Draw(current.Memory, NativeBattleUltraWavesGrid.Wave(current.Grid, 18), current.Quads);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(drawn)), NativeBattleTestSnapshots.Json(Next(restored)));
        Assert.Throws<NotSupportedException>(() => Draw(Memory() with { GeometryRegisters = null }, initialized.Grid, initialized.Quads));
    }

    private const uint GridAddress = 0x2000000;
    private const uint QuadsAddress = 0x2010000;
    private static readonly NativeBattleCameraProjection Projection = new(null, 100, 160, 108);
    private static NativeBattleUltraWavesGridDraw Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena grid, NativeBattleEffectArena quads) => NativeBattleUltraWavesGrid.Draw(memory, grid, quads, GridAddress, QuadsAddress, NativeBattleLogicalOrdering.Clear(), Projection, 512, 0, 0x123456);
    private static NativeBattleUltraWavesGridInitialization Initialize(bool knownPackets) => NativeBattleUltraWavesGrid.Initialize(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleUltraWavesGrid.GridLength), knownPackets ? NativeBattleEffectArena.Clear() : NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleUltraWavesGrid.QuadsLength), GridAddress);
    private static short Word(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(at, 2));
    private static uint Dword(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at, 4));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
}
