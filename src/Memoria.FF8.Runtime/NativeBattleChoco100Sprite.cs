using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco100Sprite
{
    public const uint Callback = 0x715AB0;

    public static NativeBattleChoco97SpriteStep Advance715AB0(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, ImmutableArray<byte> contextActorRaw8, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if (task.Callback != Callback)
        {
            throw new InvalidDataException("Native Choco sprite requires its actual caller callback.");
        }
        return NativeBattleChoco97Sprite.AdvanceProfiled(task, resources, contextActorRaw8, view, projection, cameraRoll, 0x1529804, 0x70, 0x6C);
    }
}
