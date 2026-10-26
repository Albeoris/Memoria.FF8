using System.Buffers.Binary;
using System.Diagnostics;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianAnimationSkipTests
{
    [Theory]
    [InlineData(0, 116)]
    [InlineData(1, 185)]
    [InlineData(2, 201)]
    [InlineData(3, 95)]
    [InlineData(5, 325)]
    public void Installed_ordinary_guardian_skip_preserves_contacts_and_gameplay_and_restores_at_request_and_finale(int guardian, ushort effectId) => Run(guardian, effectId);

    [Theory]
    [InlineData(4, 205)]
    [InlineData(6, 278)]
    [InlineData(7, 6)]
    [InlineData(8, 291)]
    [InlineData(9, 203)]
    [InlineData(10, 204)]
    [InlineData(11, 191)]
    [InlineData(12, 202)]
    [InlineData(13, 199)]
    [InlineData(14, 90)]
    [InlineData(15, 206)]
    public void Installed_remaining_guardian_skip_preserves_contacts_and_gameplay_and_restores_at_request_and_finale(int guardian, ushort effectId) => Run(guardian, effectId);

    [Fact]
    public void Installed_named_guardian_skip_continuation_preserves_contacts_and_finale()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_SKIP_CONTINUATION");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Select a standalone ordinary guardian continuation with MEMORIA_FF8_GUARDIAN_SKIP_CONTINUATION.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        Assert.Null(source.NativeGraphics!.GuardianAnimationSkip);
        Assert.True(source.NativeGraphics.OriginalEffect is { ReturnedDescriptor: > 0 });
        Assert.True(source.ActiveAction is { NativeQueuedCommand: 254, GuardianFormInvocation: null });
        NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[source.NativeGraphics.ActionGraphics!.HeaderIndex];
        Assert.Equal((byte)254, header.ReadByte(1));
        int guardian = (header.ReadByte(4) | header.ReadByte(5) << 8) - 64;
        Assert.InRange(guardian, 0, 15);
        ushort effectId = checked((ushort)source.NativeGraphics.OriginalEffect!.Binding.Effect);
        Run(guardian, effectId, source);
    }

    private static void Run(int guardian, ushort effectId, BattleSnapshot? continuation = null)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        string? outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_SKIP_EVIDENCE");
        string? output = string.IsNullOrWhiteSpace(outputRoot) ? null : Path.Combine(outputRoot, "guardian-" + guardian + "-" + Guid.NewGuid().ToString("N"));
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        BattleDefinition definition = continuation?.Definition ?? NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, guardian, effectId);
        if (continuation is null && (guardian is 3 or 11))
        {
            byte[] resistances = new byte[40];
            if (guardian == 11)
            {
                // This target accepts Doomtrain's other statuses and is immune to Doom and Gradual Petrify.
                resistances[18] = 255;
                resistances[20] = 255;
            }
            definition = definition with { Combatants = definition.Combatants.Select(actor => actor.Id == "enemy" ? actor with { NativeStatusResistances = resistances } : actor).ToArray() };
        }
        BattleActionDefinition action = definition.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == guardian);
        string target = action.Allies ? "party" : "enemy";
        BattleSession skipped = continuation is null ? new(definition, BattleAtbMode.TurnBased) : BattleSession.Restore(continuation);
        if (continuation is null)
        {
            Assert.False(skipped.CanSkipGuardianFormFinisher);
            Assert.False(skipped.SkipGuardianFormFinisher());
            for (int callback = 0; callback < 400 && (!skipped.NativeEntryReady || !skipped.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
            {
                skipped.Tick();
            }
            Assert.True(skipped.NativeEntryReady);
            Assert.True(skipped.Combatants.Single(actor => actor.Id == "party").IsReady);
            skipped.QueueAction("party", target, BattleAbility.GuardianForce, guardian);
            for (int callback = 0; callback < 4096 && !skipped.CanSkipGuardianFormFinisher; callback++)
            {
                skipped.Tick();
            }
        }
        else
        {
            Assert.Equal(JsonSerializer.Serialize(continuation), JsonSerializer.Serialize(skipped.Capture()));
            Assert.Null(skipped.NativeGraphics!.GuardianAnimationSkip);
            Assert.False(NativeBattleGuardianAnimationFinale.HasReachedFinale(skipped.NativeGraphics.OriginalEffect!, skipped.NativeEffectMemory!));
        }
        Assert.True(skipped.CanSkipGuardianFormFinisher);
        Assert.Equal(effectId, skipped.NativeGraphics!.OriginalEffect!.Binding.Effect);
        BattleSession ordinary = RoundTrip(skipped);
        BattleScenePresentation scene = new(assets, definition, true);
        HashSet<ushort> ordinaryFlags = [];
        bool lateRequestChecked = false;
        TraceOrdinary(ordinary, 0);
        Complete(ordinary, TraceOrdinary);
        Assert.True(lateRequestChecked);

        Assert.True(skipped.SkipGuardianFormFinisher());
        Assert.False(skipped.CanSkipGuardianFormFinisher);
        Assert.False(skipped.SkipGuardianFormFinisher());
        NativeBattleRestorationPresentationTests.Publish(output, "requested", skipped.Capture(), null);
        BattleSession requested = RoundTrip(skipped);
        long beforeSkip = skipped.NativeGraphics.Callback;
        Stopwatch? skipTimer = output is null ? null : Stopwatch.StartNew();
        skipped.Tick();
        skipTimer?.Stop();
        requested.Tick();
        Assert.Equal(JsonSerializer.Serialize(skipped.Capture()), JsonSerializer.Serialize(requested.Capture()));
        Assert.True(skipped.NativeGraphics.Callback > beforeSkip + 1);
        Assert.False(NativeBattleTargetGuardianCastTests.Released(skipped.Capture()));
        Assert.True(NativeBattleGuardianAnimationFinale.HasReachedFinale(skipped.NativeGraphics.OriginalEffect!, skipped.NativeEffectMemory!));

        BattleSession finale = RoundTrip(skipped);
        BattleSnapshot finaleSnapshot = skipped.Capture();
        byte[] finalePixels = scene.Render(skipped, 320, 224).ToArray();
        Assert.Equal(finalePixels, new BattleScenePresentation(assets, definition, true).Render(finale, 320, 224).ToArray());
        NativeBattleRestorationPresentationTests.Publish(output, "finale", finaleSnapshot, output is null ? null : finalePixels);
        long finaleCallback = skipped.NativeGraphics.Callback;
        skipped.Tick();
        finale.Tick();
        Assert.Equal(finaleCallback + 1, skipped.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(skipped.Capture()), JsonSerializer.Serialize(finale.Capture()));
        List<int> finaleSamples = [];
        Complete(skipped, (battle, callbacks) =>
        {
            int offset = callbacks + 1;
            if (output is not null && offset is 6 or 12 or 24)
            {
                finaleSamples.Add(offset);
                NativeBattleRestorationPresentationTests.Publish(output, "finale-" + offset.ToString("D3"), battle.Capture(), scene.Render(battle, 320, 224).ToArray());
            }
        });

        BattleSnapshot expected = ordinary.Capture();
        BattleSnapshot actual = skipped.Capture();
        Assert.Single(actual.Events, item => item.Kind == "GuardianForceCharging" && item.ActorId == "party");
        Assert.Single(actual.Events, item => item.Kind == "GuardianForceReady" && item.ActorId == "party");
        BattleEvent[] expectedDamage = expected.Events.Where(item => item.Kind == "Damage" && item.ActorId == "party").Select(item => item with { Frame = 0 }).ToArray();
        BattleEvent[] actualDamage = actual.Events.Where(item => item.Kind == "Damage" && item.ActorId == "party").Select(item => item with { Frame = 0 }).ToArray();
        Assert.NotEmpty(expectedDamage);
        Assert.All(actualDamage, item => Assert.Equal(target, item.TargetId));
        Assert.Equal(expectedDamage, actualDamage);
        Assert.Equal(expectedDamage.Sum(item => item.Amount), actualDamage.Sum(item => item.Amount));
        Assert.Equal(JsonSerializer.Serialize(expected.NativeActionPackets), JsonSerializer.Serialize(actual.NativeActionPackets));
        Assert.Equal(JsonSerializer.Serialize(expected.GuardianForces), JsonSerializer.Serialize(actual.GuardianForces));
        Assert.Equal(expected.NativeGlobalRandom, actual.NativeGlobalRandom);
        Assert.Equal(expected.NativeGlobalRandomCalls, actual.NativeGlobalRandomCalls);
        Assert.Equal(expected.NativeCrtRandom, actual.NativeCrtRandom);
        foreach (BattleCombatantSnapshot combatant in expected.Combatants)
        {
            BattleCombatantSnapshot result = actual.Combatants.Single(actor => actor.Id == combatant.Id);
            Assert.Equal(combatant.CurrentHp, result.CurrentHp);
            Assert.Equal(JsonSerializer.Serialize(combatant.StatusEffects), JsonSerializer.Serialize(result.StatusEffects));
            Assert.Equal(JsonSerializer.Serialize(combatant.NativeStatus), JsonSerializer.Serialize(result.NativeStatus));
            Assert.Equal(combatant.GuardianForceCompatibility, result.GuardianForceCompatibility);
        }
        Assert.False(skipped.CanSkipGuardianFormFinisher);
        Assert.False(skipped.SkipGuardianFormFinisher());
        if (guardian == 3)
        {
            NativeBattleStatusSnapshot statuses = actual.Combatants.Single(actor => actor.Id == target).NativeStatus!;
            Assert.NotEqual(0u, (uint)action.NativeAction!.Statuses0 | action.NativeAction.Statuses1);
            Assert.Equal(action.NativeAction.Statuses0, statuses.Saved & action.NativeAction.Statuses0);
            Assert.Equal(action.NativeAction.Statuses1, statuses.Battle & action.NativeAction.Statuses1);
        }
        if (guardian == 11)
        {
            NativeBattleStatusSnapshot statuses = actual.Combatants.Single(actor => actor.Id == target).NativeStatus!;
            Assert.NotEqual(0u, (uint)(statuses.Saved & action.NativeAction!.Statuses0) | (statuses.Battle & action.NativeAction.Statuses1));
        }

        if (output is not null)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "completed", actual, scene.Render(skipped, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            {
                Status = "passed",
                guardian,
                effectId,
                Target = target,
                ContinuationFrame = continuation?.Frame,
                ContinuationCallback = continuation?.NativeGraphics?.Callback,
                SkippedCallbacks = finaleCallback - beforeSkip,
                SkipElapsedMilliseconds = skipTimer?.Elapsed.TotalMilliseconds,
                FinaleFrame = finaleSnapshot.Frame,
                FinaleCallback = finaleCallback,
                FinaleSamples = finaleSamples,
                OrdinaryHeaderFlags = ordinaryFlags,
                CompletedFrame = actual.Frame,
                CompletedCallback = actual.NativeGraphics!.Callback,
                DamageContacts = actualDamage.Length,
                TotalDamage = actualDamage.Sum(item => item.Amount),
                Combatants = actual.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, actor.NativeStatus }),
                RequestRestoreMatches = true,
                FinaleRestoreMatches = true,
                FinalePixelsMatch = true,
                OrdinaryGameplayMatches = true,
                LateRequestPreservesOneCallback = lateRequestChecked
            }));
            Console.WriteLine($"Guardian {guardian} skip artifacts: {output}");
        }

        void TraceOrdinary(BattleSession battle, int callbacks)
        {
            if (!lateRequestChecked && battle.CanSkipGuardianFormFinisher && battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } original && NativeBattleGuardianAnimationFinale.HasReachedFinale(original, battle.NativeEffectMemory!))
            {
                BattleSession late = RoundTrip(battle);
                Assert.True(late.SkipGuardianFormFinisher());
                Assert.False(late.SkipGuardianFormFinisher());
                long callback = late.NativeGraphics!.Callback;
                late.Tick();
                Assert.Equal(callback + 1, late.NativeGraphics!.Callback);
                lateRequestChecked = true;
            }
            if (output is null || effectId is not (6 or 202 or 203 or 204 or 205 or 206) || battle.NativeGraphics?.OriginalEffect is not { ReturnedDescriptor: > 0 } || battle.NativeEffectMemory?.ScriptedEffects is not { } core)
            {
                return;
            }
            uint header = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x279744C, 4));
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(checked(header + 2), 2));
            if (ordinaryFlags.Add(flags))
            {
                string name = "ordinary-" + battle.NativeGraphics.Callback.ToString("D5") + "-flags-" + flags.ToString("X4");
                byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                if (effectId is 204 or 206)
                {
                    NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), pixels);
                }
                else
                {
                    PublishFrame(output, name, pixels);
                }
                File.WriteAllText(Path.Combine(output, name + ".frame.json"), JsonSerializer.Serialize(new { battle.Frame, battle.NativeGraphics.Callback, HeaderFlags = flags, CallbacksSinceOrdinaryStart = callbacks }));
            }
        }
    }

    private static void PublishFrame(string directory, string name, byte[] pixels)
    {
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        bitmap.SavePng(Path.Combine(directory, name + ".png"));
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        string json = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        return restored;
    }

    private static void Complete(BattleSession battle, Action<BattleSession, int>? afterCallback = null)
    {
        for (int callback = 0; callback < 4096; callback++)
        {
            if (NativeBattleTargetGuardianCastTests.Released(battle.Capture()))
            {
                return;
            }
            battle.Tick();
            afterCallback?.Invoke(battle, callback + 1);
        }
        Assert.Fail("The ordinary guardian command did not release its native graphics and gameplay owners within the callback bound.");
    }
}
