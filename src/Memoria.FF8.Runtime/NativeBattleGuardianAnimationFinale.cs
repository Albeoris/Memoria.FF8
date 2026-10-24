using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGuardianAnimationFinale
{
    public static bool IsSupported(int effect) => effect is 6 or 90 or 95 or 116 or 185 or 191 or 199 or 201 or 202 or 203 or 204 or 205 or 206 or 278 or 291 or 325;

    public static bool HasReachedFinale(NativeBattleOriginalEffectState effect, NativeBattleEffectMemory memory)
    {
        return effect.Binding.Effect switch
        {
            // The final arena animation runs from phase 319 through 354, with contact at 345.
            116 => ActiveTasks(0x2521738).Any(task => task.Callback == 0x6C3940 && task.ReadWord(0x0C) >= 319),
            // The model controller creates this worker when it releases the final ice shatter.
            185 => ActiveTasks(0x22BC198).Any(task => task.Callback == 0x5C77C0) || memory.Shiva?.Entry is { } shiva && (shiva.Counter22BD010 & 2) != 0,
            201 => memory.ScriptedEffects is { } core && NativeBattleIfritContinuation.HasReachedFinale(core),
            // Stage seven creates the target finale meshes before the director releases contact.
            95 => memory.Siren95 is { DirectorAddress1533010: { } director } siren && siren.ReadWord(director + 0x40) >= 7,
            // The recipient sink begins at 368; its original contact remains at 499.
            325 => ActiveTasks(NativeBattleDiablos325Construction.TimelineDescriptor).Any(task => task.Callback == NativeBattleDiablos325Construction.TimelineCallback && task.ReadWord(0x0C) >= 368),
            // Release the target wave, after the summon and water buildup.
            6 => HeaderFlag(0x80),
            // The creature enters its knife animation at phase nine.
            90 => RetainedTasks(0x25A3F90).Any(task => task.Callback == 0x7674E0 && task.ReadByte(0x29) >= 9),
            // The final charge precedes the target explosions and contact publication.
            191 => RetainedTasks(NativeBattleDoomtrain191Controller.ChildDescriptor).Any(task => task.Callback == 0x641EA0 && task.ReadWord(0xC) >= 242 || task.Callback == 0x63F2D0 && task.ReadWord(0xC) >= 406),
            // Pose four emits the needle volley; the terminal bit survives body removal.
            199 => RetainedTasks(NativeBattleCactuar199Controller.ChildDescriptor).Any(task => task.Callback == 0x5A8C20 && task.ReadWord(0xC) >= 87) || memory.Cactuar199 is { TerminalFlags: var flags } && (flags & 1) != 0,
            // The last golden projectile travels before the final sphere expands.
            202 => MainHasReached(0x280, 0x2C0),
            // The final three recipients receive the party effect.
            203 => HeaderFlag(0x200),
            // Incoming golden shots at the targets, after the outgoing sky beams.
            204 => MainHasReached(0x1B8, 0x1E8),
            // Release the final falling actor sequence.
            205 => HeaderFlag(0x800),
            // Release the moving galactic beam before its impact and collapse.
            206 => MainHasReached(0x41C, 0x45E),
            // The second programme publishes the reflection recipients.
            278 => RetainedTasks(0x2508188).Any(task => task.Callback == 0x681630 && task.ReadWord(0xC) >= 145),
            // Retain target attraction, conversion and release as one final attack.
            291 => RetainedTasks(0x25562A8).Any(task => task.Callback == 0x6ED900 && task.ReadWord(0xC) >= 254),
            _ => false
        };

        IEnumerable<NativeBattleModulePoolTask> ActiveTasks(uint descriptor)
        {
            return effect.Pools.Where(pool => pool.DescriptorAddress == descriptor).SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0);
        }

        IEnumerable<NativeBattleModulePoolTask> RetainedTasks(uint descriptor)
        {
            // Released slots retain their callback and payload until their next native allocation.
            return effect.Pools.Where(pool => pool.DescriptorAddress == descriptor).SelectMany(pool => pool.Slots);
        }

        bool HeaderFlag(ushort mask)
        {
            if (memory.ScriptedEffects is not { } core)
            {
                return false;
            }
            uint header = Dword(core.Read(0x279744C, 4));
            return header != 0 && (BinaryPrimitives.ReadUInt16LittleEndian(core.Read(checked(header + 2), 2)) & mask) != 0;
        }

        bool MainHasReached(int threshold, int lastInstruction)
        {
            if (memory.ScriptedEffects is not { } core || memory.Arena is not { } arena)
            {
                return false;
            }
            uint manager = Dword(core.Read(0x27973EC, 4));
            if (manager == 0)
            {
                return false;
            }
            NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
            NativeBattleEffectResourcePointer? source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.File0Publication);
            NativeBattleEffectResourcePointer? slots = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == profile.File1Publication);
            var assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
            if (source is null || slots is null || assets.Length != 2)
            {
                throw new InvalidDataException("Native guardian finale requires both executed cinematic resource owners.");
            }
            if (source.ArenaOffset < 0 || assets[0].Data.Length <= lastInstruction || source.ArenaOffset > NativeBattleEffectArena.Length - assets[0].Data.Length || slots.ArenaOffset < 0 || slots.ArenaOffset > NativeBattleEffectArena.Length - assets[1].Data.Length)
            {
                throw new InvalidDataException("Native guardian finale resources leave their original arena extents.");
            }
            uint bank = Dword(core.Read(checked(manager + 0x90), 4));
            long rootOffset = (long)bank - NativeBattleEffectNodeMemory.ArenaAddress;
            if (rootOffset < slots.ArenaOffset || rootOffset > (long)slots.ArenaOffset + assets[1].Data.Length - 0x100)
            {
                throw new InvalidDataException("Native guardian finale root leaves its current cinematic slot resource.");
            }
            int root = checked((int)rootOffset);
            int depth = unchecked((sbyte)arena.Read(root + 0x44, 1)[0]);
            if (depth is < 0 or > 2)
            {
                throw new InvalidDataException("Native guardian finale root has an invalid main-stream call depth.");
            }
            // The oldest return address identifies the linear main phase inside nested IO calls.
            uint pc = Dword(arena.Read(root + (depth == 0 ? 0 : 0x24), 4));
            uint file = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)source.ArenaOffset);
            long relative = (long)pc - file;
            return relative >= threshold && relative <= lastInstruction;
        }
    }

    private static uint Dword(byte[] value) => BinaryPrimitives.ReadUInt32LittleEndian(value);
}
