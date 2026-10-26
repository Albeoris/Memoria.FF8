using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattlePackedMatrix32 ShadowMatrix633AC0(NativeBattleActionMatrix currentView, NativeBattleActionMatrix modelWorld, NativeBattleActionMatrix bone)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(-1024, 0, 0));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, 21504, 21504, 21504) with { X = 0, Y = 0, Z = 0 };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(modelWorld, bone);
        matrix = NativeBattleGfMatrices.Compose56C2F0(matrix, local);
        matrix = NativeBattleGfMatrices.Compose56C2F0(currentView, matrix);
        return NativeBattlePackedMatrix32.FromMatrix(matrix, null);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceShadow633AC0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        byte flags = frame.State.ReadByte(0x24F3544);
        if ((flags & 2) != 0)
        {
            return (frame, 2);
        }
        if (mesh.Address != 0xE254D8)
        {
            throw new InvalidDataException("Effect218 shadow requires its actual selected mesh.");
        }
        uint indirect = frame.State.ReadDword(EmbeddedModelAddress + 0x64);
        uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, indirect, 4).Read(0, 4));
        NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(ReadBytes(frame, checked(skeleton + 0x50), 32), 0);
        NativeBattleActionMatrix modelWorld = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(EmbeddedModelAddress + 0x40, 32), 0);
        NativeBattleActionMatrix currentView = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CurrentView, 32), 0);
        frame = LoadMatrix(frame, ShadowMatrix633AC0(currentView, modelWorld, bone));
        if (Signed(Read(frame, address), 12) > 0 && (flags & 4) == 0)
        {
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(28, BitConverter.GetBytes(0x33u));
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, projection, context, colors, depthShift: 3);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
            frame = deliver(frame, drawn.Batch);
            frame = Release(frame, 88);
        }
        if ((graphicsFlags & 0x201) == 0)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        }
        return (frame, 0);
    }
}
