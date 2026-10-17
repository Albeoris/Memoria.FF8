using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleActionMatrix? SequenceDeformationMatrix1D99BF8 { get; init; }
}

public sealed partial record NativeBattleAuxiliaryState
{
    public NativeBattleAuxiliaryState AppendModelDeformation50F250(int model, byte group, byte bone, NativeBattleVector velocity, short angle, uint color)
    {
        Validate();
        if (!Slots.Any(task => (task.Flags & 1) == 0))
        {
            return this;
        }
        NativeBattleAuxiliaryState state = AppendModelGroupFade50F0B0(model, group);
        int slot = state.Tail!.Value;
        NativeBattleAuxiliaryTask task = state.Slots[slot];
        byte[] payload = task.Payload.ToArray();
        payload[5] = bone;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), velocity.X);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), velocity.Y);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), velocity.Z);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), color);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(20), angle);
        task = task with { Kind = NativeBattleAuxiliaryKind.ModelDeformation50F2E0, Payload = payload.ToImmutableArray(), UnknownByteMask = task.UnknownByteMask & ~0x3F3F7Fu | 0xC000u };
        return state with { Slots = state.Slots.SetItem(slot, task) };
    }
}

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ScheduleNativeModelDeformation50F250(NativeBattleGraphicsState graphics, int index, byte group, NativeBattleVector velocity, short angle)
    {
        if (!graphics.Auxiliary.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return graphics;
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native50F250 requires its original geometry directory.");
        NativeBattleRawMesh mesh = group < geometry.Length ? geometry[group] : throw new InvalidDataException("Native50F250 object selector exceeds its original geometry directory.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native50F250 requires its observed linked resource shape.");
        if (group == 1 && !linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton == true)
        {
            mesh = linked.Length == 1 ? linked[0] : throw new InvalidDataException("Native50F250 requires its single shared-skeleton weapon object.");
        }
        ImmutableArray<short> groups = mesh.BoneGroups ?? throw new NotSupportedException("Native50F250 requires its original bone-run headers, including empty runs; migrate the named resource snapshot explicitly.");
        int bone = 80;
        foreach (short candidate in groups)
        {
            bone = Math.Min(bone, candidate);
        }
        uint color = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native50F250 requires its retained process color DWORD.");
        return graphics with { Auxiliary = graphics.Auxiliary.AppendModelDeformation50F250(index, group, unchecked((byte)bone), velocity, angle, color) };
    }

    private NativeBattleAuxiliaryTask AdvanceNativeModelDeformation50F2E0(ref NativeBattleGraphicsState graphics, NativeBattleAuxiliaryTask task)
    {
        if ((task.UnknownByteMask & 0x3F3F7F) != 0)
        {
            throw new InvalidDataException("Native50F2E0 lacks its actual model/group/bone/velocity/color/angle fields.");
        }
        byte[] payload = task.Payload.ToArray();
        uint relative = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(payload) - 0x1D972C0u);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new InvalidDataException("Native50F2E0 model pointer exceeds its seven-row bank.");
        }
        int index = (int)(relative / 156);
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native50F2E0 requires its original decoded body skeleton.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50F2E0 requires its shared process matrix owner.");
        uint unknown = task.UnknownByteMask;
        if (payload[6] == 0)
        {
            NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native50F2E0 requires its model mesh mask.");
            model = model with { SequenceState = state with { MeshMask124 = state.MeshMask124 & ~(1u << payload[4]) } };
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            memory = memory with { SequenceDeformationMatrix1D99BF8 = NativeBattlePoseMatrices.ReadDeformationBone50F2E0(model, source, payload[5]) };
            payload[6] = 1;
            payload[7] = 0;
            unknown &= ~0x80u;
        }
        if ((unknown & 0x80) != 0)
        {
            throw new InvalidDataException("Native50F2E0 lacks its initialized frame BYTE.");
        }
        if (payload[6] == 1)
        {
            NativeBattleActionMatrix retained = memory.SequenceDeformationMatrix1D99BF8 ?? throw new NotSupportedException("Native50F2E0 requires its actual retained shared deformation matrix.");
            NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native50F2E0 requires its prior published camera view.");
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.DeformedGroupMatrices50F2E0(model, source, view, payload[5], retained);
            graphics = SubmitNativeModelGroup50F190(graphics, index, payload[4], payload[7], BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)), matrices);
            if ((graphics.Flags & 1) == 0)
            {
                short angle = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(20));
                short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
                short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
                NativeBattleActionMatrix axis = new([4096, 0, 0, 0, cosine, unchecked((short)-sine), 0, sine, cosine], 0, 0, 0, 4096);
                short x = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8));
                short y = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10));
                short z = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(12));
                retained = retained with { Rotation = NativeBattleGfMatrices.Multiply56C090(axis, retained).Rotation, X = unchecked(retained.X + x), Y = unchecked(retained.Y + y), Z = unchecked(retained.Z + z) };
                memory = memory with { SequenceDeformationMatrix1D99BF8 = retained };
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), unchecked((short)(x - (x << 9 >> 12))));
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), unchecked((short)(y - (y << 9 >> 12) + 100)));
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), unchecked((short)(z - (z << 9 >> 12))));
            }
        }
        NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native50F2E0 lost its process memory during group publication.")) with { SequenceDeformationMatrix1D99BF8 = memory.SequenceDeformationMatrix1D99BF8 };
        payload[7] = unchecked((byte)(payload[7] + 1));
        return task with { Payload = payload.ToImmutableArray(), UnknownByteMask = unknown };
    }
}
