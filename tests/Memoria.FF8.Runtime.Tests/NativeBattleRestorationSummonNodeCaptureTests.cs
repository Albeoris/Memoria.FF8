using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationSummonNodeCaptureTests
{
    [Theory]
    [InlineData((ushort)93, false)]
    [InlineData((ushort)94, false)]
    [InlineData((ushort)96, true)]
    public void Opted_builtin_mesh_policy_preserves_detached_native_state_resource_updates_and_actual_node_birth(ushort effect, bool fullLife)
    {
        var native = Fixture();
        var visual = Fixture();
        int nativeUpdates = 0;
        int visualUpdates = 0;
        NativeBattlePilotVisualIdentity? expectedRowAtResourceUpdate = null;
        uint? resourceAtUpdate = null;
        NativeBattleRecover93NodeStep plain = Run(native.Frame, native.Task, null, false, () => nativeUpdates++);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(effect, new NativeBattleEffectHierarchy(visual.Frame.Effect.Pools).Read(visual.Task));
        NativeBattleRecover93NodeStep captured = Run(visual.Frame, visual.Task, identity, true, () => visualUpdates++);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(plain));
        Assert.Equal(nativeUpdates, visualUpdates);
        Assert.True(nativeUpdates > 0);
        NativeBattleGeometryPrimitive[] polygons = captured.CommonMeshes.SelectMany(batch => batch.Attempts).Where(attempt => attempt.Primitive is not null && ((attempt.Primitive.Words[1] >> 24) & 0xE0) == 0x20).Select(attempt => attempt.Primitive!).ToArray();
        Assert.NotEmpty(polygons);
        using NativeBattleEffectNodeMemory rows = new(captured.Frame.Memory, captured.Frame.Effect, visual.Task, NativeBattleCuragaSpawnTests.Emitter, 90 * 0x6C, NativeBattleCuragaSpawnTests.Node, 140 * 0x2A0);
        List<int> activeGeometry = [];
        NativeBattleEffectNodeAllocation.WalkList(() => rows, (node, kind) =>
        {
            if (kind == 1)
            {
                activeGeometry.Add(node);
            }
        });
        Assert.Equal(NativeBattleCuragaSpawnTests.Node, Assert.Single(activeGeometry));
        Assert.Equal((ushort)1, rows.ContextWord(0x16));
        Assert.Equal((byte)1, rows.Byte(NativeBattleCuragaSpawnTests.Node + 0x1D8));
        NativeBattlePilotVisualIdentity row = Assert.IsType<NativeBattlePilotVisualIdentity>(expectedRowAtResourceUpdate);
        uint selectedResource = rows.Dword(NativeBattleCuragaSpawnTests.Node + 0x170);
        Assert.Equal(NativeBattleCuragaExecutableState.Address + 0x400u, selectedResource);
        Assert.Equal(selectedResource, resourceAtUpdate);
        long expectedResource = NativeBattleEffectNodeCaller.ResourceIdentity(row, NativeBattleCuragaExecutableState.Address, selectedResource).ResourceGeneration;
        Assert.All(polygons, primitive =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(expectedResource, geometry.Identity.ResourceGeneration);
            Assert.Equal((byte)4, geometry.CornerCount);
        });
        Assert.NotEmpty(captured.Frame.Effect.PresentationPackets);
        Assert.Empty(plain.Frame.Effect.PresentationPackets);

        NativeBattleRecover93NodeStep Run(NativeBattleRecover93NodeFrame frame, uint task, NativeBattlePilotVisualIdentity? owner, bool enabled, Action update)
        {
            NativeBattleModulePoolTask context = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(task);
            NativeBattleEffectNodeCallerPolicy policy = new(NativeBattleCuragaExecutableState.Address, frame.State.Data!.Length, context.Callback, 90, 140, fullLife);
            return NativeBattleEffectNodeCaller.Advance74F6E0(policy, frame, task, _ => throw new InvalidOperationException("No actor read belongs to the declared node update."), new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108), 0, NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270, resourceUpdate: (_, resource, kind, sourceIndex) =>
            {
                update();
                if (owner is { } selected)
                {
                    Assert.Equal(4, kind);
                    Assert.Equal((byte)0, sourceIndex);
                    NativeBattlePilotVisualIdentity birth = NativeBattlePilotVisualLifetimes.Identity(selected.Effect, frame.Memory.Arena!, NativeBattleEffectNodeMemory.ArenaAddress + (uint)NativeBattleCuragaSpawnTests.Node, 0);
                    expectedRowAtResourceUpdate = selected with { Primitive = unchecked(NativeBattleCuragaSpawnTests.Node * 65537), ResourceGeneration = birth.Generation };
                    resourceAtUpdate = resource;
                }
            }, pilotIdentity: owner, captureBuiltInMesh: enabled);
        }
    }

    [Fact]
    public void Resource_replacement_at_the_same_row_birth_and_row_reuse_hold_the_previous_presentation_endpoint()
    {
        NativeBattlePilotVisualIdentity row = new(96, 1, 2, 3, 4) { ResourceGeneration = 7 };
        NativeBattlePilotVisualIdentity owner = NativeBattleEffectNodeCaller.ResourceIdentity(row, 0x1000, 0x1400);
        NativeBattlePilotVisualGeometry earlier = new(owner, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3);
        NativeBattlePilotVisualGeometry moved = earlier with { A = new(100, 200, 300) };
        Assert.Equal(new Vector3(50, 100, 150), NativeBattlePilotVisualGeometry.Sample(earlier, moved, 0.5f).A);
        NativeBattlePilotVisualIdentity[] replacements =
        [
            NativeBattleEffectNodeCaller.ResourceIdentity(row, 0x1000, 0x1800),
            NativeBattleEffectNodeCaller.ResourceIdentity(row, 0x2000, 0x1400),
            NativeBattleEffectNodeCaller.ResourceIdentity(row with { ResourceGeneration = 8 }, 0x1000, 0x1400)
        ];
        foreach (NativeBattlePilotVisualIdentity replacement in replacements)
        {
            Assert.Equal(row.Generation, replacement.Generation);
            Assert.Equal(row.Primitive, replacement.Primitive);
            NativeBattlePilotVisualGeometry changed = moved with { Identity = replacement };
            Assert.Equal(earlier, NativeBattlePilotVisualGeometry.Sample(earlier, changed, 0.5f));
            Assert.Equal(changed, NativeBattlePilotVisualGeometry.Sample(earlier, changed, 1));
        }
    }

    [Fact]
    public void Custom_native_preparer_is_not_replaced_by_opted_builtin_capture_without_an_explicit_pilot_override()
    {
        var fixture = Fixture();
        int prepared = 0;
        NativeBattleModulePoolTask context = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Read(fixture.Task);
        NativeBattleEffectNodeCallerPolicy policy = new(NativeBattleCuragaExecutableState.Address, fixture.Frame.State.Data!.Length, context.Callback, 90, 140, true);
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(96, context);
        _ = NativeBattleEffectNodeCaller.Advance74F6E0(policy, fixture.Frame, fixture.Task, _ => throw new InvalidOperationException(), new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108), 0, NativeBattleLogicalOrdering.Clear(), NativeBattleGteColorInterpolation.CodeConstants45F270, prepareMeshOverride: (_, _, _) =>
        {
            prepared++;
            return (_, _) => new([], 0);
        }, pilotIdentity: identity, captureBuiltInMesh: true);
        Assert.Equal(1, prepared);
    }

    private static (NativeBattleRecover93NodeFrame Frame, uint Task) Fixture()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame prepared = NativeBattleCuragaNodeCycleTests.EmitOnce(fixture.Frame, 1);
        prepared = NativeBattleCuragaSpawnTests.Task(prepared, fixture.Task, task => task.WriteWord(0x34 + 0x20, 1).WriteWord(0x34 + 0x18, 1));
        prepared = NativeBattleCuragaSpawnTests.Bank(prepared, bytes =>
        {
            byte[] mesh = new byte[96];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 40);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
            short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
            for (int index = 0; index < coordinates.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), coordinates[index]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(52), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 0x2C808080);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + corner * 2), (ushort)(corner * 2));
            }
            mesh.CopyTo(bytes, 0x400);
        });
        prepared = prepared with { Memory = prepared.Memory with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() } };
        prepared = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(NativeBattleTestSnapshots.Json(prepared))!;
        NativeBattleNodeSource source = prepared.Memory.CuragaExecutable!.Data!;
        NativeBattleRecover93NodeState state = new(1, source, NativeBattleCuragaSpawnTests.Emitter, NativeBattleCuragaSpawnTests.Node, 0, 0, 0, 0, prepared.State.CopiedCamera, 0, null, null);
        return (new(prepared.Memory, prepared.Effect, state, new(1)), fixture.Task);
    }
}
