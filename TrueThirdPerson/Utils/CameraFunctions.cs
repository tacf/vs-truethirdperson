using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace TrueThirdPerson;

class CameraFunctions
{
    static public bool OverrideCamera;
    
    private static bool _cameraNeedsUpdate;
    private static EnumCameraMode _cameraMode;

    private static ICoreClientAPI _clientApi;
    
    public void Initialize(ICoreClientAPI api)
    {
        _clientApi = api;
        RegisterKey("camerastepright", GlKeys.Right);
        RegisterKey("camerastepleft", GlKeys.Left);
        RegisterKey("camerastepup", GlKeys.Up);
        RegisterKey("camerastepdown", GlKeys.Down);
        RegisterKey("scrollzoommodifier", GlKeys.Tilde);

        _clientApi.Input.AddHotkeyListener(HotKeyListener);
        Logger.Log("Hotkeys registered");
    }

    public void TearDown()
    {
        OverrideCamera = false;
    }
    
    public static bool CameraNeedsUpdate() => _cameraNeedsUpdate;

    public static void CameraModeUpdated(EnumCameraMode mode)
    {
        _cameraMode = mode;
        _cameraNeedsUpdate = false;
        OverrideCamera = mode == EnumCameraMode.ThirdPerson;
    }
    
    public static EnumCameraMode GetDesiredCameraMode() => _cameraMode;

    public static void RequestCameraUpdate(EnumCameraMode mode)
    {
        _cameraMode = mode;
        _cameraNeedsUpdate = true;
    }

    private void RegisterKey(string keyCode, GlKeys key)
    {
        _clientApi.Input.RegisterHotKey(
            keyCode,
            $"[{TrueThirdPerson.ModInfo.Name}] {Lang.Get($"{TrueThirdPerson.ModInfo.ModID}:{keyCode}")}",
            key, HotkeyType.GUIOrOtherControls);
        _clientApi.Input.SetHotKeyHandler(keyCode, (_) => true);
    }

    // Listining for the cycle camera
    private void HotKeyListener(string hotkeycode, KeyCombination keyComb)
    {
        switch (hotkeycode)
        {
            case "cyclecamera": CheckThirdPerson(); return;
            case "camerastepright": IncreaseCameraRight(); return;
            case "camerastepleft": IncreaseCameraLeft(); return;
            case "camerastepup": IncreaseCameraUp(); return;
            case "camerastepdown": IncreaseCameraDown(); return;
        }
    }

    private static void IncreaseCameraUp() => ApplyCameraStep(ref CameraOverwritePatch.cameraYPosition, 0.1);
    private static void IncreaseCameraDown() => ApplyCameraStep(ref CameraOverwritePatch.cameraYPosition, -0.1);
    private static void IncreaseCameraLeft() => ApplyCameraStep(ref CameraOverwritePatch.cameraXPosition, -0.1);
    private static void IncreaseCameraRight() => ApplyCameraStep(ref CameraOverwritePatch.cameraXPosition, 0.1);
    private static void ApplyCameraStep(ref double posVar, double step) => posVar = double.Clamp(posVar + step, -2, 2);

    private static void CheckThirdPerson()
    {
        // Prevent camera jumping from First Person to Third Person with a far zoom
        if (_clientApi.World.Player.CameraMode == EnumCameraMode.Overhead)
            InputHandlerPatch.ResetCameraZoom();
        CameraModeUpdated(_clientApi.World.Player.CameraMode);
    }
}
