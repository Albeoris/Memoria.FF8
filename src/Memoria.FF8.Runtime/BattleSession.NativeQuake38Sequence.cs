using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeQuake38Execution
    {
        private bool TargetsParty => state.Word(0x278C9B0) != 0;
        private IEnumerable<int> FadedActors => TargetsParty ? Enumerable.Range(3, 4) : Enumerable.Range(0, 3);
        private IEnumerable<int> TargetActors => TargetsParty ? Enumerable.Range(0, 3) : Enumerable.Range(3, 4);
        private ImmutableArray<uint> SavedFlags => Enumerable.Range(0, 7).Select(index => state.Dword(checked(0x278C8F0u + (uint)index * 4))).ToImmutableArray();
        private NativeBattleStageColors Colors => session.NativeStage?.ModelColors ?? throw new NotSupportedException("Native Quake requires its current original stage model colors.");

        private void Sequence(uint address, ref NativeBattleModulePoolTask task)
        {
            switch (unchecked((sbyte)task.ReadByte(41)))
            {
                case 0:
                    InitializeGround(address, task);
                    task = Next(task);
                    break;
                case 1:
                    DrawGround();
                    task = Next(task.WriteWord(48, 16));
                    break;
                case 2:
                    DrawGround();
                    task = SetWord(task, 48, Word(task, 48) - 1);
                    if (Word(task, 48) < 0)
                    {
                        task = Next(task);
                    }
                    break;
                case 3:
                    for (int actor = 0; actor < 7; actor++)
                    {
                        state = state.SetDword(checked(0x278C8F0u + (uint)actor * 4), graphics.Models[actor].Flags0);
                    }
                    task = task.WriteWord(28, 0);
                    foreach (int actor in FadedActors)
                    {
                        if ((SavedFlags[actor] & 4) == 0)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[actor];
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x800), Color40 = (Colors.Color40 & 0xFFFFFFu) | 0x02000000u }) };
                        }
                    }
                    DrawGround();
                    task = Next(task);
                    break;
                case 4:
                    task = SetWord(task, 28, Word(task, 28) + 512);
                    if (Word(task, 28) >= 4096)
                    {
                        task = task.WriteWord(28, 4096);
                        state = state.SetWord(0x2792E20, 1);
                        Sound(resources.RumbleSound, 0);
                        task = Next(task);
                    }
                    Fade(Word(task, 28));
                    DrawGround();
                    break;
                case 5:
                    foreach (int actor in FadedActors)
                    {
                        if ((SavedFlags[actor] & 4) == 0)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[actor];
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 4) }) };
                        }
                    }
                    foreach (int actor in TargetActors)
                    {
                        if ((SavedFlags[actor] & 4) == 0)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[actor];
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x1020) }) };
                        }
                    }
                    task = Next(task);
                    DrawGround();
                    break;
                case 6:
                    if (state.Word(0x279299E) == 5)
                    {
                        state.SetSavedActors(graphics.Models.Select(NativeBattleQuake38SavedActor.Capture8DBBA0).ToImmutableArray());
                        Sound(resources.QuakeSound, 1);
                        task = Next(task);
                    }
                    DrawGround();
                    break;
                case 7:
                    NativeBattleActionHeaderSlot header = Header(task);
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Quake bounce requires its actual target packet list.");
                    foreach (int actor in TargetActors)
                    {
                        if ((SavedFlags[actor] & 4) != 0)
                        {
                            continue;
                        }
                        bool hit = Enumerable.Range(0, header.ReadByte(16)).Any(index => session.nativeActionPackets.ReadContact(first + index).Primary.ActorIndex == actor);
                        NativeBattleQuake38ActorBounceStep bounce = NativeBattleQuake38ActorBounce.Advance8DBCA0(graphics.Models[actor], state.SavedActors[actor], state, resources, RandomState, hit);
                        RandomState = bounce.Random;
                        NativeBattleGraphicsModel model = bounce.Model;
                        if (bounce.ModelSequence is { } sequence)
                        {
                            model = QueueNativeModelSequence(model, (byte)sequence);
                        }
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                        state.SetSavedActor(actor, bounce.Saved);
                    }
                    DrawGround();
                    if (state.Word(0x279299E) == 7)
                    {
                        for (int actor = 0; actor < 7; actor++)
                        {
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, state.SavedActors[actor].Restore8DBE40(graphics.Models[actor])) };
                        }
                        task = Next(task);
                    }
                    break;
                case 8:
                    task = task.WriteWord(28, 4096);
                    for (int actor = 0; actor < 7; actor++)
                    {
                        NativeBattleGraphicsModel model = graphics.Models[actor];
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = NativeBattleQuake38ActorFlags.Restore8DBE80(actor, model.Flags0, SavedFlags, TargetsParty) }) };
                    }
                    task = Next(task);
                    break;
                case 9:
                    task = SetWord(task, 28, Word(task, 28) - 256);
                    if (Word(task, 28) <= 0)
                    {
                        task = Next(task.WriteWord(28, 0));
                    }
                    Fade(Word(task, 28));
                    break;
                case 10:
                    foreach (int actor in FadedActors)
                    {
                        if ((SavedFlags[actor] & 4) == 0)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[actor];
                            graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = NativeBattleQuake38ActorFlags.Restore8DC180(actor, model.Flags0, SavedFlags, TargetsParty), Color40 = Colors.Color40, Color44 = Colors.Color44 }) };
                        }
                    }
                    task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
                    break;
                case 11:
                    break;
                default:
                    throw new NotSupportedException("Native Quake sequence phase leaves its original table.");
            }
        }

        private void InitializeGround(uint address, NativeBattleModulePoolTask task)
        {
            state = state.SetDword(0x27929C8, address).SetWord(0x278C9B0, (short)(task.ReadByte(45) < 3 ? 1 : 0));
            foreach (uint field in new uint[] { 0x2792E06, 0x2792E2E })
            {
                state = state.SetWord(field, -8192);
            }
            foreach (uint field in new uint[] { 0x278C8E8, 0x278C8E6, 0x2792E04, 0x2792E2C })
            {
                state = state.SetWord(field, 8192);
            }
            foreach (uint field in new uint[] { 0x2792D6E, 0x279299E, 0x2792E20, 0x278C8EA, 0x2792D70, 0x2792B40, 0x279299C, 0x2792D6C, 0x2792E18, 0x2792D6A })
            {
                state = state.SetWord(field, 0);
            }
            Memory = Memory with { Arena = Memory.Arena!.Write(0x28000, new byte[0xB600]).Write(0x33600, new byte[0xCC0]) };
            state = state.Write(0x27929A0, NativeBattleQuake38Templates.Crack8D88C0(state.Read(0x27929A0, 40)).AsSpan());
            state = state.Write(0x2792E38, NativeBattleQuake38Templates.Wall8D88C0(state.Read(0x2792E38, 40)).AsSpan());
        }

        private void Fade(int fade)
        {
            foreach (int actor in FadedActors)
            {
                if ((SavedFlags[actor] & 4) == 0)
                {
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Color40 = FadeRgb(model.Color40 ?? throw new NotSupportedException("Native Quake fade requires its current actor transition color."), Colors.Color40, fade), Color44 = FadeRgb(model.Color44 ?? throw new NotSupportedException("Native Quake fade requires its current actor normal color."), Colors.Color44, fade) }) };
                }
            }
        }

        private static uint FadeRgb(uint current, uint original, int fade)
        {
            uint result = current & 0xFF000000;
            for (int shift = 0; shift < 24; shift += 8)
            {
                int color = (byte)(original >> shift);
                result |= (uint)(color - fade * color / 4096) << shift;
            }
            return result;
        }
    }
}
