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
        NativeBattleGteDepthTrace depthTrace = new();
        if (phase is < 1 or > 302)
        {
            throw new InvalidOperationException("Native Quezacotl workspace owner requires actual phase1..302.");
        }
        if (phase == 86)
        {
            ApplyRig(NativeBattleQuezacotlRig86.InitializeBeforeSecondWindow(memory, effect));
        }
        if (phase is >= 86 and <= 256)
        {
            ApplyRig(NativeBattleQuezacotlRig86.PreparePose6C3A1E(memory, effect, phase));
            bool frozen = (memory.QuezacotlPause ?? throw new NotSupportedException("Native6FBDB0 requires its retained pause owner.")) != 0;
            ApplyRig(NativeBattleQuezacotlRig86.Compose5095B0(memory, effect));
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C6770 requires its prior projection owner.");
            NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native6C6770 requires its authenticated PC color rules.");
            int localFrame = phase - 86;
            NativeBattleQuezacotlSkinBatch batch = NativeBattleQuezacotlRig86.Emit6C6770(memory, effect, projection, colors, localFrame < 50 ? -3500 * localFrame / 50 : -4096);
            NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6770 requires its current logical ordering table.");
            NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlSkinArena.Append(memory, effect, frame.Pending, batch);
            memory = written.Memory;
            effect = written.Effect;
            graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
            ApplyRig(NativeBattleQuezacotlRig86.Restore508C90(memory, effect));
            if (!frozen)
            {
                ApplyRig(NativeBattleQuezacotlRig86.AddFrameCounter(memory, effect, localFrame));
            }
        }
        if (phase is >= 257 and <= 276)
        {
            if ((memory.QuezacotlPause ?? throw new NotSupportedException("Native6C3AE0 requires its retained pause DWORD.")) == 0)
            {
                NativeCamera = NativeBattleQuezacotlLaterCamera.Orbit6C3AEC(NativeCamera ?? throw new NotSupportedException("Native6C3AEC requires its current camera globals."), phase - 257, effect.Quezacotl!.TargetZ1770);
            }
            ApplyRig(NativeBattleQuezacotlRig86.PreparePose6C3A1E(memory, effect, phase));
            graphics = SubmitNativeQuezacotlPathModel6A79B0(graphics with { OriginalEffect = effect });
            memory = NativeEffectMemory!;
            effect = graphics.OriginalEffect!;
        }
        if (phase <= 164)
        {
            bool thirdWindow = phase >= 101;
            bool secondWindow = phase is >= 77 and < 101;
            int workspaceLength = thirdWindow ? NativeBattleQuezacotlWorkspace.ThirdLength : secondWindow ? NativeBattleQuezacotlWorkspace.SecondLength : NativeBattleQuezacotlWorkspace.Length;
            NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, secondWindow ? 0x12A9664u : NativeBattleQuezacotlExecutableMemory.Address);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.Read(memory, workspaceLength);
            if (phase is 1 or 77 or 101)
            {
                workspace = workspace.Initialize7016B0(source, workspaceLength);
                Persist(workspace);
                if (!thirdWindow)
                {
                    NativeStage = NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(NativeStage ?? throw new NotSupportedException("Native6C6500 requires its retained four stage rows."), secondWindow ? 1 : 0);
                }
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
                    ? NativeBattleQuezacotlGeometry.Emit6C53A0(memory, effect, node, projection, colors, NativeBattleGfMatrices.SecondWindowContext6C3C47(projection.View ?? throw new NotSupportedException("Native6C3C99 requires its prior native view matrix."), effect.Quezacotl!.TargetZ1778), false, 0, observeDepth: depthTrace.Push)
                    : NativeBattleQuezacotlGeometry.Emit6C6060(memory, effect, node, projection, colors, thirdWindow ? new(0, 0, unchecked((short)effect.Quezacotl!.TargetZ1778)) : new(0, -5000, 0), observeDepth: depthTrace.Push);
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
        }
        if (phase is >= 87 and < 162)
        {
            if (memory.QuezacotlPause == 0)
            {
                (NativeBattleQuezacotlRigWrite rig, NativeBattleCameraState camera) = NativeBattleQuezacotlRig86.UpdateFlightCamera6C3DDF(memory, effect, NativeCamera ?? throw new NotSupportedException("Native6C3DDF requires its retained camera globals."), phase - 86);
                NativeCamera = camera;
                ApplyRig(rig);
                NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, phase - 86);
                ApplyRig(new(spawned.Memory, spawned.Effect));
                if (phase - 86 < 15 && ((phase - 86) & 1) == 0)
                {
                    NativeBattleQuezacotlParticleSpawn particle = NativeBattleQuezacotlParticleFlight.Spawn6C4043(memory, effect);
                    ApplyRig(new(particle.Memory, particle.Effect));
                }
            }
        }
        if (phase is >= 157 and < 177 && ((phase - 157) & 1) == 0 && memory.QuezacotlPause == 0)
        {
            NativeBattleQuezacotlParticleSpawn particle = NativeBattleQuezacotlParticleFlight.Spawn6C41BF(memory, effect);
            ApplyRig(new(particle.Memory, particle.Effect));
        }
        if (phase is >= 207 and < 227)
        {
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native6C4290 requires its current camera globals.");
            NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native6C4290 requires its initialized camera coordinates.");
            if (phase == 207)
            {
                pose = pose with { Eye = new(677, -3571, unchecked((short)(effect.Quezacotl!.TargetZ1778 - 200))) };
            }
            NativeBattleQuezacotlAnchor anchor = NativeBattleQuezacotlRig86.ReadMeshAnchor6C6440(memory, effect, 0, 50);
            ApplyRig(new(anchor.Memory, anchor.Effect));
            pose = pose with { Target = new(anchor.Position.X, anchor.Position.Y, anchor.Position.Z) };
            NativeCamera = camera.WriteEffectGlobals(new(pose.Eye, pose.Target, camera.FocalLength, camera.Roll));
            if (memory.QuezacotlPause == 0)
            {
                NativeBattleQuezacotlRibbonBank bank = effect.Binding.NativeQuezacotlResources?.Ribbon?.SecondaryBank ?? throw new NotSupportedException("Native6C4300 requires the authenticated5-pair12AF800/12AF828 bank; explicitly migrate the named standalone metadata.");
                bank.Validate();
                NativeBattleQuezacotlRibbonSpawn spawned = NativeBattleQuezacotlRibbonProducer.Spawn6C87B0(memory, effect, checked(bank.PairAddress + (uint)((phase - 207) % 5) * 8), bank.ControlAddress, 12, 7, 256);
                ApplyRig(new(spawned.Memory, spawned.Effect));
            }
        }
        if (phase is >= 212 and <= 256)
        {
            if (phase == 247)
            {
                NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(memory);
                memory = poll.Memory;
                NativeEffectMemory = memory;
                if (poll.Request is { } pendingRequest)
                {
                    QueueNativeQuezacotlFile(pendingRequest, effect);
                    memory = NativeEffectMemory!;
                }
                if (poll.Busy)
                {
                    return FinishSecondaryLists();
                }
            }
            bool meshContext = phase is >= 227 and < 247;
            bool directContext = phase >= 247;
            uint sourceAddress = directContext ? 0x129BA94u : meshContext ? 0x12A9664u : 0x12A1164u;
            int workspaceLength = directContext ? NativeBattleQuezacotlWorkspace.SixthLength : meshContext ? NativeBattleQuezacotlWorkspace.FifthLength : NativeBattleQuezacotlWorkspace.FourthLength;
            NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, sourceAddress);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.Read(memory, workspaceLength);
            if (phase is 212 or 227 or 247)
            {
                workspace = workspace.Initialize7016B0(source, workspaceLength);
                Persist(workspace);
            }
            NativeBattleVector? meshAnchor = null;
            if (meshContext)
            {
                NativeBattleQuezacotlAnchor anchor = NativeBattleQuezacotlRig86.ReadMeshAnchor6C6440(memory, effect, 0, 50);
                ApplyRig(new(anchor.Memory, anchor.Effect));
                meshAnchor = anchor.Position;
            }
            NativeBattleGfAnimationAdvance stepped = workspace.Advance701970(source, memory.QuezacotlPause ?? throw new NotSupportedException("Native701970 requires its retained pause DWORD."), (current, node) =>
            {
                Persist(current);
                if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
                {
                    return current;
                }
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C43AD requires its prior projection owner.");
                NativeBattleActionMatrix context;
                if (directContext)
                {
                    context = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 2000, 4000, 4096);
                }
                else
                {
                    NativeBattleVector translation;
                    if (meshAnchor is { } anchor)
                    {
                        translation = anchor;
                    }
                    else
                    {
                        NativeBattleCameraPoint target = NativeCamera?.CurrentPose?.Target ?? throw new NotSupportedException("Native6C4345 requires its actual global camera TargetXYZ.");
                        translation = new(target.X, target.Y, target.Z);
                    }
                    short yaw = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(memory.Arena!.Read(0x36, 2));
                    context = NativeBattleGfMatrices.LaterWindowContext6C64C0(projection.View ?? throw new NotSupportedException("Native6C43AD requires its prior view matrix."), -128, -(int)yaw, translation);
                }
                NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native6C53A0 requires its authenticated PC color rules.");
                NativeBattleQuezacotlGeometryStep emitted = NativeBattleQuezacotlGeometry.Emit6C53A0(memory, effect, node, projection, colors, context, false, 0, sourceAddress, depthTrace.Push);
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C53A0 requires its current logical OT frame.");
                NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(emitted.Memory, effect, frame.Pending, emitted.Batch);
                memory = written.Memory;
                effect = written.Effect;
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
                NativeEffectMemory = memory;
                NativeGraphics = graphics;
                return current;
            });
            Persist(stepped.Workspace);
        }
        if (phase is >= 257 and <= 302)
        {
            NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.SpawnFour6C45AA(memory, effect);
            ApplyRig(new(spawned.Memory, spawned.Effect));
            NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x1299718);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.Read(memory, NativeBattleQuezacotlWorkspace.SeventhLength);
            if (phase == 257)
            {
                workspace = workspace.Initialize7016B0(source, NativeBattleQuezacotlWorkspace.SeventhLength);
                Persist(workspace);
            }
            int baseY = memory.QuezacotlTargetY1774 ?? throw new NotSupportedException("Native6C45F0 requires its retained2521774 DWORD; explicitly migrate only this standalone's proved cold owner.");
            NativeBattleGfAnimationAdvance stepped = workspace.Advance701970(source, memory.QuezacotlPause ?? throw new NotSupportedException("Native701970 requires its retained pause DWORD."), (current, node) =>
            {
                Persist(current);
                if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
                {
                    return current;
                }
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C6060 requires its prior projection registers.");
                NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native6C6060 requires its authenticated PC color rules.");
                NativeBattleVector position = new(0, unchecked((short)baseY), unchecked((short)effect.Quezacotl!.TargetZ1770));
                NativeBattleQuezacotlGeometryStep emitted = NativeBattleQuezacotlGeometry.Emit6C6060(memory, effect, node, projection, colors, position, true, unchecked((byte)(phase - 257)), -128, 0x1299718, depthTrace.Push);
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C6060 requires its current ordering table.");
                NativeBattleQuezacotlPacketWrite written = emitted.CapturedPackets ? NativeBattleQuezacotlPacketArena.AppendCaptured6C5710(emitted.Memory, effect, frame.Pending, emitted.Batch) : NativeBattleQuezacotlPacketArena.Append(emitted.Memory, effect, frame.Pending, emitted.Batch);
                memory = written.Memory;
                effect = written.Effect;
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
                NativeEffectMemory = memory;
                NativeGraphics = graphics;
                return current;
            });
            Persist(stepped.Workspace);
        }
        if (phase is >= 277 and <= 302)
        {
            NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, 0x129CD84);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleQuezacotlWorkspace.ReadExecutable(memory);
            if (phase == 277)
            {
                workspace = workspace.Initialize7016B0(source, NativeBattleQuezacotlWorkspace.EighthLength);
                PersistExecutable(workspace);
            }
            int frozen = memory.QuezacotlPause ?? throw new NotSupportedException("Native6C4672 requires its retained pause DWORD.");
            if (frozen == 0)
            {
                NativeCamera = NativeBattleQuezacotlLaterCamera.Shift6C4685(NativeCamera ?? throw new NotSupportedException("Native6C4685 requires its current camera globals."), phase - 257);
            }
            int baseY = memory.QuezacotlTargetY1774 ?? throw new NotSupportedException("Native6C46BB requires its retained2521774 DWORD.");
            NativeBattleGfAnimationAdvance stepped = workspace.Advance701970(source, frozen, (current, node) =>
            {
                PersistExecutable(current);
                if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
                {
                    return current;
                }
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6C46E3 requires its prior projection registers.");
                NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native6C46E3 requires its authenticated PC color rules.");
                NativeBattleVector position = new(0, unchecked((short)(baseY - 3000)), unchecked((short)effect.Quezacotl!.TargetZ1770));
                NativeBattleQuezacotlGeometryStep emitted = NativeBattleQuezacotlGeometry.Emit6C6060(memory, effect, node, projection, colors, position, sourceAddress: 0x129CD84, observeDepth: depthTrace.Push);
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native6C46E3 requires its current ordering table.");
                NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(emitted.Memory, effect, frame.Pending, emitted.Batch);
                memory = written.Memory;
                effect = written.Effect;
                graphics = graphics with { OriginalEffect = effect, PrimitiveFrame = frame with { Pending = written.Ordering } };
                NativeEffectMemory = memory;
                NativeGraphics = graphics;
                return current;
            });
            PersistExecutable(stepped.Workspace);
        }
        if (phase == 86)
        {
            NativeBattleQuezacotlRigWrite rig = NativeBattleQuezacotlRig86.InitializeAfterSecondWindow(memory, effect, NativeCamera ?? throw new NotSupportedException("Native86 requires its retained camera owner."));
            memory = rig.Memory;
            effect = rig.Effect;
        }
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
        if (phase == 302 && io.AdvancePhase)
        {
            effect = NativeBattleQuezacotlFirstWindowIo.ResetPools6C4D3C(effect);
        }
        if (io.Request is { } request)
        {
            NativeEffectMemory = memory;
            QueueNativeQuezacotlFile(request, effect);
            memory = NativeEffectMemory!;
        }
        if (!io.AdvancePhase)
        {
            NativeEffectMemory = memory;
            return FinishSecondaryLists();
        }
        if (phase == 65)
        {
            uint sound = effect.Binding.NativeQuezacotlResources?.Frame65Sound ?? throw new NotSupportedException("Native6C4EDB/501330 requires the actual12AE4F4 Quezacotl sound DWORD; explicitly migrate this snapshot's resource binding.");
            PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
        }
        else if (phase == 69)
        {
            graphics = SetNativeQuezacotlTargets6C5300(graphics, effect, false);
            NativeBattleModuleSound sound = (memory.SummonStream ?? throw new NotSupportedException("Native6C4EF7/5018C0 requires its actual501860 summon stream owner.")).Play5018C0(128, 127);
            PublishNativeModuleSound(sound);
        }
        else if (phase == 76)
        {
            int z = effect.Quezacotl!.TargetZ1778;
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native6C4F07 requires its actual retained camera globals.");
            NativeCamera = camera.WriteEffectGlobals(new(new(-1809, -356, unchecked((short)(z + 2276))), new(0, -652, unchecked((short)z)), camera.FocalLength, camera.Roll));
        }
        else if (phase == 209)
        {
            uint sound = effect.Binding.NativeQuezacotlResources?.Frame209Sound ?? throw new NotSupportedException("Native6C4F54/501330 requires its authenticated12AE4F8 sound DWORD; explicitly migrate the named standalone metadata.");
            PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
        }
        else if (phase == 277)
        {
            uint sound = effect.Binding.NativeQuezacotlResources?.Frame277Sound ?? throw new NotSupportedException("Native6C50C3/501330 requires its authenticated12AE4FC sound DWORD; explicitly migrate the named standalone metadata.");
            PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
        }
        if (phase == 126)
        {
            graphics = SetNativeQuezacotlTargets6C5300(graphics, effect, true);
        }
        else if (phase == 212)
        {
            graphics = SetNativeQuezacotlTargets6C5300(graphics, effect, false);
        }
        else if (phase == 226)
        {
            int z = effect.Quezacotl!.TargetZ1778;
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native6C4FCC requires its current camera globals.");
            NativeCamera = camera.WriteEffectGlobals(new(new(2857, -553, unchecked((short)(z + 2568))), new(-83, -1604, unchecked((short)(z - 2098))), camera.FocalLength, camera.Roll));
        }
        else if (phase is 246 or 256 or 276 or 302)
        {
            NativeCamera = NativeBattleQuezacotlLaterCamera.Apply6C4F80(NativeCamera ?? throw new NotSupportedException("Native Quezacotl camera tail requires its current globals."), phase, phase == 302 ? effect.Quezacotl!.TargetZ1778 : effect.Quezacotl!.TargetZ1770);
            if (phase == 276)
            {
                memory = memory with { QuezacotlTargetY1774 = -4096 };
            }
            else if (phase == 302)
            {
                memory = memory with { QuezacotlTargetY1774 = 0 };
            }
        }
        NativeBattleQuezacotlWorkspaceWrite advancedPhase = NativeBattleQuezacotlWorkspace.AdvancePhase6C51F1(memory, effect);
        ApplyRig(new(advancedPhase.Memory, advancedPhase.Effect));
        return FinishSecondaryLists();

        NativeBattleGraphicsState FinishSecondaryLists()
        {
            if (phase >= 87)
            {
                graphics = AdvanceNativeQuezacotlRibbon(graphics with { OriginalEffect = effect }, depthTrace);
                memory = NativeEffectMemory!;
                effect = graphics.OriginalEffect!;
            }
            if (phase >= 88)
            {
                graphics = AdvanceNativeQuezacotlParticles(graphics with { OriginalEffect = effect }, depthTrace);
                memory = NativeEffectMemory!;
                effect = graphics.OriginalEffect!;
            }
            if (phase >= 257)
            {
                graphics = AdvanceNativeQuezacotlPaths(graphics with { OriginalEffect = effect }, depthTrace);
                memory = NativeEffectMemory!;
                effect = graphics.OriginalEffect!;
            }
            effect = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect, phase >= 87, phase >= 88, phase >= 257);
            return graphics with { OriginalEffect = effect };
        }
        void Persist(NativeBattleGfAnimationWorkspace current)
        {
            NativeBattleQuezacotlWorkspaceWrite write = NativeBattleQuezacotlWorkspace.Write(memory, effect, current);
            memory = write.Memory;
            effect = write.Effect;
            NativeEffectMemory = memory;
            graphics = graphics with { OriginalEffect = effect };
            NativeGraphics = graphics;
        }

        void PersistExecutable(NativeBattleGfAnimationWorkspace current)
        {
            memory = NativeBattleQuezacotlWorkspace.WriteExecutable(memory, current);
            NativeEffectMemory = memory;
        }

        void ApplyRig(NativeBattleQuezacotlRigWrite rig)
        {
            memory = rig.Memory;
            effect = rig.Effect;
            NativeEffectMemory = memory;
            graphics = graphics with { OriginalEffect = effect };
            NativeGraphics = graphics;
        }
    }
}
