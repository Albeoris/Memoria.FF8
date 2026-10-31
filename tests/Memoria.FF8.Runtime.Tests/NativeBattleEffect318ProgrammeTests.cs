using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect318LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect318ProgrammeTests
{
    [Theory]
    [InlineData(0, 40100, 8192)]
    [InlineData(0x8000, 40100, 4096)]
    [InlineData(0x200, 20100, 4096)]
    public void Header_branches_preserve_full_MAC_translation_and_selected_rotation_composition(uint header, int expectedX, short expectedR)
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100 };
        NativeBattleEffectArena caller = Caller(matrix).MarkUnknown(0x24, 4);
        NativeBattleEffect318Frame result = NativeBattleEffect318Programme.DrawNode(fixture.Frame, source, Node(header, 20000), caller, fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedX, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(unchecked((byte)expectedR), result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal(unchecked((byte)(expectedR >> 8)), result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.IsType<NativeBattleEffect318DrawBatch>(Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Intermediate_morph_writes_XYZ_only_into_actual_scratch_and_publishes_GTE_cue()
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Arena + 0x19D8;
        fixture.Frame = WriteBytes(fixture.Frame, scratch, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8);
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(0x24, BitConverter.GetBytes(scratch));
        NativeBattleGfAnimationNode node = Node(0x200, 0, 0, 1, 2048);
        NativeBattleEffect318Frame result = NativeBattleEffect318Programme.DrawNode(fixture.Frame, source, node, caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, scratch, 8);
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(row.Read(0, 2)));
        Assert.DoesNotContain(true, row.ReadSnapshot(6, 2).Known);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(scratch, NativeBattleEffect318Lower.Dword318(temporary, 4));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(4096, 20)]
    public void Endpoint_morph_selects_source_rows_without_consuming_unknown_scratch_pointer(short factor, int vertexOffset)
    {
        Fixture fixture = new(0x65F2B0);
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect318Frame result = NativeBattleEffect318Programme.DrawNode(fixture.Frame, source, Node(0x200, 0, 0, 1, factor), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(0x24, 4), fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(mesh.Address + (uint)vertexOffset, NativeBattleEffect318Lower.Dword318(temporary, 4));
    }

    [Fact]
    public void Fully_faded_zero_color_node_does_not_allocate_or_read_unknown_caller()
    {
        Fixture fixture = new(0x65F2B0);
        NativeBattleGfAnimationNode node = Node(0, 0);
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        NativeBattleEffect318Frame result = NativeBattleEffect318Programme.DrawNode(fixture.Frame, null!, node with { Data = data.ToImmutableArray() }, NativeBattleEffectArena.Clear().MarkUnknown(0, 40), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_selected_node_capture_keeps_native_XYZ_W_morph_stores_packets_and_worlds(bool morph)
    {
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = CapturedSource();
        Fixture Build(bool capture)
        {
            Fixture fixture = new(0x65F2B0);
            fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
            if (capture)
            {
                fixture.Frame = fixture.Frame with { CapturePresentation = true, PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(318, Read(fixture.Frame, fixture.Address), 44) };
            }
            return fixture;
        }
        NativeBattleGfAnimationNode node = Node(0x200, 0, 0, morph ? (short)1 : (short)0, morph ? (short)2048 : (short)0);
        Fixture plainFixture = Build(false);
        Fixture capturedFixture = Build(true);
        uint scratch = Fixture.Arena + 0x19D8;
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }).Write(0x24, BitConverter.GetBytes(scratch));
        var plain = NativeBattleEffect318Programme.DrawNode(plainFixture.Frame, source, node, caller, plainFixture.Resources, CapturedInputs(plainFixture));
        string expected = System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain));
        string packets = System.Text.Json.JsonSerializer.Serialize(plainFixture.Operations);
        var captured = NativeBattleEffect318Programme.DrawNode(capturedFixture.Frame, source, node, caller, capturedFixture.Resources, CapturedInputs(capturedFixture));
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        Assert.Equal(packets, System.Text.Json.JsonSerializer.Serialize(capturedFixture.Operations));
        var batch = Assert.IsType<NativeBattleEffect318DrawBatch>(Assert.Single(capturedFixture.Operations)).Batch;
        var primitive = Assert.Single(batch.Attempts).Primitive!;
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(morph ? -10 : -20, -20, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(morph ? 10 : 0, 20, 2000), visual.C);
        Assert.Equal((ushort)318, visual.Identity.Effect);
        Assert.True(visual.Identity.Generation > 0);
        Assert.NotEqual(0, visual.Identity.ResourceGeneration);
        Assert.False(visual.ScreenSpace);
    }

    [Fact]
    public void Live_programme_nodes_keep_actual_task_workspace_identity_until_successful_reinitialize()
    {
        (NativeBattleGfAnimationSource original, NativeBattleStaticMeshResource mesh) = CapturedSource();
        byte[] bytes = new byte[192];
        original.Data.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 128);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(128), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(132), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(136), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(160), 0x200);
        NativeBattleGfAnimationSource source = original with { Data = bytes.ToImmutableArray() };
        const uint bank = 0x40000000;
        const uint workspace = bank + 32;
        Fixture Build(bool capture)
        {
            Fixture fixture = new(0x65F2B0);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x50000000, bank, 256, 1);
            (pool, int slot) = pool.Append(0x65F2B0);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[244]));
            fixture.Frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] }, CapturePresentation = capture, PilotIdentity = capture ? NativeBattlePilotVisualLifetimes.Identity(318, pool.Slots[slot]) : null };
            fixture.Resources = fixture.Resources with { Programmes = fixture.Resources.Programmes.Add(source.Address, source), Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
            fixture.Frame = NativeBattleEffect318Programme.Initialize(fixture.Frame, workspace, source.Address, 64, fixture.Resources);
            return fixture;
        }
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 });
        Fixture plain = Build(false);
        Fixture captured = Build(true);
        for (int frame = 0; frame < 2; frame++)
        {
            plain.Frame = NativeBattleEffect318Programme.Advance(plain.Frame, workspace, 64, caller, plain.Resources, CapturedInputs(plain)).Frame;
        }
        string native = System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Frame));
        string packets = System.Text.Json.JsonSerializer.Serialize(plain.Operations);
        for (int frame = 0; frame < 2; frame++)
        {
            captured.Frame = NativeBattleEffect318Programme.Advance(captured.Frame, workspace, 64, caller, captured.Resources, CapturedInputs(captured)).Frame;
        }
        Assert.Equal(native, System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        Assert.Equal(packets, System.Text.Json.JsonSerializer.Serialize(captured.Operations));
        NativeBattlePilotVisualIdentity Identity(int index)
        {
            var batch = Assert.IsType<NativeBattleEffect318DrawBatch>(captured.Operations[index]).Batch;
            return Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batch.Attempts).Primitive!.PilotGeometry).Identity;
        }
        NativeBattlePilotVisualIdentity first = Identity(0);
        Assert.Equal(first, Identity(1));
        captured.Frame = NativeBattleEffect318Programme.Initialize(captured.Frame, workspace, source.Address, 64, captured.Resources);
        captured.Frame = NativeBattleEffect318Programme.Advance(captured.Frame, workspace, 64, caller, captured.Resources, CapturedInputs(captured)).Frame;
        Assert.Equal(first.Generation, Identity(2).Generation);
        Assert.NotEqual(first.ResourceGeneration, Identity(2).ResourceGeneration);
    }

    private static NativeBattleEffect318Inputs CapturedInputs(Fixture fixture) => fixture.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108) };

    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) CapturedSource()
    {
        byte[] bytes = new byte[128];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 60);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] x = [-20, 20, 0];
        short[] y = [-20, -20, 20];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 28 + shape * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)(x[vertex] + shape * 20)));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y[vertex]);
            }
        }
        int cursor = 76;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), group == 2 ? 1 : 0);
            cursor += 4;
            if (group == 2)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), 0x24112233);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), 4);
                cursor += 20;
            }
        }
        var source = new NativeBattleGfAnimationSource(0xFF8520, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray()));
    }

    private static NativeBattleEffectArena Caller(NativeBattleActionMatrix matrix) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(-128));

    private static NativeBattleGfAnimationNode Node(uint header, short x, short first = 0, short second = 0, short factor = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), factor);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), first);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) Source()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 3);
        for (int shape = 0; shape < 3; shape++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + shape * 8), (short)(100 + shape * 200));
        }
        NativeBattleGfAnimationSource source = new(0xFFD9B8, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray()));
    }
}
