using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) ExecuteNativeCinematicBone5B(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicProfile profile, NativeBattleCinematicResources resources, int resourceLength, NativeBattleGraphicsState graphics, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> update)
    {
        NativeBattleCinematicBone5BServices services = new((current, selector) =>
        {
            NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(current, resourceLength);
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(current, bank), current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, index));
        }, (current, source, physical) =>
        {
            if (physical)
            {
                return RestorePhysical(current, source);
            }
            byte kind = current.Arena.Read(ArenaOffset(source + 0x1C, 1), 1)[0];
            uint handler = NativeBattleCinematicDispatch.RequireSlot(profile.Effect, "draw_table", kind, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.DrawTable + 4u * kind, 4)));
            uint model = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(source + 0xBC, 4), 4));
            if (handler == 0xAF6040)
            {
                return RestorePhysical(current, BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(model + 0x0C, 4), 4)));
            }
            if (handler is not (0xB26B80 or 0xAF5810 or 0xA5D730))
            {
                throw new NotSupportedException("Native bone positioning requires its selected embedded or physical clone descriptor layout.");
            }
            uint table = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(model + 0x14, 4), 4));
            uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(ArenaOffset(table, 4), 4));
            int start = ArenaOffset(skeleton, 1);
            int count = current.Arena.Read(start, 1)[0];
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(current.Arena, start, count);
            current = current with { Arena = NativeBattleArenaPose.RestoreBoneMatrices(current.Arena, start, pose) };
            ImmutableArray<NativeBattleActionMatrix>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(count);
            for (int index = 0; index < count; index++)
            {
                bones.Add(NativeBattleArenaPose.ReadMatrix(current.Arena, checked(start + 32 + 48 * index)));
            }
            NativeBattleEffectArena metadataOwner = current.Arena;
            return new(current, [], bones.MoveToImmutable(), null) { MetadataLength = checked(16 + 48 * count), ReadMetadata = (offset, length) => metadataOwner.Read(checked(start + offset), length) };
        }, update, (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native bone positioning requires its canonical V0 input registers.")) };
            return current;
        }, CreateNativeActorAnchorServices());
        return (NativeBattleCinematicBone5B.Execute(frame, program, services), graphics);

        NativeBattleCinematicBonePose RestorePhysical(NativeBattleWaterExecutionFrame current, uint address)
        {
            if (address < 0x1D972C0 || (address - 0x1D972C0) % 156 != 0 || (address - 0x1D972C0) / 156 >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native bone positioning requires its actual physical model pointer.");
            }
            int index = checked((int)((address - 0x1D972C0) / 156));
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native bone positioning requires its actual physical rig source.");
            NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source);
            graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
            return new(current, source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native bone positioning requires its actual physical actor matrix."));
        }
    }
}
