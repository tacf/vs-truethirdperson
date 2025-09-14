using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace TrueThirdPerson;

[HarmonyPatchCategory("truethirdperson_input")]
class InputHandlerPatch
{
    private const float DefaultMinTpDistance = 1.5f; // Default minimum for the game is 1.0f we set it a bit farther
    private const float DefaultMaxTpDistance = 10f; // TODO: Should we fetch this from the Game defaults?
    private const float AutoZoomInAdjustRatio = 0.15f;
    private const float ZoomStep = 0.3f;
    private const float CameraLerpStep = 0.1f;
    
    private static float _tpDistance = DefaultMinTpDistance;
    private static float _tpDesiredDistance = DefaultMinTpDistance;
    
    private Harmony _harmony;
    private static ICoreClientAPI _clientApi;
    
    
    public void Patch(ICoreClientAPI api)
    {
        _clientApi = api;
        _harmony = TrueThirdPerson.NewPatch("Input Handler", "truethirdperson_input");
    }
    
    public void Unpatch()
    {
        _harmony.UnpatchAll();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(HudHotbar), nameof(HudHotbar.OnMouseWheel))]
    public static void OnMouseWheel(ref MouseWheelEventArgs args)
    {
        if (CameraFunctions.GetDesiredCameraMode() == EnumCameraMode.Overhead || !(_clientApi.Input.IsHotKeyPressed("scrollzoommodifier"))) return;
        
        float newZoom = float.Clamp(_tpDesiredDistance - args.delta * ZoomStep, DefaultMinTpDistance, DefaultMaxTpDistance);

        if (newZoom > DefaultMinTpDistance && !CameraFunctions.OverrideCamera)
        {
            CameraFunctions.RequestCameraUpdate(EnumCameraMode.ThirdPerson);
        }  
        else if (newZoom <= DefaultMinTpDistance && CameraFunctions.OverrideCamera)
        {
            CameraFunctions.RequestCameraUpdate(EnumCameraMode.FirstPerson);
        } 
        
        _tpDesiredDistance = newZoom;
        
        args.SetHandled();
        // By using 'ref' and setting delta to 0 we ensure that hotbar does not handle the input
        args.delta = 0;
    }

    public static void ResetCameraZoom()
    {
        _tpDistance = DefaultMinTpDistance;
        _tpDesiredDistance = DefaultMinTpDistance;
    }
    public static float GetTppDistance()
    {
        _tpDistance = GameMath.Lerp(_tpDistance, _tpDesiredDistance, CameraLerpStep);
        return _tpDistance;
    }

    public static void SetTppDistance(float distance)
    {
        if (distance >= _tpDesiredDistance) return;
        _tpDistance = distance;
        // We drop down the previous used distance
        // This is a simple technique to avoid jumping in and out of the position when in enclosed spaces.
        // TODO: Store the previous value and set a "timer" to allow lerping back to original zoom.
        _tpDesiredDistance -= (_tpDesiredDistance - _tpDistance) * AutoZoomInAdjustRatio; 
    }
}