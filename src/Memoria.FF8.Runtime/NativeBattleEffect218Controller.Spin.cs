using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattlePackedMatrix32 SpinMatrix633560(NativeBattleModulePoolTask task, NativeBattleActionMatrix currentView)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(1024, Signed(task, 24), 0));
        int angle = unchecked(Signed(task, 12) << 11) / 16;
        int scale = unchecked(NativeBattleCameraMath.Sine(angle) << 13) >> 12;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        return NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(currentView, local), null);
    }

    public static NativeBattleEffect218Frame DrawSpin633560(NativeBattleEffect218Frame frame, uint address, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if (mesh.Address != 0xE1B7E0)
        {
            throw new InvalidDataException("Effect218 spin requires its actual selected mesh.");
        }
        NativeBattleActionMatrix view = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CurrentView, 32), 0);
        frame = LoadMatrix(frame, SpinMatrix633560(Read(frame, address), view));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(28, BitConverter.GetBytes(0x33u));
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, projection, context, colors);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = deliver(frame, drawn.Batch);
        return Release(frame, 88);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceSpin633560(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, uint, NativeBattleEffect218Frame> draw)
    {
        frame = draw(frame, address);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 16 ? 2 : 0);
    }
}
