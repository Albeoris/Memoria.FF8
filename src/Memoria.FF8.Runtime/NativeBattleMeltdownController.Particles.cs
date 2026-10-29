namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltdownController
{
    private sealed partial class Execution
    {
        private bool Particles(ref NativeBattleModulePoolTask task, bool second)
        {
            int age = S(task, 12);
            uint first = second ? 0x2465060u : 0x2464BB0u;
            uint end = first + 60 * 20;
            NativeBattleActionMatrix action = Matrix(ActionFrame(S(task, 14)));
            NativeBattleModuleResource resource = second ? resources.AcidB : resources.AcidA;
            for (uint particle = first; particle < end; particle += 20)
            {
                int frame = W(particle);
                if (frame < 0)
                {
                    continue;
                }
                NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(action, new(W(particle + 4), W(particle + 6), W(particle + 8)));
                short scale = W(particle + 2);
                NativeBattleActionMatrix matrix = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], Math.Clamp(transformed.X, short.MinValue, short.MaxValue), Math.Clamp(transformed.Y, short.MinValue, short.MaxValue), Math.Clamp(transformed.Z, short.MinValue, short.MaxValue), 4096);
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(148, current.Memory.Meltdown148!.Packets, first, checked((int)((particle - first) / 20))) with { ResourceGeneration = drawingIdentity.Generation };
                Emit(new NativeBattleMeltdownSprite(NativeBattleHoly175Drawing.EmitLoadedSprite(new(resource.Address, resource.Bytes), frame, matrix, inputs.Projection, 0, 0, 0x808080, pilotIdentity: identity, pilotScreenSpace: false)));
                if (inputs.DrawOnly)
                {
                    continue;
                }
                frame++;
                if (!second && frame >= 17)
                {
                    W(particle, -1);
                    continue;
                }
                W(particle, second && frame >= 17 ? 1 : frame);
                for (uint lane = 0; lane < 6; lane += 2)
                {
                    uint velocity = particle + 12 + lane;
                    int speed = W(velocity);
                    if (second && age >= 35)
                    {
                        if (age == 35)
                        {
                            speed = unchecked((short)(speed << 4));
                        }
                        W(particle + 4 + lane, W(particle + 4 + lane) - speed);
                        W(velocity, speed - (speed >> 3));
                    }
                    else
                    {
                        W(particle + 4 + lane, W(particle + 4 + lane) + speed);
                    }
                }
            }
            if (inputs.DrawOnly)
            {
                return false;
            }
            if (age >= 1 && age <= (second ? 18 : 28))
            {
                for (int child = 0; child < 3; child++)
                {
                    uint particle = first;
                    while (particle < end && W(particle) >= 0)
                    {
                        particle += 20;
                    }
                    if (particle == end)
                    {
                        break;
                    }
                    NativeBattlePilotVisualLifetimes.Allocated(current.Memory.Meltdown148!.Packets, first, checked((int)((particle - first) / 20)));
                    W(particle, 0);
                    W(particle + 2, Roll() % (second ? 512 : 1024) + (second ? 512 : 1024));
                    int vx = Roll() % 4096 - 2048;
                    int vy = Roll() % 4096 - 2048;
                    int vz = Roll() % (second ? 1024 : 2048) - (second ? 512 : 0);
                    var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(vx, vy, vz);
                    short x = unchecked((short)normalized.X);
                    short y = unchecked((short)normalized.Y);
                    short z = unchecked((short)normalized.Z);
                    int reach = Roll() % (second ? 500 : 2800) + (second ? 700 : 4200);
                    int displacement = second ? (child + Roll() % 24 + 2 * age - 6) * 550 : 500;
                    W(particle + 4, unchecked(x * reach) >> 12);
                    W(particle + 6, unchecked(y * reach) >> 12);
                    W(particle + 8, (unchecked(z * reach) >> 12) - displacement);
                    int speed;
                    if (second)
                    {
                        speed = Roll() % 40 + 15;
                        if ((speed & 1) != 0)
                        {
                            speed = -speed;
                        }
                    }
                    else
                    {
                        speed = -(reach / 17);
                    }
                    W(particle + 12, unchecked(x * speed) >> 12);
                    W(particle + 14, unchecked(y * speed) >> 12);
                    W(particle + 16, unchecked(z * speed) >> 12);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 60;
        }
    }
}
