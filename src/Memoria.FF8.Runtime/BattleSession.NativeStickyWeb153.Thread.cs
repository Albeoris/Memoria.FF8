using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int AdvanceNativeStickyWebThread(ref NativeBattleModulePoolTask task, NativeBattleGraphicsState graphics, byte[] data, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short zsf3, short zsf4, bool paused, Action<NativeBattleGfGeometryBatch, int> append)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleActionMatrix world = graphics.Models[effect.Parameters!.Actor78].GeometryWorldMatrix ?? throw new NotSupportedException("Native Sticky Web requires its caster world matrix.");
        (int x, _, int z) = NativeBattleGfMatrices.Rotate56C3D0(world, new(-4096, 0, 0));
        int counter = Word(task, 12);
        int amplitude = unchecked(NativeBattleCameraMath.Sine(Word(task, 16)) * 600) >> 12;
        NativeBattleVector target = Vector(0x1908);
        NativeBattleVector anchor = new(unchecked((short)(target.X + (x * amplitude >> 12))), Word(task, 18), unchecked((short)(target.Z + (z * amplitude >> 12))));
        Put(0x1928, anchor.X);
        Put(0x192A, anchor.Y);
        Put(0x192C, anchor.Z);
        int ring = counter % 5;
        int record = 0x1930 + ring * 20;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(record), 1);
        data.AsSpan(0x1920, 8).CopyTo(data.AsSpan(record + 4));
        data.AsSpan(0x1928, 8).CopyTo(data.AsSpan(record + 12));
        append(NativeBattleStickyWeb153Drawing.Line(Vector(0x1920), anchor, view, projection, zsf3), 0);
        int count = 0;
        int available = counter >= 25 ? 30 - counter : 5;
        for (; count < available; count++)
        {
            record = 0x1930 + ring * 20;
            if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(record)) == 0)
            {
                break;
            }
            data.AsSpan(record + 4, 8).CopyTo(data.AsSpan(count * 8));
            data.AsSpan(record + 12, 8).CopyTo(data.AsSpan(0x30 + count * 8));
            ring = ring == 0 ? 4 : ring - 1;
        }
        if (count > 1)
        {
            NativeBattleEffectArena globals = NativeBattleEffectArena.Clear().Write(0, data);
            NativeBattleEffectArena work = NativeBattleEffectArena.Clear();
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, globals, side * 0x30, work);
                for (int sample = 0; sample < 16; sample++)
                {
                    globals = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 15, globals, 0x9E8 + side * 0x80 + sample * 8);
                }
            }
            globals.Read(0x9E8, 0x100).CopyTo(data.AsSpan(0x9E8));
            ImmutableArray<NativeBattleVector> first = Enumerable.Range(0, 16).Select(index => Vector(0x9E8 + index * 8)).ToImmutableArray();
            ImmutableArray<NativeBattleVector> second = Enumerable.Range(0, 16).Select(index => Vector(0xA68 + index * 8)).ToImmutableArray();
            append(NativeBattleStickyWeb153Drawing.Strips(first, second, view, projection, colors, zsf4), 36);
        }
        if (paused)
        {
            return 0;
        }
        if (counter < 25)
        {
            task = task.WriteWord(16, unchecked((ushort)(Word(task, 16) + Word(task, 20)))).WriteWord(18, unchecked((ushort)(Word(task, 18) + Word(task, 22))));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return counter + 1 >= 30 ? 2 : 0;

        static short Word(NativeBattleModulePoolTask owner, int offset) => unchecked((short)owner.ReadWord(offset));
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
        void Put(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value);
        NativeBattleVector Vector(int offset) => new(Read(offset), Read(offset + 2), Read(offset + 4));
    }
}
