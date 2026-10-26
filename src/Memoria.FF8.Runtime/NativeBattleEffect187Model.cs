using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187Model
{
    public const uint Address = 0x24FD8D8;
    public const uint ControlAddress = 0x24FE8F0;
    private static readonly NativeBattleRawModelBinding Binding = new(ControlAddress, 4);

    public static NativeBattleCactuarRawModelState Initialize64A1D0(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, uint workingBase)
    {
        return NativeBattleCactuarRawModel.Initialize5A8F40(state, resource, workingBase, 15, Binding);
    }

    public static NativeBattleCactuarRawModelState Select64A240(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, short animation)
    {
        return NativeBattleCactuarRawModel.Select5A8FB0(state, resource, animation, Binding);
    }

    public static NativeBattleCactuarRawModelStep Advance64A260(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        return NativeBattleCactuarRawModel.Advance5A8FD0(state, resource, Binding);
    }

    public static NativeBattleEffectMemory PublishPose508C90(NativeBattleEffectMemory memory, NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleCactuarRawModel.Validate(state, resource, Binding);
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Effect187 pose publication requires its selected compressed pose.");
        uint working = state.WorkingBase ?? throw new NotSupportedException("Effect187 pose publication requires its actual working source.");
        int skeleton = checked((int)(working - NativeBattleEffect187Profile.ArenaAddress) + resource.Section(0));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Effect187 pose publication requires its actual COMMON owner.");
        arena = NativeBattleArenaPose.WritePose(arena, skeleton, pose);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose);
        return memory with { Arena = arena };
    }

    public static NativeBattleCactuarRawModelDraw Draw64A2A0(NativeBattleCactuarRawModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix parent, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, ImmutableArray<byte> projectedScratch)
    {
        NativeBattleCactuarRawModel.Validate(state, resource, Binding);
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Effect187 model drawing requires its current decoded pose.");
        if (state.Model[7] != 0 || resource.Geometry.IsDefaultOrEmpty || projectedScratch.IsDefault)
        {
            throw new NotSupportedException("Effect187 model drawing requires its current zero-alpha body and actual projected scratch owner.");
        }
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => unchecked((short)Word(0x40 + index * 2))).ToImmutableArray(), unchecked((int)Dword(0x54)), unchecked((int)Dword(0x58)), unchecked((int)Dword(0x5C)), 4096);
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.Compose56C2F0(parent, local);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, modelView);
        NativeBattleRawModelBodyDraw body = NativeBattleRawModelBodyDrawing.Draw5099D0(resource.Geometry, bones, projection, Dword(0x28), Dword(0x7C), projectedScratch, colors, 4);
        return new(state with { ProjectedScratch = body.ProjectedScratch }, body.ProjectedBytesWritten, body.Packets);

        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(state.Model.AsSpan(offset, 2));

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(state.Model.AsSpan(offset, 4));
    }
}
