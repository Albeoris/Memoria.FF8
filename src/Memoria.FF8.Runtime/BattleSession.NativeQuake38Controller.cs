namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private void RootPhase(uint address, ref NativeBattleModulePoolTask task)
        {
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    task = Next(task);
                    break;
                case 1:
                    task = task.WriteByte(99, 1);
                    hierarchy = hierarchy.Write(address, task);
                    Spawn(address, 1, 0x8D8720, 88);
                    task = Next(hierarchy.Read(address));
                    break;
                case 2:
                    if (task.ReadByte(99) == 0)
                    {
                        sbyte group = unchecked((sbyte)task.ReadByte(42));
                        if (group >= Word(task, 88))
                        {
                            task = Next(task);
                        }
                        else
                        {
                            task = task.WriteByte(42, unchecked((byte)(group + 1))).WriteByte(41, unchecked((byte)(task.ReadByte(41) - 1)));
                        }
                    }
                    break;
                case 3:
                    if (task.ReadWord(94) == 0)
                    {
                        task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Quake root phase leaves its original table.");
            }
        }

        private void Emitter(uint address, ref NativeBattleModulePoolTask task)
        {
            task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(task, graphics.Models, ReadPose);
            int target = task.ReadByte(45);
            NativeBattlePoseSource source = ReadPose(target);
            NativeBattleSequenceState pose = graphics.Models[target].SequenceState ?? throw new NotSupportedException("Native Quake emitter requires its target's decoded skeleton owner.");
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, source.Sample(pose.Pose ?? throw new NotSupportedException("Native Quake target lacks its selected pose."), pose.PoseFrame114 ?? throw new NotSupportedException("Native Quake target lacks its decoded pose frame."), pose.PoseFlags109));
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    NativeBattleStageGroup ground = session.NativeStage!.Groups[1];
                    bool exists = (ground.Flags & 1) != 0 && session.RequireNativeStageGeometry().Any(mesh => mesh.StageGroup == 1);
                    hierarchy = hierarchy.Write(address, task);
                    if (exists)
                    {
                        Spawn(address, 2, 0x8D8800, 52);
                        Spawn(address, 2, 0x8DC250, 52);
                    }
                    else
                    {
                        Spawn(address, 2, 0x8DC360, 52);
                    }
                    task = Next(hierarchy.Read(address));
                    break;
                case 1:
                    if (task.ReadByte(40) == 0)
                    {
                        NativeBattleActionHeaderSlot header = Header(task);
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Quake contact requires its actual target packet list.");
                        for (int index = 0; index < header.ReadByte(16); index++)
                        {
                            var pending = graphics.PendingContacts;
                            graphics = session.ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [session.nativeActionPackets.ReadContact(first + index)] });
                            graphics = graphics with { PendingContacts = pending };
                        }
                        uint root = task.ReadDword(16);
                        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(99, 0));
                        task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    }
                    break;
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Native Quake emitter phase leaves its original table.");
            }
        }

        private void Shake(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 36);
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    if (age > 2)
                    {
                        task = Next(task);
                    }
                    break;
                case 1:
                    if ((uint)age >= (uint)resources.ShakeA.Length)
                    {
                        throw new InvalidDataException("Native Quake camera shake leaves its original table.");
                    }
                    short value = resources.ShakeA[age];
                    if (value == 127)
                    {
                        task = Next(task);
                    }
                    else
                    {
                        ShakeY(value);
                    }
                    break;
                case 2:
                    if (state.Word(0x279299E) == 5)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    ShakeY(resources.ShakeB[task.ReadByte(36) & 15]);
                    if (state.Word(0x279299E) == 7)
                    {
                        task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    }
                    break;
                case 4:
                    break;
                default:
                    throw new NotSupportedException("Native Quake shake phase leaves its original table.");
            }
        }

        private void Fallback(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 36);
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    Sound(resources.RumbleSound, 0);
                    task = Next(task);
                    break;
                case 1:
                    if (age >= 48)
                    {
                        Sound(resources.QuakeSound, 1);
                        task = Next(task);
                    }
                    break;
                case 2:
                    ShakeY(resources.ShakeB[age & 15]);
                    if (age >= 96)
                    {
                        task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new NotSupportedException("Native Quake fallback phase leaves its original table.");
            }
        }

        private NativeBattleActionHeaderSlot Header(NativeBattleModulePoolTask task)
        {
            int index = checked(effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(42)));
            return session.nativeActionPackets.Headers[index];
        }

        private void ShakeY(short value)
        {
            NativeBattleCameraState camera = session.NativeCamera!;
            NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Native Quake requires its actual camera translation scratch owner.");
            session.NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = value } };
        }
    }
}
