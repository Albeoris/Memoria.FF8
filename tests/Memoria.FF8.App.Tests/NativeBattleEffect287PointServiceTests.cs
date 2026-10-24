using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect287PointServiceTests
{
    [Fact]
    public void Fresh_source_point_service_preserves_full_MAC_and_retained_output_tail()
    {
        WithSource((session, assets, source, frame) =>
        {
            NativeBattleEffectArena input = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)0)).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes((short)-10000)).Write(6, BitConverter.GetBytes((short)1));
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(12, BitConverter.GetBytes(0xA55AA55Au));
            NativeBattleEffect287MacStep result = Invoke<NativeBattleEffect287MacStep>(session, "NativeEffect287PointMac", source.NativeGraphics!, frame, input, retained);
            NativeBattleActionMatrix bone = source.NativeGraphics!.Models[3].CurrentBodyBoneMatrices!.Matrices[1];
            NativeBattleFullMacVertex expected = NativeBattleGfMatrices.TransformMac460840(bone, new(0, 0, -10000));
            Assert.Equal(expected.X, BinaryPrimitives.ReadInt32LittleEndian(result.Output16.Read(0, 4)));
            Assert.Equal(expected.Y, BinaryPrimitives.ReadInt32LittleEndian(result.Output16.Read(4, 4)));
            Assert.Equal(expected.Z, BinaryPrimitives.ReadInt32LittleEndian(result.Output16.Read(8, 4)));
            Assert.Equal(0xA55AA55Au, BinaryPrimitives.ReadUInt32LittleEndian(result.Output16.Read(12, 4)));
            Assert.Equal(result.Frame.Memory.ScriptedColorRegisters!.Mac.Select(value => value!.Value), new[] { expected.X, expected.Y, expected.Z });
            return result.Frame;
        });
    }

    [Fact]
    public void Fresh_local_point_service_writes_exact_60_byte_temporary_fields_and_retains_output_W()
    {
        WithSource((session, assets, source, frame) =>
        {
            NativeBattleGraphicsModel model = source.NativeGraphics!.Models[3];
            BattleCombatantDefinition actor = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[3]!);
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(actor).PoseSource!;
            short parent = BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + 27 * 48, 2));
            short length = parent < 0 ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(18 + parent * 48, 2));
            NativeBattleFullMacVertex expected = NativeBattleGfMatrices.TransformMac460840(model.CurrentBodyBoneMatrices!.Matrices[27], new(0, 0, length));
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 60);
            frame = frame with { Staging = frame.Staging!.MarkUnknown(allocation.Address + 0x3A, 2).Write(allocation.Address + 0x3A, new byte[] { 0xA5 }) };
            (byte[] priorBytes, bool[] priorKnown) = frame.Staging.ReadSnapshot(allocation.Address, 60);
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, BitConverter.GetBytes((ushort)0x5AA5));
            NativeBattleEffect287PointStep result = Invoke<NativeBattleEffect287PointStep>(session, "NativeEffect287LocalPoint", source.NativeGraphics, frame, 0x1D972C0u + 3 * 156u, 27, retained);
            Assert.Equal(unchecked((short)expected.X), BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(0, 2)));
            Assert.Equal(unchecked((short)expected.Y), BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(2, 2)));
            Assert.Equal(unchecked((short)expected.Z), BinaryPrimitives.ReadInt16LittleEndian(result.Output8.Read(4, 2)));
            Assert.Equal((ushort)0x5AA5, BinaryPrimitives.ReadUInt16LittleEndian(result.Output8.Read(6, 2)));
            Assert.Equal(frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
            (byte[] bytes, bool[] known) = result.Frame.Staging.ReadSnapshot(allocation.Address, 60);
            for (int index = 0; index < 60; index++)
            {
                bool written = index is >= 0x20 and < 0x2C or >= 0x30 and < 0x3A;
                if (written)
                {
                    Assert.True(known[index]);
                }
                else
                {
                    Assert.Equal(priorKnown[index], known[index]);
                    if (known[index])
                    {
                        Assert.Equal(priorBytes[index], bytes[index]);
                    }
                }
            }
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x34, 4)));
            Assert.Equal(length, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x38, 2)));
            Assert.True(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
            Assert.False(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
            Assert.Equal((byte)0xA5, result.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
            return result.Frame;
        });
    }

    private static void WithSource(Func<BattleSession, BattleAssets, BattleSnapshot, NativeBattleEffect287Frame, NativeBattleEffect287Frame> check)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT287_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and the fresh effect287 current-input standalone.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        Assert.Equal((byte)125, source.NativeGraphics!.Models[3].ModelId4);
        Assert.Equal(28, source.NativeGraphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([287])[286];
        NativeBattleEffect287Frame frame = new(source.NativeEffectMemory!, new(287, 286, binding, null, null), source.NativeCrtRandom!, source.NativeEffectMemory!.Effect287!.WriteDword(NativeBattleEffect287Profile.CasterGlobal, 0x1D972C0u + 3 * 156u)) { Staging = source.NativeFileStaging };
        NativeBattleEffect287Frame result = check(session, assets, source, frame);
        result.Memory.GeometryRegisters!.Validate();
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
    }

    private static T Invoke<T>(BattleSession session, string method, params object[] arguments)
    {
        MethodInfo info = typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidDataException("The selected current point service is unavailable.");
        return Assert.IsType<T>(info.Invoke(session, arguments));
    }
}
