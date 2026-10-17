using System.Diagnostics;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record PreparedBattlePresentation(GameWorld.PreparedBattleEntry Entry, BattleScenePresentation Scene, double PreparationMilliseconds);

    private NativeBattleEntryTransition? battleEntryTransition;
    private Task<PreparedBattlePresentation>? battleEntryPreparation;
    private Texture2D? battleEntryCapture;
    private Texture2D? battleEntryGradient;
    private RenderTarget2D? battleEntryPrevious;
    private RenderTarget2D? battleEntryNext;
    private int battleEntryRenderedCounter;
    private readonly BlendState battleEntryAdditive = new() { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One };
    private readonly RasterizerState battleEntryScissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };
    private long battleEntryCallbackStarted;
    private long battleEntryStarted;

    private void BeginBattleEntry()
    {
        ushort flags = world!.CaptureBattleEntryFlags();
        battleEntryTransition = world.CreateBattleEntryTransition();
        int formation = session.State.PendingBattleEncounter!.Formation;
        NativeBattleMusicEntryPreparation music = world.BeginBattleEntryMusic(formation, flags);
        BattleAtbMode mode = session.State.GameplayPreferences.AtbMode;
        byte seed = (byte)System.Security.Cryptography.RandomNumberGenerator.GetInt32(256);
        UploadField(gameplayClock.Total);
        battleEntryCapture = PresentedFieldTexture;
        battleEntryPrevious?.Dispose();
        battleEntryNext?.Dispose();
        battleEntryPrevious = new RenderTarget2D(GraphicsDevice, battleEntryCapture.Width, battleEntryCapture.Height);
        battleEntryNext = new RenderTarget2D(GraphicsDevice, battleEntryCapture.Width, battleEntryCapture.Height);
        battleEntryRenderedCounter = -1;
        battleEntryStarted = Stopwatch.GetTimestamp();
        battleEntryCallbackStarted = battleEntryStarted;
        IsFixedTimeStep = false;
        if (battleEntryGradient is null)
        {
            battleEntryGradient = new Texture2D(GraphicsDevice, 256, 1);
            Color[] gradient = new Color[256];
            for (int index = 0; index < gradient.Length; index++)
            {
                gradient[index] = new Color(0, 0, 0, 192 * (255 - index) / 255);
            }
            battleEntryGradient.SetData(gradient);
        }
        // Until completion, only the captured texture is presented. The worker exclusively owns
        // resource preparation; publication, audio callbacks and GPU work remain on the game thread.
        battleEntryPreparation = Task.Run(() =>
        {
            long started = Stopwatch.GetTimestamp();
            GameWorld.PreparedBattleEntry prepared = world.PrepareBattleEntry(formation, flags, seed, mode, music);
            BattleScenePresentation scene = new(world.BattleResources, prepared.Battle.Capture().Definition, true);
            EnsureLiveMenuCatalog();
            return new PreparedBattlePresentation(prepared, scene, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        });
    }

    private void UpdateBattleEntry(GameTime gameTime)
    {
        long started = Stopwatch.GetTimestamp();
        battlePresentationPacer.WaitForNextFrame(uiPreferences.FieldFps);
        updateFrames++;
        if (options.ExitAfterFrames is int frames && updateFrames >= frames || options.ExitAfterSeconds is double duration && gameTime.TotalGameTime.TotalSeconds >= duration)
        {
            Exit();
        }
        NativeBattleEntryTransition transition = battleEntryTransition!;
        if (Stopwatch.GetElapsedTime(battleEntryCallbackStarted).TotalMilliseconds >= NativeBattleEntryTransition.CallbackMilliseconds)
        {
            if (!transition.Current.Complete)
            {
                transition.Advance();
                battleEntryCallbackStarted = Stopwatch.GetTimestamp();
            }
            else if (battleEntryPreparation!.IsCompleted)
            {
                FinishBattleEntry();
            }
        }
        if (battleEntryPreparation?.IsFaulted == true)
        {
            FinishBattleEntry();
        }
        fieldAudio?.Update();
        base.Update(gameTime);
        metrics.Update(started);
    }

    private void FinishBattleEntry()
    {
        try
        {
            PreparedBattlePresentation prepared = battleEntryPreparation!.GetAwaiter().GetResult();
            world!.PublishPreparedBattleEntry(prepared.Entry);
            EnsureBattleHud(world.Battle!, prepared.Scene);
            SynchronizeBattleAudio();
            Console.WriteLine(FormattableString.Invariant($"battle-entry preparation-ms={prepared.PreparationMilliseconds:F3} transition-ms={Stopwatch.GetElapsedTime(battleEntryStarted).TotalMilliseconds:F3}"));
        }
        catch (Exception exception) when (exception is Memoria.FF8.Events.EventScriptException or NotSupportedException or InvalidDataException or InvalidOperationException)
        {
            fieldFaulted = true;
            error = exception.Message;
            Console.Error.WriteLine(exception);
        }
        finally
        {
            battleEntryTransition = null;
            battleEntryPreparation = null;
            battleEntryCapture = null;
            appliedFrameLimit = null;
            ResetElapsedTime();
        }
    }

    private void DrawBattleEntry(GameTime gameTime)
    {
        if (battleEntryRenderedCounter != battleEntryTransition!.Counter)
        {
            RenderBattleEntryCallback();
        }
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        PixelBounds fit = ViewportLayout.Fit(640, 480, bounds.Width, bounds.Height);
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        batch.Draw(battleEntryPrevious!, new Rectangle(fit.X, fit.Y, fit.Width, fit.Height), Color.White);
        batch.End();
        CapturePresentedFrame(gameTime);
        base.Draw(gameTime);
        metrics.Draw();
    }

    private void RenderBattleEntryCallback()
    {
        NativeBattleEntryTransitionFrame frame = battleEntryTransition!.Current;
        RenderTargetBinding[] previousTargets = GraphicsDevice.GetRenderTargets();
        Rectangle previousScissor = GraphicsDevice.ScissorRectangle;
        GraphicsDevice.SetRenderTarget(battleEntryNext);
        float scaleX = battleEntryNext!.Width / 640f;
        float scaleY = battleEntryNext.Height / 480f;
        Vector2 origin = new(0, 16 * scaleY);
        Rectangle crop = new(0, (int)origin.Y, battleEntryNext.Width, (int)(448 * scaleY));
        Rectangle source = new(0, battleEntryCapture!.Height / 30, battleEntryCapture.Width, battleEntryCapture.Height * 14 / 15);
        GraphicsDevice.Clear(Color.Black);
        GraphicsDevice.ScissorRectangle = crop;
        batch.Begin(samplerState: SamplerState.LinearClamp, rasterizerState: battleEntryScissor);
        batch.Draw(pixel, crop, new Color(frame.ClearGray, frame.ClearGray, frame.ClearGray));
        if (frame.CopyCapture)
        {
            Texture2D previous = battleEntryRenderedCounter < 0 ? battleEntryCapture : battleEntryPrevious!;
            batch.Draw(previous, crop, source, Color.White);
        }
        batch.End();
        batch.Begin(blendState: battleEntryAdditive, samplerState: SamplerState.LinearClamp, rasterizerState: battleEntryScissor);
        foreach (NativeBattleEntryQuad quad in frame.Quads)
        {
            Vector2 position = origin + new Vector2(quad.X * scaleX, quad.Y * scaleY);
            Vector2 scale = new(quad.Width * scaleX / source.Width, quad.Height * scaleY / source.Height);
            batch.Draw(battleEntryCapture, position, source, new Color(quad.Gray, quad.Gray, quad.Gray, (byte)255), 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        batch.End();
        batch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.LinearClamp, rasterizerState: battleEntryScissor);
        foreach (NativeBattleEntryWipeRow row in frame.WipeRows)
        {
            if (row.X0 == row.X1)
            {
                continue;
            }
            Vector2 position = origin + new Vector2(Math.Min(row.X0, row.X1) * scaleX, row.Y * scaleY);
            Vector2 scale = new(Math.Abs(row.X1 - row.X0) * scaleX / 256, scaleY);
            batch.Draw(battleEntryGradient!, position, null, Color.White, 0, Vector2.Zero, scale, row.X1 < row.X0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }
        batch.End();
        GraphicsDevice.SetRenderTargets(previousTargets);
        GraphicsDevice.ScissorRectangle = previousScissor;
        (battleEntryPrevious, battleEntryNext) = (battleEntryNext, battleEntryPrevious);
        battleEntryRenderedCounter = frame.Counter;
    }

    private void DisposeBattleEntry()
    {
        if (battleEntryPreparation is not null)
        {
            try
            {
                battleEntryPreparation.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
        }
        battleEntryGradient?.Dispose();
        battleEntryPrevious?.Dispose();
        battleEntryNext?.Dispose();
        battleEntryAdditive.Dispose();
        battleEntryScissor.Dispose();
    }
}
