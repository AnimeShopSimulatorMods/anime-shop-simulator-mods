using System;
using System.IO;
using AnimeShopMods.Dev.Cheats;
using GameBridge.Net;
using GameBridge.Reflection;
using Il2CppProject.Code.Gameplay.Player.Controllers;
using MelonLoader.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameBridge.Commands
{
    internal static class VisualCommands
    {
        private const float ScreenshotTimeoutSeconds = 5f;

        public static void Register(Dispatcher d)
        {
            d.Register("screenshot", (Request request) => Screenshot(d, request));
            d.Register("teleport", Teleport);
            d.Register("look_at", LookAt);
        }

        // CaptureScreenshot writes the file at the end of a later frame, so the reply waits until the
        // file exists and has stopped growing.
        private static void Screenshot(Dispatcher d, Request request)
        {
            var folder = Path.Combine(MelonEnvironment.UserDataDirectory, "GameBridge", "shots");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            int superSize = Mathf.Clamp(request.Args.Value<int?>("superSize") ?? 1, 1, 4);

            ScreenCapture.CaptureScreenshot(path, superSize);

            float deadline = Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
            long lastSize = -1;
            d.Defer(request, () =>
            {
                long size = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (size > 0 && size == lastSize)
                {
                    request.Reply(new { path, bytes = size, width = Screen.width, height = Screen.height });
                    return true;
                }
                lastSize = size;
                if (Time.realtimeSinceStartup < deadline) return false;

                request.Fail("The screenshot was never written. ScreenCapture may be stripped from this build.");
                return true;
            });
        }

        private static object Teleport(JObject args)
        {
            // Unity overloads == so that a destroyed object compares equal to null, and ?? does not use
            // that overload; every null check on a game object goes through == on purpose.
            var player = TestingCheats.LocalPlayer();
            if (player == null) throw new InvalidOperationException("No local player. Load a save first.");
            var destination = Point(args, standOff: args.Value<float?>("distance") ?? 1.5f);

            // A CharacterController snaps the player back to where it thinks they are unless it is off
            // while the position changes.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            player.position = destination;
            if (wasEnabled) controller.enabled = true;

            return new { player = JsonValues.Vec(player.position) };
        }

        // Where the player looks is the camera controller's own two angles, not the transforms. Writing
        // the transforms looks right for one frame and is then overwritten from those angles, which is
        // what it did before: the view snapped back the moment the game's next LateUpdate ran.
        private static object LookAt(JObject args)
        {
            var player = LocalPlayer();
            var camera = player._cameraController;
            if (camera == null) throw new InvalidOperationException("The player has no camera controller yet.");
            var eye = camera._xTransform != null ? camera._xTransform.position : player.transform.position;
            var target = AimPoint(args);

            var toTarget = target - eye;
            var flat = new Vector2(toTarget.x, toTarget.z);
            if (flat.sqrMagnitude < 0.0001f)
                throw new ArgumentException("The target is directly above or below the player; nothing to turn to.");

            float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg;

            // The game refuses to look further up or down than this, so match it rather than fight it.
            var limits = camera._lookLimits;
            pitch = Mathf.Clamp(pitch, Mathf.Min(limits.x, limits.y), Mathf.Max(limits.x, limits.y));

            camera.ViewAngles = new Vector2(pitch, yaw);

            return new { yaw, pitch, eye = JsonValues.Vec(eye) };
        }

        private static PlayerCharacterController LocalPlayer()
        {
            foreach (var player in UnityEngine.Object.FindObjectsOfType<PlayerCharacterController>())
            {
                if (player != null && player.IsOwner) return player;
            }
            throw new InvalidOperationException("No local player. Load a save first.");
        }

        // What to look at, which is not the same point as where to stand. A shelf's own position sits on
        // the floor, so aiming there points the camera at the boards; the middle of what the shelf
        // actually draws is where a player would look.
        private static Vector3 AimPoint(JObject args)
        {
            if (args["target"] == null) return Point(args, standOff: 0f);

            var component = (Component)JsonValues.Cast(Handles.Get((string)args["target"]), typeof(Component));
            var renderers = component.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return component.transform.position;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        // A point from {x,y,z}, or from a handle to anything with a transform. With a stand-off, the
        // point is moved out to the side a shelf is used from, so the player does not land inside it.
        //
        // That side is the shelf's BACK: measured in game 1.0.6, a shelf's forward points into the wall
        // it stands against, so a positive stand-off drops the player behind the wall.
        private static Vector3 Point(JObject args, float standOff)
        {
            if (args["target"] != null)
            {
                var component = (Component)JsonValues.Cast(Handles.Get((string)args["target"]), typeof(Component));
                var t = component.transform;
                return t.position - t.forward * standOff;
            }
            if (args["x"] == null) throw new ArgumentException("Give target (a handle) or x, y, z.");
            return new Vector3((float)args["x"], (float)args["y"], (float)args["z"]);
        }
    }
}
