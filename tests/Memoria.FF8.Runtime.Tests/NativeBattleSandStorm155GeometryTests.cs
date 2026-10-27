using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSandStorm155GeometryTests
{
    [Fact]
    public void Reference_sort_preserves_native_exchange_order_for_equal_keys()
    {
        NativeBattleSandStorm155Frame frame = Frame();
        NativeBattleSandStorm155Resources resources = Resources();
        frame = NativeBattleSandStorm155Geometry.Sort607090(frame, resources);
        Assert.Equal(0xDB8168u, Dword(frame, 0x23F33F0));
        Assert.Equal(0xDB8160u, Dword(frame, 0x23F33FC));
        Assert.Equal(0xDB8158u, Dword(frame, 0x23F3408));
        Assert.Equal(0x23F2A80u, Dword(frame, 0x23F33F4));
        Assert.Equal(2u, Dword(frame, 0x23F33F8));
        Assert.Equal(0u, Dword(frame, 0x23F4230));
        Assert.Equal(304, Enumerable.Range(0, 304).Select(index => Dword(frame, 0x23F33F0u + (uint)(index * 12))).Distinct().Count());
    }

    [Fact]
    public void Displacement_updates_all_19_sorted_groups_before_pause_without_touching_selected_W()
    {
        NativeBattleSandStorm155Frame frame = Frame(0x607130);
        uint address = 0x23F1AF8;
        NativeBattleModulePoolTask task = NativeBattleSandStorm155Controller.Read(frame, address).WriteWord(0x18, 0).WriteWord(0x1A, 17).WriteWord(0x1C, 100).WriteWord(12, 59);
        frame = NativeBattleSandStorm155Controller.Write(frame, address, task);
        frame = frame with { State = frame.State.WriteView(0x23F2A70, NativeBattleEffectArena.Clear().MarkUnknown(0, 304 * 8), 304 * 8) };
        frame = NativeBattleSandStorm155Geometry.Sort607090(frame, Resources());
        (frame, int result) = NativeBattleSandStorm155Geometry.Displace607130(frame, address, Resources(), 0x200);
        Assert.Equal(0, result);
        Assert.Equal((short)110, Word(frame, 0x23F2A80));
        Assert.Equal((short)2, Word(frame, 0x23F2A82));
        Assert.Equal((short)30, Word(frame, 0x23F2A84));
        Assert.Throws<NotSupportedException>(() => frame.State.ReadView(0x23F2A86, 2).Read(0, 2));
        Assert.Equal(task, NativeBattleSandStorm155Controller.Read(frame, address));
        NativeBattleSandStorm155Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSandStorm155Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var expected = NativeBattleSandStorm155Geometry.Displace607130(frame, address, Resources(), 0);
        var actual = NativeBattleSandStorm155Geometry.Displace607130(restored, address, Resources(), 0);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)17, NativeBattleSandStorm155Controller.Read(actual.Frame, address).ReadWord(0x18));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
        uint lastRow = Dword(frame, 0x23F33F0 + 303 * 12 + 4);
        Assert.Equal((short)10, Word(frame, lastRow) - (unchecked(NativeBattleCameraMath.Cosine(18 * 300) * 370) >> 12));
    }

    [Fact]
    public void Six_point_curve_preserves_retained_control_and_sample_W_and_physical_workspace()
    {
        NativeBattleSandStorm155Frame frame = Frame();
        frame = Put(frame, 0x23F2948, -1);
        frame = Put(frame, 0x23F294C, -1);
        frame = Put(frame, 0x23F07F0, 0);
        frame = Put(frame, 0x23F07F4, 0);
        frame = Put(frame, 0x23F07F8, -4);
        frame = Put(frame, 0x23F07FC, -4);
        frame = frame with { State = frame.State.Write(0x23F2920, new byte[] { 7, 0, 8, 0, 9, 0 }).WriteView(0x23F290E, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).WriteView(0x23F2966, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2).Write(0x23F296E, BitConverter.GetBytes((ushort)0xCAFE)) };
        NativeBattleSandStorm155Frame actual = NativeBattleSandStorm155Curve.PrepareSamples60752A(frame);
        Assert.Equal((short)-3, Word(actual, 0x23F2910));
        Assert.Equal((short)0, Word(actual, 0x23F2918));
        Assert.Equal((short)-4, Word(actual, 0x23F2928));
        Assert.Equal(new byte[] { 7, 0, 8, 0, 9, 0 }, actual.State.ReadView(0x23F2920, 6).Read(0, 6));
        Assert.Equal((short)-1, Word(actual, 0x23F2960));
        Assert.Equal((short)-1, Word(actual, 0x23F2A68));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadView(0x23F2936, 2).Read(0, 2));
        Assert.Throws<NotSupportedException>(() => actual.State.ReadView(0x23F2966, 2).Read(0, 2));
        Assert.Equal(unchecked((short)0xCAFE), Word(actual, 0x23F296E));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Throws<NotSupportedException>(() => actual.Staging!.Read(NativeBattleTemporaryMemory.Address + 12, 4));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData(0, 0x20F3u, 4096, 4096)]
    [InlineData(5, 0x20F3u, 686, 2391)]
    [InlineData(6, 0x2033u, 0, 2048)]
    [InlineData(52, 0x20F3u, 0, 2048)]
    [InlineData(59, 0x20F3u, 3584, 3840)]
    public void Mesh_submits_two_retained_contexts_before_pause_with_native_cues(short age, uint flags, int firstCue, int secondCue)
    {
        NativeBattleSandStorm155Frame frame = Frame(0x607210);
        uint address = 0x23F1AF8;
        NativeBattleModulePoolTask task = NativeBattleSandStorm155Controller.Read(frame, address).WriteWord(12, unchecked((ushort)age)).WriteWord(0x12, 4096).WriteWord(0x16, 32);
        frame = NativeBattleSandStorm155Controller.Write(frame, address, task);
        List<NativeBattleEffectArena> contexts = [];
        List<ImmutableArray<int?>> translations = [];
        NativeBattleSandStorm155Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleSandStorm155DrawMesh88 draw = Assert.IsType<NativeBattleSandStorm155DrawMesh88>(operation);
            contexts.Add(NativeBattleTemporaryMemory.ReadView(current.Staging!, draw.TemporaryAddress, 88));
            translations.Add(current.Memory.GeometryRegisters!.Translation);
            return current;
        });
        (NativeBattleSandStorm155Frame actual, int result) = NativeBattleSandStorm155Mesh.Advance607210(frame, address, Resources(), inputs, 0x200);
        Assert.Equal(0, result);
        Assert.Equal(2, contexts.Count);
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(contexts[0].Read(0x1C, 4)));
        Assert.Equal(0x20F3u, BinaryPrimitives.ReadUInt32LittleEndian(contexts[1].Read(0x1C, 4)));
        Assert.Equal(firstCue, BinaryPrimitives.ReadInt32LittleEndian(contexts[0].Read(12, 4)));
        Assert.Equal(secondCue, BinaryPrimitives.ReadInt32LittleEndian(contexts[1].Read(12, 4)));
        Assert.Equal(0x23F2A70u, BinaryPrimitives.ReadUInt32LittleEndian(contexts[0].Read(4, 4)));
        Assert.Equal(translations[0], translations[1]);
        Assert.Equal(task, NativeBattleSandStorm155Controller.Read(actual, address));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    private static NativeBattleSandStorm155Frame Frame(uint callback = 0x607210)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23F1AE8, 0x23F1AF8, 36, 100);
        (pool, int slot) = pool.Append(callback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[24])) });
        NativeBattleOriginalEffectState effect = new(155, 154, new(155, 0x606CF0, 0x606CD0), null, 0x23F0818) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        return new(memory, effect, new(123), NativeBattleSandStorm155State.FromColdProcess() with { RootBankStorage = null, ChildBankStorage = null }) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleSandStorm155Inputs Inputs(Func<NativeBattleSandStorm155Frame, NativeBattleSandStorm155Operation, NativeBattleSandStorm155Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => NativeBattleCuragaMatrices.Identity8DD770(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), execute);
    private static NativeBattleSandStorm155Resources Resources()
    {
        byte[] mesh = new byte[8 + 304 * 8];
        for (int index = 0; index < 304; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 8), 10);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + index * 8), index == 2 ? (short)2 : index < 2 ? (short)1 : (short)0);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12 + index * 8), 30);
        }
        return new(Mesh: new(0, 0xDB8150, mesh.ToImmutableArray()), SpriteA: new(0, 0, 0, 0, []), SpriteB: new(0, 0, 0, 0, []), Camera: new("unused", []), Sound: 0);
    }
    private static short Word(NativeBattleSandStorm155Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.ReadView(address, 2).Read(0, 2));
    private static uint Dword(NativeBattleSandStorm155Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.State.ReadView(address, 4).Read(0, 4));
    private static NativeBattleSandStorm155Frame Put(NativeBattleSandStorm155Frame frame, uint address, short value) => frame with { State = frame.State.Write(address, BitConverter.GetBytes(value)) };
}
