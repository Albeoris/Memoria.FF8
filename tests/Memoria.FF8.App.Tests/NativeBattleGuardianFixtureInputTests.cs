using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianFixtureInputTests
{
    [Fact]
    public void InstalledManualHeaderFixtureInputsIncludeItsOriginalKernelRowAndColdPhysicalThreeByte()
    {
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = new() { NativeActions = assets.NativeActions.ToArray(), ActionDefinitions = BattlePartyProjection.CreateActions(game.ReadNewGameCharacterVitals(), assets.ActionDefinitions) };
        BattleNativeAction native = Assert.Single(definition.NativeActions, action => action.Command == 254 && action.ActionId == 64);
        BattleNativeAction projected = Assert.Single(definition.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0).NativeAction!;
        Assert.Equal((ushort)116, native.MagicId);
        Assert.Equal(132, native.RawRecord.Length);
        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(projected));
        Assert.Equal((byte)0, NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game));
    }
}

internal static class NativeBattleGuardianFixtureInputs
{
    public static byte ReadColdPhysicalThreeRemainingHits(GameAssets game)
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe"));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, Convert.ToHexString(SHA256.HashData(executable)));
        using PEReader pe = new(new MemoryStream(executable));
        long rva = 0x1D27BD9L + 208 * 3 - (long)pe.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = Assert.Single(pe.PEHeaders.SectionHeaders, candidate => rva >= candidate.VirtualAddress && rva < (long)candidate.VirtualAddress + candidate.VirtualSize);
        Assert.True(rva - section.VirtualAddress >= section.SizeOfRawData);
        //This is an explicit authenticated input to the declared fresh cold/manual-header fixture, never a restore default.
        return 0;
    }
}
