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
        NativeBattleEffect69Resources resources = NativeGraphics?.OriginalEffect?.Binding.Effect69Resources ?? throw new NotSupportedException("Native stage file queue requires its selected resource resolver.");
        NativeBattleEffectFileAsset source = resources.ReadFile(NativeBattleStageReload512AC0.File512AA0(index));
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage file queue requires its current memory.");
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(destination, source.Data.Length);
        NativeEffectMemory = memory with { Arena = (memory.Arena ?? throw new NotSupportedException("Native stage file queue requires its current destination arena.")).Write(offset, source.Data.AsSpan()) };
        NativeEffectIoResult = -1;
        QueueNativeFileIo482610(source.Data.Length, 0x508470);
    }

    private void ReloadNativeStage512AC0(int index, uint loadedFile)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native stage reload requires its current stage owner.");
        NativeBattleEffect69Resources resources = NativeGraphics?.OriginalEffect?.Binding.Effect69Resources ?? throw new NotSupportedException("Native stage reload requires its selected current-resource decoder.");
        NativeBattleStageReloadCopy copy = NativeBattleStageReload512AC0.Plan(stage, index, loadedFile, Read);
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native stage reload requires its addressed destination.");
        staging = staging.Write(copy.Destination, Read(copy.Source, copy.Length));
        NativeFileStaging = staging;
        byte[] currentFile = Read(loadedFile, resources.ReadFile(37).Data.Length);
        NativeBattleStageSource decoded = (resources.DecodeReloadStage ?? throw new NotSupportedException("Native stage reload requires its current byte decoder."))(currentFile);
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
}
