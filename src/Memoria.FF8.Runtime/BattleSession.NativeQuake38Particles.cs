using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private void Debris(ref NativeBattleModulePoolTask task)
        {
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    NativeBattleQuake38ParticleBirth birth = NativeBattleQuake38ParticleMotion.Initialize8DAD50(task, RandomState);
                    task = birth.Task;
                    RandomState = birth.Random;
                    break;
                case 1:
                    NativeBattleQuake38DebrisStep step = NativeBattleQuake38ParticleMotion.Advance8DADA0(task, state);
                    task = step.Task;
                    state = step.State;
                    break;
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Native Quake debris phase leaves its original table.");
            }
            Emit(NativeBattleQuake38DebrisPackets.Emit8DA960(task, Projection, Zsf3, Zsf4));
        }

        private void Sprite(ref NativeBattleModulePoolTask task)
        {
            int phase = unchecked((sbyte)task.ReadByte(41));
            if (phase == 0)
            {
                NativeBattleQuake38ParticleBirth birth = task.Callback switch
                {
                    0x8DB030 => NativeBattleQuake38ParticleMotion.Initialize8DB160(task, RandomState),
                    0x8DB410 => NativeBattleQuake38ParticleMotion.Initialize8DB4D0(task, RandomState),
                    0x8DB630 => NativeBattleQuake38ParticleMotion.Initialize8DB690(task, RandomState),
                    _ => throw new NotSupportedException("Native Quake sprite callback is unknown.")
                };
                task = birth.Task;
                RandomState = birth.Random;
            }
            else if (phase == 1)
            {
                if (task.Callback == 0x8DB630)
                {
                    task = NativeBattleQuake38ParticleMotion.ChipPosition8DB6F0(task);
                }
                short frame = unchecked((short)(Word(task, 80) + 1));
                short maximum = Word(task, 82);
                task = SetWord(task, 80, frame);
                if (frame > maximum)
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5)).WriteWord(80, unchecked((ushort)maximum));
                    uint counter = task.Callback == 0x8DB030 ? 0x2792D6Cu : task.Callback == 0x8DB410 ? 0x2792E18u : 0x2792D6Au;
                    state = state.SetWord(counter, unchecked((short)(state.Word(counter) - 1)));
                    if (task.Callback == 0x8DB630)
                    {
                        uint address = task.ReadDword(168);
                        if (address < 0x20DFAB8 || address >= 0x21DFAB8)
                        {
                            throw new InvalidDataException("Native Quake chip completion leaves its original arena counter byte.");
                        }
                        int offset = checked((int)(address - 0x20DFAB8));
                        byte value = Memory.Arena!.ReadByte(offset);
                        Memory.Arena.WriteByte(offset, unchecked((byte)(value - 1)));
                    }
                    task = Next(task);
                }
            }
            else if (phase != 2)
            {
                throw new NotSupportedException("Native Quake sprite phase leaves its original table.");
            }
            if (task.Callback == 0x8DB630)
            {
                DrawSprite(task);
                return;
            }
            bool dust = task.Callback == 0x8DB410;
            task = dust ? NativeBattleQuake38ParticleMotion.DustVelocity8DB410(task) : NativeBattleQuake38ParticleMotion.RockVelocity8DB030(task);
            for (int index = 0; index < 6; index++)
            {
                int row = 88 + index * 12;
                if (task.ReadWord(row + 8) != 1)
                {
                    continue;
                }
                if (dust)
                {
                    task = SetWord(task, row, Word(task, row) + Word(task, 160));
                    task = SetWord(task, row + 4, Word(task, row + 4) + Word(task, 164));
                }
                task = SetWord(task, row + 2, Word(task, row + 2) + Word(task, 162));
                task = Vector(task, 28, new(Word(task, row), Word(task, row + 2), Word(task, row + 4))).MarkPayloadUnknown(34, 2);
                DrawSprite(task, index);
            }
        }

        private void DrawSprite(NativeBattleModulePoolTask task, int primitive = 0)
        {
            if ((task.ReadByte(38) & 4) != 0)
            {
                return;
            }
            NativeBattleModuleResource resource = task.ReadDword(76) switch
            {
                0x1643218 => resources.Chip,
                0x164335C => resources.Dust,
                0x16434A0 => resources.Rock,
                _ => throw new NotSupportedException("Native Quake sprite requires its actual selected original resource.")
            };
            short frame = Word(task, 80);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(38, task, primitive), Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(frame), Phase = unchecked((ushort)frame), Anchor = new(Word(task, 28), Word(task, 30), Word(task, 32), 0), Scale = 4096, DepthOffset = Word(task, 84), SpriteFlags = 0 };
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(session.NativeFileStaging!, 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resource.Address, 0).Write(temporary.Address + 4, BitConverter.GetBytes(unchecked((ushort)frame)));
            NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, Projection, session.NativeCamera!.Roll, 17, 2);
            session.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 180);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(Memory, cursor, graphics.PrimitiveFrame!.Pending, drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
            Memory = written.Memory;
            cursor = written.Cursor;
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            state = state.SetDword(0x278C8E0, checked(0x20DFAB8u + (uint)cursor));
        }
    }
}
