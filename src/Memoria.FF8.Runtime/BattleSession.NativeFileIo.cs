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
            NativeFileIo = current;
            switch (completion.Callback)
            {
                case 0x508470:
                    NativeEffectIoResult = completion.Result;
                    break;
                case 0x6C3580:
                    NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6C3580 requires its retained effect arena.");
                    NativeEffectMemory = NativeBattleEffectIo.Complete6C3580(memory, ResolveNativeQuezacotlSource).Memory;
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
        if (request.DestinationAddress is < 0x20DFAB8 or >= 0x21DFAB8)
        {
            throw new NotSupportedException("Native Quezacotl file destination requires an owned native allocation.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl file request requires its current effect memory.");
        NativeEffectMemory = memory with { Arena = (memory.Arena ?? throw new NotSupportedException("Native Quezacotl file request requires its actual effect arena.")).Write(checked((int)(request.DestinationAddress - 0x20DFAB8)), file.Data.AsSpan()) };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
    }

    private NativeBattleEffectHeapAllocation ResolveNativeQuezacotlSource(uint address)
    {
        if (address is >= 0x20DFAB8 and < 0x21DFAB8)
        {
            int offset = checked((int)(address - 0x20DFAB8));
            return new($"20DFAB8+{offset:X}", (NativeEffectMemory?.Arena ?? throw new NotSupportedException("Native6C3580 requires its actual mutable effect arena.")).Read(offset, NativeBattleEffectArena.Length - offset).ToImmutableArray());
        }
        throw new NotSupportedException($"Native6C3580 source{address:X8} requires its actual mutable resource owner.");
    }
}
