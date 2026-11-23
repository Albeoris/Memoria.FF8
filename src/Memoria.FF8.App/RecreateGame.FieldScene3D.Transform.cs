using System.Globalization;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private enum Scene3DTransformMode { Translate, Rotate, Scale }
    private enum Scene3DInspectionMode { GameCamera, ImportedAuthorCamera, FreeInspection }

    private Scene3DTransformMode? scene3DTransformMode;
    private char scene3DTransformAxis;
    private string scene3DTransformText = "";
    private Scene3DInspectionMode scene3DInspection;
    private float scene3DInspectionAngle;

    private string Scene3DTransformPrompt => scene3DTransformMode is { } mode
        ? $"{mode} {(mode == Scene3DTransformMode.Scale ? "uniform" : scene3DTransformAxis)} · {scene3DTransformText}  |  Enter apply · Esc cancel"
        : "";

    private void BeginScene3DTransform(Scene3DTransformMode mode)
    {
        if (Scene3DInstance is null)
        {
            return;
        }
        scene3DTransformMode = mode;
        scene3DTransformAxis = mode == Scene3DTransformMode.Translate ? 'X' : 'Z';
        scene3DTransformText = "";
        scene3DNotice = mode switch
        {
            Scene3DTransformMode.Translate => "Choose X, Y or Z and enter a translation in native field units.",
            Scene3DTransformMode.Rotate => "Choose X, Y or Z and enter a rotation in degrees around the instance origin.",
            _ => "Enter a positive uniform scale multiplier."
        };
        gameMenus?.InvalidateLiveData();
    }

    private void TypeScene3DTransform(char value)
    {
        if (scene3DTransformText.Length < 24 && (char.IsAsciiDigit(value) || value is '-' or '+' or '.' or ','))
        {
            scene3DTransformText += value == ',' ? '.' : value;
            gameMenus?.InvalidateLiveData();
        }
    }

    private bool PollScene3DTransform(KeyboardState keys, KeyboardState previous)
    {
        bool Pressed(Keys key) => keys.IsKeyDown(key) && previous.IsKeyUp(key);
        if (scene3DTransformMode is { } active)
        {
            if (pendingCapture is null)
            {
                controls?.Poll();
            }
            if (Pressed(Keys.Escape))
            {
                scene3DTransformMode = null;
                scene3DNotice = "Placement edit cancelled.";
            }
            else if (Pressed(Keys.Enter))
            {
                if (Scene3DInstance is { } instance && TryScene3DTransform(instance, out FieldSceneInstance modified))
                {
                    try
                    {
                        ChangeScene3DInstance(_ => modified);
                        scene3DTransformMode = null;
                        scene3DNotice = DescribePlacement(modified);
                    }
                    catch (InvalidDataException exception)
                    {
                        scene3DNotice = exception.Message;
                    }
                }
                else
                {
                    scene3DNotice = "Enter a finite value within the supported placement range.";
                }
            }
            else if (Pressed(Keys.Back) && scene3DTransformText.Length != 0)
            {
                scene3DTransformText = scene3DTransformText[..^1];
            }
            else if (active != Scene3DTransformMode.Scale)
            {
                if (Pressed(Keys.X))
                {
                    scene3DTransformAxis = 'X';
                }
                else if (Pressed(Keys.Y))
                {
                    scene3DTransformAxis = 'Y';
                }
                else if (Pressed(Keys.Z))
                {
                    scene3DTransformAxis = 'Z';
                }
            }
            gameMenus?.InvalidateLiveData();
            if (scene3DTransformMode is null)
            {
                keyboard.SuppressHeldUiInput();
                drainUiButtons = true;
            }
            return true;
        }
        if (!physicalInput.CanRead || scene3DDialogOpen || scene3DDraft is null || Scene3DInstance is null ||
            gameMenus?.ScreenId is not (Scene3DEditorScreen or "field-scene-3d-placement") ||
            keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl) || keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt))
        {
            return false;
        }
        if (Pressed(Keys.G))
        {
            BeginScene3DTransform(Scene3DTransformMode.Translate);
        }
        else if (Pressed(Keys.R))
        {
            BeginScene3DTransform(Scene3DTransformMode.Rotate);
        }
        else if (Pressed(Keys.S))
        {
            BeginScene3DTransform(Scene3DTransformMode.Scale);
        }
        return scene3DTransformMode is not null;
    }

    private bool TryScene3DTransform(FieldSceneInstance instance, out FieldSceneInstance modified)
    {
        modified = instance;
        if (scene3DTransformMode is not { } mode || !float.TryParse(scene3DTransformText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            !float.IsFinite(value) || MathF.Abs(value) > 1e6f || mode == Scene3DTransformMode.Scale && value is < 0.0001f or > 1000)
        {
            return false;
        }

        Vector3 axis = scene3DTransformAxis switch { 'X' => Vector3.UnitX, 'Y' => Vector3.UnitY, _ => Vector3.UnitZ };
        Matrix4x4 current = FieldSceneDocuments.Matrix(instance.AssetToField);
        switch (mode)
        {
            case Scene3DTransformMode.Translate:
                current.Translation += axis * value;
                break;
            case Scene3DTransformMode.Rotate:
                Vector3 pivot = current.Translation;
                current.Translation = Vector3.Zero;
                current *= Matrix4x4.CreateFromAxisAngle(axis, value * MathF.PI / 180);
                current.Translation = pivot;
                break;
            case Scene3DTransformMode.Scale:
                current = Matrix4x4.CreateScale(value) * current;
                break;
        }
        float[] placement = FieldSceneDocuments.Values(current);
        try
        {
            _ = FieldSceneDocuments.Matrix(placement);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        modified = instance with { AssetToField = placement };
        return true;
    }

    private void CycleScene3DInspection()
    {
        scene3DInspection = (Scene3DInspectionMode)(((int)scene3DInspection + 1) % 3);
        if (scene3DInspection == Scene3DInspectionMode.ImportedAuthorCamera &&
            (Scene3DInstance is not { } instance || scene3DDraft!.Assets[instance.AssetId].Cameras.Count == 0))
        {
            scene3DInspection = Scene3DInspectionMode.FreeInspection;
        }
    }

    private PreparedFieldScene? Scene3DDisplayDraft()
    {
        if (scene3DDraft is not { } draft || Scene3DView is not { } current)
        {
            return scene3DDraft;
        }
        FieldSceneView visible = current;
        if (Scene3DInstance is { } instance)
        {
            if (TryScene3DTransform(instance, out FieldSceneInstance modified))
            {
                visible = visible with { Instances = current.Instances.Select(candidate => candidate.Id == instance.Id ? modified : candidate).ToArray() };
            }
            if (scene3DInspection == Scene3DInspectionMode.ImportedAuthorCamera && draft.Assets[instance.AssetId].Cameras.FirstOrDefault() is { } camera)
            {
                string cameraId = current.CameraInstance == instance.Id ? current.CameraId ?? camera.Id : camera.Id;
                visible = visible with
                {
                    Background = FieldSceneBackgroundMode.Replace,
                    Projection = FieldSceneProjection.Authored,
                    CameraSlot = world!.Events.Scene.CameraIndex,
                    CameraInstance = instance.Id,
                    CameraId = cameraId,
                    CameraMotion = FieldSceneCameraMotion.Static,
                    ScrollBaseline = null,
                    NativeStates = CaptureScene3DStates(),
                    NativeStateSets = null,
                    ReplacedModules = null
                };
                return draft with { Document = draft.Document with { Views = [visible] } };
            }
        }
        return ReferenceEquals(visible, current) ? draft : draft with { Document = draft.Document with { Views = draft.Document.Views.Select(view => view == current ? visible : view).ToArray() } };
    }

    private FieldCamera? Scene3DInspectionCamera()
    {
        if (scene3DDraft is null || lightingEditor is not null || scene3DCompare || scene3DInspection != Scene3DInspectionMode.FreeInspection || world?.Field is not { } field)
        {
            return null;
        }
        FieldCamera source = field.GetCamera(world.Events.Scene.CameraIndex);
        Vector3 pivot = LightingInitialPivot(field);
        Matrix4x4 orbit = Matrix4x4.CreateTranslation(-pivot) * Matrix4x4.CreateRotationZ(scene3DInspectionAngle) * Matrix4x4.CreateTranslation(pivot);
        if (!Matrix4x4.Invert(orbit, out Matrix4x4 inverse))
        {
            return null;
        }
        Vector3 offset = inverse.Translation;
        Vector3 translation = source.Translation + new Vector3(Vector3.Dot(source.AxisX, offset), Vector3.Dot(source.AxisY, offset), Vector3.Dot(source.AxisZ, offset));
        return source with
        {
            AxisX = Vector3.TransformNormal(source.AxisX, orbit),
            AxisY = Vector3.TransformNormal(source.AxisY, orbit),
            AxisZ = Vector3.TransformNormal(source.AxisZ, orbit),
            Translation = translation
        };
    }
}
