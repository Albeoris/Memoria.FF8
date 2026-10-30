using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251PresentationTests
{
    [Theory]
    [InlineData((byte)0x31, -50)]
    [InlineData((byte)0x41, -200)]
    [InlineData((byte)0x42, -200)]
    public void Programme_capture_uses_actual_morph_or_deformed_scratch_with_unknown_W_and_preserves_full_native_state(byte command, int expectedX)
    {
        List<NativeBattleEffect251DrawBatch> plainBatches = [];
        List<NativeBattleEffect251DrawBatch> capturedBatches = [];
        NativeBattleEffect251Frame plain = Draw(null, plainBatches);
        NativeBattleEffect251Frame captured = Draw(new(251, 0x100, 0, 123, 17), capturedBatches);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(plainBatches), NativeBattleTestSnapshots.Json(capturedBatches));
        NativeBattleGeometryPrimitive polygon = Assert.Single(capturedBatches.SelectMany(batch => batch.Batch.Attempts), attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.Equal(new Vector3(expectedX, -100, 2000), polygon.PilotGeometry!.A);
        Assert.True(polygon.PilotGeometry.UseSourceDepths);
        Assert.False(captured.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(captured.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);

        NativeBattleEffect251Frame Draw(NativeBattlePilotVisualIdentity? identity, List<NativeBattleEffect251DrawBatch> batches)
        {
            const uint sourceAddress = 0x10000000;
            const uint selectors = 0x20000000;
            uint scratch = NativeBattleEffectNodeMemory.ArenaAddress + 0xA880;
            byte[] geometry = Geometry();
            byte[] data = new byte[16 + geometry.Length];
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 16);
            geometry.CopyTo(data, 16);
            NativeBattleGfAnimationSource source = new(sourceAddress, data.ToImmutableArray());
            NativeBattleEffect251Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(sourceAddress + 16, new(0, sourceAddress + 16, geometry.ToImmutableArray())), null!, null!, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(selectors, [command]).Add(0x1438C90, new byte[16384].ToImmutableArray()));
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < 4; index++)
            {
                arena = arena.MarkUnknown(0xA880 + index * 8 + 6, 2);
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            NativeBattleEffect251Frame frame = new(memory, new(251, 250, new(251, NativeBattleEffect251Profile.Entry, NativeBattleEffect251Profile.Initializer), null, null) { Pools = [] }, new(42), NativeBattleEffect251State.FromColdProcess().WriteDword(NativeBattleEffect251Profile.ArenaGlobal, NativeBattleEffectNodeMemory.ArenaAddress)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[200])) };
            NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null)).Write(0x20, BitConverter.GetBytes(selectors)).Write(0x24, new byte[2]).Write(0x26, [7, 0]).Write(0x28, new byte[6]).Write(0x2E, BitConverter.GetBytes((short)8192)).Write(0x30, BitConverter.GetBytes((short)4096)).Write(0x34, BitConverter.GetBytes(scratch));
            byte[] node = new byte[44];
            BinaryPrimitives.WriteUInt32LittleEndian(node.AsSpan(4), 0x200);
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
            }
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
            NativeBattleEffect251Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), _ => 0, null!, (current, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleEffect251DrawBatch>(operation));
                return current;
            });
            return NativeBattleEffect251Programme.DrawNode(frame, source, new(0, 0, node.ToImmutableArray()), caller, resources, inputs, pilotIdentity: identity);
        }
    }

    private static byte[] Geometry()
    {
        const int polygon = 76;
        byte[] data = new byte[132];
        BinaryPrimitives.WriteInt32LittleEndian(data, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 2);
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int row = 12 + (shape * 4 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(row), (short)(xy[vertex * 2] + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(row + 2), xy[vertex * 2 + 1]);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(polygon + 12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(polygon + 16), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(polygon + 20 + corner * 2), (ushort)(corner * 2));
        }
        return data;
    }
}
