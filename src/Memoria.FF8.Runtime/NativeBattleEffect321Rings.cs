using static Memoria.FF8.Runtime.NativeBattleEffect321Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect321Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect321Rings
{
    public static NativeBattleEffect321Step Advance(NativeBattleEffect321Frame frame, uint address, bool anisotropic, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int delay = Signed(task, 14);
        if (delay > 0)
        {
            if (!Paused(frame, inputs))
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(delay - 1))));
            }
            return new(frame, 0);
        }
        short xz = Signed(task, 28);
        short y = anisotropic ? Signed(task, 32) : xz;
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        local = local with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, xz, y, xz);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        (frame, uint temporary) = Allocate(frame, 88);
        int age = Signed(task, 12);
        int threshold = anisotropic ? 6 : 12;
        NativeBattleEffectArena context = Put(Put(Put(Context(frame, temporary, 88), 0, anisotropic ? 0xCD1D5Cu : 0xCD153Cu), 8, 0), 28, age >= threshold ? 0xF3u : 0x33u);
        if (age >= threshold)
        {
            context = Put(context, 12, unchecked((uint)((age - threshold) << 9)));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = Release(DrawStatic(frame, temporary, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(321, task) with { ResourceGeneration = anisotropic ? 0xCD1D5C : 0xCD153C } : null), 88);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int scaleOffset = anisotropic ? 32 : 28;
        int velocityOffset = scaleOffset + 2;
        short velocity = Signed(task, velocityOffset);
        task = task.WriteWord(scaleOffset, unchecked((ushort)(Signed(task, scaleOffset) + velocity))).WriteWord(velocityOffset, unchecked((ushort)(velocity - velocity / (anisotropic ? 10 : 8))));
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= (anisotropic ? 14 : 20) ? 2 : 0);
    }
}
