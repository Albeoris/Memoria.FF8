using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco98Sprite
{
    public const uint Callback = 0x728780;

    public static NativeBattleChoco97SpriteStep Advance728780(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources resources, ImmutableArray<byte> contextActorRaw8, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if (task.Callback != Callback)
        {
            throw new InvalidDataException("Native Choco sprite requires its actual caller callback.");
        }
        return NativeBattleChoco97Sprite.AdvanceProfiled(task, resources, contextActorRaw8, view, projection, cameraRoll, 0x152AF40);
    }
}
