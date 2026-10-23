namespace Memoria.FF8.Runtime;

public static class NativeBattleGuardianAnimationFinale
{
    public static bool IsSupported(int effect) => effect is 116 or 185 or 201 or 95 or 325;

    public static bool HasReachedFinale(NativeBattleOriginalEffectState effect, NativeBattleEffectMemory memory)
    {
        return effect.Binding.Effect switch
        {
            // The final arena animation runs from phase 319 through 354, with contact at 345.
            116 => Tasks(0x2521738).Any(task => task.Callback == 0x6C3940 && task.ReadWord(0x0C) >= 319),
            // The model controller creates this worker when it releases the final ice shatter.
            185 => Tasks(0x22BC198).Any(task => task.Callback == 0x5C77C0) || memory.Shiva?.Entry is { } shiva && (shiva.Counter22BD010 & 2) != 0,
            201 => memory.ScriptedEffects is { } core && NativeBattleIfritContinuation.HasReachedFinale(core),
            // Stage seven creates the target finale meshes before the director releases contact.
            95 => memory.Siren95 is { DirectorAddress1533010: { } director } siren && siren.ReadWord(director + 0x40) >= 7,
            // The recipient sink begins at 368; its original contact remains at 499.
            325 => Tasks(NativeBattleDiablos325Construction.TimelineDescriptor).Any(task => task.Callback == NativeBattleDiablos325Construction.TimelineCallback && task.ReadWord(0x0C) >= 368),
            _ => false
        };

        IEnumerable<NativeBattleModulePoolTask> Tasks(uint descriptor)
        {
            return effect.Pools.Where(pool => pool.DescriptorAddress == descriptor).SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0);
        }
    }
}
