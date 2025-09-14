using System;
using HarmonyLib;
using TrueThirdPerson;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace TrueThirdPerson;

[HarmonyPatchCategory("truethirdperson_aimrender")]
public class AimRenderPatch
{
	public static Vec3i currentAimPosition = Vec3i.Zero;
	private static int _aimwidth = 32;
	private static int _aimheight = 32;
	private Harmony _harmony;
	
	public void Patch()
	{
		_harmony = TrueThirdPerson.NewPatch("Aim Renderer", "truethirdperson_aimrender");
	}
	
	public void Unpatch()
	{
		_harmony.UnpatchAll();
	}
	
	[HarmonyPrefix]
	[HarmonyPatch(typeof(SystemRenderPlayerAimAcc), "OnRenderFrame2DOverlay")]
	public static bool OnRenderFrame2DOverlay(ref ClientMain ___game, ref MeshRef[] ___aimLinesRef, ref MeshRef ___aimRectangleRef, float dt)
	{
		if (!CameraFunctions.OverrideCamera) return true;
		
		var game = ___game;
		var aimRectangleRef = ___aimRectangleRef;
		var aimLinesRef = ___aimLinesRef;
		
		// Code based on source implementation, we just need to change the position of the elements tho.
		// Could've injected the change but at least this way we may differ in the aiming animation instead of crashing
	    if (!game.MouseGrabbed || game.EntityPlayer.Attributes.GetInt("aiming", 0) == 0)
	      return false;
		game.guiShaderProg.RgbaIn = new Vec4f(1f, 1f, 1f, 1f);
		game.guiShaderProg.ExtraGlow = 0;
		game.guiShaderProg.ApplyColor = 1;
		game.guiShaderProg.Tex2d2D = 0;
		game.guiShaderProg.NoTexture = 1f;
		ScreenManager.Platform.CheckGlError();
		game.Platform.GLLineWidth(0.5f);
		game.Platform.SmoothLines(true);
		ScreenManager.Platform.CheckGlError();
		game.Platform.GlToggleBlend(true);
		game.Platform.BindTexture2d(0);
		ScreenManager.Platform.CheckGlError();
		game.GlPushMatrix();
		game.GlTranslate(currentAimPosition.X, currentAimPosition.Y, 50.0); // Changed from source implementation
		float aimingAccuracy = Math.Max(0.01f, 1f - game.EntityPlayer.Attributes.GetFloat("aimingAccuracy", 0.0f));
		float num2 = 800f * aimingAccuracy;
		game.GlScale((double) num2, (double) num2, 0.0);
		game.guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;
		game.Platform.RenderMesh(aimRectangleRef);
		game.GlPopMatrix();
		game.Platform.GLLineWidth(1f);
		game.GlPushMatrix();
		game.GlTranslate(currentAimPosition.X, currentAimPosition.Y, 50.0); // Changed from source implementation
		game.GlScale(20.0, 20.0, 0.0);
		game.GlTranslate(0.0, -10.0 * (double) aimingAccuracy + 0.5, 0.0);
		game.guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;
		game.Platform.RenderMesh(aimLinesRef[0]);
		game.GlTranslate(0.0, 20.0 * (double) aimingAccuracy - 1.0, 0.0);
		game.guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;
		game.Platform.RenderMesh(aimLinesRef[1]);
		game.GlTranslate(-10.0 * (double) aimingAccuracy + 0.5, -10.0 * (double) aimingAccuracy + 0.5, 0.0);
		game.guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;
		game.Platform.RenderMesh(aimLinesRef[2]);
		game.GlTranslate(20.0 * (double) aimingAccuracy - 1.0, 0.0, 0.0);
		game.guiShaderProg.ModelViewMatrix = game.CurrentModelViewMatrix;
		game.Platform.RenderMesh(aimLinesRef[3]);
		game.GlPopMatrix();
		return false;
	}

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SystemRenderAim), "DrawAim")]
    public static bool DrawAim(ref int ___aimTextureId, ref int ___aimHostileTextureId, ref ClientMain game)
    { 
	    if (game.Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;
        if (!CameraFunctions.OverrideCamera) return true;
		
        BlockSelection blockSelection = game.EntityPlayer.BlockSelection;
        EntitySelection entitySelection = game.EntityPlayer.EntitySelection;

		ItemStack heldStack = game.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
		float weaponAttackRange = heldStack?.Collectible.GetAttackRange(heldStack) ?? GlobalConstants.DefaultAttackRange;
		int texId = ___aimTextureId;
		Vec3d newHitScreenPos;

		if (blockSelection == null && entitySelection?.Entity == null)
		{
			// Similar approach/code used inside the implementing function
			var player = game.EntityPlayer;
			var world = game.api.World;
			BlockFilter bfilter = delegate(BlockPos pos, Block block)
			{
				if (block is not { RenderPass: EnumChunkRenderPass.Meta }) return true;
				IMetaBlock blockMeta = block.GetInterface<IMetaBlock>(world, pos);
				return blockMeta != null && blockMeta.IsSelectable(pos) && block.CollisionBoxes != null;
			};
			EntityFilter efilter = (Entity e) => e.IsInteractable && e.EntityId != player.EntityId;
			Vec3d fromPos = game.player.Entity.Pos.XYZ.Add(game.player.Entity.LocalEyePos);
			game.RayTraceForSelection(fromPos, game.player.Entity.SidedPos.Pitch, game.player.Entity.SidedPos.Yaw, 100f, ref blockSelection, ref entitySelection, bfilter, efilter);
		}

        if(blockSelection != null)
        {
            newHitScreenPos = PosToScreen(game.api, Vec3d.Add(blockSelection.Position.ToVec3d(), blockSelection.HitPosition));
            DrawReticleSmooth(game, newHitScreenPos, texId);
        }
		else  if (entitySelection?.Entity != null)
		{
			Entity entity = entitySelection.Entity;
			Cuboidd cuboidd = entity.SelectionBox.ToDouble().Translate(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
			EntityPos sidedPos = game.EntityPlayer.SidedPos;
			Vec3d reach = new Vec3d(
				sidedPos.X + game.EntityPlayer.LocalEyePos.X,
				sidedPos.Y + game.EntityPlayer.LocalEyePos.Y,
				sidedPos.Z + game.EntityPlayer.LocalEyePos.Z
			);
			if (cuboidd.ShortestDistanceFrom(reach) <= (double) weaponAttackRange - 0.08)
				texId = ___aimHostileTextureId; 
			newHitScreenPos = PosToScreen(game.api, entitySelection.Position.Add(entitySelection.HitPosition));
			DrawReticleSmooth(game, newHitScreenPos, texId);	
		}
        return false;
    }

    private static void DrawReticleSmooth(ClientMain game, Vec3d position, int texId)
    {
	    if (currentAimPosition == Vec3i.Zero || currentAimPosition.X <= 0 || currentAimPosition.Y <= 0)
	    {
		    currentAimPosition.X = (int)Double.Round(position.X);
		    currentAimPosition.Y = (int)Double.Round(position.Y);
	    }
	    else
	    {
		    currentAimPosition.X = (int)Double.Round(Double.Lerp(currentAimPosition.X, position.X, 0.3));
		    currentAimPosition.Y = (int)Double.Round(Double.Lerp(currentAimPosition.Y, position.Y, 0.3));
	    }
		
	    game.Render2DTexture(texId, (float)currentAimPosition.X - (_aimwidth / 2), (float)currentAimPosition.Y - (_aimheight / 2), _aimwidth, _aimheight, 10000f);
    }

    public static Vec3d PosToScreen(ICoreClientAPI capi, Vec3d pos)
    {
        IRenderAPI rpi = capi.Render;
        pos = MatrixToolsd.Project(pos, rpi.PerspectiveProjectionMat, rpi.PerspectiveViewMat, rpi.FrameWidth, rpi.FrameHeight);
        pos.Y = rpi.FrameHeight - pos.Y;
        return pos;
    }
}