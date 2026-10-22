using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    private sealed partial class Execution
    {
        private void Creature(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            NativeBattleEffectOwnedModelState model = State.Models.Single(value => value.TaskAddress == address);
            switch (Stage(task))
            {
                case 0:
                    task = task.WriteDword(0x4C, Dword(0x27423F8)).WriteDword(0x50, Dword(0x27423FC)).WriteWord(0x3E, Word(0x2742442)).WriteDword(0x60, address + 0x114).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                    Word(0x2742858, 0);
                    if (task.ReadByte(0x2E) == 0)
                    {
                        Select(5);
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8));
                        Store(address, task);
                        SpawnActors(address, 0x1801FD0, 2);
                        Append(5, 0x8BD580, 0x70, address);
                        Emit(new NativeBattleDeathSound(resources.AppearanceSound));
                        task = Next(Read(address));
                    }
                    else
                    {
                        Select(6);
                        task = Recolor(task.WriteWord(0x13A, 4096).WriteWord(0x13C, 4096));
                        Store(address, task);
                        Emit(new NativeBattleDeathSound(resources.StrikeSound));
                        task = Next(Next(Read(address)));
                    }
                    break;
                case 1:
                    if (task.ReadWord(0x136) == 20)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xF7));
                    }
                    if (Once())
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xF7));
                        Select(6);
                        Store(address, task);
                        Emit(new NativeBattleDeathSound(resources.StrikeSound));
                        task = Next(Read(address));
                    }
                    break;
                case 2:
                    short visibility = unchecked((short)(task.ReadWord(0x13A) - 1024));
                    if (visibility <= 0)
                    {
                        visibility = 0;
                    }
                    task = Recolor(task.WriteWord(0x13A, unchecked((ushort)visibility)).WriteWord(0x13C, unchecked((ushort)visibility)));
                    Store(address, task);
                    if (task.ReadWord(0x136) == 5)
                    {
                        Word(0x2742440, 1);
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[current.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Death requires its actual immunity result row.");
                        NativeBattleContactPacket contact = inputs.Packets.ReadContact(first + unchecked((sbyte)task.ReadByte(0x2B)));
                        if ((contact.Primary.HitFlags3 & 4) == 0)
                        {
                            SpawnActors(address, 0x18069C4, 0);
                            Append(6, 0x8BD840, 0x118, address);
                        }
                    }
                    bool ended = Once();
                    if (task.ReadByte(0x2E) == task.ReadByte(0x2F) ? ended : S(task, 0x136) >= 11)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    _ = Once();
                    short fade = unchecked((short)(task.ReadWord(0x13A) + 512));
                    if (fade >= 4096)
                    {
                        fade = 4096;
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)));
                    }
                    task = Recolor(task.WriteWord(0x13A, unchecked((ushort)fade)).WriteWord(0x13C, unchecked((ushort)fade)));
                    break;
                case 4:
                    Store(address, task);
                    if (task.ReadByte(0x2E) == task.ReadByte(0x2F))
                    {
                        Append(5, 0x8BDF40, 0x70, address);
                    }
                    Word(0x2742444, 1);
                    task = Next(Read(address).WriteWord(0x26, (ushort)(Read(address).ReadWord(0x26) | 1)));
                    break;
                case 5:
                    break;
                default:
                    throw new NotSupportedException("Native Death reaper reached an unproved state.");
            }
            byte[] light = task.Payload.AsSpan().Slice(0x70 - 12, 32).ToArray();
            current = current with { Memory = current.Memory with { EffectLight1D99AB0 = light.ToImmutableArray(), EffectLightKnown1D99AB0 = null } };
            task = task.WriteDword(0x84, unchecked((uint)S(task, 0x4C))).WriteDword(0x88, unchecked((uint)S(task, 0x4E))).WriteDword(0x8C, unchecked((uint)S(task, 0x50)));
            Store(address, task);
            SetState(State with { Models = State.Models.Select(value => value.TaskAddress == address ? model : value).ToImmutableArray() });
            if ((task.ReadByte(0x26) & 4) == 0)
            {
                current = current with { Effect = current.Effect with { Pools = hierarchy.Pools } };
                NativeBattleDeathModelStep draw = inputs.DrawModel(current, task, model);
                current = draw.Frame;
                hierarchy = new(current.Effect.Pools);
                Store(address, draw.Task);
            }

            void Select(short animation)
            {
                NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(task, model, resources.Model, animation);
                task = selected.Task;
                model = selected.State;
            }
            bool Once()
            {
                NativeBattleEffectOwnedModelStep advanced = NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resources.Model);
                task = advanced.Task;
                model = advanced.State;
                return advanced.Completed;
            }
        }

        private NativeBattleModulePoolTask Recolor(NativeBattleModulePoolTask task)
        {
            task = NativeBattleTonberry90Creature.Recolor7678D0(task, S(task, 0x13A), 1, inputs.TransitionColor);
            for (int channel = 0; channel < 3; channel++)
            {
                byte ambient = unchecked((byte)(inputs.DefaultColor >> (8 * channel)));
                int reduction = unchecked(ambient * S(task, 0x13C)) / 4096;
                task = task.WriteByte(0x5C + channel, unchecked((byte)(ambient - reduction)));
            }
            return task;
        }

        private void SpawnActors(uint parent, uint source, ushort mode)
        {
            uint actor = Append(4, 0x8B78C0, 0x2A4, parent);
            Store(actor, Read(actor).WriteDword(0x30, source == 0x1801FD0 ? profile.AuraAddress : source == 0x18069C4 ? profile.DarkAddress : throw new NotSupportedException("Native Death family source has no original counterpart.")).WriteWord(0x298, 0).WriteWord(0x29C, mode).WriteWord(0x29E, 20));
        }
    }
}

