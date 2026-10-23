using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136MouthTests
{
    [Fact]
    public void Fresh_508C90_replaces_retained_composed_bones_before_world_anchor_transform()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, NativeBattleCuragaMatrices.Identity8DD770() with { X = 9999 });
        string before = JsonSerializer.Serialize(actor);
        ImmutableArray<byte> anchor = Anchor(1, 2, 3, 1);
        NativeBattleGraphicsModel refreshed = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Compose56C2F0(actor.GeometryWorldMatrix!, NativeBattleBodyBoneMatrices.Require(refreshed, source)[1]);
        var result = NativeBattleOilShot136Mouth.Prepare(Memory(), actor, source, anchor);
        var direct = NativeBattleComposedAnchorTransform.Transform(Memory(), expected, anchor);
        Assert.Equal(direct.Position, result.Position);
        Assert.Equal(JsonSerializer.Serialize(direct.Memory), JsonSerializer.Serialize(result.Memory));
        Assert.Equal(JsonSerializer.Serialize(refreshed), JsonSerializer.Serialize(result.Model));
        Assert.Equal(before, JsonSerializer.Serialize(actor));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
    }

    [Fact]
    public void Fresh_body_pose_owner_is_required_even_with_retained_bones()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        Assert.Throws<NotSupportedException>(() => NativeBattleOilShot136Mouth.Prepare(Memory(), actor with { SequenceState = null }, source, Anchor(0, 0, 0, 1)));
        Assert.Throws<NotSupportedException>(() => NativeBattleOilShot136Mouth.Prepare(Memory(), actor with { GeometryWorldMatrix = null }, source, Anchor(0, 0, 0, 1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleOilShot136Mouth.Prepare(Memory(), actor, source, Anchor(0, 0, 0, -1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleOilShot136Mouth.Prepare(Memory(), actor, source, Anchor(0, 0, 0, 2)));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };

    private static ImmutableArray<byte> Anchor(short x, short y, short z, short bone)
    {
        byte[] bytes = new byte[8];
        short[] words = [x, y, z, bone];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[0x70];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x12), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x42), 150);
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model() => new(0, 3)
    {
        GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 },
        SequenceState = new() { DecodedPose = new(new(10, 20, 30), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 128, 0), new(1024, 1024, 1024))], false) }
    };
}
