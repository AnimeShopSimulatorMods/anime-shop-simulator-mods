using System;
using System.IO;
using AnimeShopMods.Dev.Cheats;
using GameBridge.Net;
using GameBridge.Reflection;
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
            var player = TestingCheats.LocalPlayer() ?? throw new InvalidOperationException("No local player.");
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

        private static object LookAt(JObject args)
        {
            var player = TestingCheats.LocalPlayer() ?? throw new InvalidOperationException("No local player.");
            var camera = Camera.main ?? throw new InvalidOperationException("No main camera.");
            var target = Point(args, standOff: 0f);

            var flat = target - player.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) player.rotation = Quaternion.LookRotation(flat);

            var fromEye = target - camera.transform.position;
            float pitch = -Mathf.Atan2(fromEye.y, new Vector2(fromEye.x, fromEye.z).magnitude) * Mathf.Rad2Deg;
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            return new { yaw = player.rotation.eulerAngles.y, pitch, camera = JsonValues.Vec(camera.transform.position) };
        }

        // A point from {x,y,z}, or from a handle to anything with a transform. With a stand-off, the
        // point is moved out in front of the object so the player does not land inside a shelf.
        private static Vector3 Point(JObject args, float standOff)
        {
            if (args["target"] != null)
            {
                var component = (Component)JsonValues.Cast(Handles.Get((string)args["target"]), typeof(Component));
                var t = component.transform;
                return t.position + t.forward * standOff;
            }
            if (args["x"] == null) throw new ArgumentException("Give target (a handle) or x, y, z.");
            return new Vector3((float)args["x"], (float)args["y"], (float)args["z"]);
        }
    }
}
