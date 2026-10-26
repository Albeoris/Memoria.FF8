namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattlePackedMatrix32 PairMatrix633090(NativeBattleModulePoolTask task, short normalDepth)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(1024, Signed(task, 24), 0));
        short scale = Signed(task, 28);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale) with { X = 0, Y = 0, Z = normalDepth };
        return NativeBattlePackedMatrix32.FromMatrix(matrix, null);
    }

    public static NativeBattleEffectArena PairContext633090(NativeBattleEffectArena retained, NativeBattleModulePoolTask task)
    {
        uint flags = task.ReadWord(14) == 0 ? 0xF3u : 0xFCu;
        return retained.Write(0, BitConverter.GetBytes(0xE21CC0u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes((int)Signed(task, 32))).Write(28, BitConverter.GetBytes(flags));
    }

    public static NativeBattleEffect218Frame DrawPair633090(NativeBattleEffect218Frame frame, uint address, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short normalDepth, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if (mesh.Address != 0xE21CC0)
        {
            throw new InvalidDataException("Effect218 pair requires its actual selected mesh.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = LoadMatrix(frame, PairMatrix633090(task, normalDepth));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = PairContext633090(Context(frame, temporary, 88), task);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, projection, context, colors, 4122, 14);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = deliver(frame, drawn.Batch);
        return Release(frame, 88);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvancePair633090(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, uint, NativeBattleEffect218Frame> draw)
    {
        frame = draw(frame, address);
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + task.ReadWord(26)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 61 ? 2 : 0);
    }
}
