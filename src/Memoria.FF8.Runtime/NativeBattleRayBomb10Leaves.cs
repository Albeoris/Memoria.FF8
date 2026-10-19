namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRayBomb10Controller
{
    private sealed partial class Execution
    {
        public int Advance(int index)
        {
            return Read(index).Callback switch
            {
                0x627F10 => Director(index),
                0x628700 => Debris(index),
                0x628870 => Ring(index),
                0x6289B0 => Puff(index),
                0x628B00 => Beam(index),
                0x628F60 => Spark(index),
                0x629000 => Flash(index),
                0x6290F0 => Charge(index),
                0x629200 => Bounce(index),
                _ => throw new NotSupportedException("Native Ray-Bomb child queue requires its selected original callback.")
            };
        }

        private static NativeBattleModulePoolTask W(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
        private void Mesh(uint resource, NativeBattleActionMatrix matrix, bool camera, uint flags, int? fade = null, uint? color = null) => Execute(new NativeBattleRayBomb10Mesh(resource, matrix, camera, flags, fade, color));

        private int Debris(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            int life = Word(task, 14);
            bool fading = counter >= life - 8;
            int model = Word(task, 24);
            uint resource = model switch
            {
                0 => 0xDEC13C,
                1 => 0xDEC3F4,
                _ => throw new NotSupportedException("Native628700 model selector leaves its two original resources.")
            };
            int scale = Word(task, 26);
            Mesh(resource, World(task, new(Word(task, 28), 0, Word(task, 32)), scale, scale, scale), false, fading ? 0xC3u : 0, fading ? (counter - life + 8) << 9 : null, fading ? 0u : null);
            if (DrawOnly)
            {
                return 0;
            }
            short velocity = Word(task, 22);
            task = Add(task, 18, velocity);
            task = Add(task, 32, Word(task, 34));
            task = W(task, 22, velocity - velocity / 9);
            task = Add(task, 28, Word(task, 30));
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= life ? 2 : 0;
        }

        private int Ring(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            bool fading = counter >= 10;
            Mesh(0xDEAD64, World(task, new(0, 0, 0), Word(task, 28), Word(task, 32), Word(task, 28)), false, fading ? 0xF0u : 0x30u, fading ? (counter - 10) * 0x124 : null, fading ? 0u : null);
            if (DrawOnly)
            {
                return 0;
            }
            short step = Word(task, 34);
            task = Add(task, 32, step);
            task = W(task, 34, step - (step >> 1));
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= 24 ? 2 : 0;
        }

        private bool Delay(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            if (Word(task, 14) <= 0)
            {
                return false;
            }
            if (!DrawOnly)
            {
                Put(index, Add(task, 14, -1));
            }
            return true;
        }

        private int Puff(int index)
        {
            if (Delay(index))
            {
                return 0;
            }
            NativeBattleModulePoolTask task = Read(index);
            int life = Word(task, 32);
            byte level = unchecked((byte)(128 - unchecked((byte)((128 / life) * Word(task, 12)))));
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(new(NativeBattlePoseMatrices.RotationWords(new(0x400, Word(task, 24), 0)), Word(task, 16), Word(task, 18), Word(task, 20), 4096), Word(task, 28), Word(task, 28), Word(task, 28));
            Execute(new NativeBattleRayBomb10WorldSprite(0xDEA9E4, 0, world, 0xC, (uint)(level * 0x10101)));
            if (DrawOnly)
            {
                return 0;
            }
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= life ? 2 : 0;
        }

        private int Beam(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            short counter = Word(task, 12);
            NativeBattleRayBomb10BeamGeometry geometry = NativeBattleRayBomb10Geometry.Beam628B00(new(Word(task, 16), Word(task, 18), Word(task, 20)), Process.Muzzle, inputs.Eye, Word(task, 24), Word(task, 32), counter);
            bool fading = counter >= 3;
            Mesh(0xDED00C, geometry.World, false, fading ? 0xF3u : 0x33u, fading ? (counter - 3) * 0x2AA : null, fading ? 0u : null);
            if (DrawOnly)
            {
                return 0;
            }
            short step = Word(task, 26);
            task = Add(task, 24, step);
            task = W(task, 26, step + (step >> 4));
            Put(index, task);
            if (counter >= 2)
            {
                int count = Rand() % 5 + 3;
                for (int ordinal = 0; ordinal < count; ordinal++)
                {
                    int spark = Append(0x628F60);
                    NativeBattleModulePoolTask child = Point(Read(spark), geometry.End);
                    child = W(child, 12, 0);
                    child = W(child, 14, (Rand() & 7) + 8);
                    child = Add(child, 16, Rand() % 1000 - 500);
                    child = W(child, 18, 0);
                    child = Add(child, 20, Rand() % 900 - 400);
                    child = W(child, 22, -10 - Rand() % 20);
                    child = W(child, 28, Rand() % 0x700 + 0xC00);
                    Put(spark, child);
                }
            }
            task = Add(Read(index), 12, 1);
            Put(index, task);
            return Word(task, 12) > 9 ? 2 : 0;
        }

        private int Spark(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int scale = Word(task, 28);
            Sprite(0xDEAA0C, Word(task, 12), Position(task), scale, -(scale >> 3));
            if (DrawOnly)
            {
                return 0;
            }
            task = Add(task, 18, Word(task, 22));
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= Word(task, 14) ? 2 : 0;
        }

        private int Flash(int index)
        {
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            short scale = Word(task, 28);
            int? fade = counter < 16 ? (16 - counter) << 8 : counter >= 30 ? (counter - 30) * 0x2AA : null;
            Mesh(0xDEC964, Billboard(Process.Muzzle, scale, -(scale >> 3)), true, fade.HasValue ? 0xC0u : 0, fade, 0);
            if (DrawOnly)
            {
                return 0;
            }
            if (counter < 18)
            {
                short step = Word(task, 30);
                task = Add(task, 28, -step);
                task = W(task, 30, step + (step >> 5));
            }
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= 36 ? 2 : 0;
        }

        private int Charge(int index)
        {
            if (Delay(index))
            {
                return 0;
            }
            NativeBattleModulePoolTask task = Read(index);
            int counter = Word(task, 12);
            int scale = Word(task, 28);
            bool fading = counter < 8 || counter >= 22;
            byte level = counter < 8 ? unchecked((byte)(counter << 4)) : unchecked((byte)(0xE0 - unchecked((byte)(counter << 4))));
            NativeBattleModuleSpriteFrame image = Sprite(0xDEA838, Word(task, 32), Process.Muzzle, scale, -(scale >> 4), fading ? (ushort)5 : (ushort)1, fading ? (uint)(level * 0x10101) : 0x808080, Word(task, 24));
            if (DrawOnly)
            {
                return 0;
            }
            task = W(task, 32, image.NextOffset < 0 ? 0 : Word(task, 32) + 1);
            task = Add(task, 28, Word(task, 30));
            task = Add(task, 12, 1);
            Put(index, task);
            return Word(task, 12) >= 30 ? 2 : 0;
        }

        private static NativeBattleModulePoolTask Point(NativeBattleModulePoolTask task, NativeBattleModuleAnchor point)
        {
            task = W(task, 16, point.X);
            task = W(task, 18, point.Y);
            task = W(task, 20, point.Z);
            return W(task, 22, point.W);
        }
    }
}
