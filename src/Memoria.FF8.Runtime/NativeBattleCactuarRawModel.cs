using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCactuarRawModelState([property: JsonRequired] ImmutableArray<byte> Model, [property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] uint? ResourceAddress, [property: JsonRequired] uint? WorkingBase, [property: JsonRequired] NativeBattleDecodedPose? DecodedPose, [property: JsonRequired] ImmutableArray<byte>? ProjectedScratch)
{
    public static NativeBattleCactuarRawModelState FromColdProcess() => new(new byte[0x90].ToImmutableArray(), new byte[0x34].ToImmutableArray(), null, null, null, null);
}
public sealed record NativeBattleRawModelBinding(uint ControlAddress, byte DepthShift);

public sealed record NativeBattleCactuarRawModelStep(NativeBattleCactuarRawModelState State, bool Completed);
public sealed record NativeBattleCactuarRawModelDraw(NativeBattleCactuarRawModelState State, int ProjectedBytesWritten, NativeBattleGfGeometryBatch Packets);

/// <summary>The independent raw model/control owners used by the5A8F40 model leaf.</summary>
public static class NativeBattleCactuarRawModel
{
    public const uint ModelAddress = 0x225A838;
    public const uint ControlAddress = 0x2259998;

    public static NativeBattleCactuarRawModelState Initialize5A8F40(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, uint workingBase, ushort argument1, NativeBattleRawModelBinding? binding = null)
    {
        RequireStorage(state);
        uint controlAddress = ResolveBinding(binding).ControlAddress;
        resource.PoseSource.Validate();
        if (workingBase == 0 || resource.Address == 0 || resource.Bytes.IsDefault || resource.Bytes.Length < 20)
        {
            throw new InvalidDataException("Native raw model initialization requires its actual working/source owners.");
        }
        byte[] model = state.Model.ToArray();
        byte[] control = state.Control.ToArray();
        WriteWord(model, 0, 2);
        model[7] = 0;
        WriteDword(model, 0x28, 0x00808080);
        model[0x60] = 0;
        model[0x61] = 0;
        WriteDword(model, 0x64, controlAddress + 0xC);
        WriteDword(model, 0x68, controlAddress + 0xC);
        WriteDword(model, 0x78, 0);
        WriteDword(model, 0x7C, uint.MaxValue);
        WriteDword(model, 0x84, controlAddress);
        WriteDword(model, 0x8C, 0);
        for (int index = 0; index < 3; index++)
        {
            WriteDword(control, 0xC + index * 4, SectionPointer(resource, workingBase, index));
        }
        WriteWord(control, 2, argument1);
        WriteDword(control, 0x30, SectionPointer(resource, workingBase, 3));
        return state with { Model = model.ToImmutableArray(), Control = control.ToImmutableArray(), ResourceAddress = resource.Address, WorkingBase = workingBase };
    }

