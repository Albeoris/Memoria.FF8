using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216PreparationTests
{
    [Fact]
    public void PrepareAndRestoreKeepOriginalPackedBackupsAndNativeServiceOrder()
    {
        NativeBattleDuel216Frame frame = Frame();
        NativeBattleGraphicsModel[] models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 6)
        {
            Transform = new(new(0, 0, 0), new(10, 20, 30), 40, 11, index == 3 ? (short)2048 : (short)4096),
            Rotation12 = 11,
            Rotation14 = 22,
            Rotation16 = 33,
            HasLinkedModelRing140 = false
        }).ToArray();
        models[3] = models[3].WriteLinkedModelIndex140(4);
        models[4] = models[4].WriteLinkedModelIndex140(3) with { Flags0 = 0 };
        List<string> operations = [];
        NativeBattleDuel216PreparationServices services = new(index => models[index], (index, model) => models[index] = model, index =>
        {
            Assert.Equal(3, index);
            return new(300, 500, 700, 11);
        }, (current, anchor, bounds) =>
        {
            operations.Add("camera");
            Assert.Equal(new NativeBattleModuleAnchor(300, 0, 700, 11), anchor);
            Assert.Equal(new NativeBattleVector(-512, -256, -1024), bounds.Minimum);
            Assert.Equal(new NativeBattleVector(512, 256, 1024), bounds.Maximum);
            return current;
        }, (current, actor, row) =>
        {
            operations.Add("bind");
            Assert.Equal(0, actor);
            Assert.Equal(0x20DFAB8u + 16, row);
            models[actor] = models[actor] with { Rotation12 = 100, Rotation14 = 200, Rotation16 = 300, RotationPadding18 = 400 };
            return current;
        }, (current, actor, sequence) =>
        {
            operations.Add("sequence");
            Assert.Equal(0, actor);
            Assert.Equal((byte)1, sequence);
            return current;
        }, (current, target) =>
        {
            operations.Add("bounds");
            Assert.Equal(3, target);
            Assert.Equal((ushort)2, models[0].Flags0);
            Assert.Equal(11408, current.Memory.EffectIo!.BaseOffset);
            return new(current, 30, -20);
        }, current =>
        {
            operations.Add("focal");
            return current;
        });
        frame = NativeBattleDuel216Preparation.Prepare695AB0(frame, 0, services);
        NativeBattleModulePoolTask task = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(new[] { "camera", "bind", "sequence", "bounds" }, operations);
        Assert.Equal(10u | 20u << 16, task.ReadDword(64));
        Assert.Equal(30u | 40u << 16, task.ReadDword(68));
        Assert.Equal((ushort)3772, task.ReadWord(52));
        Assert.Equal((ushort)500, task.ReadWord(58));
        Assert.Equal((ushort)11, task.ReadWord(62));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(78));
        Assert.Equal((short)-10, models[3].Transform!.Position28.Y);
        Assert.Equal((ushort)14, models[3].Flags0);
        Assert.Equal((ushort)0, models[4].Flags0);
        Assert.Equal(1199, BinaryPrimitives.ReadInt32LittleEndian(frame.Staging.Read(0x2508908, 4)));
        Assert.Equal((byte)18, task.ReadByte(3672));
        frame = NativeBattleDuel216Preparation.Restore695AB0(frame, services);
        Assert.Equal("focal", operations.Last());
        Assert.Equal(new NativeBattleVector(10, 20, 30), models[3].Transform!.Position28);
        Assert.Equal((ushort)6, models[3].Flags0);
        Assert.Equal((short?)11, models[0].Rotation12);
        Assert.Equal((short?)22, models[0].Rotation14);
        Assert.Equal((short?)33, models[0].Rotation16);
        Assert.Null(models[0].RotationPadding18);
    }

    [Fact]
    public void CameraExtentsIgnoreVertexPaddingAndScaleWithSignedLowWordStores()
    {
        byte[] geometry = new byte[36];
        BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 3);
        short[] data = [-32768, 4, -8, 12345, 32767, -10, 80, -12345, 7, 20, 3, 1];
        for (int index = 0; index < data.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(12 + index * 2), data[index]);
        }
        NativeBattleDuel216Extents bounds = NativeBattleDuel216Camera.Extents69A700(geometry.ToImmutableArray());
        Assert.Equal(new NativeBattleVector(-32768, -10, -8), bounds.Minimum);
        Assert.Equal(new NativeBattleVector(32767, 20, 80), bounds.Maximum);
        NativeBattleDuel216Extents scaled = NativeBattleDuel216Camera.Scale695AB0(bounds, 8192);
        Assert.Equal((short)0, scaled.Minimum.X);
        Assert.Equal((short)-2, scaled.Maximum.X);
    }

    [Fact]
    public void CaptureCameraUsesOriginalQuotientModuloAndTruncatingDivision()
    {
        NativeBattleDuel216CameraTransform transform = NativeBattleDuel216Camera.Transform69A790(new(-131, 0, -129, 0), new(new(0, 0, 0), new(16512, 0, 16384)), new(null, 256, 160, 108), 288);
        Assert.Equal(-62, transform.Matrix.X);
        Assert.Equal(63, transform.Matrix.Y);
        Assert.Equal(288, transform.Matrix.Z);
        Assert.Equal((short?)64, transform.Projection.OffsetX);
        Assert.Equal((short?)64, transform.Projection.OffsetY);
        Assert.Equal((short)160, transform.PreviousX);
        Assert.Equal((short)108, transform.PreviousY);
    }

    private static NativeBattleDuel216Frame Frame()
    {
        byte[] bytes = new byte[NativeBattleDuel216Resources.EndAddress - NativeBattleDuel216Resources.Address];
        void Int(uint address, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(checked((int)(address - NativeBattleDuel216Resources.Address))), value);
        Int(0x114A6E8, 0x100);
        uint geometry = 0x114A7DC;
        Int(geometry + 4, 2);
        short[] vertices = [-1024, -512, -2048, 0, 1024, 512, 2048, 0];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(checked((int)(geometry + 12 + (uint)index * 2 - NativeBattleDuel216Resources.Address))), vertices[index]);
        }
        NativeBattleDuel216Resources resources = new(bytes.ToImmutableArray(), [], new("operands", []), []);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel216Controller.Main, 0x20DFAB8, 6128, 1);
        (pool, int slot) = pool.Append(0x6960E0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[6116])) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), parameters, NativeBattleDuel216Controller.Root) { Pools = [pool] };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2508880, BitConverter.GetBytes(0x1D972C0u)).Write(0x2508884, BitConverter.GetBytes(0x1D972C0u + 3 * 156));
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Duel216 = new(true, parameters, resources) }, effect, staging, NativeCrtRandomState.FromColdProcess());
    }
}

