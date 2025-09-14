using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TrueThirdPerson;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace TrueThirdPerson;

public class TrueThirdPerson : ModSystem
{
    public static ICoreClientAPI clientApi;
    
    public static ModInfo ModInfo;

    readonly CameraFunctions _cameraFunctions = new();
    readonly CameraOverwritePatch _cameraOverwritePatch = new();
    readonly InputHandlerPatch _inputHandlerPatch = new();
    readonly AimRenderPatch _aimRenderPatch = new();

    public override void StartClientSide(ICoreClientAPI api)
    {
        clientApi = api;
        base.StartClientSide(api);
        _cameraFunctions.Initialize(clientApi);
        _cameraOverwritePatch.Patch(clientApi);
        _inputHandlerPatch.Patch(clientApi);
        _aimRenderPatch.Patch();
    }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        ModInfo = base.Mod.Info;
        Logger.Init(api.Logger);
        Logger.Log($"Running on Version: {Mod.Info.Version}");
    }

    public override void Dispose()
    {
        base.Dispose();
        _inputHandlerPatch.Unpatch();
        _cameraOverwritePatch.Unpatch();
        _aimRenderPatch.Unpatch();
        _cameraFunctions.TearDown();
    }

    public static Harmony NewPatch(string description, string category)
    {
        Harmony patcher = null;
        if (!Harmony.HasAnyPatches(category))
        {
            patcher = new Harmony(category);
            patcher.PatchCategory(category);
            Logger.Log($"Patched {description}");
        }
        else Logger.Error($"Patch '{category}' ('{description}') failed. Check if other patches with same id have been loaded");

        return patcher;
    }
}

public class Logger
{
    private static ILogger _logger;
    private string _logBaseFormat = string.Format("[{0}] {1}", TrueThirdPerson.ModInfo.Name);

    public static void Init(ILogger logger) => _logger = logger;
    public static void Log(string message) => _logger.Log(EnumLogType.Build, message);
    public static void Error(string message) => _logger.Log(EnumLogType.Error, message);
}