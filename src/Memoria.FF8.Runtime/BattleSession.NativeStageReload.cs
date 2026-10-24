using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private uint RequireNativeCurrentStageGeometryAddress(int group)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native stage geometry requires its current stage owner.");
        return stage.CurrentGroupGeometryAddress(group, (address, length) => (NativeFileStaging ?? throw new NotSupportedException("Native stage geometry requires its addressed current bytes.")).Read(address, length)) ?? throw new NotSupportedException("Native stage geometry selected an empty current group.");
    }

    private void QueueNativeStage512AA0(int index, uint destination)
    {
        (NativeBattleEffectFileAsset source, _) = NativeStageReloadResource(index, false);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage file queue requires its current memory.");
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(destination, source.Data.Length);
        NativeEffectMemory = memory with { Arena = (memory.Arena ?? throw new NotSupportedException("Native stage file queue requires its current destination arena.")).Write(offset, source.Data.AsSpan()) };
        NativeEffectIoResult = -1;
        QueueNativeFileIo482610(source.Data.Length, 0x508470);
    }

    private void ReloadNativeStage512AC0(int index, uint loadedFile)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native stage reload requires its current stage owner.");
        (NativeBattleEffectFileAsset source, Func<byte[], NativeBattleStageSource>? decode) = NativeStageReloadResource(index, true);
        NativeBattleStageReloadCopy copy = NativeBattleStageReload512AC0.Plan(stage, index, loadedFile, Read);
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native stage reload requires its addressed destination.");
        staging = staging.Write(copy.Destination, Read(copy.Source, copy.Length));
        NativeFileStaging = staging;
        byte[] currentFile = Read(loadedFile, source.Data.Length);
        // Decoder code belongs to the host; the active source bytes remain in the current arena.
        NativeBattleStageSource decoded = (decode ?? throw new NotSupportedException("Native stage reload requires its host byte decoder."))(currentFile);
        stage = stage.Reload512AC0(index, decoded);
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native stage reload requires its current scratch capacity.");
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native stage reload requires its retained scratch maximum.");
        for (int group = 0; group < 4; group++)
        {
            if (stage.CurrentGroupGeometryAddress(group, Read) is null)
            {
                continue;
            }
            uint directory = stage.SourceAddress1D9A820!.Value;
            uint groupOffset = BinaryPrimitives.ReadUInt32LittleEndian(Read(directory + 4 + (uint)group * 4, 4));
            uint section = checked(directory + groupOffset);
            uint animations = BinaryPrimitives.ReadUInt32LittleEndian(Read(section + 12, 4));
            int requested = BinaryPrimitives.ReadInt32LittleEndian(Read(checked(section + animations - 4), 4));
            if (maximum < requested)
            {
                maximum = unchecked((ushort)requested);
            }
        }
        NativeModelCpuResources = cpu with { ScratchMaximum = maximum };
        NativeBattleFinalStage142State final = stage.FinalStage142 ?? throw new NotSupportedException("Native stage reload requires its retained CLUT owner.");
        stage = stage with { FinalStage142 = final with { ClutSource = new("current-stage-clut", Read(copy.ClutDestination, 8192).ToImmutableArray()) } };
        NativeStage = stage;
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage reload requires its texture owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native stage reload requires its current texture queue.");
        NativeEffectMemory = memory with { Textures = textures.EnqueueTim505E30(decoded.Texture).State };

        byte[] Read(uint address, int length)
        {
            if (address >= 0x20DFAB8 && (ulong)address + (uint)length <= 0x20DFAB8ul + NativeBattleEffectArena.Length)
            {
                return (NativeEffectMemory?.Arena ?? throw new NotSupportedException("Native stage reload requires its current loaded arena bytes.")).Read(checked((int)(address - 0x20DFAB8)), length);
            }
            return (NativeFileStaging ?? throw new NotSupportedException("Native stage reload requires its current physical stage bytes.")).Read(address, length);
        }
    }

    private (NativeBattleEffectFileAsset Source, Func<byte[], NativeBattleStageSource>? Decode) NativeStageReloadResource(int index, bool requireDecoder)
    {
        short file = NativeBattleStageReload512AC0.File512AA0(index);
        NativeBattleEffectBinding binding = NativeGraphics?.OriginalEffect?.Binding ?? throw new NotSupportedException("Native stage reload requires its actual selected binding.");
        NativeBattleEffectFileAsset source;
        Func<byte[], NativeBattleStageSource>? decode;
        switch (binding.Effect)
        {
            case 69:
                NativeBattleEffect69Resources resources69 = binding.Effect69Resources ?? throw new NotSupportedException("Native stage reload requires its selected resources.");
                source = resources69.ReadFile(file);
                decode = resources69.DecodeReloadStage;
                if (requireDecoder && decode is null)
                {
                    decode = nativeEffectResourceResolver?.Invoke(binding.Effect).Effect69Resources?.DecodeReloadStage;
                }
                break;
            case 76:
                NativeBattleEffect76Resources resources76 = binding.Effect76Resources ?? throw new NotSupportedException("Native stage reload requires its selected resources.");
                source = resources76.ReadFile(file);
                decode = resources76.DecodeReloadStage;
                if (requireDecoder && decode is null)
                {
                    decode = nativeEffectResourceResolver?.Invoke(binding.Effect).Effect76Resources?.DecodeReloadStage;
                }
                break;
            default:
                throw new NotSupportedException("Native stage reload has no selected resource provider.");
        }
        return (source, decode);
    }
}
