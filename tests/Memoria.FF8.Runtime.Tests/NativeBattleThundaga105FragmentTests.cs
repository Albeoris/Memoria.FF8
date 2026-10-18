using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaga105FragmentTests
{
    [Fact]
    public void InitializationRetainsMeshPaddingUsesGlobalElseIfAndConsumesSixDrawsInOrder()
    {
        NativeBattleThundagaFragmentPair pair = Pair();
        NativeCrtRandomState random = new(7);
        ushort[] draws = new ushort[6];
        NativeCrtRandomState expected = random;
        for (int index = 0; index < draws.Length; index++)
        {
            expected = expected.Draw(out draws[index]);
        }
        NativeBattleThundaga105FragmentInitialization initialized = NativeBattleThundaga105Fragments.Initialize6D5990(Task(), pair, pair.MutableMesh, new(100, 200, 300), 4096, NativeBattleThundaga105Bounds.Begin6D5FA0(), random);
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((ushort)(8 + (draws[0] & 15)), initialized.Task.ReadWord(0xC));
        Assert.Equal((short)(30 + (draws[1] & 31)), S(initialized.Task, 0x22));
        Assert.Equal((short)(-1000 - (draws[2] & 1023)), S(initialized.Task, 0x1A));
        Assert.Equal((short)((draws[3] & 127) - 64), S(initialized.Task, 0x30));
        Assert.Equal((short)101, S(initialized.Task, 0x10));
        Assert.Equal(new NativeBattleVector(-5, -5, -5), initialized.Bounds.Minimum);
        Assert.Equal(new NativeBattleVector(-32767, -32767, -32767), initialized.Bounds.Maximum);
        Assert.Equal((short)-6, BinaryPrimitives.ReadInt16LittleEndian(initialized.Mesh.Bytes.AsSpan().Slice(8)));
        Assert.Equal((byte)0xA5, initialized.Mesh.Bytes[14]);
        Assert.Equal((byte)0xA5, initialized.Mesh.Bytes[15]);
        Assert.Equal(pair.MutableMesh.Bytes.AsSpan().Slice(32).ToArray(), initialized.Mesh.Bytes.AsSpan().Slice(32).ToArray());
        Assert.Equal((short)0, S(initialized.Task, 0x28));
        Assert.Equal((short)0, S(initialized.Task, 0xE));
        string json = JsonSerializer.Serialize(initialized);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleThundaga105FragmentInitialization>(json)));
    }

    [Fact]
    public void ExpiringFragmentDoesNotIntegrateAndBothFaceListsUseSharedDebrisRows()
    {
        NativeBattleThundagaFragmentPair pair = Pair();
        NativeBattleThundaga105FragmentInitialization initialized = NativeBattleThundaga105Fragments.Initialize6D5990(Task(), pair, pair.MutableMesh, new(0, 0, 0), 4096, NativeBattleThundaga105Bounds.Begin6D5FA0(), new(9));
        NativeBattleModulePoolTask last = initialized.Task.WriteWord(0xC, 1);
        NativeBattleThundaga105FragmentStep finished = NativeBattleThundaga105Fragments.Step6D5AD0(last);
        Assert.True(finished.Complete);
        Assert.Equal((ushort)0, finished.Task.ReadWord(0xC));
        Assert.Equal(last.Payload.AsSpan().Slice(4).ToArray(), finished.Task.Payload.AsSpan().Slice(4).ToArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = new NativeBattleEffectArena([]) };
        NativeBattleThundaga105FragmentEmission emitted = NativeBattleThundaga105Fragments.EmitDebris6D5C00(memory, finished.Task, initialized.Mesh, initialized.Random);
        Assert.Equal(2, emitted.Allocated);
        NativeCrtRandomState expected = initialized.Random;
        for (int draw = 0; draw < 8; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, emitted.Random);
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(emitted.Memory.Arena!.Read(0x2A24 + 6, 2)));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(emitted.Memory.Arena.Read(0x2A24 + 0x50 + 6, 2)));
        Assert.Equal((short)0x13B, BinaryPrimitives.ReadInt16LittleEndian(emitted.Memory.Arena.Read(0x2A24 + 0xE, 2)));
    }

    [Fact]
    public void ActiveFragmentIntegratesSignedVelocityBeforeAccelerationAndWrapsAllWords()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0xC, 2).WriteWord(0x10, 32767).WriteWord(0x12, 0).WriteWord(0x18, 16).WriteWord(0x1A, unchecked((ushort)-17)).WriteWord(0x20, 2).WriteWord(0x22, 30).WriteWord(0x28, 32767).WriteWord(0x30, 1);
        NativeBattleThundaga105FragmentStep advanced = NativeBattleThundaga105Fragments.Step6D5AD0(task);
        Assert.False(advanced.Complete);
        Assert.Equal((short)-32768, S(advanced.Task, 0x10));
        Assert.Equal((short)-2, S(advanced.Task, 0x12));
        Assert.Equal((short)18, S(advanced.Task, 0x18));
        Assert.Equal((short)13, S(advanced.Task, 0x1A));
        Assert.Equal((short)-32768, S(advanced.Task, 0x28));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x6D5AD0, Enumerable.Repeat((byte)0xA5, 0x30).ToImmutableArray());
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static NativeBattleThundagaFragmentPair Pair()
    {
        byte[] mesh = new byte[112];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan().Slice(4), 3);
        byte[] reference = new byte[32];
        short[] values = [-5, -2, 7];
        for (int vertex = 0; vertex < 3; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(8 + vertex * 8 + axis * 2), values[vertex]);
            }
            mesh[14 + vertex * 8] = 0xA5;
            mesh[15 + vertex * 8] = 0xA5;
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan().Slice(40), 1);
        Face(44, 0x24808080, false);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan().Slice(76), 1);
        Face(80, 0x34808080, true);
        return new(new(0, 0x1000, mesh.ToImmutableArray()), new(0x2000, reference.ToImmutableArray()));

        void Face(int offset, uint color, bool gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan().Slice(offset), color);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 8), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 10), 0x9ABC);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 12), 0x77771234);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 16), 0x013B5678);
            if (gouraud)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 20), 0x00112233);
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 24), 0x00445566);
            }
        }
    }
}
