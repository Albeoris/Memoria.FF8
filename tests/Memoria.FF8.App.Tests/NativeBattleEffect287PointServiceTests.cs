using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect287PointServiceTests
{
    [Fact]
    public void Fresh_source_point_service_preserves_full_MAC_and_retained_output_tail()
    {
        WithSource((process, _, _, frame) =>
        {
            NativeBattleEffectArena input = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)0)).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes((short)-10000)).Write(6, BitConverter.GetBytes((short)1));
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(12, BitConverter.GetBytes(0xA55AA55Au));
            NativeBattleEffect287MacStep result = Invoke<NativeBattleEffect287MacStep>(process, "NativeEffect287PointMac", process.Graphics, frame, input, retained);
            NativeBattleActionMatrix bone = ReadOriginalModelCell(process.Graphics, 3).CurrentBodyBoneMatrices!.Matrices[1];
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
        WithSource((process, assets, actor, frame) =>
        {
            NativeBattleGraphicsModel model = ReadOriginalModelCell(process.Graphics, 3);
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(actor).PoseSource!;
            short parent = BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + 27 * 48, 2));
            short length = parent < 0 ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(18 + parent * 48, 2));
            NativeBattleFullMacVertex expected = NativeBattleGfMatrices.TransformMac460840(model.CurrentBodyBoneMatrices!.Matrices[27], new(0, 0, length));
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 60);
            frame = frame with { Staging = frame.Staging!.MarkUnknown(allocation.Address + 0x3A, 2).Write(allocation.Address + 0x3A, new byte[] { 0xA5 }) };
            (byte[] priorBytes, bool[] priorKnown) = frame.Staging.ReadSnapshot(allocation.Address, 60);
            NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, BitConverter.GetBytes((ushort)0x5AA5));
            NativeBattleEffect287PointStep result = Invoke<NativeBattleEffect287PointStep>(process, "NativeEffect287LocalPoint", process.Graphics, frame, RequireOriginalModelOwner(process.Graphics, 3), 27, retained);
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

    private static void WithSource(Func<BattlePresentationProcess, BattleAssets, BattleCombatantDefinition, NativeBattleEffect287Frame, NativeBattleEffect287Frame> check)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select the installed Effect287 source resources.");
        using GameAssets game = new(directory!);
        GameSession owner = GameWorld.CreateColdProcess(directory!);
        owner.StartNewGame();
        using GameWorld world = new(owner, directory!, sharedAssets: game);
        const ushort formation = 431;
        BattleAssets assets = world.BattleResources;
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = assets.FormationSourceHash(formation) };
        BattleSession battle = world.StartDiagnosticBattle(new(formation, 0, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Roster = roster });
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        BattleEnemyActorModel caster = Assert.IsType<BattleEnemyActorModel>(process.FindOriginalActor(NativeBattleModelSlot.Decode(3)));
        NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(caster);
        Assert.Equal((byte)125, model.ModelId4);
        Assert.Equal(28, model.CurrentBodyBoneMatrices!.Matrices.Length);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([287])[286];
        NativeBattleEffect287State state = NativeBattleEffect287State.FromColdProcess().WriteModelAddress(NativeBattleEffect287Profile.CasterGlobal, caster, process.ActionPackets.ModelAliases);
        NativeBattleEffect287Frame frame = new(process.Memory, new(287, 286, binding, null, null), battle.Capture().NativeCrtRandom!, state) { Staging = process.NativeFileStaging };
        NativeBattleEffect287Frame result = check(process, assets, battle.GetCombatantDefinition(caster.Actor.ActorId), frame);
        result.Memory.GeometryRegisters!.Validate();
    }

    private static T Invoke<T>(BattlePresentationProcess process, string method, params object[] arguments)
    {
        MethodInfo info = typeof(BattlePresentationProcess).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidDataException("The selected current point service is unavailable.");
        return Assert.IsType<T>(info.Invoke(process, arguments));
    }
}
