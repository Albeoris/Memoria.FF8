using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleStageVariant137Entry ReadNativeEffect209Variant(NativeBattleEffect209Resources resources)
    {
        NativeBattleStageVariant137State variants = NativeStage?.Extended?.Stage137.Variants ?? throw new NotSupportedException("Effect209 requires its retained stage variant owner.");
        int index = variants.ReadIndex();
        if ((uint)index >= (uint)resources.StageVariants.Length)
        {
            throw new NotSupportedException("Effect209 stage variant leaves its original resource table.");
        }
        return resources.StageVariants[index];
    }

    private NativeBattleEffect209Frame RebuildNativeEffect209Stage(NativeBattleEffect209Frame frame, NativeBattleEffect209Resources resources, uint bufferAddress)
    {
        PublishNativeEffect209Frame(frame);
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect209 requires its current stage.");
        NativeBattleStageVariant137Entry selected = ReadNativeEffect209Variant(resources);
        NativeBattleEffectFileAsset file = resources.ReadFile(selected.FileId);
        NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect209 requires its loaded variant file.");
        int workOffset = checked((int)(bufferAddress - 0x209FAB8));
        byte[] loaded = work.Read(workOffset, file.Data.Length);
        int length = BinaryPrimitives.ReadInt32LittleEndian(loaded.AsSpan(selected.HeaderOffset + 0x18));
        if (length < 32 || length > 0x8000 || selected.HeaderOffset > loaded.Length - length)
        {
            throw new InvalidDataException("Effect209 stage copy leaves its loaded file or original reserved geometry extent.");
        }
        NativeBattleStageVariant137State variants = stage.Extended?.Stage137.Variants ?? throw new NotSupportedException("Effect209 lost its current variant process.");
        uint destination = variants.GeometryAddress is > 0 ? variants.GeometryAddress.Value : throw new NotSupportedException("Effect209 requires its retained variant geometry destination.");
        if (stage.SourceAddress1D9A820 != destination)
        {
            throw new NotSupportedException("Effect209 requires its current stage rows to bind the actual variant geometry directory.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect209 requires its current model heap owner.");
        uint reserveEnd = checked(destination + 0x8000);
        if (cpu.EnemyCursor is not int cursor || NativeBattleModelCpuResources.EnemyAddress + (uint)cursor < reserveEnd)
        {
            throw new NotSupportedException("Effect209 requires its original stage geometry reservation before model allocations.");
        }
        Func<int, byte[], NativeBattleStageSource>? decode = resources.DecodeReloadStage ?? nativeEffectResourceResolver?.Invoke(209).Effect209Resources?.DecodeReloadStage;
        NativeBattleStageSource decoded = (decode ?? throw new NotSupportedException("Effect209 requires its current stage byte decoder."))(selected.FileId, loaded);
        NativeBattleStagePhysicalSource physical = decoded.PhysicalSource ?? throw new InvalidDataException("Effect209 stage decoder lost its physical directory.");
        if (physical.DirectoryOffset != selected.HeaderOffset || physical.Prefix.Length - selected.HeaderOffset != length)
        {
            throw new InvalidDataException("Effect209 stage decoder disagrees with its actual copy operands.");
        }
        NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Effect209 requires its current stage bytes.")).Write(destination, loaded.AsSpan(selected.HeaderOffset, length));
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Effect209 requires its retained projected vertex capacity.");
        for (int group = 0; group < 4; group++)
        {
            int section = checked(selected.HeaderOffset + BinaryPrimitives.ReadInt32LittleEndian(loaded.AsSpan(selected.HeaderOffset + 4 + group * 4)));
            int animation = checked(section + BinaryPrimitives.ReadInt32LittleEndian(loaded.AsSpan(section + 12)));
            int requested = BinaryPrimitives.ReadInt32LittleEndian(loaded.AsSpan(animation - 4));
            if (maximum < requested)
            {
                maximum = unchecked((ushort)requested);
            }
        }
        NativeModelCpuResources = cpu with { ScratchMaximum = maximum };
        short speed = selected.RotationSource is >= 0x1D99BE8 and <= 0x1D99BEE ? variants.ReadRotationWord(selected.RotationSource) : BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(selected.RotationSource, 2));
        NativeStage = stage.Rebuild50E3C0(decoded, new(selected.Color40, selected.Color44), speed, () => NextNativeGlobalRandom(), DrawCrt);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Effect209 stage rebuild lost its process memory.");
        NativeEffectMemory = memory with { Textures = (memory.Textures ?? throw new NotSupportedException("Effect209 stage rebuild requires its texture queue.")).EnqueueTim505E30(decoded.Texture).State };
        return ImportNativeEffect209Frame(frame);

        int DrawCrt()
        {
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Effect209 stage wrap requires its current CRT stream.");
            nativeCrtRandom = random.Draw(out ushort value);
            return value;
        }
    }
}
