using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame DrawBurst218(NativeBattleEffect218Frame frame, uint address, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (uint expected, int start, int fadeMultiplier, _) = BurstProfile218(task.Callback);
        if (mesh.Address != expected)
        {
            throw new InvalidDataException("Effect218 burst requires its actual selected mesh.");
        }
        short angle = task.Callback == 0x634750 ? (short)0 : Signed(task, 24);
        short horizontal = Signed(task, 28);
        short vertical = task.Callback == 0x634750 ? horizontal : Signed(task, 32);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, angle, 0));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, horizontal, vertical, horizontal) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        NativeBattleActionMatrix view = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CurrentView, 32), 0);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(view, local), null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(28, BitConverter.GetBytes(0x33u));
        short age = Signed(task, 12);
        if (age >= start)
        {
            context = context.Write(28, BitConverter.GetBytes(0xF3u)).Write(12, BitConverter.GetBytes(unchecked((age - start) * fadeMultiplier)));
        }
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, projection, context, colors, pilotIdentity: NativeBattleEffect218Presentation.Resource(frame, mesh.Address));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = deliver(frame, drawn.Batch);
        return Release(frame, 88);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceBurst218(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, uint, NativeBattleEffect218Frame> draw)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (_, _, _, short limit) = BurstProfile218(task.Callback);
        frame = draw(frame, address);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        int divisor = task.Callback == 0x634890 ? 6 : 5;
        short horizontalVelocity = Signed(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + task.ReadWord(30)))).WriteWord(30, unchecked((ushort)(horizontalVelocity - horizontalVelocity / divisor)));
        if (task.Callback != 0x634750)
        {
            short verticalVelocity = Signed(task, 34);
            int verticalDivisor = task.Callback == 0x634890 ? 18 : 8;
            task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26)))).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + task.ReadWord(34)))).WriteWord(34, unchecked((ushort)(verticalVelocity - verticalVelocity / verticalDivisor)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= limit ? 2 : 0);
    }

    private static (uint Mesh, int FadeStart, int FadeMultiplier, short Limit) BurstProfile218(uint callback) => callback switch
    {
        0x634750 => (0xE1EF08, 6, 512, 14),
        0x634890 => (0xE1C918, 10, 341, 22),
        0x634A10 => (0xE1E260, 10, 682, 16),
        _ => throw new InvalidDataException("Effect218 burst selected an unsupported native callback.")
    };
}
