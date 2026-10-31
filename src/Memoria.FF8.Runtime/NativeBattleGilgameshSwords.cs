using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordDraw(NativeBattleCactuarRawModelState State, NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Packets);
public sealed record NativeBattleGilgameshSwordStep(NativeBattleGilgameshModelMemory ModelMemory, NativeBattleEffectArena Arena, NativeBattleModulePoolTask UpdatedTask, ImmutableArray<NativeBattleGfGeometryBatch> Packets, int Result);

/// <summary>594FD0/5951E0/595250's four current sword models and world-plane draw, with balanced local descriptor lifetime.</summary>
public static class NativeBattleGilgameshSwords
{
    public static NativeBattleGilgameshSwordStep Step594FD0(NativeBattleGilgameshModelMemory modelMemory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, ImmutableArray<NativeBattleEffectOwnedModelResource> staticModels, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, uint graphicsFlags)
    {
        return Step594FD0WithPresentation(modelMemory, arena, task, staticModels, worldView, projection, registers, graphicsFlags, null);
    }

    public static NativeBattleGilgameshSwordStep Step594FD0WithPresentation(NativeBattleGilgameshModelMemory modelMemory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, ImmutableArray<NativeBattleEffectOwnedModelResource> staticModels, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, uint graphicsFlags, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (task.PayloadLength != 24 || staticModels.IsDefault || staticModels.Length != 4)
        {
            throw new InvalidDataException("Native594FD0 requires its raw0x24-byte task and all four selected static model resources.");
        }
        int counter = unchecked((short)task.ReadWord(12));
        if (counter == 0)
        {
            for (int index = 0; index < 4; index++)
            {
                NativeBattleGilgameshModelOwner owner = modelMemory.Read(Address(index));
                NativeBattleCactuarRawModelState state = owner.State;
                byte[] bytes = state.Model.ToArray();
                bytes.AsSpan(0x40, 16).Clear();
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x40), 4096);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x48), 4096);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x50), 4096);
                bytes.AsSpan(0x54, 12).Clear();
                NativeBattleRawModelBinding binding = new(owner.ControlAddress, 4);
                state = NativeBattleCactuarRawModel.Initialize5A8F40(state with { Model = bytes.ToImmutableArray() }, staticModels[index], staticModels[index].Address, 16, binding);
                state = NativeBattleCactuarRawModel.Select5A8FB0(state, staticModels[index], unchecked((short)task.ReadWord(14)), binding);
                modelMemory = modelMemory.Write(owner.Address, state);
            }
        }
        NativeBattleGilgameshModelOwner first = modelMemory.Read(Address(0));
        modelMemory = modelMemory.Write(first.Address, first.State with { Model = first.State.Model.SetItem(0, (byte)(first.State.Model[0] | 4)) });
        ImmutableArray<NativeBattleGfGeometryBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGfGeometryBatch>();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleGilgameshModelOwner owner = modelMemory.Read(Address(index));
            NativeBattleGilgameshSwordDraw drawn = Draw5951E0WithPresentation(owner.State, staticModels[index], owner.ControlAddress, arena, worldView, projection, registers, pilotIdentity);
            modelMemory = modelMemory.Write(owner.Address, drawn.State);
            arena = drawn.Arena;
            packets.Add(drawn.Packets);
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(modelMemory, arena, task, packets.ToImmutable(), 0);
        }
        NativeBattleGilgameshModelOwner sword0 = modelMemory.Read(Address(0));
        if (counter == 10)
        {
            modelMemory = modelMemory.Write(sword0.Address, sword0.State with { Model = sword0.State.Model.SetItem(0, (byte)(sword0.State.Model[0] | 8)) });
        }
        else if (counter == 45)
        {
            modelMemory = modelMemory.Write(sword0.Address, sword0.State with { Model = sword0.State.Model.SetItem(0, (byte)(sword0.State.Model[0] & 0xF7)) });
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(modelMemory, arena, task, packets.ToImmutable(), unchecked((short)task.ReadWord(12)) >= 105 ? 2 : 0);
    }

    public static NativeBattleGilgameshSwordDraw Draw5951E0(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, uint controlAddress, NativeBattleEffectArena arena, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers)
    {
        return Draw5951E0WithPresentation(state, resource, controlAddress, arena, worldView, projection, registers, null);
    }

    public static NativeBattleGilgameshSwordDraw Draw5951E0WithPresentation(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, uint controlAddress, NativeBattleEffectArena arena, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        RequireStaticSource(resource);
        NativeBattleCactuarRawModel.Validate(state, resource, new(controlAddress, 4));
        if (state.ProjectedScratch is not null)
        {
            throw new InvalidDataException("Native sword drawing requires its one actual shared arena scratch owner.");
        }
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Native5951E0 requires its selected current decoded pose owner.");
        ImmutableArray<byte> bytes = state.Model;
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(0x40 + index * 2))).ToImmutableArray(), Int(bytes, 0x54), Int(bytes, 0x58), Int(bytes, 0x5C), 4096);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, local);
        if ((unchecked((uint)Int(bytes, 0x7C)) & 1) == 0)
        {
            return new(state, arena, new([], 0));
        }
        NativeBattleRawMesh mesh = resource.Geometry[0];
        arena = SkinWorld595250(mesh, bones, arena, 0x31800);
        NativeBattleGfGeometryBatch packets = NativeBattleGilgameshSwordPackets.Emit595550WithPresentation(mesh, arena, 0x31800, worldView, projection, unchecked((uint)Int(bytes, 0x28)), registers, 2, NativeBattleGilgameshVisuals.Resource(pilotIdentity, resource, resource.Address, unchecked((int)controlAddress)));
        return new(state, arena, packets);
    }

    public static NativeBattleEffectArena SkinWorld595250(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleEffectArena arena, int worldOffset)
    {
        mesh.Validate();
        if (bones.IsDefault)
        {
            throw new InvalidDataException("Native595250 requires its current composed bone matrices.");
        }
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (vertex.Bone >= bones.Length)
            {
                throw new InvalidDataException("Native595250 vertex references a missing current bone matrix.");
            }
            NativeBattleVector point = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
            byte[] bytes = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
            arena = arena.Write(checked(worldOffset + index * 8), bytes);
        }
        return arena;
    }

    private static uint Address(int index) => checked(0x21FF4F0u + (uint)index * 0x9C);
    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static void RequireStaticSource(NativeBattleEffectOwnedModelResource resource)
    {
        (int extent, int vertices, int triangles, int quads) = resource.Address switch
        {
            0xE808D0 => (0x1064, 62, 37, 36),
            0xE81934 => (0xED4, 49, 17, 36),
            0xE82808 or 0xE839F4 => (0x11EC, 76, 50, 41),
            _ => throw new NotSupportedException("Native sword clipping requires one of its actual four selected static model sources.")
        };
        if (resource.Bytes.IsDefault || resource.Bytes.Length != extent || resource.PoseSource.BoneCount != 2 || resource.Geometry.IsDefault || resource.Geometry.Length != 1 || resource.Geometry[0].Vertices.Length != vertices || resource.Geometry[0].Polygons.Count(polygon => !polygon.Quad) != triangles || resource.Geometry[0].Polygons.Count(polygon => polygon.Quad) != quads || resource.Geometry[0].Polygons.Any(polygon => polygon.Bytes.Length != (polygon.Quad ? 20 : 16)))
        {
            throw new InvalidDataException("Native sword clipping requires its complete actual plain-only static model geometry.");
        }
    }
}
