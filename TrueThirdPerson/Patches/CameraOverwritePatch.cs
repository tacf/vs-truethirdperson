using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace TrueThirdPerson;

[HarmonyPatchCategory("truethirdperson_camera")]
class CameraOverwritePatch
{
    private Harmony _harmony;
    public static readonly double cameraDefaultXPosition = 1.5;
    public static double cameraXPosition = cameraDefaultXPosition;
    public static double cameraYPosition = 0.00;

    private static ICoreClientAPI _clientApi;

    public void Patch(ICoreClientAPI api)
    {
        _clientApi = api;
        _harmony = TrueThirdPerson.NewPatch("Camera Override", "truethirdperson_camera");
    }

    public void Unpatch()
    {
        _harmony.UnpatchAll();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Camera), nameof(Camera.GetCameraMatrix))]
    public static void GetCameraMatrixStart(ref Camera __instance, Vec3d camEyePosIn, Vec3d worldPos, double yaw,
        double pitch, AABBIntersectionTest intersectionTester)
    {
        if (CameraFunctions.CameraNeedsUpdate())
        {
            Traverse traverse = Traverse.Create(__instance).Field("CameraMode");
            traverse.SetValue(CameraFunctions.GetDesiredCameraMode());
            EnumCameraMode value = (EnumCameraMode)traverse.GetValue();
            if ( value == CameraFunctions.GetDesiredCameraMode())
            {
                CameraFunctions.CameraModeUpdated(value);
            }
        }

        // Return early if there's nothing to do.
        if (!CameraFunctions.OverrideCamera) return;
        
        __instance.Tppcameradistance = InputHandlerPatch.GetTppDistance();
        // Normalizing Yaw
        if (yaw < 0) yaw = 6.28 - (-1 * yaw);
        while (yaw < 0) yaw += 6.28;
        while (yaw >= 6.28) yaw -= 6.28;

        // Get the percentage between the 2 numbers and desired number
        static double GetPercentage(double value, double minValue, double maxValue)
        {
            if (value <= minValue) return 0.0;
            else if (value >= maxValue) return 100.0;
            else return (value - minValue) / (maxValue - minValue) * 100.0;
        }

        // South to East
        if (yaw < 1.5)
        {
            var percentage = GetPercentage(yaw, 0.0, 1.5);
            camEyePosIn[0] -= cameraXPosition * (1 - percentage / 100); // percentage 0 = max
            camEyePosIn[1] += cameraYPosition;
            camEyePosIn[2] += cameraXPosition * percentage / 100; // percentage 0 = min
        }
        // North to East
        else if (yaw >= 1.5 && yaw <= 3.15)
        {
            var percentage = GetPercentage(yaw, 1.5, 3.15);
            camEyePosIn[0] += cameraXPosition * percentage / 100; // percentage 0 = min
            camEyePosIn[1] += cameraYPosition;
            camEyePosIn[2] += cameraXPosition * (1 - percentage / 100); // percentage 0 = max 
        }
        // North to West
        else if (yaw > 3.15 && yaw <= 4.75)
        {
            var percentage = GetPercentage(yaw, 3.15, 4.75);
            camEyePosIn[0] += cameraXPosition * (1 - percentage / 100); // percentage 0 = max
            camEyePosIn[1] += cameraYPosition;
            camEyePosIn[2] -= cameraXPosition * percentage / 100; // percentage 0 = min
        }
        // South to West
        else
        {
            var percentage = GetPercentage(yaw, 4.75, 6.28);
            camEyePosIn[0] -= cameraXPosition * percentage / 100; // percentage 0 = min
            camEyePosIn[1] += cameraYPosition;
            camEyePosIn[2] -= cameraXPosition * (1 - percentage / 100); // percentage 0 = max 
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Camera), nameof(Camera.GetCameraMatrix))]
    public static double[] GetCameraMatrixFinish(double[] __result, Camera __instance, Vec3d camEyePosIn,
        Vec3d worldPos, double yaw, double pitch, AABBIntersectionTest intersectionTester)
    {
        int TppCameraDistanceMax = 10;
        float Tppcameradistance = 1;
        float currentTppcameradistance = (float)__instance.CameraEyePos.Clone().Sub(__instance.CamSourcePosition).Length();
        Vec3d camEyePosOutTmp = __instance.CameraEyePos.Clone();
        Vec3d camTargetTmp = new();

        // Recreating the internal functions from Camera
        bool LimitThirdPersonCameraToWalls(AABBIntersectionTest intersectionTester, double yaw, Vec3d eye, Vec3d target,
            FloatRef curtppcameradistance)
        {
            // This is, for the most part, the original calculation. Here the 'simplified'
            // for improved readability
            float GetIntersectionDistance(AABBIntersectionTest intersectionTester, Vec3d eye, Vec3d target)
            {
                Line3D pick = new();
                Vec3d raydir = eye.Clone().Sub(target); 
                float raydirLength = (float)raydir.Length();
                raydir /= raydirLength;
                raydir *= (Tppcameradistance + 1f);
                pick.Start = target.ToDoubleArray();
                pick.End = target.Clone().Add(raydir).ToDoubleArray();
                intersectionTester.LoadRayAndPos(pick);
                BlockSelection selection = intersectionTester.GetSelectedBlock(TppCameraDistanceMax,
                    (BlockPos pos, Block block) => block.CollisionBoxes != null && block.CollisionBoxes.Length != 0 &&
                                                   block.RenderPass != EnumChunkRenderPass.Transparent &&
                                                   block.RenderPass != EnumChunkRenderPass.Meta);

                if (selection == null) return 999f;
                float pickdistance = 
                    selection.Position.ToVec3f()
                    .Add(selection.HitPosition.ToVec3f()).Length();
                return GameMath.Max(0.3f, pickdistance - 1f);
            }

            float centerDistance = GetIntersectionDistance(intersectionTester, eye, target);
            float leftDistance = GetIntersectionDistance(intersectionTester,
                eye.AheadCopy(0.15000000596046448, 0.0, yaw + 1.5707963705062866),
                target.AheadCopy(0.15000000596046448, 0.0, yaw + 1.5707963705062866));
            float rightDistance = GetIntersectionDistance(intersectionTester,
                eye.AheadCopy(-0.15000000596046448, 0.0, yaw + 1.5707963705062866),
                target.AheadCopy(-0.15000000596046448, 0.0, yaw + 1.5707963705062866));
            float distance = GameMath.Min(centerDistance, leftDistance, rightDistance);
            return !(distance < 0.35);
        }

        if (CameraFunctions.OverrideCamera)
        {
            IClientWorldAccessor cworld = intersectionTester.bsTester as IClientWorldAccessor;
            EntityPlayer playerEntity = cworld?.Player.Entity;
            ClientPlayer clientPlayer = cworld?.Player as ClientPlayer;
            if (clientPlayer == null || playerEntity == null)
            {
                Logger.Error("Could not load player data to override camera matrix -- Stopping manipulation attempt");
                return __result;
            }

            // Specific pixel xray treatment
            double xDiff = Math.Abs(playerEntity.Pos.X - camEyePosOutTmp.X);
            double zDiff = Math.Abs(playerEntity.Pos.Z - camEyePosOutTmp.Z);
            if (xDiff < 0.3 && zDiff < 0.4)
                clientPlayer.OverrideCameraMode = EnumCameraMode.FirstPerson;

            double yawBrute = yaw;
            // Normalizing Yaw
            if (yaw < 0) yaw = 6.28 - (-1 * yaw);
            while (yaw < 0) yaw += 6.28;
            while (yaw >= 6.28) yaw -= 6.28;

            // Load Untouched private method
            MethodInfo lookAt = AccessTools.Method(typeof(Camera), "lookAt");

            // Override private method to reverse the changes made in prefix
            double[] lookatFp(EntityPlayer plr, Vec3d camEyePosIn)
            {
                // if this function is called by LimitThirdPersonCameraToWalls, we need to change the camera mode
                clientPlayer.OverrideCameraMode = EnumCameraMode.FirstPerson;

                #region native
                camEyePosOutTmp = camEyePosIn.Clone().Add(plr.LocalEyePos);
                #endregion
                
                // Get the percentage between the 2 numbers and desired number
                static double GetPercentage(double value, double minValue, double maxValue)
                {
                    if (value <= minValue) return 0.0;
                    if (value >= maxValue) return 100.0;
                    return (value - minValue) / (maxValue - minValue) * 100.0;
                }


                // We need to reverse the changes made in prefix to the camera position -- so we reverse the math

                // South to East
                if (yaw < 1.5)
                {
                    var percentage = GetPercentage(yaw, 0.0, 1.5);
                    camEyePosOutTmp.X += cameraXPosition * (1 - percentage / 100); // percentage 0 = max
                    camEyePosOutTmp.Z -= cameraXPosition * percentage / 100; // percentage 0 = min
                }
                // North to East
                else if (yaw >= 1.5 && yaw <= 3.15)
                {
                    var percentage = GetPercentage(yaw, 1.5, 3.15);
                    camEyePosOutTmp.X -= cameraXPosition * percentage / 100; // percentage 0 = min
                    camEyePosOutTmp.Z -= cameraXPosition * (1 - percentage / 100); // percentage 0 = max 
                }
                // North to West
                else if (yaw > 3.15 && yaw <= 4.75)
                {
                    var percentage = GetPercentage(yaw, 3.15, 4.75);
                    camEyePosOutTmp.X -= cameraXPosition * (1 - percentage / 100); // percentage 0 = max
                    camEyePosOutTmp.Z += cameraXPosition * percentage / 100; // percentage 0 = min
                }
                // South to West
                else
                {
                    var percentage = GetPercentage(yaw, 4.75, 6.28);
                    camEyePosOutTmp.X += cameraXPosition * percentage / 100; // percentage 0 = min
                    camEyePosOutTmp.Z += cameraXPosition * (1 - percentage / 100); // percentage 0 = max 
                }

                #region native
                camTargetTmp = camEyePosOutTmp.Clone().Add(__instance.forwardVec);
                #endregion
                
                return (double[])lookAt.Invoke(__instance, [camEyePosOutTmp, camTargetTmp]);
            }

            camTargetTmp.X = worldPos.X + playerEntity.LocalEyePos.X;
            camTargetTmp.Y = worldPos.Y + playerEntity.LocalEyePos.Y;
            camTargetTmp.Z = worldPos.Z + playerEntity.LocalEyePos.Z;
            camEyePosOutTmp.X = camTargetTmp.X + __instance.forwardVec.X * (double)(0f - Tppcameradistance);
            camEyePosOutTmp.Y = camTargetTmp.Y + __instance.forwardVec.Y * (double)(0f - Tppcameradistance);
            camEyePosOutTmp.Z = camTargetTmp.Z + __instance.forwardVec.Z * (double)(0f - Tppcameradistance);
            if (clientPlayer.OverrideCameraMode == EnumCameraMode.FirstPerson ||
                !LimitThirdPersonCameraToWalls(
                    intersectionTester, yawBrute, camEyePosOutTmp, camTargetTmp, FloatRef.Create(Tppcameradistance))
               )
                return lookatFp(playerEntity, camEyePosIn);
            InputHandlerPatch.SetTppDistance(currentTppcameradistance);
        }
        return __result;
    }
}