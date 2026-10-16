namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeQuezacotlFirstWindow(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C3940 requires retained effect memory.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C3940 lost its module state.");
        effect = effect with { Quezacotl = (effect.Quezacotl ?? throw new InvalidDataException("Native6C3940 lost its intensity global.")) with { Intensity17A4 = 2400 } };
        NativeBattleEffectTaskPool originalChild = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        ushort phase = originalChild.Slots[0].ReadWord(12);
        if (phase is < 1 or >= 30)
        {
            throw new InvalidOperationException("Native Quezacotl default first-window owner requires actual phase1..29.");
        }
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.Read(memory);
        if (phase == 1)
        {
            workspace = workspace.Initialize7016B0(source, NativeBattleQuezacotlWorkspace.Length);
            Persist(workspace);
            NativeStage = NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(NativeStage ?? throw new NotSupportedException("Native6C6500 requires its retained four stage rows."), 0);
        }
        NativeBattleGfAnimationAdvance stepped = workspace.Advance701970(source, memory.QuezacotlPause ?? throw new NotSupportedException("Native701970 requires the retained Quezacotl pause DWORD."), (current, node) =>
        {
            Persist(current);
            if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
            {
                return current;
            }
            throw new NotSupportedException($"Native Quezacotl6C3940 phase{phase} reached original6C6060 drawable node{node.NodeIndex}; its model geometry/morph scratch and7043B0/6C5710 packet contracts are not implemented.");
        });
        Persist(stepped.Workspace);
        //6C521C maps phases1..29 to the default534210 branch; no extra descriptor is appended in this interval.
        NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(memory);
        memory = io.Memory;
        if (io.Request is { } request)
        {
            NativeEffectMemory = memory;
            QueueNativeQuezacotlFile(request, effect);
            memory = NativeEffectMemory!;
        }
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        ushort nextPhase = unchecked((ushort)(phase + 1));
        child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, nextPhase)) };
        memory = memory with { Arena = memory.Arena!.Write(12, [(byte)nextPhase, 0]) };
        effect = effect with { Pools = effect.Pools.SetItem(effect.Pools.IndexOf(effect.Pools.Single(pool => pool.DescriptorAddress == child.DescriptorAddress)), child) };
        effect = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect);
        NativeEffectMemory = memory;
        return graphics with { OriginalEffect = effect };

        void Persist(NativeBattleGfAnimationWorkspace current)
        {
            NativeBattleQuezacotlWorkspaceWrite write = NativeBattleQuezacotlWorkspace.Write(memory, effect, current);
            memory = write.Memory;
            effect = write.Effect;
            NativeEffectMemory = memory;
            graphics = graphics with { OriginalEffect = effect };
            NativeGraphics = graphics;
        }
    }
}
