using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshModelADraw(NativeBattleCactuarRawModelState State, NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Packets, int LabelEndOffset);
public sealed record NativeBattleGilgameshModelAStep(NativeBattleGilgameshModelMemory ModelMemory, NativeBattleEffectArena Arena, NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Packets, int? LabelEndOffset, int Result);

/// <summary>595EE0's modelA controller and596010/5960A0's separate saturated world-short skinning before radial view projection.</summary>
public static class NativeBattleGilgameshModelA
{
    public static NativeBattleGilgameshModelAStep Step595EE0(NativeBattleGilgameshModelMemory modelMemory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, uint graphicsFlags)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 24)
        {
            throw new InvalidDataException("Native595EE0 requires its original raw0x24-byte task.");
        }
        int counter = unchecked((short)task.ReadWord(12));
        NativeBattleGilgameshModelOwner owner = modelMemory.Read(0x21FF760);
        NativeBattleCactuarRawModelState model = owner.State;
        if (counter == 0)
        {
            byte[] bytes = model.Model.ToArray();
            bytes.AsSpan(0x40, 16).Clear();
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x40), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x48), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x50), 4096);
            bytes.AsSpan(0x54, 12).Clear();
            model = NativeBattleCactuarRawModel.Initialize5A8F40(model with { Model = bytes.ToImmutableArray() }, resource, resource.Address, 32, new(owner.ControlAddress, 4));
        }
        NativeBattleGfGeometryBatch packets = new([], 0);
        int? labelEnd = null;
        if (counter == 0)
        {
            model = SetByteFlags(model, 4);
            model = NativeBattleCactuarRawModel.Select5A8FB0(model, resource, 0, new(owner.ControlAddress, 4));
        }
        if (counter == 10)
        {
            model = SetByteFlags(model, 8);
        }
        if (counter is >= 25 and <= 44)
        {
            if (counter == 25)
            {
                byte[] labels = new byte[2048];
                for (int offset = 0; offset < labels.Length; offset += 4)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(labels.AsSpan(offset), 0x40004000);
                }
                arena = arena.Write(0x22000, labels);
            }
            NativeBattleGilgameshModelADraw drawn = Draw596010(model, resource, arena, worldView, projection, registers, counter - 25);
            model = drawn.State;
            arena = drawn.Arena;
            packets = drawn.Packets;
            labelEnd = drawn.LabelEndOffset;
        }
        if (counter == 45)
        {
            byte[] bytes = model.Model.ToArray();
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(flags & 0xFFF3));
            model = model with { Model = bytes.ToImmutableArray() };
        }
        modelMemory = modelMemory.Write(owner.Address, model);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(modelMemory, arena, task, packets, labelEnd, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(modelMemory, arena, task, packets, labelEnd, unchecked((short)task.ReadWord(12)) >= 60 ? 2 : 0);
    }

    public static NativeBattleGilgameshModelADraw Draw596010(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleEffectArena arena, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, int relativePhase)
    {
        RequireModelA(resource);
        NativeBattleCactuarRawModel.Validate(state, resource, new(0x21FF4B8, 4));
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Native596010 requires its current decoded pose owner.");
        ImmutableArray<byte> bytes = state.Model;
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(0x40 + index * 2))).ToImmutableArray(), Int(bytes, 0x54), Int(bytes, 0x58), Int(bytes, 0x5C), 4096);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, local);
        if ((unchecked((uint)Int(bytes, 0x7C)) & 1) == 0)
        {
            return new(state, arena, new([], 0), 0x22000);
        }
        if (bytes[7] != 0)
        {
            throw new NotSupportedException("Native596010 requires its proved initialized zero-alpha path.");
        }
        NativeBattleRawMesh mesh = resource.Geometry[0];
        arena = SkinWorld5960A0(mesh, bones, arena, 0x31800);
        if (relativePhase == 0)
        {
            NativeBattleGilgameshModelALabels labels = NativeBattleGilgameshModelAPackets.SetupLabels5964B0(mesh, arena, 0x31800, 0x22000);
            return new(state, labels.Arena, new([], 0), labels.LabelEndOffset);
        }
        NativeBattleGilgameshModelAReveal reveal = NativeBattleGilgameshModelAPackets.Emit596220(mesh, arena, 0x31800, 0x22000, relativePhase, worldView, projection, unchecked((uint)Int(bytes, 0x28)), registers);
        return new(state, arena, reveal.Packets, reveal.LabelEndOffset);
    }

    public static NativeBattleEffectArena SkinWorld5960A0(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleEffectArena arena, int worldOffset)
    {
        mesh.Validate();
        if (bones.IsDefault)
        {
            throw new InvalidDataException("Native5960A0 requires its current composed bone matrices.");
        }
        byte[] bytes = new byte[checked(mesh.Vertices.Length * 8)];
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (vertex.Bone >= bones.Length)
            {
                throw new InvalidDataException("Native5960A0 vertex references a missing current bone matrix.");
            }
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), transformed.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 2), transformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), transformed.Z);
        }
        return arena.Write(worldOffset, bytes);
    }

    private static NativeBattleCactuarRawModelState SetByteFlags(NativeBattleCactuarRawModelState state, byte flags) => state with { Model = state.Model.SetItem(0, (byte)(state.Model[0] | flags)) };
    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static void RequireModelA(NativeBattleEffectOwnedModelResource resource)
    {
        if (resource.Address != 0x20DFAB8 || resource.Bytes.IsDefault || resource.Bytes.Length != 0x77E0 || resource.Geometry.IsDefault || resource.Geometry.Length != 1 || resource.Geometry[0].Vertices.Length != 505 || resource.Geometry[0].Polygons.Length != 983 || resource.Geometry[0].Polygons.Count(polygon => polygon.Quad) != 1 || resource.Geometry[0].Polygons.Any(polygon => polygon.Bytes.Length != (polygon.Quad ? 20 : 16)))
        {
            throw new InvalidDataException("Native modelA radial reveal requires its actual complete plain-only original model resource.");
        }
    }
}


