using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDrinkMagic37Rendering
{
    public static NativeBattleDrawStock15RibbonStep Ribbon(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, ushort? cameraPadding18 = null)
    {
        if (task.Callback != 0x85E930)
        {
            throw new InvalidDataException("Drink Magic ribbon requires its original trajectory task.");
        }
        return NativeBattleDrawStock15Rendering.Ribbon8B5F30(task with { Callback = 0x8B5C70 }, camera, projection, cameraPadding18);
    }

    public static ImmutableArray<NativeBattleDrawStock15ParticleOperation> SpritePreparation(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, ushort? cameraPadding18 = null)
    {
        uint callback = task.Callback switch
        {
            0x85BD50 or 0x85E930 => 0x8B5C70,
            0x85F060 => 0x8B6440,
            _ => throw new InvalidDataException("Drink Magic sprite preparation requires its original particle callback.")
        };
        return NativeBattleDrawStock15SpriteRendering.Preparation(task with { Callback = callback }, camera, cameraPadding18);
    }

    public static NativeBattleDrawStock15SpriteComposition ComposeSprite(NativeBattleModulePoolTask task, NativeBattleDrinkMagic37Resources resources, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, short cameraRoll)
    {
        uint source = task.Callback switch
        {
            0x85BD50 => 0x15D9624,
            0x85E930 => 0x15D92F4,
            0x85F060 => 0x15D9504,
            _ => throw new InvalidDataException("Drink Magic sprite requires its original particle callback.")
        };
        if (task.ReadDword(0x4C) != source)
        {
            throw new InvalidDataException("Drink Magic sprite task refers to another source directory.");
        }
        NativeBattleModuleResource resource = resources.Sprite(source);
        bool head = task.Callback != 0x85F060;
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(task.ReadWord(0x50)),
            Phase = task.ReadWord(0x50),
            Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), Word(task, 0x22)),
            Scale = 4096,
            Direction = 4096,
            DepthOffset = head ? Word(task, 0x54) : 0,
            SpriteFlags = 0
        };
        NativeBattleActionMatrix setup;
        if (head)
        {
            setup = NativeBattlePoseMatrices.ModuleSpriteCamera(draw with { SpriteFlags = 0x200 }, camera, 0);
        }
        else
        {
            NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
            world = NativeBattleCuragaMatrices.RotateY8DD8A0(world, Word(task, 0x42));
            world = NativeBattleCuragaMatrices.RotateZ8DD960(world, Word(task, 0x44));
            world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, Word(task, 0x40));
            draw = draw with { SpriteWorldRotation = world.Rotation };
            setup = NativeBattleGfMatrices.Compose56C2F0(camera, world with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) });
        }
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, camera, cameraRoll);
        return new(draw, setup, main, NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, 17, 2, main));
    }

    public static NativeBattleDrawStock15SpriteTemporaryResult Emit180(NativeBattleFileStaging staging, uint temporary, NativeBattleModulePoolTask task, NativeBattleDrinkMagic37Resources resources, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePackedMatrix32? capturedMatrix = null)
    {
        NativeBattleDrawStock15SpriteComposition composition = ComposeSprite(task, resources, camera, projection, cameraRoll);
        return NativeBattleDrawStock15SpriteRendering.EmitPrepared180(staging, temporary, task, composition, projection, cameraRoll, capturedMatrix);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }
}
