using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeUltima149Runner
    {
        private NativeBattleActionHeaderSlot Header(int action) => owner.nativeActionPackets.Headers[effect.Parameters!.HeaderIndex7C + action];
        private ImmutableArray<NativeBattleContactPacket> Contacts(int action)
        {
            NativeBattleActionHeaderSlot header = Header(action);
            int first = header.FirstContactIndex ?? throw new InvalidDataException("Native Ultima requires its original target contact cursor.");
            int count = header.ReadByte(16);
            if (count == 0)
            {
                throw new InvalidDataException("Native Ultima cannot average an empty native target list.");
            }
            return Enumerable.Range(0, count).Select(index => owner.nativeActionPackets.ReadContact(first + index)).ToImmutableArray();
        }
        private NativeBattleVector Average(int action)
        {
            ImmutableArray<NativeBattleContactPacket> contacts = Contacts(action);
            short x = 0;
            short y = 0;
            short z = 0;
            foreach (NativeBattleContactPacket contact in contacts)
            {
                NativeBattleVector point = owner.ReadNativeDefaultPosition571400(graphics, contact.Primary.ActorIndex);
                x = unchecked((short)(x + point.X));
                y = unchecked((short)(y + point.Y));
                z = unchecked((short)(z + point.Z));
            }
            return new((short)(x / contacts.Length), (short)(y / contacts.Length), (short)(z / contacts.Length));
        }
        private int Part(uint callback, NativeBattleModulePoolTask director, bool swap)
        {
            int slot = Allocate(1, callback);
            NativeBattleModulePoolTask task = pools[1].Slots[slot].WriteWord(12, 0);
            task = task.WriteDword(16, director.ReadDword(16)).WriteDword(20, director.ReadDword(20));
            if (swap)
            {
                ushort y = task.ReadWord(18);
                task = task.WriteWord(18, task.ReadWord(22)).WriteWord(22, y);
            }
            Put(1, slot, task);
            return slot;
        }
        private int Director(ref NativeBattleModulePoolTask task)
        {
            if (DrawOnly)
            {
                return 0;
            }
            int age = Word(task, 12);
            int action = Word(task, 14);
            if (age == 0)
            {
                NativeBattleVector point = Average(action);
                task = task.WriteWord(16, unchecked((ushort)point.X)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)point.Z)).WriteWord(22, unchecked((ushort)-1700)).WriteWord(32, Contacts(action)[0].Primary.ActorIndex).WriteWord(34, 0);
                NativeVibrationProcessState process = owner.NativeVibrationProcess ?? throw new NotSupportedException("Native Ultima requires its current vibration process.");
                NativeVibrationEnqueue queued = process.Enqueue(resources.Vibration, 1, 128);
                owner.NativeVibrationProcess = queued.State;
                state = state with { VibrationHandle = queued.Handle };
                SpawnSideFade(task, false, 24);
            }
            if (age == 1)
            {
                Part(0x60FC90, task, false);
                foreach ((int spin, int acceleration) in new[] { (40, 12), (-40, 25) })
                {
                    int slot = Part(0x610700, task, true);
                    Put(1, slot, pools[1].Slots[slot].WriteWord(24, (ushort)(Random() % 4096)).WriteWord(26, unchecked((ushort)spin)).WriteWord(28, 0).WriteWord(30, (ushort)acceleration));
                }
            }
            if (age == 10)
            {
                Part(0x60FF20, task, true);
            }
            if (age == 35)
            {
                Part(0x610310, task, true);
            }
            if (age == 94)
            {
                int slot = Part(0x610840, task, false);
                Put(1, slot, pools[1].Slots[slot].WriteWord(24, (ushort)(Random() % 4096)).WriteWord(28, 4096).WriteWord(30, 585));
                Part(0x6115D0, task, false);
            }
            if (age == 95)
            {
                Part(0x6118A0, task, false);
            }
            if (age == 100)
            {
                int slot = Part(0x6113A0, task, false);
                Put(1, slot, pools[1].Slots[slot].WriteWord(14, 0).WriteWord(24, (ushort)(Random() % 4096)).WriteWord(26, 200));
            }
            if (age == 160)
            {
                SpawnSideFade(task, true, 8);
            }
            int last = Header(0).ReadByte(17);
            if (action == 0 && age <= 24)
            {
                Stage(age == 24 ? null : age * 170, age == 24 ? false : null);
            }
            else if (action == last && age >= 160)
            {
                Stage(age == 160 ? null : (168 - age) << 9, age == 160 ? true : null);
            }
            if (age == 91)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(3, 1, 8, 255) };
            }
            if (age == 138)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(15, 8, 10, 255) };
            }
            if (age == 166)
            {
                graphics = owner.ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Contacts(action) });
            }
            if (age == 167 && action + 1 <= last)
            {
                int slot = Allocate(1, 0x60F770);
                Put(1, slot, pools[1].Slots[slot].WriteWord(12, 0).WriteWord(14, (ushort)(action + 1)));
            }
            if (age is 1 or 94)
            {
                NativeBattleVector point = Average(action);
                owner.PublishNativeModuleSound(age == 1 ? resources.StartSound : resources.BlastSound, 1, new(point.X, point.Y, point.Z, 0));
            }
            if (age == 165)
            {
                NativeVibrationProcessState process = owner.NativeVibrationProcess ?? throw new NotSupportedException("Native Ultima requires its retained vibration process.");
                if (process.IsPlaying(state.VibrationHandle))
                {
                    owner.NativeVibrationProcess = process.Stop4A2940(state.VibrationHandle);
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age < 168)
            {
                return 0;
            }
            if (action == last)
            {
                Stage(0, null);
            }
            return 2;
        }
        private void Stage(int? depth, bool? enabled)
        {
            NativeBattleStageState stage = owner.NativeStage ?? throw new NotSupportedException("Native Ultima requires its actual four stage rows.");
            stage.Validate();
            owner.NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth is int value ? unchecked((short)value) : group.ColorDepth, ColorFlags = depth is not null ? 0u : group.ColorFlags, Flags = enabled is bool set ? set ? (byte)(group.Flags | 2) : (byte)(group.Flags & 0xFD) : group.Flags }).ToImmutableArray() };
        }
        private void SpawnSideFade(NativeBattleModulePoolTask director, bool incoming, int duration)
        {
            int target = Word(director, 32);
            int first = target >= 3 ? 0 : 3;
            int end = target >= 3 ? 3 : 7;
            for (int actor = first; actor < end; actor++)
            {
                ushort flags = graphics.Models[actor].Flags0;
                bool selected = incoming ? state.SideFlags[actor] != 0 : (flags & 2) != 0 && (flags & 4) == 0;
                if (!incoming)
                {
                    state = state with { SideFlags = state.SideFlags.SetItem(actor, selected ? 1u : 0u) };
                }
                if (!selected)
                {
                    continue;
                }
                int slot = Allocate(1, incoming ? 0x611C20u : 0x611E10u);
                Put(1, slot, pools[1].Slots[slot].WriteWord(12, 0).WriteWord(14, (ushort)actor).WriteWord(24, (ushort)duration).WriteWord(26, (ushort)(flags & 0x800)));
            }
        }
        private int SideFade(ref NativeBattleModulePoolTask task, bool incoming)
        {
            int actor = Word(task, 14);
            if ((uint)actor >= 7)
            {
                throw new InvalidDataException("Native Ultima side fade leaves its physical actor bank.");
            }
            int age = Word(task, 12);
            int duration = Word(task, 24);
            if (duration == 0)
            {
                throw new InvalidDataException("Native Ultima side fade has a zero native denominator.");
            }
            int level = incoming ? (age << 12) / duration : 4096 - (age << 12) / duration;
            uint transition = Colors.Transition;
            uint half = (transition & 0xFEFEFE) >> 1;
            uint color44 = Colors.Interpolate(0x32000000, level, Colors.Normal);
            uint color40 = (Colors.Interpolate(0, level, incoming ? transition : half) & 0xFFFFFF) | 0x02000000;
            NativeBattleGraphicsModel model = graphics.Models[actor] with { Color44 = color44, Color40 = color40 };
            if (!incoming)
            {
                model = model with { Alpha7 = 0 };
            }
            if (!DrawOnly)
            {
                if (age == 0)
                {
                    model = model with { Flags0 = incoming ? (ushort)((model.Flags0 & 0xFFFB) | 0x800) : (ushort)(model.Flags0 | 0x800) };
                }
                task = task.WriteWord(12, unchecked((ushort)(age + 1)));
                if (age + 1 >= duration)
                {
                    model = model with { Flags0 = (ushort)((model.Flags0 & 0xF7FF) | task.ReadWord(26) | (incoming ? 0 : 4)) };
                    if (incoming)
                    {
                        model = model with { Color40 = Colors.Transition };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                    return 2;
                }
            }
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            return 0;
        }
    }
}
