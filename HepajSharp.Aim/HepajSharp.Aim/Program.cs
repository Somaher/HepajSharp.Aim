using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HepajSharp.HepajSharp;
using HepajSharp.HepajSharp.Entities;
using HepajSharp.HepajSharp.Utils;
using HepajSharp.HepajSharpKernel.Interfaces;
using Bones = HepajSharp.HepajSharp.Enumerations.Definitions.ECSPlayerBones;
using utils = HepajSharp.Utils.Utils;

namespace HepajSharp.Aim
{
    public class Program
    {
        private static GUIManager.MenuItem menu = new GUIManager.MenuItem();
        private static GUIManager.ToggleMenu active = new GUIManager.ToggleMenu("Activate", true);
        private static GUIManager.HotkeyMenu hotkey = new GUIManager.HotkeyMenu("Key", 0x01);  //VK_LBUTTON
        private static GUIManager.SliderMenu fov = new GUIManager.SliderMenu("FoV", 10, 1, 25);
        private static GUIManager.SliderMenu smooth = new GUIManager.SliderMenu("Smooth", 0.1f, 0.022f, 0.2f);
        private static float currentfov = 90;
        
        public static void Main()
        {
            menu.Name = "Aim Assistance";
            active.SetParent(menu);
            hotkey.SetParent(menu);
            fov.SetParent(menu);
            smooth.SetParent(menu);
            GUIManager.AddToRoot(menu);
            unsafe
            {
                OverrideMouseInput.OnOverrideMouseInput += OverrideMouseInput_OnOverrideMouseInput;
            }
            PaintTraverse.AfterPaintTraverse += PaintTraverse_AfterPaintTraverse;
            OverrideView.AfterOverrideView += OverrideView_AfterOverrideView;   //getting FoV
        }

        private static void OverrideView_AfterOverrideView(IntPtr pViewSetup)
        {
            if (CEngineClient.Instance.IsConnected() && CEngineClient.Instance.IsInGame())
            {
                var local = C_CSPlayer.GetLocalPlayer();
        
                if (!local.IsValid() || !local.IsAlive())
                    return;
        
                var viewSetup = Marshal.PtrToStructure<CViewSetup>(pViewSetup);
                currentfov = viewSetup.fov;
            }
        }

        private static void PaintTraverse_AfterPaintTraverse(IntPtr pPanel, uint vguiPanel)
        {
            if (!active.IsToggled())
                return;

            if (CEngineClient.Instance.IsConnected() && CEngineClient.Instance.IsInGame())
            {
                var local = C_CSPlayer.GetLocalPlayer();

                if (!local.IsValid() || !local.IsAlive())
                    return;

                int width = 0, height = 0;
                CEngineClient.Instance.GetScreenSize(ref width, ref height);
                float radius = (float) (Math.Tan(utils.DEG2RAD(fov.GetValue()) / 2) /
                                        Math.Tan(utils.DEG2RAD(currentfov) / 2) * width);

                Drawing.DrawCircle(new Vector2(width / 2, height / 2), 20, radius, new Color(0, 0, 255, 150));
            }
        }

        private static unsafe void OverrideMouseInput_OnOverrideMouseInput(float* x, float* y)
        {
            if (!active.IsToggled())
                return;

            if (!hotkey.IsToggled())
                return;

            var local = C_CSPlayer.GetLocalPlayer();

            if (!local.IsValid() || !local.IsAlive())
                return;

            var weapon = local.GetActiveWeapon();

            if (!weapon.IsValid() || weapon.IsC4() || weapon.IsKnife() || weapon.IsGrenade())
                return;

            var target = GetClosestPlayer(local);
            if (target.IsValid() && utils.TraceBone(local, target, Bones.HEAD_0))
            {
                var delta = GetDelta(local, target, Bones.HEAD_0);
                delta.X /= smooth.GetValue();
                delta.Y /= smooth.GetValue();
                var finalDelta = new Vector3(delta.Y, -delta.X, 0);
                finalDelta.X = (finalDelta.X + *x) / 2.0f;
                finalDelta.Y = (finalDelta.Y + *y) / 2.0f;

                *x = finalDelta.X;
                *y = finalDelta.Y;
            }
        }

        private static Vector3 GetDelta(C_CSPlayer local, C_CSPlayer target, Bones bone)
        {
            var viewAngles = CEngineClient.Instance.GetViewAngles();
            
            var eyePos = local.GetEyePos();
            var targetBone = utils.GetEntityBone(target, bone);
            var aimAngles = utils.CalcAngle(eyePos, targetBone);
            var delta = viewAngles - aimAngles;
            delta.Clamp();
            return delta;
        }

        private static C_CSPlayer GetClosestPlayer(C_CSPlayer local)
        {
            var maxFov = fov.GetValue();
            C_CSPlayer target = new C_CSPlayer(IntPtr.Zero);

            var viewAngles = CEngineClient.Instance.GetViewAngles();
            var eyePos = local.GetEyePos();
            for (int i = 1; i < CEngineClient.Instance.GetMaxClients(); i++)
            {
                var potentialTarget = new C_CSPlayer(i);
                if (!potentialTarget.IsValid() || potentialTarget.m_BaseAddress == local.m_BaseAddress)
                    continue;

                if (!potentialTarget.IsPlayer() || !potentialTarget.IsAlive() || potentialTarget.IsDormant() || !potentialTarget.IsEnemy())
                    continue;
                
                var targetBone = utils.GetEntityBone(potentialTarget, Bones.HEAD_0);
                var targetFov = GetFov(viewAngles, utils.CalcAngle(eyePos, targetBone));
                if (targetFov > maxFov)
                    continue;

                maxFov = targetFov;
                target = potentialTarget;
            }

            return target;
        }
        

        private static float GetFov(Vector3 viewAngle, Vector3 targetAngle)
        {
            var delta = targetAngle - viewAngle;
            delta.Clamp();
            return (float)Math.Sqrt(Math.Pow(delta.X, 2.0f) + Math.Pow(delta.Y, 2.0f));
        }
    }
}
