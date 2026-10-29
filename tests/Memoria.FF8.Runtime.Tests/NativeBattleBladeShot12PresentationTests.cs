using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Controller;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBladeShot12PresentationTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Custom_mesh_capture_keeps_native_registers_context_masks_and_actual_source_corners(int group)
    {
        NativeBattleEffectMemory memory = NativeBattleBladeShot12MeshContext88Tests.Memory(2000);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleEffectArena context = Context();
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattleBladeShot12MeshContext88Step plain = NativeBattleBladeShot12MeshContext88.DrawLoaded(memory, mesh, projection, context, colors);
        NativeBattlePilotVisualIdentity identity = new(12, ChildADescriptor, 0, 123, 7);
        NativeBattleBladeShot12MeshContext88Step captured = NativeBattleBladeShot12MeshContext88.DrawLoaded(memory, mesh, projection, context, colors, pilotIdentity: identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context88), NativeBattleTestSnapshots.Json(captured.Context88));
        Assert.Equal(plain.Batch.AllocationBytes, captured.Batch.AllocationBytes);
        Assert.Equal(plain.Batch.NativeUnlinkedAllocation, captured.Batch.NativeUnlinkedAllocation);
        NativeBattleGfGeometryAttempt a = Assert.Single(plain.Batch.Attempts);
        NativeBattleGfGeometryAttempt b = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(a.Data.ToArray(), b.Data.ToArray());
        Assert.Equal(a.WriteMask.ToArray(), b.WriteMask.ToArray());
        Assert.Equal(a.Primitive!.Bucket, b.Primitive!.Bucket);
        NativeBattlePilotVisualGeometry geometry = b.Primitive.PilotGeometry!;
        Assert.Equal(group == 4 ? 3 : 4, geometry.CornerCount);
        Assert.Equal(new Vector3(-400, -400, 2000), geometry.A);
        Assert.Equal(new Vector3(-400, 400, 2000), geometry.C);
        if (group == 5)
        {
            Assert.Equal(new Vector3(400, 400, 2000), geometry.D);
        }
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
    }

    [Fact]
    public void Rejected_custom_quad_does_not_read_unreached_fourth_source_for_capture()
    {
        NativeBattleStaticMeshResource mesh = Mesh(5);
        byte[] bytes = mesh.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44 + 5 * 4 + 10), 0xFFFE);
        NativeBattleBladeShot12MeshContext88Step captured = NativeBattleBladeShot12MeshContext88.DrawLoaded(NativeBattleBladeShot12MeshContext88Tests.Memory(1), mesh with { Bytes = bytes.ToImmutableArray() }, new(NativeBattleCuragaMatrices.Identity8DD770(), 2, 160, 108), Context().MarkUnknown(8, 8), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: new(12, ChildADescriptor, 0, 123, 7));
        Assert.Null(Assert.Single(captured.Batch.Attempts).Primitive);
        Assert.Equal(0, captured.Batch.AllocationBytes);
    }

    [Theory]
    [InlineData(0x6E9B50u)]
    [InlineData(0x6E9C10u)]
    public void Sprite_operations_keep_actual_child_owner_and_do_not_serialize_presentation_identity(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildBDescriptor, ChildBBank, 52, 1);
        (pool, _) = pool.Append(callback);
        NativeBattleOriginalEffectState effect = new(12, 11, new(12, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleBladeShot12MeshContext88Tests.Memory(2000) with { ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
        NativeBattleBladeShot12Frame frame = new(memory, effect, new(123), NativeBattleBladeShot12State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleModulePoolTask task = Read(frame, ChildBBank).WriteWord(12, 2).WriteDword(16, 0x13627E4).WriteWord(0x14, 10).WriteWord(0x16, 20).WriteWord(0x18, 30).WriteWord(0x1A, 3).WriteWord(0x22, 2048).WriteWord(0x2C, 1).WriteWord(0x2E, 2).WriteWord(0x30, 3);
        frame = Write(frame, ChildBBank, task);
        NativeBattleBladeShot12DrawSprite180? observed = null;
        NativeBattleBladeShot12Inputs inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0x201, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            observed = Assert.IsType<NativeBattleBladeShot12DrawSprite180>(operation);
            return current;
        });
        NativeBattleBladeShot12Resources resources = new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, 0);
        var advanced = AdvanceSprite(frame, ChildBBank, resources, inputs);
        Assert.NotNull(observed);
        Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(12, task).Generation, observed!.PilotIdentity!.Value.Generation);
        Assert.Equal(ChildBDescriptor, observed.PilotIdentity.Value.Bank);
        Assert.Equal(0x13627E4, observed.PilotIdentity.Value.ResourceGeneration);
        Assert.DoesNotContain("PilotIdentity", NativeBattleTestSnapshots.Json(observed));
        Assert.Equal(2, advanced.Result);
        Assert.Equal((ushort)3, Read(advanced.Frame, ChildBBank).ReadWord(12));
        Assert.Equal(frame.Random, advanced.Frame.Random);
    }

    [Fact]
    public void Concurrent_transient_copies_use_distinct_authored_identity_without_changing_native_state()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 100, 1);
        (pool, _) = pool.Append(0x6E8C90);
        NativeBattleOriginalEffectState effect = new(12, 11, new(12, Entry, Initializer), null, RootDescriptor) { Pools = [pool] };
        NativeBattleBladeShot12Frame frame = new(NativeBattleBladeShot12MeshContext88Tests.Memory(2000), effect, new(123), NativeBattleBladeShot12State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        List<NativeBattleBladeShot12DrawStatic88> draws = [];
        NativeBattleBladeShot12Inputs inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => new(0, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096) }, (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            draws.Add(Assert.IsType<NativeBattleBladeShot12DrawStatic88>(operation));
            return current;
        });
        NativeBattleBladeShot12Resources resources = new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, 0);
        NativeBattleBladeShot12Frame first = DrawTransient6EA3E0(frame, RootBank, 2, resources, inputs, 0);
        NativeBattleBladeShot12Frame second = DrawTransient6EA3E0(frame, RootBank, 2, resources, inputs, 1);
        Assert.Equal(2, draws.Count);
        Assert.Equal(draws[0].PilotIdentity!.Value.Generation, draws[1].PilotIdentity!.Value.Generation);
        Assert.NotEqual(draws[0].PilotIdentity!.Value.Primitive, draws[1].PilotIdentity!.Value.Primitive);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
    }

    private static NativeBattleEffectArena Context() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x1364F14u)).Write(8, new byte[] { 10, 20, 30 }).Write(12, BitConverter.GetBytes(2048)).Write(28, BitConverter.GetBytes(0xC0u));

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        byte[] bytes = new byte[112];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 111, 400, -400, 0, 222, -400, 400, 0, 333, 400, 400, 0, 444];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 4 ? 0x30112233u : 0x38112233u);
        for (int corner = 0; corner < (group == 4 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x30224466);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x30336699);
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x384488CC);
        }
        return new(0, 0x1364F14, bytes.ToImmutableArray());
    }
}
