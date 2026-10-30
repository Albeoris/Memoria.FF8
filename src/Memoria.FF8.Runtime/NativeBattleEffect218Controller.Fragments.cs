using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame DrawFragment635110(NativeBattleEffect218Frame frame, uint address, NativeBattleEffect218Resources resources, NativeBattlePackedMatrix32 currentView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int selected = Signed(task, 14);
        uint location = FragmentLocation218(selected);
        NativeBattleEffectArena row = ReadBytes(frame, location, 16);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        short scale = Signed(task, 28);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale) with { X = ParticleWord218(row, 0), Y = ParticleWord218(row, 2), Z = ParticleWord218(row, 4) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(currentView.ReadMatrix(), local), null));
        (frame, uint temporary) = Allocate(frame, 96);
        uint meshAddress = resources.FragmentMeshes[selected];
        NativeBattleEffectArena context = Context(frame, temporary, 96).Write(0, BitConverter.GetBytes(meshAddress)).Write(8, new byte[4]).Write(28, BitConverter.GetBytes(0x3Cu)).Write(0x58, resources.FragmentOrigins.AsSpan().Slice(selected * 8, 8));
        NativeBattleEffect218FragmentMeshStep drawn = NativeBattleEffect218FragmentMesh.DrawPresentation(frame.Memory, resources.Mesh(meshAddress), projection, context, colors, NativeBattleEffect218Presentation.Resource(frame, meshAddress, selected));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context96, 96);
        frame = deliver(frame, drawn.Batch);
        return Release(frame, 96);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceFragment635110(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, Func<NativeBattleEffect218Frame, uint, NativeBattleEffect218Frame> draw)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short delay = Signed(task, 22);
        bool paused = (graphicsFlags & 0x201) != 0;
        if (delay > 0)
        {
            return (paused ? frame : Write(frame, address, task.WriteWord(22, unchecked((ushort)(delay - 1)))), 0);
        }
        uint location = FragmentLocation218(Signed(task, 14));
        frame = draw(frame, address);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleEffectArena row = ReadBytes(frame, location, 16);
        for (int axis = 0; axis < 3; axis++)
        {
            row = ParticleWord218(row, axis * 2, ParticleWord218(row, axis * 2) + ParticleWord218(row, 8 + axis * 2));
        }
        frame = WriteBytes(frame, location, row, 16);
        task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 46 ? 2 : 0);
    }

    private static uint FragmentLocation218(int selected)
    {
        if ((uint)selected >= 24)
        {
            throw new InvalidDataException("Effect218 fragment selector leaves its actual twenty-four rows.");
        }
        return checked(0x24F3378u + (uint)(selected * 16));
    }
}
