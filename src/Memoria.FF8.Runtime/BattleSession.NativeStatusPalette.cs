using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void UploadNativeStatusPalette509E00(BattleModelOwner owner, uint flags)
    {
        NativeBattleModelCpuResources resources = NativeModelCpuResources ?? throw new NotSupportedException("Native509E00 requires the saved original party palettes.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native509E00 requires its process texture and packet owners.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native509E00 requires the actual505DF0 queue.");
        var material = SelectNativeModelTextureBank(owner, textures);
        byte[] original;
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        bool hasOriginalSlot = graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte originalSlot);
        int textureSlot;
        if (material is not null)
        {
            original = resources.ReadPartyStatusPalette(owner);
            textureSlot = hasOriginalSlot ? originalSlot : 0;
        }
        else
        {
            if (!hasOriginalSlot || owner is not BattlePartyModelOwner)
            {
                throw new NotSupportedException("The original status palette requires its original party texture slot.");
            }
            original = resources.Palettes.Read(originalSlot * 1024, 1024);
            textureSlot = originalSlot;
        }
        NativeBattleEffectHeapAllocation source;
        if ((flags & 0x410) != 0)
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native509E00 requires its retained scratch packet cursor.");
            string bank = process.PacketBank ?? throw new NotSupportedException("Native509E00 requires the allocated native packet bank.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Native509E00 requires the observed native packet cursor.");
            NativeBattlePrimitiveProcess next = process with { ByteCursor = checked(cursor + 1024) };
            next.Validate();
            source = new($"509E00:{bank}:{process.PacketBuffer}:{cursor}", NativeBattleStatusPalette.Transform509E00(original, flags));
            next = next.ApplyCommonWrite(cursor, source.Data, Enumerable.Repeat(NativeBattleGfPacketByteState.Known, source.Data.Length).ToImmutableArray());
            memory = memory with { PrimitiveProcess = next };
        }
        else
        {
            string paletteIdentity = hasOriginalSlot ? originalSlot.ToString(System.Globalization.CultureInfo.InvariantCulture) : ReadGraphicsPresentationId(owner);
            source = new($"1D98B60:{paletteIdentity}", NativeBattleStatusPalette.Transform509E00(original, flags));
        }
        NativeBattleEffectTextureState updated = (material?.Bank.Textures ?? textures).EnqueueRectangle505DF0(new(0, checked((short)(224 + 2 * textureSlot)), 256, 2), source);
        NativeEffectMemory = memory with { Textures = PublishNativeModelTextures(textures, material, updated) };
    }
}
