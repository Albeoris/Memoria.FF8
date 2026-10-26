using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCinematic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native cinematic entry requires its selected effect.");
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
        NativeBattleScriptedInitializationDirectories directories = effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native cinematic requires its original initialization and owner directories.");
        NativeBattleGuardianFormPresentation? presentation = graphics.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.EntryOnly } formPresentation ? formPresentation : null;
        NativeBattleEffectParameters parameters = effect.Parameters ?? (presentation is null ? throw new InvalidDataException("Native cinematic requires its actual action parameters.") : GuardianFormPositioningParameters(presentation));
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native cinematic requires both complete executed initializer resources.");
        }
        NativeBattleActionHeaderSlot? header = presentation is null ? nativeActionPackets.Headers[parameters.HeaderIndex7C] : null;
        ImmutableArray<byte> contacts = presentation is null ? WaterContacts(header!) : [presentation.ActorIndex];
        if (contacts.IsEmpty)
        {
            throw new NotSupportedException("Native cinematic entry requires its first actual contact actor.");
        }
        NativeBattleWaterInitializationInputs inputs = new(Resource(0, profile.File0Publication), Resource(1, profile.File1Publication), NativeBattleWater222Controller.CreateTemporaryFrame(WaterTemporaryAddress), WaterParametersAddress, WaterHeadersAddress + checked((uint)(20 * parameters.HeaderIndex7C)), profile.InitializationAddress, parameters.Actor78, parameters.HeaderIndex7C, contacts[0], contacts, header?.ReadByte(0x10) ?? 1, header?.ReadByte(0x11) ?? 1, graphics.Models);
        NativeBattleCinematicEntryStep initialized = presentation is null ? NativeBattleCinematicEntry.Initialize(profile, RequireWaterMemory(), effect, directories, inputs) : NativeBattleCinematicEntry.InitializeIfritPresentation(RequireWaterMemory(), effect, directories, inputs);
        NativeEffectMemory = initialized.Memory;
        return graphics with { OriginalEffect = initialized.Effect };

        NativeBattleWaterInitializationResource Resource(int index, uint publication)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == publication) ?? throw new NotSupportedException("Native cinematic requires its actual initializer resource publication.");
            return new(pointer.ArenaOffset, assets[index].Data.Length, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        }
    }

    private NativeBattleGraphicsState AdvanceNativeCinematic(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleEffectParameters? outputParameters = effect.Parameters;
        NativeBattleGuardianFormPresentation? presentation = graphics.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.EntryOnly } formPresentation ? formPresentation : null;
        NativeBattleCinematicResources resources = effect.Binding.CinematicResources ?? throw new NotSupportedException("Native cinematic requires its authenticated cinematic tables and resources.");
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect);
        ImmutableArray<NativeBattleEffectInitializerAsset> assets = NativeBattleEffectInitializerPolicy.Assets(effect.Binding);
        if (assets.Length != 2)
        {
            throw new NotSupportedException("Native cinematic callback requires both current resource extents.");
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native cinematic requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native cinematic requires its current projection.");
        NativeBattlePrimitiveProcess process = RequireWaterMemory().PrimitiveProcess ?? throw new NotSupportedException("Native cinematic requires its actual COMMON process.");
        NativeBattleScriptedCallbackInput input = new(effect.Binding.ScriptedInitializationDirectories ?? throw new NotSupportedException("Native cinematic requires its original directories."), assets[1].Data.Length, checked((ushort)graphics.Flags), MatrixBytes(projection.View ?? throw new NotSupportedException("Native cinematic requires its current view matrix.")), 0, checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native cinematic COMMON cursor is unknown."))), camera.FocalLength ?? throw new NotSupportedException("Native cinematic requires its retained focal WORD."));
        NativeBattleWaterContextServices context = new((_, pointer) =>
        {
            int? linked = graphics.Models[PhysicalActor(pointer)].ReadLinkedModelIndex140();
            return linked is int row ? ActorPointer(row) : 0;
        }, (_, pointer) => ActorPointer(NativeBattleModelRings.Walk(graphics.Models, PhysicalActor(pointer)).Min()), (_, address, length) => ReadAction(address, length), (current, address) =>
        {
            if (presentation is not null)
            {
                throw new InvalidDataException("Presentation-only Ifrit entrance reached an offensive contact before its landing handoff.");
            }
            NativeEffectMemory = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(ContactIndex(address))] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Arena = RequireWaterMemory().Arena!, Core = RequireWaterMemory().ScriptedEffects! };
        }, current =>
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its camera owner.")).Apply50A730();
            return current;
        }, (current, index, depth, color) =>
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native cinematic requires its four stage groups.");
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, stage.Groups[index] with { ColorDepth = depth, ColorFlags = color }) };
            return current;
        });
        NativeBattleScriptedCallbackServices services = new(ReadInstruction, ExecuteInstruction, UpdateSlot, DrawSlots, ClearOrderingTable, PublishFocal) { ReadCurrentFocal = _ => NativeCamera?.FocalLength ?? throw new NotSupportedException("Native cinematic requires its current scratch focal WORD after script execution.") };
        if (effect.Binding.Effect == 201 && graphics.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 4 } continuation && ActiveAction is { GuardianFormInvocation: { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } invocation } currentAction && currentAction.ActorId == continuation.ActorId && invocation.Id == continuation.InvocationId)
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleWaterTemporaryFrame temporary = NativeBattleWater222Controller.CreateTemporaryFrame(WaterTemporaryAddress);
            NativeBattleWaterFrameBindingResult binding = NativeBattleWaterFrameBinding.Bind(memory.ScriptedEffects!, temporary, input.Directories);
            NativeBattleWaterExecutionFrame execution = new(binding.Core, memory.Arena!, binding.TemporaryFrame, nativeCrtRandom!);
            execution = execution.WriteTemporaryByte(0x145, 0).WriteTemporaryByte(0x143, 0).WriteTemporaryByte(0x144, 0);
            execution = NativeBattleIfritContinuation.Initialize(execution, CurrentResource(0, profile.File0Publication), CurrentResource(1, profile.File1Publication), memory.MagicTextureBufferAddress1D99A88 ?? throw new NotSupportedException("GF continuation requires its published magic texture buffer."), ModelServices(0), InitializeResourceInstruction);
            NativeEffectMemory = RequireWaterMemory() with { Arena = execution.Arena, ScriptedEffects = execution.Core };
            nativeCrtRandom = execution.Random;
            NativeCamera = NativeCamera!.PublishProjection(graphics.Flags);
            input = input with { ViewMatrix = MatrixBytes(NativeCamera.Projection!.View!) };
            graphics = graphics with { GuardianFormPresentation = continuation with { Phase = 5 } };
            NativeGraphics = graphics;
        }
        NativeBattleCinematicFrame advanced = NativeBattleCinematicController.Advance(profile, new(RequireWaterMemory(), effect, nativeCrtRandom ?? throw new NotSupportedException("Native cinematic callback requires its retained CRT state.")), input, services, WaterTemporaryAddress, RequireWaterMemory);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect with { Parameters = outputParameters } };

        NativeBattleWaterInitializationResource CurrentResource(int index, uint publication)
        {
            NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == publication);
            return new(pointer.ArenaOffset, assets[index].Data.Length, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset));
        }

        NativeBattleWaterExecutionFrame InitializeResourceInstruction(NativeBattleWaterExecutionFrame current, int offset)
        {
            ushort opcode = (ushort)(current.ReadTemporaryWord(0x14A) & 0x1FF);
            if (opcode == 6)
            {
                // Synchronous dependency loads reuse the work buffer after preceding uploads have consumed it.
                NativeBattleEffectMemory pending = RequireWaterMemory() with { Arena = current.Arena, ScriptedEffects = current.Core };
                NativeEffectMemory = DrainNativeTextureUploads(pending, pending.Textures ?? throw new NotSupportedException("Ifrit continuation requires its pending texture uploads."));
                current = current with { Arena = NativeEffectMemory.Arena!, Core = NativeEffectMemory.ScriptedEffects! };
            }
            if (opcode != 6 || offset == 0x2AE)
            {
                return ExecuteInstruction(current, opcode);
            }
            NativeBattleCinematicIoStep prepared = NativeBattleCinematicIoCommands.Execute(current, Program(current, CoreDword(current, 0x2797450)), resources, profile.LoadCallback);
            NativeBattleWaterFileReadRequest request = prepared.Request ?? throw new InvalidDataException("Ifrit continuation resource preparation encountered an unresolved earlier load.");
            NativeBattleEffectMemory memory = RequireWaterMemory() with { Arena = prepared.Frame.Arena, ScriptedEffects = prepared.Frame.Core };
            NativeBattleWaterIoQueued loaded = NativeBattleCinematicIoCommands.CopyAndQueue(memory, NativeBattleFileIoState.FromColdProcess(), request, resources);
            NativeBattleScriptedEffectMemory completed = NativeBattleWaterIoCommands.CompleteAA0ED0(loaded.Memory.ScriptedEffects!);
            NativeEffectMemory = loaded.Memory with { ScriptedEffects = completed };
            return prepared.Frame with { Arena = loaded.Memory.Arena!, Core = completed };
        }

        byte[] ReadAction(uint address, int length)
        {
            if (presentation is not null)
            {
                return ReadGuardianFormPositioning(presentation, address, length);
            }
            if (address >= WaterParametersAddress && (long)address - WaterParametersAddress <= 12L - length)
            {
                NativeBattleEffectParameters parameters = outputParameters ?? throw new NotSupportedException("Native cinematic parameter read requires its current action owner.");
                return NativeBattleCinematicParameterBytes.Read(parameters, checked((int)(address - WaterParametersAddress)), length);
            }
            if (address >= WaterHeadersAddress && address < WaterContactsAddress)
            {
                uint relative = address - WaterHeadersAddress;
                NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[checked((int)(relative / 20))];
                return Enumerable.Range((int)(relative % 20), length).Select(header.ReadByte).ToArray();
            }
            if (length < 0 || address < WaterContactsAddress || (long)address - WaterContactsAddress > nativeActionPackets.Contacts.Length * 24L - length)
            {
                throw new NotSupportedException("Native cinematic action read leaves its actual header, contact and parameter owners.");
            }
            uint contact = address - WaterContactsAddress;
            byte[] bytes = nativeActionPackets.ReadContact(checked((int)(contact / 24))).Encode();
            return bytes.AsSpan((int)(contact % 24), length).ToArray();
        }

        NativeBattleWaterScriptProgram Program(NativeBattleWaterExecutionFrame current, uint address)
        {
            for (int index = 0; index < assets.Length; index++)
            {
                uint start = CoreDword(current, 0x2798A68 + (uint)(index * 4));
                int length = assets[index].Data.Length;
                if (address >= start && address - start < length)
                {
                    return new(ArenaOffset(start, length), length, start);
                }
            }
            string owners = string.Join(", ", Enumerable.Range(0, assets.Length).Select(index => $"resource {index} at {CoreDword(current, 0x2798A68 + (uint)(index * 4)):X8} length {assets[index].Data.Length:X}"));
            throw new NotSupportedException($"Native cinematic effect {profile.Effect} instruction at PC {address:X8} leaves its current resource owners ({owners}); slot {current.ReadTemporaryByte(0x142)}, stream {current.ReadTemporaryByte(0x143)}.");
        }

        ushort ReadInstruction(NativeBattleWaterExecutionFrame current, uint address)
        {
            NativeBattleWaterScriptProgram program = Program(current, address);
            int offset = checked((int)(address - program.Address));
            if (offset > program.Length - 2)
            {
                throw new InvalidDataException("Native cinematic instruction leaves its complete WORD operand owner.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(program.ArenaOffset + offset, 2));
        }

        NativeBattleWaterExecutionFrame ExecuteInstruction(NativeBattleWaterExecutionFrame current, ushort opcode)
        {
            try
            {
                return ExecuteInstructionCore(current, opcode);
            }
            catch (NotSupportedException error)
            {
                throw new NotSupportedException($"Native cinematic effect {profile.Effect} instruction {current.ReadTemporaryWord(0x14A):X4} at PC {CoreDword(current, 0x2797450):X8} requires its complete command contract.", error);
            }
        }

        int PhysicalActorIndex(uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native actor store requires its actual physical model pointer.");
            }
            return checked((int)((address - 0x1D972C0) / 156));
        }

        NativeBattleWaterExecutionFrame ExecuteInstructionCore(NativeBattleWaterExecutionFrame current, ushort opcode)
        {
            NativeBattleWaterExecutionFrame QueueTextureCopy(NativeBattleWaterExecutionFrame copyFrame, NativeBattleEffectVramCopy copy)
            {
                NativeBattleEffectMemory memory = RequireWaterMemory();
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native cinematic frame copy requires its current texture queue.");
                NativeEffectMemory = memory with { Textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY) };
                return copyFrame;
            }
            uint pc = CoreDword(current, 0x2797450);
            uint selectedHandler;
            try
            {
                selectedHandler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "vm_table", opcode, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.InstructionTable + 4u * opcode), 4)));
            }
            catch (NotSupportedException error)
            {
                throw new NotSupportedException($"Native cinematic effect {profile.Effect} instruction {current.ReadTemporaryWord(0x14A):X4} at PC {pc:X8} requires its selected command contract.", error);
            }
            if (NativeBattleCinematicDispatch.IsNoOp(profile.Effect, "vm_table", opcode, selectedHandler))
            {
                return current;
            }
            NativeBattleWaterScriptProgram program = Program(current, pc);
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            switch (opcode)
            {
                case 0x94 when (profile.Effect == 230 && selectedHandler == 0xA77C10) || (profile.Effect == 238 && selectedHandler == 0xA26E20):
                    return NativeBattleCinematicRangeBranch94.Execute(current, bank, program);
                case 0x87 when selectedHandler == 0xB2D2B0:
                    return NativeBattleCinematicScale87.Execute(current, program);
                case 0xAF when selectedHandler == 0xB52F20:
                    return NativeBattleCinematicContextCommandAF.Execute(current, program);
                case 0xB3 when selectedHandler == 0xAC7800:
                    return NativeBattleCinematicPath179.Execute(current, program, ResolveCurrentNode);
                case 0xB9 when selectedHandler == 0xB2CA80:
                    return NativeBattleCinematicNegateB9.Execute(current, program, bank, index, UpdateSlot);
                case 0xBE when selectedHandler == 0xB3A9B0:
                    return NativeBattleCinematicActorCommands77.BranchBE(current, program, pointer => unchecked((byte)graphics.Models[PhysicalActor(pointer)].Flags0));
                case 0xC1 when selectedHandler == 0xB3AA00:
                    return NativeBattleCinematicActorCommands77.ExecuteC1(current, new(frame =>
                    {
                        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native cinematic menu command requires its retained UI flags.");
                        graphics = graphics with { UiServices = ui.SetFlags(0x200, 0x100) };
                        return frame;
                    }, frame =>
                    {
                        NativeEffectMemory = RequireWaterMemory() with { Arena = frame.Arena, ScriptedEffects = frame.Core };
                        graphics = BeginNativeOutcome47DF60(graphics);
                        return frame;
                    }, (frame, pointer) =>
                    {
                        graphics = SetNativeDescriptorStatusAndStanding(graphics, PhysicalActor(pointer), 0x8000);
                        return frame;
                    }));
                case 0x55 when selectedHandler == 0xB2B380:
                    return NativeBattleCinematicActionBranch55.Execute(current, program, context.ReadActionBytes);
                case 0x106 when profile.Effect == 223:
                    return NativeBattleCinematicFieldWait106.Execute(current, program, ResolveCurrentNode);
                case 0x35 when profile.Effect == 223:
                case 0x35 when selectedHandler == 0xA996A0:
                case 0xBB when profile.Effect == 223:
                case 0xBD when profile.Effect == 223:
                    return ExecuteNativeMeteor223Texture(current, program, resources);
                case 0x2C when selectedHandler == 0xB25CD0:
                    return NativeBattleCinematicSoundStop2C.Execute(current, NativeGlobalSoundProcess ?? throw new NotSupportedException("Native46B450 requires its actual retained original sound IDs."), voice => events.Add(new(Frame, "NativeSoundStopVoice", null, null, voice, "Native46B450 stops the selected sound-effect voice.") { NativeStopSoundVoice = voice }));
                case 0x2D:
                    return NativeBattleCinematicPresentationCommands.SetGraphicsFlag2D(current, graphics.Flags, value => graphics = graphics with { Flags = value });
                case 0x53:
                    return NativeBattleCinematicPresentationCommands.FadeMusic53(current, program, (duration, direct) =>
                    {
                        if (!direct && (ResolvedNativeFlags & 2) != 0)
                        {
                            return;
                        }
                        uint channel = direct ? 0u : unchecked((uint)(short)(NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Native cinematic music fade requires its retained return channel.")));
                        NativeGlobalSoundProcess soundOwner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Native cinematic music fade requires its actual sound process.");
                        NativeMusicPlaybackState playback = soundOwner.MusicPlayback ?? throw new NotSupportedException("Native cinematic music fade requires its actual playback owner.");
                        NativeGlobalSoundProcess = soundOwner with { MusicPlayback = playback.FadeGain46BBC0(channel, unchecked((byte)duration), 0) };
                    });
                case 0xAB when profile.Effect is 228 or 229 or 230 or 261 or 262 or 263:
                    return NativeBattleCinematicActorResourceAB.Execute(current, program, (resourceFrame, actorPointer, row) =>
                    {
                        int actorIndex = PhysicalActorIndex(actorPointer);
                        uint directory = profile.Effect switch { 228 => 0x18821A0, 229 => 0x188062C, 230 => 0x187EAB8, 261 => 0x187C20C, 262 => 0x187A2A4, 263 => 0x1877DAC, _ => throw new InvalidOperationException() };
                        if (!resources.AdditionalStaticData.TryGetValue(directory, out ImmutableArray<byte> bytes))
                        {
                            throw new NotSupportedException("Native cinematic actor resource requires its original static DAT; migrate the selected older snapshot explicitly.");
                        }
                        NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actorIndex], NativeGraphicsPoseResource(graphics, actorIndex));
                        NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(directory, bytes.AsSpan());
                        NativeFileStaging = staging;
                        NativeBattleEffectArena arena = resourceFrame.Arena;
                        var binding = NativeBattleVictoryResources.Bind5022C0(bytes, NativeModelCpuResources ?? throw new NotSupportedException("Native cinematic actor binding requires actual CPU resources."), actor, row, directory, ReadPointer, (address, pointer) => arena = arena.WriteResourcePointer(ArenaOffset(address, 4), pointer));
                        NativeEffectMemory = RequireWaterMemory() with { Arena = arena };
                        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, binding.Model) };
                        return resourceFrame with { Arena = arena };

                        NativeBattleResourcePointer? ReadPointer(uint address) => IsNativeEffectArenaAddress(address) ? arena.ReadResourcePointer(ArenaOffset(address, 4)) : staging.ReadResourcePointer(address);
                    }, (resourceFrame, actorPointer, sequence) =>
                    {
                        graphics = SelectNativeHeaderSequence(graphics, checked((byte)PhysicalActorIndex(actorPointer)), sequence);
                        return resourceFrame;
                    });
                case 0x12E:
                    return NativeBattleCinematicNextContact12E.Execute(current, context);
                case 0xB5:
                    return NativeBattleCinematicActorIdentityB5.Execute(current, program);
                case 0xB4:
                case 0xAD:
                case 0x6E when profile.Effect is 228 or 261 or 262 or 263:
                case 0x60 when profile.Effect is 228 or 261 or 262 or 263:
                    var anchored = ExecuteNativeCinematicActorAnchorCommand(current, program, graphics, UpdateSlot);
                    graphics = anchored.Graphics;
                    return anchored.Frame;
                case 0x46:
                    return NativeBattleCinematicContactBranch46.Execute(current, program, context.ReadActionBytes, (_, actor) => NativeBattleStatusBits(combatants.Single(combatant => combatant.NativeIndex == actor)));
                case 0x49:
                    return NativeBattleCinematicActorStore48.BranchScale49(current, program, address => graphics.Models[PhysicalActorIndex(address)]);
                case 0x5F:
                    return NativeBattleCinematicEntityPosition.FromEntityOffset5F(current, program, actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native cinematic entity offset requires its actual position WORDs."), UpdateSlot);
                case 0x60:
                    return NativeBattleCinematicEntityPosition.FromEntityJoint60(current, program, ReadEntityAnchor, actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native cinematic entity orbit requires its actual scale WORD."), UpdateSlot);
                case 0x6E:
                    return NativeBattleCinematicEntityPosition.FromTargetJointAverage6E(current, program, ReadEntityAnchor, UpdateSlot);
                case 0x140:
                    return NativeBattleCinematicEntityPosition.WaitScaledBySlot140(current, program);
                case 0x64:
                    return NativeBattleCinematicActorSelector64.Execute(current, program, context.ReadActionBytes);
                case 0x66:
                    NativeBattleEffectMemory cameraMemory = RequireWaterMemory();
                    NativeBattleScriptedColorRegisters cameraRegisters = cameraMemory.ScriptedColorRegisters ?? throw new NotSupportedException("Native camera-relative matrix requires retained packed input registers.");
                    NativeEffectMemory = cameraMemory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, CoreDword(current, 0x27973E8) + 0x94, cameraRegisters) };
                    return NativeBattleWaterMatrixCommands.Execute(current, program, bank, CreateNativeActorAnchorServices());
                case 0x38:
                    return NativeBattleCinematicActorStore48.StoreColor38(current, definition.NativeGraphics?.Rules.SummonColors?.Transition ?? throw new NotSupportedException("Native actor color store requires the original background color."), address => graphics.Models[PhysicalActorIndex(address)], (address, actor) => graphics = graphics with { Models = graphics.Models.SetItem(PhysicalActorIndex(address), actor) });
                case 0x48:
                    return NativeBattleCinematicActorStore48.Execute(current, address => graphics.Models[PhysicalActorIndex(address)], (address, actor) => graphics = graphics with { Models = graphics.Models.SetItem(PhysicalActorIndex(address), actor) });
                case 0xCB:
                    return NativeBattleCinematicMatrixAimCB.Execute(current, program, ResolveCurrentNode);
                case 0x4E:
                    return NativeBattleCinematicInlineData4E.Execute(current, program);
                case 0xF2:
                    return NativeBattleCinematicControlCommands.BakeCachedPositionF2(current);
                case 0x83 when selectedHandler == 0xB2DF10:
                    return NativeBattleCinematicControlCommands.StoreCachedColor83(current);
                case 0x9B:
                    return NativeBattleCinematicControlCommands.ClearFlags9B(current);
                case 0xFB:
                    return NativeBattleCinematicControlCommands.WaitWhileCachedAtLeastFB(current, program);
                case 0xAC:
                    return NativeBattleCinematicAccelerationAC.Execute(current, program, NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length), ResolveCurrentNode);
                case 0x92:
                    return NativeBattleCinematicControlCommands.StoreTexturePair92(current, program);
                case 0x91:
                    return NativeBattleCinematicCopyPose91.Execute(current, program, ResolveCurrentNode, UpdateSlot);
                case 0xAE when selectedHandler == 0xB2FBE0:
                    return NativeBattleCinematicNextActionAE.Execute(current, program, value => NativeBattleCinematicNextActionAE.CaptureCurrent(value, assets[1].Data.Length, context.ReadActionBytes, graphics.Models));
                case 0xB7 when profile.Effect == 206 || selectedHandler == 0xAE3350:
                    return NativeBattleCinematicControlCommands.DebugParametersB7(current, program);
                case 0xB6 when profile.Effect == 206:
                    return NativeBattleCinematicBlitB8.ExecuteRelativeB6(current, program, QueueTextureCopy);
                case 0xB8 when profile.Effect == 206:
                    return NativeBattleCinematicBlitB8.Execute(current, program, resources, (copyFrame, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(copyFrame, resources, selector).Selected, QueueTextureCopy);
                case 0xB2:
                    return NativeBattleCinematicControlCommands.StoreRenderMaskB2(current, program);
                case 0x1A:
                    return NativeBattleCinematicControlCommands.RegisterUpdateBlock1A(current, program);
                case 0x1C:
                    return NativeBattleCinematicControlCommands.StoreRelative1C(current, program);
                case 0xB0:
                    return NativeBattleCinematicControlCommands.WaitRandomB0(current, program);
                case 0x57:
                    return NativeBattleScriptedCommandReads.Advance(current, 2);
                case 0x9D:
                    return NativeBattleCinematicCenterShape9D.Execute(current, program, (frame, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, selector).Selected, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0x82:
                    return NativeBattleCinematicVelocity82.Execute(current, program, (frame, selector) =>
                    {
                        int selected = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, index, frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                        return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, selected));
                    });
                case 0xA4:
                    return NativeBattleCinematicPositionModeA4.Execute(current, program, (frame, selector) =>
                    {
                        int selected = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, index, frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                        return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, selected));
                    }, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0x89:
                {
                    uint modelAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(current, 0xBC, 4));
                    byte shapeIndex = current.Arena.Read(ArenaOffset(unchecked(modelAddress + 8), 1), 1)[0];
                    uint shapeAddress = NativeBattleWaterShapeSelection.ResolveCinematic(current, resources, shapeIndex).Selected;
                    return NativeBattleCinematicModelRefresh89.Execute(current, (frame, selector) => NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, selector), ModelServices(shapeAddress));
                }
                case 0x72:
                    return NativeBattleCinematicActorRotation72.Execute(current, (_, address, length) =>
                    {
                        uint relative = unchecked(address - 0x1D972C0);
                        int row = checked((int)(relative / 156));
                        uint offset = relative % 156;
                        if (row >= graphics.Models.Length || length != 2 || offset is not (12 or 14 or 16))
                        {
                            throw new NotSupportedException("Native cinematic actor rotation requires its exact current model WORD.");
                        }
                        NativeBattleGraphicsModel actor = graphics.Models[row];
                        short value = (offset switch
                        {
                            12 => actor.Rotation12,
                            14 => actor.Rotation14,
                            _ => actor.Rotation16
                        }) ?? throw new NotSupportedException("Native cinematic actor rotation is unknown.");
                        return BitConverter.GetBytes(value);
                    });
                case 0xCE:
                    return NativeBattleCinematicControlCommands.SetRelativeActorIdCE(current, program);
                case 0xD3:
                    return NativeBattleCinematicControlCommands.ScaleReferencedPositionColorD3(current, program, ResolveCurrentNode);
                case 0x7C:
                    return NativeBattleCinematicPolar7C.Execute(current, program);
                case 0x9C:
                    return NativeBattleWaterChildCommands.Execute9C(current, program, bank, checked(current.TemporaryFrame.Address + 0x100), UpdateSlot);
                case 0x5D:
                    return NativeBattleWaterChildCommands.Execute5D(current, program, bank, checked(current.TemporaryFrame.Address + 0x100), UpdateSlot);
                case 0x70:
                    return NativeBattleCinematicProfileHeight70.Execute(current, program, new(ResolveCurrentNode, (frame, address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2)), UpdateSlot));
                case 0x71:
                    var explicitShadow = ExecuteNativeCinematicShadow71(current, graphics);
                    graphics = explicitShadow.Graphics;
                    return explicitShadow.Frame;
                case 0x6F:
                    var groundCapture = ExecuteNativeCinematicOffscreen(current, program, assets[1].Data.Length, graphics);
                    graphics = groundCapture.Graphics;
                    return groundCapture.Frame;
                case 0x4F:
                case 0x90:
                    if (selectedHandler is 0xB62F70 or 0xAFDA00 or 0xB23AF0)
                    {
                        var offscreenStep = ExecuteNativeCinematicOffscreen(current, program, assets[1].Data.Length, graphics);
                        graphics = offscreenStep.Graphics;
                        return offscreenStep.Frame;
                    }
                    return NativeBattleScriptedCommandReads.Advance(current, opcode == 0x4F ? 8u : 10u);
                case 0x10C:
                    return NativeBattleCinematicFlagControl10C.Execute(current, program);
                case 0x112 when selectedHandler == 0xA32E10:
                case 0x113 when selectedHandler == 0xA32F50:
                    return NativeBattleCinematicNodeMeasurements.Execute(current, program, new(ResolveCurrentNode, frame => frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, index) }, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0])));
                case 0x10D:
                    return NativeBattleCinematicControlCommands.Wait10D(current, program, ResolveCurrentNode);
                case 0x120:
                    return NativeBattleCinematicControlCommands.SetResourceCursor120(current, program);
                case 0x51:
                    return NativeBattleCinematicControlCommands.Execute51(current, program);
                case 0x41:
                    return NativeBattleCinematicModel.Control41(current, program);
                case 0xD1:
                    return selectedHandler == 0xAFB270 ? NativeBattleCinematicControlCommands.BranchD1(current, program, graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native Brothers stage branch requires its retained resource BYTE98990.")) : current;
                case 0:
                    return NativeBattleWaterStopCommand.StopRemoveAA5120(current);
                case 1:
                    NativeBattleWaterHeaderNextResult next = NativeBattleWaterHeaderNextCommand.Execute(current, frame => Program(frame, CoreDword(frame, 0x2797450)), frame =>
                    {
                        uint address = CoreDword(frame, 0x2798A6C);
                        return new(ArenaOffset(address, assets[1].Data.Length), assets[1].Data.Length, address);
                    }, (_, _, address) =>
                    {
                        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native cinematic reset requires its current action parameters.");
                        return address == WaterParametersAddress ? WaterHeadersAddress + (uint)(20 * parameters.HeaderIndex7C) : throw new NotSupportedException("Native cinematic reset requires its selected parameter base.");
                    }, (_, _, address, _) =>
                    {
                        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new NotSupportedException("Native cinematic reset requires its current action parameters.");
                        int headerIndex = HeaderIndex(address);
                        NativeBattleActionHeaderSlot selected = nativeActionPackets.Headers[headerIndex];
                        NativeBattleActionHeaderSlot first = nativeActionPackets.Headers[parameters.HeaderIndex7C];
                        return new(parameters.Actor78, headerIndex, WaterContacts(first)[0], WaterContacts(selected), selected.ReadByte(0x10), graphics.Models);
                    });
                    return next.Frame;
                case 0x1D:
                    return NativeBattleWaterStopCommand.WaitStop1D(current, program, ResolveCurrentNode);
                case 2:
                case 3:
                case 4:
                case 5:
                case 9:
                case 0x1E:
                case 0x1F:
                case 0x33:
                case 0x74:
                case 0x79:
                case 0xBC:
                case 0xE7:
                    uint header = opcode == 5 ? CoreDword(current, 0x279744C) : 0;
                    NativeBattleWaterScriptControlResult control = NativeBattleWaterScriptControl.Execute(current.Arena, bank, index, program, pc, current.ReadTemporaryByte(0x143), opcode == 5 ? CoreWord(current, header + 2) : (ushort)0);
                    current = current with { Arena = control.Arena };
                    current = WriteCoreDword(current, 0x2797450, control.NextInstructionPointer);
                    if (opcode == 5)
                    {
                        current = WriteCoreWord(current, header + 2, control.HeaderFlagsWord2);
                    }
                    return control.YieldWait is ushort wait ? current.WriteTemporaryWord(0x13E, wait) : current;
                case 0x21:
                case 0x7D:
                    return NativeBattleWaterWorkCommands.Execute(current, program, RequireWaterMemory().MagicTextureBufferAddress1D99A88);
                case 0x20:
                    return NativeBattleWaterContextCommands.Execute(current, program, context);
                case 0x22:
                    return selectedHandler is 0xAFA6E0 or 0xAEF450 ? NativeBattleIfritContactCommand.ExecuteSavedActors(current, context) : NativeBattleIfritContactCommand.Execute(current, context);
                case 0x93:
                case 0xAA:
                    return NativeBattleScriptedLightCommands.Execute(current, program, bank, index);
                case 0x30:
                    PublishNativeModuleSound((RequireWaterMemory().SummonStream ?? throw new NotSupportedException("Native cinematic volume requires its retained summon stream number.")).Play5018C0(128, (uint)(current.ReadTemporaryWord(0x14A) >> 9)));
                    return NativeBattleScriptedCommandReads.Advance(current, 2);
                case 0x3F:
                    return NativeBattleCinematicModel.Register(current, program, ModelServices(0));
                case 0x37:
                    return NativeBattleScriptedMotionReset.Execute(current, bank, UpdateSlot);
                case 0x47:
                    byte[] viewTranslation = ReadSlot(current, 0x94, 6);
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic view translation requires its current camera owner.")) with { ViewTranslationScratch = new(BinaryPrimitives.ReadInt16LittleEndian(viewTranslation), BinaryPrimitives.ReadInt16LittleEndian(viewTranslation.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(viewTranslation.AsSpan(4))) };
                    return NativeBattleScriptedCommandReads.Advance(current, 2);
                case 0x4C:
                    return NativeBattleWaterScalarReferenceCommands.ChangeUpdate(current, program, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0xD2:
                    return NativeBattleWaterScalarReferenceCommands.NegateMotion(current, program, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]), frame => frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) });
                case 0x61:
                case 0x62:
                    return NativeBattleWaterScalarReferenceCommands.CopyMotion(current, program, ResolveCurrentNode, frame => frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) }, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 0x4B:
                    return NativeBattleCinematicPaletteCommands.Execute(current, program, resources, bank, index, (frame, rectangle, address) =>
                    {
                        NativeBattleEffectTextureState textures = RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic palette transfer requires its current texture queue.");
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures.EnqueueScriptedRectangle505DF0(rectangle, address) };
                        return frame;
                    });
                case 0x3C:
                case 0x56:
                case 0x4A:
                case 0x2F:
                case 0x25:
                case 0x81:
                case 0x73:
                    return NativeBattleWaterRectangleCommands.Execute(current, program, bank, index, (frame, shapeIndex) => NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected, (frame, shapeIndex) =>
                    {
                        var selected = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex);
                        return (frame.WriteTemporaryDword(0xFC, selected.Directory), selected.Selected);
                    });
                case >= 0xA and <= 0x17:
                    return NativeBattleWaterInstructionDispatcher.ExecuteVector(current, program, assets[1].Data.Length, frame => UpdateSlot(frame, ReadSlot(frame, 0x18, 1)[0]));
                case 7:
                case 0xA0:
                case 0xA1:
                    return NativeBattleScriptedSchedulingCommands.Execute(current, program);
                case 0x26:
                case 0x32:
                case 0x8B:
                    return NativeBattleWaterChildCommands.Execute(current, program, bank, opcode == 0x32 ? checked(current.TemporaryFrame.Address + 0x100) : 0, UpdateSlot);
                case 0x6D:
                    NativeBattleScriptedProcessFlagsStep flags = NativeBattleScriptedProcessFlags.Execute(current, NativeStage ?? throw new NotSupportedException("Native cinematic flags require their current four stage groups."));
                    NativeStage = flags.State;
                    return flags.Frame;
                case 0x8D:
                    return NativeBattleCinematicActorMeshMask8D.Execute(current, program, address => graphics.Models[PhysicalActorIndex(address)].SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native actor mesh mask requires its current physical sequence owner."), (address, value) =>
                    {
                        int actorIndex = PhysicalActorIndex(address);
                        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
                        NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Native actor mesh mask requires its current physical sequence owner.");
                        graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor with { SequenceState = sequence with { MeshMask124 = value } }) };
                    });
                case 0x2B:
                case 0x4D:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x5A:
                case 0x5E:
                case 0xD0:
                    return NativeBattleWaterPresentationCommands.Execute(current, program, bank, index, new((_, pointer) => graphics.Models[PhysicalActor(pointer)].Flags0, (frame, pointer, value) =>
                    {
                        int row = PhysicalActor(pointer);
                        graphics = graphics with { Models = graphics.Models.SetItem(row, graphics.Models[row] with { Flags0 = value }) };
                        return frame;
                    }, (frame, address, argument, pan) =>
                    {
                        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
                        PublishNativeModuleSound(new(sound, unchecked((uint)(int)argument), null) { Pan = pan, RawPan = pan });
                        return frame;
                    }) { UpdateSlot = UpdateSlot });
                case 0x52:
                    return NativeBattleWaterTextureMetadata.Execute52AA3A60(current, resources);
                case 0x13D:
                    return NativeBattleScriptedVisibility.Execute(current, program, new((_, row) => graphics.Models[row].Flags0, (frame, row, value) =>
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(row, graphics.Models[row] with { Flags0 = value }) };
                        return frame;
                    }));
                case 0x31:
                case 0x34:
                    NativeBattleScriptedStreamStep streamed = NativeBattleScriptedStreamCommands.Execute(current, RequireWaterMemory().SummonStream ?? throw new NotSupportedException("Native cinematic requires its retained summon-stream owner."), (_, address, length) => resources.ReadStatic(address, length).ToArray(), frame =>
                    {
                        NativeEffectMemory = RequireWaterMemory() with { GuardianCasterBank = 0 };
                        return frame;
                    });
                    NativeEffectMemory = RequireWaterMemory() with { SummonStream = streamed.Stream };
                    return streamed.Frame;
                case 0x27:
                case 0x28:
                case 0x29:
                    NativeBattleCinematicTextureCommandStep transferred = NativeBattleCinematicTextureCommands.Execute(current, program, resources, RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic requires its current texture queue."));
                    NativeEffectMemory = RequireWaterMemory() with { Textures = transferred.Textures };
                    return transferred.Frame;
                case 6:
                case 8:
                    NativeBattleCinematicIoStep io = NativeBattleCinematicIoCommands.Execute(current, program, resources, NativeBattleCinematicProfile.ForEffect(effect.Binding.Effect).LoadCallback);
                    if (io.Request is not null)
                    {
                        NativeEffectMemory = RequireWaterMemory() with { Arena = io.Frame.Arena, ScriptedEffects = io.Frame.Core };
                        NativeBattleWaterIoQueued queued = NativeBattleCinematicIoCommands.CopyAndQueue(RequireWaterMemory(), NativeFileIo ?? throw new NotSupportedException("Native cinematic requires its retained file IO scheduler."), io.Request, resources);
                        NativeEffectMemory = queued.Memory;
                        NativeFileIo = queued.FileIo;
                        return io.Frame with { Arena = queued.Memory.Arena!, Core = queued.Memory.ScriptedEffects! };
                    }
                    return io.Frame;
                case 0x39 when current.ReadTemporaryWord(0x14A) >> 12 is 2 or 3:
                    NativeBattleScriptedVibrationStep vibration = NativeBattleScriptedVibrationCommands.Execute(current, program, NativeVibrationProcess ?? throw new NotSupportedException("Native cinematic requires its retained vibration process."), (frame, address) =>
                    {
                        uint file = CoreDword(frame, 0x2798A68);
                        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(file + 0x24, 4), 4));
                        if (address != unchecked(file + relative))
                        {
                            throw new InvalidDataException("Native cinematic vibration resource does not match its current file header.");
                        }
                        return resources.Vibration ?? throw new NotSupportedException("Native cinematic vibration command requires its actual non-null original resource pointer.");
                    });
                    NativeVibrationProcess = vibration.Vibration;
                    return vibration.Frame;
                case 0x39 when profile.Effect == 228 && current.ReadTemporaryWord(0x14A) >> 12 == 5:
                    NativeEffectMemory = RequireWaterMemory() with { SharedCustomMatrix = NativeBattleGuestCamera39.CaptureMode5(current) };
                    return NativeBattleScriptedCommandReads.Advance(current, 4);
                case 0x39:
                    return NativeBattleScriptedCameraCommands.Execute(current, new((frame, address) =>
                    {
                        NativeBattleWaterScriptProgram file = Program(frame, address);
                        int relative = checked((int)(address - file.Address));
                        NativeBattleEffectHeapAllocation animation = new("cinematic-camera", frame.Arena.Read(file.ArenaOffset + relative, file.Length - relative).ToImmutableArray());
                        NativeBattleEffectTextureState textures = (RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic camera requires its current texture control owner.")).Register5099A0(animation);
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures };
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera owner.")).RegisterControlResource(textures.Animation);
                        return frame;
                    }, frame =>
                    {
                        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera owner.")).Apply50A730();
                        return frame;
                    })
                    {
                        ReadCamera = _ => NativeCamera ?? throw new NotSupportedException("Native cinematic requires its current camera globals."),
                        PublishCamera = (frame, cameraState) =>
                        {
                            NativeCamera = cameraState;
                            return frame;
                        },
                        ResolveBoneB65370 = (frame, selector) =>
                        {
                            NativeBattleWaterSlotBank currentBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, assets[1].Data.Length);
                            int found = NativeBattleWaterSlots.LookupB65370(frame.Arena, currentBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, currentBank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)currentBank.SlotOffset(frame.Arena, found));
                        },
                        ReadGraphicsFlags = _ => graphics.Flags,
                        PublishGraphicsFlags = (frame, value) =>
                        {
                            graphics = graphics with { Flags = value };
                            return frame;
                        }
                    }, program);
                case 0x1B:
                case 0xA2:
                case 0xCD:
                case 0x23:
                case 0x24:
                case 0x2A:
                case 0x3B:
                case 0x3E:
                case 0x40:
                case 0x68:
                case 0xB1:
                    return NativeBattleWaterFieldControl.Execute(current, program, bank, index);
                case 0x58:
                case 0x59:
                    return NativeBattleWaterParticleCommands.Execute(current, program, ResolveCurrentNode);
                case 0x63:
                case 0x69:
                case 0x6A:
                case 0xC4:
                case 0x67:
                case 0xD4:
                case 0x104:
                case 0x105:
                case 0x116:
                    return NativeBattleWaterMatrixCommands.Execute(current, program, bank, CreateNativeActorAnchorServices());
                case 0xC3:
                    return NativeBattleScriptedSoundCommands.ExecuteC3(current, (NativeGlobalSoundProcess ?? throw new NotSupportedException("Native cinematic sound control requires its current backend owner.")).Backend);
                case 0xC5:
                    return NativeBattleCinematicEncounterBranchC5.Execute(current, program, checked((ushort)(definition.EncounterId ?? throw new NotSupportedException("Native Brothers encounter branch requires its current encounter WORD."))));
                case 0xCF:
                    return selectedHandler is 0xAFCBE0 or 0xAF1830 ? NativeBattleWaterContextCommands.ExecuteRing(current, context) : current;
                case 0x5B:
                    var bone = ExecuteNativeCinematicBone5B(current, program, profile, resources, assets[1].Data.Length, graphics, UpdateSlot);
                    graphics = bone.Graphics;
                    return bone.Frame;
                case 0x5C:
                    if (selectedHandler is not (0xB64090 or 0xAFEBF0 or 0xAF3660))
                    {
                        return current;
                    }
                    return NativeBattleCinematicShapePosition5C.Execute(current, program, new((frame, selector) => ResolveCurrentNode(frame, selector), (frame, selector) => NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, selector).Selected, (frame, address) =>
                    {
                        NativeBattleEffectMemory memory = RequireWaterMemory();
                        NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native shape position requires its current packed vector input.");
                        NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(frame, address, registers) };
                        return frame;
                    }, UpdateSlot));
                case 0x84:
                    return NativeBattleCinematicMatrixCopy84.Execute(current, program, ResolveCurrentNode);
                case 0xEB:
                    return NativeBattleScriptedCommandReads.Advance(NativeBattleWaterParticleCommands.AppendDrawB652E0(current, current.ReadTemporaryByte(0x142)), 2);
                case 0x8A:
                    return NativeBattleCinematicDisplay8A.Execute(current, program, new((address, parameter83, parameter81) =>
                    {
                        if (address != WaterParametersAddress)
                        {
                            throw new NotSupportedException("Native cinematic display command requires its current action parameter owner.");
                        }
                        outputParameters = (outputParameters ?? throw new NotSupportedException("Native cinematic display command requires its current action parameters.")) with { Parameter83 = parameter83, Parameter81 = parameter81 };
                    }, duration => graphics = graphics with { Auxiliary = graphics.Auxiliary.ReverseScreenBlend501F30(duration) }, duration => graphics = graphics with { Auxiliary = graphics.Auxiliary.BeginScreenBlend501E40(duration) }, () =>
                    {
                        NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Native display command requires its current UI flags.");
                        graphics = graphics with { UiServices = ui.SetFlags(0x200, 0x100) };
                    }));
                case 0x7A:
                    return NativeBattleCinematicMatrixCompose7A.Execute(current, program, ResolveCurrentNode);
                case 0x9A:
                    return NativeBattleCinematicActorOrder9A.Execute(current, program);
                case 0x42:
                    if (selectedHandler is not (0xB24BB0 or 0xAFEF60 or 0xB57280 or 0x9C8CC0 or 0x963E30))
                    {
                        return current;
                    }
                    return NativeBattleCinematicSequence42.Execute(current, program, new((actor, sequence) => graphics = SelectNativeHeaderSequence(graphics, PhysicalActor42(actor), sequence), actor =>
                    {
                        NativeBattleSequenceState state = graphics.Models[PhysicalActor42(actor)].SequenceState ?? throw new NotSupportedException("Native sequence branch requires its current pose state.");
                        return (state.PoseFrame114 ?? throw new NotSupportedException("Native sequence branch requires its pose frame.")) == (state.PoseLimit115 ?? throw new NotSupportedException("Native sequence branch requires its pose limit."));
                    }));
                    byte PhysicalActor42(byte actor)
                    {
                        uint pointer = CoreDword(current, unchecked(CoreDword(current, 0x279744C) + 0x60u + 4u * actor));
                        if (pointer < 0x1D972C0 || (pointer - 0x1D972C0) % 156 != 0 || (pointer - 0x1D972C0) / 156 >= graphics.Models.Length)
                        {
                            throw new NotSupportedException("Native sequence command requires its actual physical model pointer.");
                        }
                        return checked((byte)((pointer - 0x1D972C0) / 156));
                    }
                case 0x96:
                    if (selectedHandler is not (0xB16F60 or 0xAF2760))
                    {
                        return current;
                    }
                    var capture = ExecuteNativeCinematicCapture96(current, program, graphics);
                    graphics = capture.Graphics;
                    return capture.Frame;
                case 0xD5:
                    if (selectedHandler != 0xAFB630)
                    {
                        return current;
                    }
                    ushort encounter = checked((ushort)(definition.EncounterId ?? throw new NotSupportedException("Native Brothers target correction requires its encounter WORD1CFF6E0.")));
                    return NativeBattleBrothersTargetCommand.Execute(current, program, new(encounter, () => RequireWaterMemory().ScriptedTarget2796D40 ?? throw new NotSupportedException("Native Brothers target branch requires its retained2796D40 byte."), value => NativeEffectMemory = RequireWaterMemory() with { ScriptedTarget2796D40 = value }, _ => graphics.Models[3]));
                default:
                    throw new NotSupportedException($"Native cinematic instruction{current.ReadTemporaryWord(0x14A):X4} at{pc:X} requires its selected command contract.");
            }
        }

        NativeBattleCinematicEntityAnchor ReadEntityAnchor(NativeBattleWaterExecutionFrame current, int actorIndex, short selector)
        {
            uint pointer = CoreDword(current, unchecked(CoreDword(current, 0x279744C) + 0x60u + (uint)(4 * actorIndex)));
            if (actorIndex < 0 || actorIndex >= graphics.Models.Length || pointer != ActorPointer(actorIndex))
            {
                throw new NotSupportedException("Native cinematic entity anchor requires its actual physical actor row.");
            }
            NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actorIndex).PoseSource ?? throw new NotSupportedException("Native cinematic entity anchor requires its actual current rig.");
            NativeBattleActionMatrix basis = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native cinematic entity anchor requires its current entity matrix.");
            NativeBattleWaterActorAnchorResult result = CreateNativeActorAnchorServices().EvaluateB66B80(current, source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), selector, 4096, basis);
            return new(result.Frame, result.Anchor);
        }

        NativeBattleWaterExecutionFrame UpdateSlot(NativeBattleWaterExecutionFrame current, byte kind)
        {
            uint handler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "bone_table", kind, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.BoneTable + 4u * kind), 4)));
            if (NativeBattleCinematicDispatch.IsNoOp(profile.Effect, "bone_table", kind, handler))
            {
                return current;
            }
            if (handler == 0xB264A0)
            {
                return current;
            }
            if (handler == 0xAA6270)
            {
                return NativeBattleApocalypse221Orbit11.Execute(current, ResolveCurrentNode);
            }
            if (handler == 0xA5D100)
            {
                return NativeBattleCinematicBetween10.Update(current, ResolveCurrentNode);
            }
            if (handler == 0xA671C0)
            {
                return NativeBattleCinematicBetweenUpdate4.Update(current, ResolveCurrentNode);
            }
            if (handler == 0xA2D400)
            {
                return NativeBattleCinematicShapeAnchor8.Update(current, ResolveCurrentNode);
            }
            if (handler == 0xB265B0)
            {
                return NativeBattleCinematicPosePosition9.Update(current, pointer =>
                {
                    int actorIndex = PhysicalActorIndex(pointer);
                    NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
                    NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actorIndex).PoseSource ?? throw new NotSupportedException("Native pose position requires its current actor rig.");
                    NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadDecodedPose(actor.SequenceState ?? throw new NotSupportedException("Native pose position requires its retained current decoded pose."), source);
                    return new(BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(2, 2)), pose.Translation);
                });
            }
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank);
            ImmutableArray<byte> active = kind is 3 or 5 or 7 ? current.Core.Read(0x2797454, 128).ToImmutableArray() : [];
            try
            {
                return current with { Arena = NativeBattleWaterSlots.UpdateSelected(current.Arena, bank, index, active) };
            }
            catch (NotSupportedException error)
            {
                throw new NotSupportedException($"Native cinematic bone kind {kind:X2} handler {handler:X8} requires its current slot-update contract.", error);
            }
        }

        NativeBattleWaterExecutionFrame DrawSlots(NativeBattleWaterExecutionFrame current, int budget)
        {
            return NativeBattleCinematicDraw.Run(profile, current, PrepareLighting, (frame, kind) =>
            {
                uint selectedDrawHandler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "draw_table", kind, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(profile.DrawTable + 4u * kind), 4)));
                if (selectedDrawHandler == 0xA2D000)
                {
                    var culled = DrawNativeCinematicCullingMesh31(frame, assets[1].Data.Length, resources, profile, graphics);
                    graphics = culled.Graphics;
                    return culled.Frame;
                }
                if (selectedDrawHandler == 0xA0E3D0)
                {
                    var clamped = DrawNativeCinematicClampedMesh22(frame, assets[1].Data.Length, resources, graphics);
                    graphics = clamped.Graphics;
                    return clamped.Frame;
                }
                if (selectedDrawHandler == 0xA52E90)
                {
                    var ground = DrawNativeCinematicGroundProfile18(frame, assets[1].Data.Length, graphics);
                    graphics = ground.Graphics;
                    return ground.Frame;
                }
                if (selectedDrawHandler == 0xA5D730)
                {
                    var animation = DrawNativeCinematicActorAnimation2C(frame, assets[1].Data.Length, graphics);
                    graphics = animation.Graphics;
                    return animation.Frame;
                }
                if (selectedDrawHandler == 0xAD9160)
                {
                    var dissolve = DrawNativeCinematicActorDissolve52(frame, assets[1].Data.Length, graphics);
                    graphics = dissolve.Graphics;
                    return dissolve.Frame;
                }
                if (selectedDrawHandler == 0xB429A0)
                {
                    var detached = DrawNativeCinematicDetached66(frame, assets[1].Data.Length, resources, assets, graphics);
                    graphics = detached.Graphics;
                    return detached.Frame;
                }
                if (selectedDrawHandler is 0xAD9BF0 or 0xACE7B0)
                {
                    var palette = DrawNativeCinematicPalette47(frame, graphics);
                    graphics = palette.Graphics;
                    return palette.Frame;
                }
                if (selectedDrawHandler == 0xAC26B0)
                {
                    var shatter = DrawNativeCinematicStageShatter49(frame, assets[1].Data.Length, graphics);
                    graphics = shatter.Graphics;
                    return shatter.Frame;
                }
                if (selectedDrawHandler == 0xAA6770)
                {
                    return NativeBattleApocalypse221ActorTint69.Execute(frame, new((_, address) => graphics.Models[PhysicalActorIndex(address)], (current, address, actor) =>
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(PhysicalActorIndex(address), actor) };
                        return current;
                    }));
                }
                if (selectedDrawHandler is 0xAAB290 or 0xAAB6D0 or 0xAC2230)
                {
                    var grid = DrawNativeCinematicGrid28(frame, assets[1].Data.Length, resources, graphics, selectedDrawHandler);
                    graphics = grid.Graphics;
                    return grid.Frame;
                }
                if (selectedDrawHandler == 0xAABB70)
                {
                    var breakup = DrawNativeApocalypse221Breakup(frame, assets[1].Data.Length, resources, graphics);
                    graphics = breakup.Graphics;
                    return breakup.Frame;
                }
                if (selectedDrawHandler == 0xA90AA0)
                {
                    var meteor = DrawNativeMeteor223Grid(frame, assets[1].Data.Length, resources, graphics);
                    graphics = meteor.Graphics;
                    return meteor.Frame;
                }
                if (selectedDrawHandler == 0xA91F20)
                {
                    uint manager = CoreDword(frame, 0x27973EC);
                    uint action = CoreDword(frame, manager + 0xCC);
                    int count = context.ReadActionBytes(frame, action + 0x10, 1)[0];
                    uint targets = BinaryPrimitives.ReadUInt32LittleEndian(context.ReadActionBytes(frame, action + 8, 4));
                    return NativeBattleMeteor223Commands.SpreadResults(frame, count, (current, target) => context.ApplyContact506690(current, unchecked(targets + (uint)(24 * target))));
                }
                if (selectedDrawHandler == 0xB4BBE0)
                {
                    var breakup = DrawNativeControl18Breakup(frame, assets[1].Data.Length, graphics);
                    graphics = breakup.Graphics;
                    return breakup.Frame;
                }
                if (selectedDrawHandler == 0xA16E80)
                {
                    var actorGlow = DrawNativeCinematicActorVertexColor20(frame, assets[1].Data.Length, resources, graphics);
                    graphics = actorGlow.Graphics;
                    return actorGlow.Frame;
                }
                if (selectedDrawHandler is 0xA49C20 or 0xA3F700)
                {
                    var monster = DrawNativeCinematicMonsterActor(frame, selectedDrawHandler == 0xA3F700, assets[1].Data.Length, resources, ModelServices(0), graphics);
                    graphics = monster.Graphics;
                    return monster.Frame;
                }
                if (NativeBattleCinematicDispatch.IsNoOp(profile.Effect, "draw_table", kind, selectedDrawHandler))
                {
                    return frame;
                }
                if (selectedDrawHandler == 0x9469E0)
                {
                    return NativeBattleCinematicLinkedPosition70.Draw(frame, pointer => graphics.Models[PhysicalActorIndex(pointer)].LinkedTransform ?? throw new NotSupportedException("Native linked draw requires its actual linked model."), (pointer, linked) =>
                    {
                        int actor = PhysicalActorIndex(pointer);
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { LinkedTransform = linked }) };
                    });
                }
                if (selectedDrawHandler == 0x950B50)
                {
                    var trail = DrawNativeKiros262Trail(frame, graphics);
                    graphics = trail.Graphics;
                    return trail.Frame;
                }
                if (selectedDrawHandler == 0xAEA550)
                {
                    NativeBattleEffectMemory feedbackMemory = RequireWaterMemory();
                    NativeBattlePcRenderRequest request = feedbackMemory.PcRenderRequest ?? throw new NotSupportedException("Native cinematic feedback requires its retained PC render request owner.");
                    NativeEffectMemory = feedbackMemory with { PcRenderRequest = request.Request47CF50(1, 255, 255, 255, 127) };
                    return frame;
                }
                if (selectedDrawHandler == 0xAE52C0)
                {
                    var wire = DrawNativeCinematicWireGrid38(frame, assets[1].Data.Length, resources, graphics);
                    graphics = wire.Graphics;
                    return wire.Frame;
                }
                if (selectedDrawHandler == 0xAE59E0)
                {
                    var grid = DrawNativeCinematicGrid59(frame, assets[1].Data.Length, resources, graphics);
                    graphics = grid.Graphics;
                    return grid.Frame;
                }
                if (selectedDrawHandler == 0xAE6CA0)
                {
                    var actor = DrawNativeCinematicGridActor3C(frame, assets[1].Data.Length, graphics);
                    graphics = actor.Graphics;
                    return actor.Frame;
                }
                if (selectedDrawHandler is 0xAE71D0 or 0xAE8020)
                {
                    var radial = DrawNativeCinematicRadialMesh(frame, selectedDrawHandler == 0xAE8020, assets[1].Data.Length, resources, graphics);
                    graphics = radial.Graphics;
                    return radial.Frame;
                }
                if (selectedDrawHandler is 0xAE8C10 or 0xAE43A0)
                {
                    var placement = DrawNativeCinematicActorPlacement(frame, selectedDrawHandler == 0xAE8C10, graphics);
                    graphics = placement.Graphics;
                    return placement.Frame;
                }
                if (selectedDrawHandler == 0xAEA0C0)
                {
                    var copy = DrawNativeCinematicFrameCopy26(frame, graphics);
                    graphics = copy.Graphics;
                    return copy.Frame;
                }
                if (selectedDrawHandler == 0xAE44E0)
                {
                    var actor = DrawNativeCinematicBrothersActor38(frame, graphics, true);
                    graphics = actor.Graphics;
                    return actor.Frame;
                }
                if (selectedDrawHandler == 0xAED040)
                {
                    var sparks = DrawNativeCinematicSpark53(frame, assets[1].Data.Length, graphics);
                    graphics = sparks.Graphics;
                    return sparks.Frame;
                }
                if (selectedDrawHandler == 0xAE86F0)
                {
                    var wireMorph = DrawNativeCinematicWireMorph3F(frame, assets[1].Data.Length, resources, graphics);
                    graphics = wireMorph.Graphics;
                    return wireMorph.Frame;
                }
                if (selectedDrawHandler == 0xB5BB00)
                {
                    var mesh = DrawNativeCinematicMesh(frame, 7, assets[1].Data.Length, resources, graphics);
                    graphics = mesh.Graphics;
                    return mesh.Frame;
                }
                if (selectedDrawHandler == 0xB5A5B0)
                {
                    uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
                    uint shape = model == 0 ? 0 : NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, frame.Arena.Read(ArenaOffset(model + 8, 1), 1)[0]).Selected;
                    var posed = DrawNativeCinematicLeviathanModel20(frame, assets[1].Data.Length, resources, ModelServices(shape), graphics);
                    graphics = posed.Graphics;
                    return posed.Frame;
                }
                if (selectedDrawHandler == 0xB5AB80)
                {
                    uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
                    uint shape = model == 0 ? 0 : NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, frame.Arena.Read(ArenaOffset(model + 8, 1), 1)[0]).Selected;
                    var leviathan = DrawNativeCinematicLeviathan24(frame, assets[1].Data.Length, resources, ModelServices(shape), graphics);
                    graphics = leviathan.Graphics;
                    return leviathan.Frame;
                }
                if (selectedDrawHandler is 0xAF59D0 or 0x95ACB0)
                {
                    var actor = DrawNativeCinematicBrothersActor38(frame, graphics);
                    graphics = actor.Graphics;
                    return actor.Frame;
                }
                if (selectedDrawHandler == 0xAF6040)
                {
                    var clone = DrawNativeCinematicActorClone48(frame, assets[1].Data.Length, graphics);
                    graphics = clone.Graphics;
                    return clone.Frame;
                }
                if (selectedDrawHandler is 0xB01290 or 0xB1A130 or 0xB0D690 or 0xAE8E20)
                {
                    var ribbon = DrawNativeCinematicRibbon37(frame, assets[1].Data.Length, graphics);
                    graphics = ribbon.Graphics;
                    return ribbon.Frame;
                }
                if (selectedDrawHandler == 0xAE4C70)
                {
                    var scanlines = DrawNativeCinematicScanlines37(frame, graphics);
                    graphics = scanlines.Graphics;
                    return scanlines.Frame;
                }
                if (selectedDrawHandler is 0xB1E3F0 or 0xAECD20)
                {
                    var points = DrawNativeCinematicPointCloud27(frame, assets[1].Data.Length, graphics);
                    graphics = points.Graphics;
                    return points.Frame;
                }
                if (selectedDrawHandler == 0xB594B0)
                {
                    var wave = DrawNativeCinematicWave(frame, assets[1].Data.Length, resources, graphics);
                    graphics = wave.Graphics;
                    return wave.Frame;
                }
                if (selectedDrawHandler == 0xB1E6C0)
                {
                    var grid = DrawNativeCinematicGrid28(frame, assets[1].Data.Length, resources, graphics);
                    graphics = grid.Graphics;
                    return grid.Frame;
                }
                if (selectedDrawHandler == 0xB1B5C0)
                {
                    var strip = DrawNativeCinematicStrip29(frame, assets[1].Data.Length, resources, graphics);
                    graphics = strip.Graphics;
                    return strip.Frame;
                }
                if (selectedDrawHandler == 0xB1B9E0)
                {
                    var trail = DrawNativeCinematicTrail2B(frame, assets[1].Data.Length, resources, graphics);
                    graphics = trail.Graphics;
                    return trail.Frame;
                }
                if (selectedDrawHandler == 0xB11C30)
                {
                    var columns = DrawNativeCinematicColumns2D(frame, graphics);
                    graphics = columns.Graphics;
                    return columns.Frame;
                }
                if (selectedDrawHandler == 0xAEA420)
                {
                    var environment = DrawNativeCinematicEnvironment36(frame, graphics);
                    graphics = environment.Graphics;
                    return environment.Frame;
                }
                if (selectedDrawHandler == 0xB1F2A0)
                {
                    var sprite = DrawNativeCinematicSprite2E(frame, assets[1].Data.Length, graphics);
                    graphics = sprite.Graphics;
                    return sprite.Frame;
                }
                if (kind == 4)
                {
                    var overlay = DrawNativeCinematicOverlay(frame, graphics);
                    graphics = overlay.Graphics;
                    return overlay.Frame;
                }
                if (kind is 5 or 6)
                {
                    var drawn = DrawNativeCinematicRectangles(frame, kind == 6 ? Program(frame, CoreDword(frame, 0x2798A68)) : null, assets[1].Data.Length, resources, graphics, kind);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                if (kind == 0x15)
                {
                    return NativeBattleCinematicScroll.Draw(frame, resources, (scrollFrame, rectangle, source) =>
                    {
                        NativeBattleEffectTextureState textures = RequireWaterMemory().Textures ?? throw new NotSupportedException("Native cinematic scroll requires its current texture queue.");
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures.EnqueueArenaRectangle505DF0(rectangle, scrollFrame.Arena, ArenaOffset(source, 0)) };
                        return scrollFrame;
                    });
                }
                if (selectedDrawHandler is 0xB198C0 or 0xB58FA0 or 0xB59150 or 0xB4B450)
                {
                    return NativeBattleCinematicRowScroll.Draw(frame, resources, selectedDrawHandler == 0xB59150, (scrollFrame, rectangle, source) =>
                    {
                        NativeBattleEffectTextureState textures = RequireWaterMemory().Textures ?? throw new NotSupportedException("Native row scroll requires its current texture queue.");
                        NativeEffectMemory = RequireWaterMemory() with { Textures = textures.EnqueueArenaRectangle505DF0(rectangle, scrollFrame.Arena, ArenaOffset(source, 0)) };
                        return scrollFrame;
                    });
                }
                if (kind is 1 or 2 or 9 or 0x1B or 0x39)
                {
                    var drawn = DrawNativeCinematicMesh(frame, kind, assets[1].Data.Length, resources, graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                if (kind == 3)
                {
                    uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
                    byte shapeIndex = frame.Arena.Read(ArenaOffset(unchecked(model + 8), 1), 1)[0];
                    uint shape = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected;
                    var drawn = selectedDrawHandler == 0xAF5810 ? DrawNativeCinematicBrothersModel3(frame, assets[1].Data.Length, ModelServices(shape), graphics) : DrawNativeCinematicModel(frame, assets[1].Data.Length, ModelServices(shape), graphics);
                    graphics = drawn.Graphics;
                    return drawn.Frame;
                }
                return kind == 0x26 ? frame : throw new NotSupportedException($"Native cinematic draw{kind:X2} requires its selected lit geometry contract.");
            }, budget);
        }

        uint ResolveCurrentNode(NativeBattleWaterExecutionFrame frame, short selector)
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, assets[1].Data.Length);
            int found = NativeBattleWaterSlots.LookupB65370(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank), frame.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, found));
        }

        NativeBattleCinematicModelServices ModelServices(uint selectedShape)
        {
            return new((frame, shapeIndex) =>
            {
                selectedShape = NativeBattleWaterShapeSelection.ResolveCinematic(frame, resources, shapeIndex).Selected;
                return selectedShape;
            }, (frame, animationAddress) =>
            {
                uint extent = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(selectedShape, 4), 4));
                uint relative = unchecked(animationAddress - selectedShape);
                NativeBattleWaterScriptProgram owner = Program(frame, selectedShape);
                if (relative > extent || extent > int.MaxValue || unchecked(selectedShape - owner.Address) > owner.Length || extent > owner.Length - unchecked(selectedShape - owner.Address))
                {
                    throw new NotSupportedException("Native embedded model animation section leaves its current complete source owner.");
                }
                return frame.Arena.Read(ArenaOffset(animationAddress, checked((int)(extent - relative))), checked((int)(extent - relative)));
            });
        }

        NativeBattleWaterExecutionFrame PrepareLighting(NativeBattleWaterExecutionFrame current, sbyte selector)
        {
            NativeBattleScriptedLightingMemory lighting = RequireWaterMemory().ScriptedLighting ?? throw new NotSupportedException("Native cinematic requires its retained light and color register owner.");
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, assets[1].Data.Length);
            NativeBattleScriptedLightingStep prepared = NativeBattleScriptedLighting.Prepare(current, lighting, (frame, parent) => NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, parent));
            NativeEffectMemory = RequireWaterMemory() with { ScriptedLighting = prepared.Lighting };
            return prepared.Frame;
        }

        NativeBattleWaterExecutionFrame ClearOrderingTable(NativeBattleWaterExecutionFrame current, uint address, int count)
        {
            if (address != 0x44 || count != 0x1000)
            {
                throw new NotSupportedException("Native cinematic clear requires its selected ordering-table range.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic requires its current ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = primitive.Pending.ClearRange45D550(checked((int)(address / 4)), count) } };
            return current;
        }

        NativeBattleWaterExecutionFrame PublishFocal(NativeBattleWaterExecutionFrame current, short focal)
        {
            NativeCamera = NativeCamera! with { Projection = NativeCamera!.Projection! with { FocalLength = unchecked((ushort)focal) } };
            return current;
        }
    }
}
