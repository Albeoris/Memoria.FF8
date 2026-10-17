using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState StartNativeVictoryCamera5064F0(NativeBattleGraphicsState graphics)
    {
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native5064F0 requires its current camera owner.");
        byte mode = camera.Mode ?? throw new NotSupportedException("Native5064F0 requires its actual camera mode BYTE.");
        if (mode is 0 or 3)
        {
            return graphics;
        }
        byte stage = graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native5064F0 requires its actual stage resource BYTE.");
        if (stage == 11 || stage is >= 0x98 and <= 0x9C)
        {
            return graphics;
        }
        byte actor = graphics.ActionGraphics?.ActorSelector56 ?? throw new NotSupportedException("Native5064F0 requires its retained action actor selector BYTE56.");
        bool resourceCamera = actor >= 3;
        if (!resourceCamera)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            resourceCamera = (model.Flags0 & 2) == 0 || ((model.Flags8 ?? throw new NotSupportedException("Native5064F0 requires its selected actor DWORD8.")) & 0x1A) != 0 || NextNativeGlobalRandom() >= 0x2000;
        }
        if (resourceCamera)
        {
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native5064F0 requires its current shared R0WIN camera bytes.");
            uint start = checked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleFileStaging.Address + 8, 4)));
            uint end = checked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleFileStaging.Address + 12, 4)));
            NativeBattleStagingBlock block = new(start, checked((int)(end - start)));
            NativeBattleEffectAnimationRegisters registers = NativeBattleCameraState.ReadStagedControlResource(staging, block);
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5099A0 requires its retained process animation registers.");
            NativeEffectMemory = memory with { Textures = (memory.Textures ?? throw new NotSupportedException("Native5099A0 requires its retained animation/texture owner.")).Register5099A0(registers.Source) };
            NativeCamera = camera with { ControlResource = registers, ControlStagingSource = block, ControlCursor = 0 };
            return graphics;
        }
        BattleCombatant selected = GraphicsActor(graphics, actor);
        graphics = PrepareNativeActionGeometry(graphics, selected, new(selected.Id, selected.Id) { NativeTargetMask = (ushort)(1 << actor) });
        byte selector = (byte)((NextNativeGlobalRandom() & 1) + 22);
        NativeBattleGraphicsResource source = NativeActionResource(graphics, actor);
        NativeCamera = camera.Start(source.ActionCameraBank ?? throw new NotSupportedException("Native5064F0 requires the original actor's action camera bank."), selector);
        return graphics;
    }
}
