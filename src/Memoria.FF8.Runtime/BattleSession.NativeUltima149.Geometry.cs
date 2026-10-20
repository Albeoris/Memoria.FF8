using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeUltima149Runner
    {
        private int Energy(ref NativeBattleModulePoolTask task)
        {
            NativeBattleVector center = Position(task);
            int scale = Word(task, 28);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = new(center.X, center.Y, center.Z, 0), Scale = scale, DepthOffset = 0, SpriteFlags = 0 };
            NativeBattleActionMatrix shadow = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, View, owner.NativeCamera!.Roll);
            NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 24)));
            NativeBattleActionMatrix matrix = shadow with { Rotation = NativeBattleGfMatrices.Multiply56C090(shadow, spin).Rotation };
            int age = Word(task, 12);
            int? fade = age >= 91 ? (age - 91) << 10 : null;
            Mesh(resources.Energy, matrix, fade is null ? 0x33u : 0xF3u, 0, fade, 13);
            if (DrawOnly)
            {
                return 0;
            }
            task = NativeBattleUltima149Motion.Energy(task).WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 95 ? 2 : 0;
        }

        private int Ring(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 12);
            int scale = Word(task, 28);
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(Place(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Position(task)), scale, scale, scale);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(View, world);
            state = state with { RingScale = scale };
            Mesh(resources.Ring, matrix, 0xC0, 0xCEE8CE, resources.RingFades[age & 15]);
            Append(NativeBattleUltima149Dome.Draw(resources.Dome, matrix, Projection, Colors, age));
            if (DrawOnly)
            {
                return 0;
            }
            short step = Word(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(scale + step))).WriteWord(12, unchecked((ushort)(age + 1))).WriteWord(30, unchecked((ushort)(step - step / 20)));
            return age + 1 >= 62 ? 2 : 0;
        }

        private int Core(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 12);
            int index = Word(task, 14);
            if ((uint)index >= 2)
            {
                throw new InvalidDataException("Native Ultima core leaves its two actual morph buffers.");
            }
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(Place(NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Position(task)), state.RingScale, state.RingScale, state.RingScale);
            ImmutableArray<byte> vertices = NativeBattleUltima149Motion.MorphCore(resources.Core, resources.CoreShape1, state.CoreVertices.Slice(index * 960, 960), age);
            byte[] retained = state.CoreVertices.ToArray();
            vertices.AsSpan().CopyTo(retained.AsSpan(index * 960, 960));
            state = state with { CoreVertices = retained.ToImmutableArray() };
            int? fade = age < 4 ? (4 - age) << 10 : age >= 32 ? (age - 32) << 9 : null;
            Mesh(resources.Core, NativeBattleGfMatrices.Compose56C2F0(View, world), fade is null ? 0x2033u : 0x20F3u, 0, fade, 2, vertices);
            if (DrawOnly)
            {
                return 0;
            }
            short spin = Word(task, 26);
            task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + spin))).WriteWord(12, unchecked((ushort)(age + 1))).WriteWord(26, unchecked((ushort)(spin - (spin >> 4))));
            return age + 1 >= 40 ? 2 : 0;
        }
    }
}
