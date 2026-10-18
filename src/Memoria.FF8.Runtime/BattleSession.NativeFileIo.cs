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
                case 0x732590:
                    NativeBattleEffectMemory moogleMemory = NativeEffectMemory ?? throw new NotSupportedException("Native732590 requires its retained effect arena.");
                    NativeBattleMoogle96State moogle = moogleMemory.Moogle96 ?? throw new NotSupportedException("Native732590 requires its current Moogle IO owner.");
                    NativeBattleMoogle96IoCompletion uploaded = NativeBattleMoogle96Io.Complete732590(moogle.Io ?? throw new NotSupportedException("Native732590 requires its retained script and current destination."), moogleMemory);
                    NativeEffectMemory = uploaded.Memory with { Moogle96 = moogle with { Io = uploaded.State } };
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