    public static NativeBattleCactuarRawModelState Select5A8FB0(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, short animation, NativeBattleRawModelBinding? binding = null)
    {
        Validate(state, resource, binding);
        RequireNormalSpeed(state);
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        if (animation < 0 || animation >= counts.Length || animation > byte.MaxValue)
        {
            throw new NotSupportedException("Native raw model pose selector has no supplied compressed stream.");
        }
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), resource.PoseSource.BoneCount, animation);
        byte[] model = state.Model.ToArray();
        model[0x6C] = (byte)animation;
        model[0x6D] = (byte)((model[0x6D] & 0xF3) | 4);
        WriteWord(model, 0x6E, first.ByteOffset);
        WriteWord(model, 0x70, first.BitOffset);
        model[0x72] = 1;
        model[0x73] = counts[animation];
        return state with { Model = model.ToImmutableArray(), DecodedPose = first.Pose };
    }

    public static NativeBattleCactuarRawModelStep Advance5A8FD0(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleRawModelBinding? binding = null)
    {
        Validate(state, resource, binding);
        RequireNormalSpeed(state);
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Native raw model must select its current pose before advancing.");
        byte[] model = state.Model.ToArray();
        int animation = model[0x6C];
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        if (animation >= counts.Length || model[0x73] != counts[animation] || model[0x72] == 0)
        {
            throw new InvalidDataException("Native raw model cursor counters contradict its compressed pose source.");
        }
        if (model[0x72] >= model[0x73])
        {
            return new((model[0] & 1) == 0 ? Select5A8FB0(state, resource, (short)animation, binding) : state, true);
        }
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(resource.PoseSource.Animations.AsSpan(), resource.PoseSource.BoneCount, animation, pose, ReadWord(model, 0x6E), ReadWord(model, 0x70));
        WriteWord(model, 0x6E, next.ByteOffset);
        WriteWord(model, 0x70, next.BitOffset);
        model[0x72]++;
        model[0x6D] = (byte)((model[0x6D] & 0xF3) | ((((model[0x6D] >> 2) + 1) & 1) << 2));
        return new(state with { Model = model.ToImmutableArray(), DecodedPose = next.Pose }, false);
    }

    public static NativeBattleCactuarRawModelDraw Draw5A9010(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix parent, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleRawModelBinding? binding = null, ImmutableArray<byte>? projectedScratch = null)
    {
        Validate(state, resource, binding);
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Native raw model body requires its current decoded skeleton.");
        if (state.Model[7] != 0)
        {
            throw new NotSupportedException("Native raw model body requires its proved zero-alpha path.");
        }
        if (resource.Geometry.IsDefault || resource.Geometry.Length != 1)
        {
            throw new InvalidDataException("Native raw model body requires its one explicit packed geometry object.");
        }
        uint mask = ReadDword(state.Model.AsSpan(), 0x7C);
        if ((mask & 1) == 0)
        {
            return new(state, 0, new([], 0));
        }
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => unchecked((short)ReadWord(state.Model.AsSpan(), 0x40 + index * 2))).ToImmutableArray(), unchecked((int)ReadDword(state.Model.AsSpan(), 0x54)), unchecked((int)ReadDword(state.Model.AsSpan(), 0x58)), unchecked((int)ReadDword(state.Model.AsSpan(), 0x5C)), 4096);
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.Compose56C2F0(parent, local);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, modelView);
        int length = checked(resource.Geometry[0].Vertices.Length * 8);
        ImmutableArray<byte> scratch = projectedScratch ?? (binding is not null ? throw new NotSupportedException("Native explicitly bound raw model requires its actual shared projected scratch prefix.") : state.ProjectedScratch ?? new byte[length].ToImmutableArray());
        if (scratch.IsDefault || scratch.Length != length)
        {
            throw new InvalidDataException("Native raw model projected scratch contradicts its complete vertex owner.");
        }
        NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(resource.Geometry[0], bones, projection, ReadDword(state.Model.AsSpan(), 0x28), mask, scratch, colors, ResolveBinding(binding).DepthShift);
        if (draw.ProjectedBytesWritten != length)
        {
            throw new InvalidDataException("Native raw model body did not establish its complete projected scratch.");
        }
        return new(state with { ProjectedScratch = draw.ProjectedScratch }, draw.ProjectedBytesWritten, draw.Packets);
    }

    public static void Validate(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleRawModelBinding? binding = null)
    {
        RequireStorage(state);
        uint controlAddress = ResolveBinding(binding).ControlAddress;
        resource.PoseSource.Validate();
        if (state.ResourceAddress != resource.Address || state.WorkingBase is not > 0 || ReadDword(state.Model.AsSpan(), 0x64) != controlAddress + 0xC || ReadDword(state.Model.AsSpan(), 0x68) != controlAddress + 0xC || ReadDword(state.Model.AsSpan(), 0x84) != controlAddress)
        {
            throw new InvalidDataException("Native raw model pointers contradict its symbolic model/control/source owners.");
        }
        for (int index = 0; index < 3; index++)
        {
            if (ReadDword(state.Control.AsSpan(), 0xC + index * 4) != SectionPointer(resource, state.WorkingBase.Value, index))
            {
                throw new InvalidDataException("Native raw model section pointer changed its working owner.");
            }
        }
        if (ReadDword(state.Control.AsSpan(), 0x30) != SectionPointer(resource, state.WorkingBase.Value, 3) || state.DecodedPose is { } pose && (pose.Bones.IsDefault || pose.Bones.Length != resource.PoseSource.BoneCount))
        {
            throw new InvalidDataException("Native raw model control or decoded skeleton contradicts its source.");
        }
    }
    private static NativeBattleRawModelBinding ResolveBinding(NativeBattleRawModelBinding? binding)
    {
        NativeBattleRawModelBinding actual = binding ?? new(ControlAddress, 4);
        if (actual.ControlAddress == 0 || (actual.ControlAddress & 3) != 0 || actual.DepthShift > 31)
        {
            throw new InvalidDataException("Native raw model requires its aligned control owner and actual depth shift.");
        }
        return actual;
    }

    private static void RequireStorage(NativeBattleCactuarRawModelState state)
    {
        if (state.Model.IsDefault || state.Model.Length != 0x90 || state.Control.IsDefault || state.Control.Length != 0x34)
        {
            throw new InvalidDataException("Native raw model requires its retained90h model and34h control owners.");
        }
    }
    private static void RequireNormalSpeed(NativeBattleCactuarRawModelState state)
    {
        if ((state.Model[0x6D] & 3) != 0)
        {
            throw new NotSupportedException("Native raw model cursor requires its proved normal-speed flags.");
        }
    }
    private static uint SectionPointer(NativeBattleEffectOwnedModelResource resource, uint workingBase, int index)
    {
        int offset = BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(4 + index * 4, 4));
        if (offset < 0 || offset > resource.Bytes.Length)
        {
            throw new InvalidDataException("Native raw model section/end pointer leaves its complete source.");
        }
        return checked(workingBase + (uint)offset);
    }
    private static ushort ReadWord(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
    private static uint ReadDword(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    private static void WriteWord(Span<byte> data, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(offset, 2), value);
    private static void WriteDword(Span<byte> data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.Slice(offset, 4), value);
}
