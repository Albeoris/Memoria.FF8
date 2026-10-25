using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleFileIoState? NativeFileIo { get; private set; }
    public NativeVibrationProcessState? NativeVibrationProcess { get; private set; }

    private void QueueNativeFileIo482610(int fileResult, uint completionCallback, int? outerBattlePhase = null)
    {
        NativeBattleFileIoState state = NativeFileIo ?? throw new NotSupportedException("Native482610 requires its actual sixteen-row file IO owner.");
        NativeFileIo = state.Queue482610(fileResult, completionCallback, outerBattlePhase);
    }

    private void PumpNativeFileIo482590()
    {
        NativeBattleFileIoState state = NativeFileIo ?? throw new NotSupportedException("Native48D0C0/482590 requires its actual retained file IO owner; explicitly migrate this standalone snapshot.");
        NativeFileIo = state.Advance482590((current, completion) =>
        {
            ObserveNativeUnknownStackCallback(completion.Callback, "file completion");
            NativeFileIo = current;
            switch (completion.Callback)
            {
                case 0x508470:
                    NativeEffectIoResult = completion.Result;
                    break;
                case 0x82AF90:
                case 0x82AFF0:
                case 0x82B000:
                case 0x82B010:
                case 0x82B020:
                case 0x82AFC0:
                    CompleteNativeCurse47File(completion.Callback);
                    break;
                case 0x8C5160:
                case 0x8C5190:
                case 0x8C51C0:
                case 0x8C51D0:
                case 0x8C51E0:
                case 0x8C51F0:
                    CompleteNativeDoom8File(completion.Callback);
                    break;
                case 0x7B53C0:
                case 0x7B53F0:
                case 0x7B5420:
                case 0x7B5430:
                case 0x7B5440:
                case 0x7B5450:
                case 0x7BE1E0:
                    CompleteNativeEffect79File(completion.Callback);
                    break;
                case 0x6A6360:
                    CompleteNativeEffect140File();
                    break;
                case 0x6C3580:
                    NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C3580 requires its retained effect arena.");
                    NativeEffectMemory = NativeBattleEffectIo.Complete6C3580(memory, ResolveNativeQuezacotlSource).Memory;
                    break;
                case 0x534150:
                    NativeEffectMemory = NativeBattleEffectIo.Complete534150(NativeEffectMemory ?? throw new NotSupportedException("Native534150 requires its retained effect arena.")).Memory;
                    break;
                case 0x689810:
                    NativeEffectMemory = NativeBattleEffectIo.Complete689810(NativeEffectMemory ?? throw new NotSupportedException("Native Duel file completion requires its retained effect arena."), ResolveNativeQuezacotlSource).Memory;
                    break;
                case 0x774030:
                case 0x7740A0:
                    CompleteNativeRinoa87Io(completion.Callback);
                    break;
                case 0x768CC0:
                case 0x768D20:
                case 0x768D30:
                case 0x768D40:
                    CompleteNativeRinoa88Io(completion.Callback);
                    break;
                case 0x77F980:
                    CompleteNativeRinoa86Io(completion.Callback);
                    break;
                case 0x732590:
                    NativeBattleEffectMemory moogleMemory = NativeEffectMemory ?? throw new NotSupportedException("Native732590 requires its retained effect arena.");
                    NativeBattleMoogle96State moogle = moogleMemory.Moogle96 ?? throw new NotSupportedException("Native732590 requires its current Moogle IO owner.");
                    NativeBattleMoogle96IoCompletion uploaded = NativeBattleMoogle96Io.Complete732590(moogle.Io ?? throw new NotSupportedException("Native732590 requires its retained script and current destination."), moogleMemory);
                    NativeEffectMemory = uploaded.Memory with { Moogle96 = moogle with { Io = uploaded.State } };
                    break;
                case 0x72A1F0:
                case 0x72A250:
                case 0x72A260:
                    NativeBattleEffectMemory chocoMemory = NativeEffectMemory ?? throw new NotSupportedException("Native Choco file completion requires retained effect memory.");
                    NativeBattleChoco97State choco = chocoMemory.Choco97 ?? throw new NotSupportedException("Native Choco file completion requires its retained process.");
                    NativeBattleChoco97IoCompletion chocoUploaded = NativeBattleChoco97Io.Complete(choco.Io ?? throw new NotSupportedException("Native Choco file completion requires its current IO script."), chocoMemory, completion.Callback);
                    NativeEffectMemory = chocoUploaded.Memory with { Choco97 = choco with { Io = chocoUploaded.State } };
                    break;
                case 0x721FF0:
                case 0x722050:
                case 0x722060:
                    NativeBattleEffectMemory choco98Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Choco98 file completion requires retained effect memory.");
                    NativeBattleChoco98State choco98 = choco98Memory.Choco98 ?? throw new NotSupportedException("Native Choco98 file completion requires its retained process.");
                    NativeBattleChoco98IoCompletion choco98Uploaded = NativeBattleChoco98Io.Complete(choco98.Io ?? throw new NotSupportedException("Native Choco98 file completion requires its current IO script."), choco98Memory, completion.Callback);
                    NativeEffectMemory = choco98Uploaded.Memory with { Choco98 = choco98 with { Io = choco98Uploaded.State } };
                    break;
                case 0x70F130:
                case 0x70F190:
                case 0x70F1A0:
                case 0x70F1C0:
                    NativeBattleEffectMemory choco100Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Choco100 file completion requires retained effect memory.");
                    NativeBattleChoco100State choco100 = choco100Memory.Choco100 ?? throw new NotSupportedException("Native Choco100 file completion requires its retained process.");
                    NativeBattleChoco100IoCompletion choco100Uploaded = NativeBattleChoco100Io.Complete(choco100.Io ?? throw new NotSupportedException("Native Choco100 file completion requires its current IO script."), choco100Memory, completion.Callback);
                    NativeEffectMemory = choco100Uploaded.Memory with { Choco100 = choco100 with { Io = choco100Uploaded.State } };
                    break;
                case 0x718510:
                case 0x718570:
                case 0x718580:
                case 0x718590:
                    NativeBattleEffectMemory choco99Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Choco99 file completion requires retained effect memory.");
                    NativeBattleChoco99State choco99 = choco99Memory.Choco99 ?? throw new NotSupportedException("Native Choco99 file completion requires its retained process.");
                    NativeBattleChoco99IoCompletion choco99Uploaded = NativeBattleChoco99Io.Complete(choco99.Io ?? throw new NotSupportedException("Native Choco99 file completion requires its current IO script."), choco99Memory, completion.Callback);
                    NativeEffectMemory = choco99Uploaded.Memory with { Choco99 = choco99 with { Io = choco99Uploaded.State } };
                    break;
                case 0x73A580:
                case 0x73A5B0:
                case 0x73A5E0:
                case 0x73A5F0:
                case 0x73A600:
                case 0x73A610:
                    CompleteNativeSiren95File(completion.Callback);
                    break;
                case 0x784C60:
                case 0x784CC0:
                case 0x784CD0:
                case 0x784CE0:
                case 0x784CF0:
                    CompleteNativeTheEnd85File(completion.Callback);
                    break;
                case 0x762A50:
                case 0x762A80:
                case 0x762AB0:
                case 0x762AC0:
                case 0x762AD0:
                case 0x762AE0:
                    CompleteNativeTonberry90File(completion.Callback);
                    break;
                case 0xAA0ED0:
                case 0xB2BB40:
                case 0xB60530:
                case 0xB21090:
                case 0xB143F0:
                case 0xB07C20:
                case 0xAFAD70:
                case 0xAEFAE0:
                case 0xA57CA0:
                case 0xA4D9C0:
                case 0xA44370:
                case 0xA80F60:
                case 0xA76420:
                case 0xA8B230:
                case 0xA96AF0:
                case 0xAAEA40:
                case 0x960270:
                case 0x955350:
                case 0x94B470:
                case 0x97E0A0:
                case 0x974E80:
                case 0xA25630:
                case 0xA1B840:
                case 0xB45FB0:
                case 0xB3A150:
                case 0xADE7D0:
                case 0xAD3300:
                case 0xAC5950:
                case 0xAB8EA0:
                    NativeBattleEffectMemory scriptedMemory = NativeEffectMemory ?? throw new NotSupportedException("Native scripted-effect completion requires its current effect memory.");
                    NativeBattleScriptedEffectMemory scripted = scriptedMemory.ScriptedEffects ?? throw new NotSupportedException("Native scripted-effect completion requires its retained process owner.");
                    NativeEffectMemory = scriptedMemory with { ScriptedEffects = NativeBattleWaterIoCommands.CompleteAA0ED0(scripted) };
                    break;
                default:
                    throw new NotSupportedException($"Native482870 file completion callback{completion.Callback:X8} is unsupported.");
            }
            return NativeFileIo ?? throw new InvalidDataException("Native482870 completion lost its file IO owner.");
        });
    }

    private void QueueNativeQuezacotlFile(NativeBattleEffectFileRequest request, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectFileAsset file = (effect.Binding.NativeQuezacotlResources ?? throw new NotSupportedException("Native6C3940 requires its original battle archive stream resources.")).Files.Single(file => file.FileId == request.FileId);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl file request requires its current effect memory.");
        if (request.DestinationAddress is >= 0x20DFAB8 and < 0x21DFAB8)
        {
            NativeEffectMemory = memory with { Arena = (memory.Arena ?? throw new NotSupportedException("Native Quezacotl file request requires its actual effect arena.")).Write(checked((int)(request.DestinationAddress - 0x20DFAB8)), file.Data.AsSpan()) };
        }
        else if (request.DestinationAddress >= NativeBattleQuezacotlExecutableMemory.Address && request.DestinationAddress < NativeBattleQuezacotlExecutableMemory.Address + NativeBattleQuezacotlExecutableMemory.Length)
        {
            NativeEffectMemory = memory with { QuezacotlExecutableMemory = (memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native Quezacotl file request requires its current executable model regions.")).Write(request.DestinationAddress, file.Data.AsSpan()) };
        }
        else
        {
            throw new NotSupportedException("Native Quezacotl file destination requires an owned native allocation.");
        }
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
    }

    private NativeBattleEffectHeapAllocation ResolveNativeQuezacotlSource(uint address, int? requestedLength)
    {
        if (address is >= 0x20DFAB8 and < 0x21DFAB8)
        {
            int offset = checked((int)(address - 0x20DFAB8));
            return new($"20DFAB8+{offset:X}", (NativeEffectMemory?.Arena ?? throw new NotSupportedException("Native6C3580 requires its actual mutable effect arena.")).Read(offset, requestedLength ?? throw new InvalidOperationException("Native mutable effect source requires its actual bounded read; arena TIM uploads have their own parser.")).ToImmutableArray());
        }
        if (address >= NativeBattleQuezacotlExecutableMemory.Address && address < NativeBattleQuezacotlExecutableMemory.Address + NativeBattleQuezacotlExecutableMemory.Length)
        {
            NativeBattleQuezacotlExecutableMemory executable = NativeEffectMemory?.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C3580 requires its current executable model source.");
            return new($"FF8_EN:{address:X8}", executable.Data.AsSpan().Slice(checked((int)(address - NativeBattleQuezacotlExecutableMemory.Address)), requestedLength ?? checked(executable.Data.Length - (int)(address - NativeBattleQuezacotlExecutableMemory.Address))).ToArray().ToImmutableArray());
        }
        throw new NotSupportedException($"Native6C3580 source{address:X8} requires its actual mutable resource owner.");
    }
}
