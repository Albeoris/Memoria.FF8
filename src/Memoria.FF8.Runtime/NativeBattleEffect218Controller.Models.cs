using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceModels637350(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Resources resources, Func<NativeBattleEffect218Frame, uint, NativeBattleEffect218Frame> drawModel)
    {
        ushort flags = frame.State.ReadWord(StreamedModelAddress);
        frame = Word(frame, EmbeddedModelAddress, flags);
        if ((flags & 4) == 0)
        {
            frame = Copy(frame, StreamedModelAddress + 0x40, EmbeddedModelAddress + 0x40, 32);
            frame = Dword(frame, EmbeddedModelAddress + 0x28, frame.State.ReadDword(StreamedModelAddress + 0x28));
            frame = drawModel(frame, EmbeddedModelAddress);
            frame = drawModel(frame, StreamedModelAddress);
            flags = frame.State.ReadWord(StreamedModelAddress);
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if ((flags & 8) == 0)
        {
            frame = AdvanceOwnedPose6373F0(frame, EmbeddedModelAddress, resources.EmbeddedModel);
            frame = AdvanceOwnedPose6373F0(frame, StreamedModelAddress, resources.StreamedModel);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 526 ? 2 : 0);
    }

    public static NativeBattleEffect218Frame AdvanceOwnedPose6373F0(NativeBattleEffect218Frame frame, uint modelAddress, NativeBattleEffectOwnedModelResource resource)
    {
        if ((modelAddress != EmbeddedModelAddress || resource.Address != 0xF1DD54) && (modelAddress != StreamedModelAddress || resource.Address != frame.State.ReadDword(ArenaGlobal)))
        {
            throw new InvalidDataException("Effect218 pose requires its actual selected model source.");
        }
        NativeBattleEffectArena model = frame.State.ReadView(modelAddress, 0x90);
        uint indirect = BinaryPrimitives.ReadUInt32LittleEndian(model.Read(0x64, 4));
        uint skeletonAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, indirect, 4).Read(0, 4));
        uint expected = checked(resource.Address + (uint)resource.Section(0));
        if (skeletonAddress != expected)
        {
            throw new InvalidDataException("Effect218 pose pointer leaves its current model skeleton.");
        }
        NativeBattleEffectArena skeleton = ReadBytes(frame, skeletonAddress, resource.PoseSource.Skeleton.Length);
        NativeBattleEffect218ModelPose next = NativeBattleEffect218Model.Advance6373F0(model, skeleton, resource.PoseSource.Animations.AsSpan());
        frame = WriteBytes(frame, modelAddress, next.Model, 0x90);
        return WriteBytes(frame, skeletonAddress, next.Skeleton, resource.PoseSource.Skeleton.Length);
    }
}
