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
        if (phase is < 1 or > 85)
        {
            throw new InvalidOperationException("Native Quezacotl workspace owner requires actual phase1..85.");
        }
        bool secondWindow = phase >= 77;
        int workspaceLength = secondWindow ? NativeBattleQuezacotlWorkspace.SecondLength : NativeBattleQuezacotlWorkspace.Length;
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, secondWindow ? 0x12A9664u : NativeBattleQuezacotlExecutableMemory.Address);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.Read(memory, workspaceLength);
        if (phase is 1 or 77)
        {
            workspace = workspace.Initialize7016B0(source, workspaceLength);
            Persist(workspace);
            NativeStage = NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(NativeStage ?? throw new NotSupportedException("Native6C6500 requires its retained four stage rows."), secondWindow ? 1 : 0);
        }
        NativeBattleGfAnimationAdvance stepped = workspace.Advance701970(source, memory.QuezacotlPause ?? throw new NotSupportedException("Native701970 requires the retained Quezacotl pause DWORD."), (current, node) =>
        {
            Persist(current);
            if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
            {
                return current;
            }
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C6060 requires its prior projection/register owner.");
            NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native6C6060 depth cue requires its authenticated PC color rules.");
            NativeBattleQuezacotlGeometryStep emitted = secondWindow
                ? NativeBattleQuezacotlGeometry.Emit6C53A0(memory, effect, node, projection, colors, NativeBattleGfMatrices.SecondWindowContext6C3C47(projection.View ?? throw new NotSupportedException("Native6C3C99 requires its prior native view matrix."), effect.Quezacotl!.TargetZ1778), false, 0)
                : NativeBattleQuezacotlGeometry.Emit6C6060(memory, effect, node, projection, colors);
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6060 requires its original logical OT frame.");
            NativeBattleQuezacotlPacketWrite written = emitted.CapturedPackets ? NativeBattleQuezacotlPacketArena.AppendCaptured6C5710(emitted.Memory, effect, frame.Pending, emitted.Batch) : NativeBattleQuezacotlPacketArena.Append(emitted.Memory, effect, frame.Pending, emitted.Batch);
            memory = written.Memory;
            effect = written.Effect;
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
            NativeEffectMemory = memory;
            NativeGraphics = graphics;
            return current;
        });
        Persist(stepped.Workspace);
        foreach (NativeBattleQuezacotlScreenPacket packet in NativeBattleQuezacotlFlash.Emit(phase, effect.Binding.NativeQuezacotlResources!))
        {
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6530 requires its original logical OT frame.");
            NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, frame.Pending, packet.Batch, packet.Flags20);
            memory = written.Memory;
            effect = written.Effect;
            graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
        }
        NativeBattleQuezacotlFirstWindowIoStep io = NativeBattleQuezacotlFirstWindowIo.Advance(memory, phase);
        memory = io.Memory;
        if (io.Request is { } request)
        {
            NativeEffectMemory = memory;
            QueueNativeQuezacotlFile(request, effect);
            memory = NativeEffectMemory!;
        }
        if (!io.AdvancePhase)
        {
            NativeEffectMemory = memory;
            return graphics with { OriginalEffect = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect) };
        }
        if (phase == 65)
        {
            uint sound = effect.Binding.NativeQuezacotlResources?.Frame65Sound ?? throw new NotSupportedException("Native6C4EDB/501330 requires the actual12AE4F4 Quezacotl sound DWORD; explicitly migrate this snapshot's resource binding.");
            events.Add(new BattleEvent(Frame, "NativeModuleSound", null, null, 0, string.Empty) { NativeModuleSound = new(sound, 1, null) { Pan = 128 } });
        }
        else if (phase == 69)
        {
            NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native6C5350 lost its actual action header.");
            NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Native6C5350 requires its actual target contact pointer.");
            for (int index = 0; index < header.ReadByte(16); index++)
            {
                int target = nativeActionPackets.ReadContact(first + index).Primary.ActorIndex;
                NativeBattleGraphicsModel model = graphics.Models[target];
                graphics = graphics with { Models = graphics.Models.SetItem(target, model with { Flags0 = (ushort)(model.Flags0 & ~4) }) };
            }
            NativeBattleModuleSound sound = (memory.SummonStream ?? throw new NotSupportedException("Native6C4EF7/5018C0 requires its actual501860 summon stream owner.")).Play5018C0(128, 127);
            events.Add(new BattleEvent(Frame, "NativeModuleSound", null, null, 0, string.Empty) { NativeModuleSound = sound });
        }
        else if (phase == 76)
        {
            int z = effect.Quezacotl!.TargetZ1778;
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native6C4F07 requires its actual retained camera globals.");
            NativeCamera = camera.WriteEffectGlobals(new(new(-1809, -356, unchecked((short)(z + 2276))), new(0, -652, unchecked((short)z)), camera.FocalLength, camera.Roll));
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
