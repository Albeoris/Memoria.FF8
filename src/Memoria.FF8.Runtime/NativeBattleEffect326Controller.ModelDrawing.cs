using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame DrawOwnedModel62C820(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs, uint modelAddress, NativeBattleActionMatrix parent, int? drawingEntryModulo8 = null)
    {
        NativeBattleCactuarRawModelState model = RequireModel(frame, modelAddress);
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Effect326 model drawing requires its current decoded pose.");
        NativeBattleEffectOwnedModelResource resource = ModelResource(resources, modelAddress);
        uint working = model.WorkingBase ?? throw new NotSupportedException("Effect326 model drawing requires its current working source.");
        uint skeleton = checked(working + (uint)resource.Section(0));
        uint geometryAddress = checked(working + (uint)resource.Section(1));
        NativeBattleActionMatrix local = ReadPacked(frame, modelAddress + 0x40).ReadMatrix();
        NativeBattleActionMatrix modelView = NativeBattleGfMatrices.Compose56C2F0(parent, local);
        (frame, uint temporary) = Allocate(frame, 76);
        NativeBattleEffectArena context = Context(frame, temporary, 76);
        context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(modelView, null)), 0, context, 0, 32);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, modelView);
        ImmutableArray<NativeBattleActionMatrix> localBones = NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, pose);
        ImmutableArray<short?> padding = NativeBattleBoneMatrixPadding.Compose5095B0After62C83D(local, modelView, localBones, drawingEntryModulo8);
        for (int bone = 0; bone < bones.Length; bone++)
        {
            ushort? paddingWord = padding[bone] is short known ? unchecked((ushort)known) : null;
            frame = WriteBytes(frame, checked(skeleton + 32u + (uint)(bone * 48)), PackedArena(NativeBattlePackedMatrix32.FromMatrix(bones[bone], paddingWord)), 32);
        }
        uint scratchAddress = frame.State.ReadDword(0xE196B0);
        uint color = frame.State.ReadDword(modelAddress + 0x28);
        uint mask = frame.State.ReadDword(modelAddress + 0x7C);
        context = context.Write(0x24, BitConverter.GetBytes(scratchAddress)).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((ushort)320)).Write(0x3A, BitConverter.GetBytes((ushort)216)).Write(0x3C, BitConverter.GetBytes(color)).Write(0x40, BitConverter.GetBytes(mask)).Write(0x44, new byte[2]).Write(0x48, new byte[4]);
        context = CopyBytes(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), 0x1D969A8, 4), 0, context, 0x30, 4);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect326 model requires its actual renderer process.");
        var gate = process.Enter5099D0(inputs.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect326 model requires its actual hardware capability."));
        process = gate.Process;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (gate.Draw)
        {
            process = process.PublishModelColor(color);
            Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)> metadata = [];
            int geometryLength = resource.Section(2) - resource.Section(1);
            NativeBattleEffect326Frame current = frame;
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, count) =>
            {
                if (offset < 0 || count < 0 || offset > geometryLength - count)
                {
                    throw new InvalidDataException("Effect326 model drawing leaves its actual geometry owner.");
                }
                return ReadBytes(current, checked(geometryAddress + (uint)offset), count).Read(0, count);
            }, resource.PoseSource.BoneCount, (index, counts, first, end) => metadata.Add(index, (counts, first, end)));
            uint firstPacket = BinaryPrimitives.ReadUInt32LittleEndian(RequireStaging(frame).Read(0x1D8E054, 4));
            for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
            {
                if ((mask & (1u << (objectIndex & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[objectIndex];
                foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect326 model requires its actual packed vertex groups."))
                {
                    frame = LoadMatrix(frame, ReadPacked(frame, checked(skeleton + 32u + (uint)(group.Bone * 48))));
                }
                var layout = metadata[objectIndex];
                context = context.Write(0x20, BitConverter.GetBytes(checked(geometryAddress + (uint)layout.First)));
                for (int group = 0; group < 4; group++)
                {
                    context = context.Write(0x28 + group * 2, BitConverter.GetBytes(layout.Counts[group]));
                }
                int length = checked(mesh.Vertices.Length * 8);
                (byte[] previous, _) = ReadBytes(frame, scratchAddress, length).ReadSnapshot(0, length);
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, bones, inputs.ReadProjection(frame), color, 1, previous.ToImmutableArray(), inputs.Colors, 4, pilotIdentity: NativeBattleEffect326Presentation.Task(inputs, unchecked(geometryAddress * 397 ^ modelAddress), objectIndex));
                if (drawn.ProjectedBytesWritten != 0)
                {
                    frame = WriteBytes(frame, scratchAddress, NativeBattleEffectArena.Clear().Write(0, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)), drawn.ProjectedBytesWritten);
                }
                attempts.AddRange(drawn.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
                allocated = checked(allocated + drawn.Packets.AllocationBytes);
                process = process.Publish50FDF0(checked(firstPacket + (uint)allocated), checked(geometryAddress + (uint)layout.End));
            }
        }
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process } };
        frame = PublishContext(frame, temporary, context, 76);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(inputs.CapturePresentation ? NativeBattleEffect326Presentation.PreserveNativeDepths(new(attempts.ToImmutable(), allocated)) : new(attempts.ToImmutable(), allocated), 0x1D8E054));
        frame = PublishModelPose(frame, resources, modelAddress);
        return Release(frame, 76);
    }
}
