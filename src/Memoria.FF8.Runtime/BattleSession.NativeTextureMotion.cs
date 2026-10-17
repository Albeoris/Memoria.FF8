using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleModelTextureCache RegisterNativeEnemyTextureMotion(NativeBattleModelTextureCacheLoad load, ImmutableArray<byte>? original)
    {
        if (load.Upload is null)
        {
            return load.Cache;
        }
        byte model = load.Cache.Rows[load.ResourceIndex].ModelId ?? throw new InvalidDataException("Native enemy texture heap has no loaded model identity.");
        return load.Cache with { EnemyTextureMotion = original is { } table ? load.Cache.EnemyTextureMotion.SetItem(model, table) : load.Cache.EnemyTextureMotion.Remove(model) };
    }

    private ImmutableArray<byte> ReadNativeTextureMotion(NativeBattleGraphicsModel model)
    {
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native texture motion requires its physical resource descriptor owner.");
        int index = model.TextureResourceIndex ?? throw new NotSupportedException("Native texture motion requires actual model DWORD132.");
        NativeBattleModelTextureCacheRow row = cache.Rows[index];
        if (row.Kind == 3)
        {
            return row.ModelId is byte enemy && cache.EnemyTextureMotion.TryGetValue(enemy, out ImmutableArray<byte> table) ? table : throw new NotSupportedException("Native enemy texture motion requires its actually loaded shared heap table; migrate the named snapshot explicitly.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native texture motion requires its retained party CPU banks.");
        NativeBattleModelCpuResourceRow descriptor = cpu.Rows[index];
        if (!descriptor.Fields.TryGetValue(48, out NativeBattleModelCpuReference? source))
        {
            throw new NotSupportedException("Native model resource DWORD48 remains unknown.");
        }
        if (source is null)
        {
            return [];
        }
        NativeBattleModelCpuReference end = descriptor.Require(44);
        if (end.Bank != source.Bank || end.Offset < source.Offset)
        {
            throw new NotSupportedException("Native texture table requires its actual same-bank section boundary.");
        }
        return cpu.Cpu.Banks[source.Bank].Read(source.Offset, end.Offset - source.Offset).ToImmutableArray();
    }

    private void AdvanceNativeTextureMotion(NativeBattleGraphicsModel model, byte selector)
    {
        ImmutableArray<byte> source = ReadNativeTextureMotion(model);
        NativeBattleTextureMotionStep step = NativeBattleTextureMotion.Advance50C950(source, model.TextureAllocationMask, selector, (model.Flags0 & 2) != 0);
        NativeBattleModelTextureCache cache = NativeModelTextureCache!;
        int index = model.TextureResourceIndex!.Value;
        if (!step.Source.Equals(source))
        {
            NativeBattleModelTextureCacheRow row = cache.Rows[index];
            if (row.Kind == 3)
            {
                NativeModelTextureCache = cache with { EnemyTextureMotion = cache.EnemyTextureMotion.SetItem(row.ModelId!.Value, step.Source) };
            }
            else
            {
                NativeBattleModelCpuResources cpu = NativeModelCpuResources!;
                NativeBattleModelCpuReference target = cpu.Rows[index].Require(48);
                NativeBattleModelCpuBank bank = cpu.Cpu.Banks[target.Bank].Write(target.Offset, step.Source.AsSpan());
                NativeModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(target.Bank, bank) } };
            }
        }
        foreach (NativeBattleTextureCopy copy in step.Copies)
        {
            EnqueueNativeTextureMotion(model, copy);
        }
    }

    private NativeBattleGraphicsState ScheduleNativeTextureAnimation(NativeBattleGraphicsState graphics, int index, NativeBattleAuxiliaryProgram program, byte sequence)
    {
        return graphics with { Auxiliary = graphics.Auxiliary.AppendTextureAnimation(index, sequence, program) };
    }

    private void ApplyNativeTextureMotion(NativeBattleGraphicsState graphics, int index, byte selector, int variant)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        if ((model.Flags0 & 2) == 0)
        {
            return;
        }
        NativeBattleTextureCopy? copy = NativeBattleTextureMotion.Plan50C780(ReadNativeTextureMotion(model), model.TextureAllocationMask, selector, variant);
        if (copy is null)
        {
            return;
        }
        EnqueueNativeTextureMotion(model, copy);
    }

    private void EnqueueNativeTextureMotion(NativeBattleGraphicsModel model, NativeBattleTextureCopy copy)
    {
        if (model.BodyTextureSlots is null)
        {
            throw new NotSupportedException("Native50C780 requires its original model TIMs already uploaded by507400; migrate the named standalone snapshot explicitly.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50C780 requires its retained process texture owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native50C780 requires its explicit505EB0 queue and VRAM owner.");
        NativeEffectMemory = memory with { Textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY) };
    }
}
