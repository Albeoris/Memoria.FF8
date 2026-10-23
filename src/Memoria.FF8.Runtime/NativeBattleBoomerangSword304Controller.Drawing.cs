using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBoomerangSword304Controller
{
    public static (NativeBattleBoomerangSword304Frame Frame, int Result) AdvanceMesh66A730(NativeBattleBoomerangSword304Frame frame, uint address, NativeBattleBoomerangSword304Resources resources, NativeBattleBoomerangSword304Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => Signed(task, 16 + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(36)), unchecked((int)task.ReadDword(40)), unchecked((int)task.ReadDword(44)), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        int angle = unchecked(Signed(task, 12) << 10) / 8;
        int cue = NativeBattleCameraMath.Sine(angle);
        int blend = Signed(task, 14);
        if (blend < 4096)
        {
            cue = unchecked(4096 - (unchecked((4096 - cue) * blend) >> 12));
        }
        int blue = unchecked(4096 - cue) >> 4;
        if (blue > 255)
        {
            blue = 255;
        }
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(resources.TrailMesh.Address)).Write(8, new byte[] { 0, 0, unchecked((byte)blue) }).Write(12, BitConverter.GetBytes(cue)).Write(28, BitConverter.GetBytes(0xC0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleBoomerangSword304DrawStatic88(temporary, resources.TrailMesh.Address));
        frame = Release(frame, 88);
        task = Read(frame, address);
        short age = unchecked((short)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)age)));
        return (frame, age < 8 ? 0 : 2);
    }
}
