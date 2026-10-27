using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSaliva138MouthTests
{
    [Fact]
    public void Actual_current_bone_is_composed_once_without_refreshing_from_decoded_pose()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, NativeBattleCuragaMatrices.Identity8DD770() with { X = 9999 });
        actor = actor with { SequenceState = null };
        string before = NativeBattleTestSnapshots.Json(actor);
        ImmutableArray<byte> anchor = Anchor(1, 2, 3, 1);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Compose56C2F0(actor.GeometryWorldMatrix!, NativeBattleBodyBoneMatrices.Require(actor, source)[1]);
        var result = NativeBattleSaliva138Mouth.Prepare(Memory(), actor, source, anchor);
        var direct = NativeBattleComposedAnchorTransform.Transform(Memory(), expected, anchor);
        Assert.Equal(direct.Position, result.Position);
        Assert.Equal(NativeBattleTestSnapshots.Json(direct.Memory), NativeBattleTestSnapshots.Json(result.Memory));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(actor));
        Assert.Equal(new byte[] { 1, 0 }, result.Memory.ScriptedColorRegisters!.NormalInput!.Value.Skip(6));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
    }

    [Fact]
    public void Missing_current_bones_world_matrix_or_reached_selector_fails_without_reconstruction()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        Assert.Throws<NotSupportedException>(() => NativeBattleSaliva138Mouth.Prepare(Memory(), actor with { CurrentBodyBoneMatrices = null }, source, Anchor(0, 0, 0, 1)));
        Assert.Throws<NotSupportedException>(() => NativeBattleSaliva138Mouth.Prepare(Memory(), actor with { GeometryWorldMatrix = null }, source, Anchor(0, 0, 0, 1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleSaliva138Mouth.Prepare(Memory(), actor, source, Anchor(0, 0, 0, -1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleSaliva138Mouth.Prepare(Memory(), actor, source, Anchor(0, 0, 0, 2)));
    }

    [Fact]
    public void Full_MAC_and_saturated_IR_survive_JSON_with_retained_current_bone()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source) with { GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -40000 } };
        NativeBattleGraphicsModel restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGraphicsModel>(NativeBattleTestSnapshots.Json(actor))!;
        var result = NativeBattleSaliva138Mouth.Prepare(Memory(), actor, source, Anchor(1, -1, 2, 0));
        var other = NativeBattleSaliva138Mouth.Prepare(Memory(), restored, source, Anchor(1, -1, 2, 0));
        Assert.Equal(short.MaxValue, result.Position.X);
        Assert.Equal(short.MinValue, result.Position.Y);
        Assert.True(result.Memory.ScriptedColorRegisters!.Mac[0] > short.MaxValue);
        Assert.Equal(result.Position, other.Position);
        Assert.Equal(NativeBattleTestSnapshots.Json(result.Memory), NativeBattleTestSnapshots.Json(other.Memory));
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
