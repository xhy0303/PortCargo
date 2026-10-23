using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PortCargo.Core;
using PortCargo.Platform;
using PortCargo.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Run through the live Editor's run_script command. This file deliberately lives
// outside Assets and does not add a component, production asset, or saved scene.
public static class VisualProbe
{
    public static async Task<object> Run(string label)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("VisualProbe requires Play Mode.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(label ?? "", "^[A-Za-z0-9_-]+$"))
            throw new ArgumentException("Use a simple evidence label.", nameof(label));
        var presenter = UnityEngine.Object.FindAnyObjectByType<GamePresenter>();
        if (!presenter || presenter.Session == null) throw new InvalidOperationException("Bootstrap is not ready.");

        var output = Path.GetFullPath("Docs/Evidence/urp-20261023");
        Directory.CreateDirectory(output);
        var saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--portcargo-save-dir") saveDirectory = args[i + 1];
        var savesBefore = SaveHashes(saveDirectory);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var firstFrame = Time.frameCount;
        var views = new List<object>();
        var errors = new List<string>();
        var interactions = new List<object>();
        var suspended = new List<InputDevice>();
        var module = UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>();
        if (!module || !module.actionsAsset || !EventSystem.current)
            throw new InvalidOperationException("Production UI input module is missing.");
        var oldDevices = module.actionsAsset.devices;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldRunInBackground = Application.runInBackground;
        Mouse mouse = null;
        string failure = null;
        var isolated = false;
        Application.LogCallback observe = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(type + ": " + message);
        };
        Application.logMessageReceived += observe;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(18)))
        {
            var token = timeout.Token;
            try
            {
                // Reinitialize against memory BEFORE any navigation, move, or fixture changes.
                // Do not reattach the physical repository afterward: stopping Play Mode also saves.
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var levels = (LevelDefinition[])typeof(GamePresenter).GetField("levels", flags).GetValue(presenter);
                var tuning = (GameTuning)typeof(GamePresenter).GetField("tuning", flags).GetValue(presenter);
                var repository = new SaveRepository(new MemoryStoragePort(), new UnityJsonCodec(),
                    value => ProgressValidator.Validate(value, levels));
                presenter.Initialize(levels, tuning, repository, new LoadResult {
                    Value = new PlayerSave {
                        profile = new PlayerProfile {
                            unlockedCount = levels.Length, tutorialCompleted = true,
                            sound = false, music = false, reducedMotion = false
                        },
                        mode = "campaign"
                    }
                });
                isolated = true;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                foreach (var device in InputSystem.devices.ToArray())
                    if (device.enabled && device is UnityEngine.InputSystem.Pointer)
                    {
                        suspended.Add(device);
                        InputSystem.DisableDevice(device);
                    }
                mouse = InputSystem.AddDevice<Mouse>("PortCargoUrpVisualProbeMouse");
                mouse.MakeCurrent();
                module.actionsAsset.devices = new InputDevice[] { mouse };
                await Frames(3, token);
                Require(presenter.Flow.Screen == FlowScreen.Home && presenter.Flow.HomeVisible, "Home did not open.");
                views.Add(await Capture(label, "home", output, presenter, token));

                Require(presenter.SelectCampaign(Math.Min(199, levels.Length - 1)), "L200 fixture selection failed.");
                presenter.Flow.ShowBoardState();
                await Frames(3, token);
                Require(presenter.Flow.Screen == FlowScreen.Playing && !presenter.Session.Busy, "Board is not playable.");
                views.Add(await Capture(label, "L200", output, presenter, token));

                var session = presenter.Session;
                var originalBoard = session.Board.ExactKey();
                var version = session.Version;
                var topId = GameRules.Top(session.Level, session.Board, 0);
                var target = presenter.BoardView.Boxes.FirstOrDefault(b => b.gameObject.activeInHierarchy && b.BoxId == topId);
                Require(target && target.IsTop, "Visible top cargo missing.");
                var position = Center(target.Rect);
                var hits = Raycast(position);
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<BoxView>() == target,
                    "Cargo center is obstructed by " + (hits.Count > 0 ? hits[0].gameObject.name : "no raycast hit") + ".");
                await Click(mouse, position, token);
                Require(session.Version == version + 1, "Pointer click did not commit exactly one move; version " + version + " -> " + session.Version + ".");
                Require(session.Board.ExactKey() != originalBoard, "Click did not change the board.");
                interactions.Add(new { kind = "cargo", id = topId, x = position.x, y = position.y,
                    raycast = hits[0].gameObject.name, beforeVersion = version, afterVersion = session.Version, passed = true });

                var undo = FindButton("撤销");
                Require(undo && undo.interactable, "Undo button unavailable after move.");
                var undoPosition = Center((RectTransform)undo.transform);
                var undoHits = Raycast(undoPosition);
                Require(undoHits.Count > 0 && undoHits[0].gameObject.GetComponentInParent<Button>() == undo, "Undo button center is obstructed.");
                await Click(mouse, undoPosition, token);
                Require(session.Version == version + 2 && session.Board.ExactKey() == originalBoard,
                    "Real UI undo did not restore the exact board.");
                Require(!presenter.SavePending, "In-memory save failed.");
                interactions.Add(new { kind = "undo", x = undoPosition.x, y = undoPosition.y,
                    raycast = undoHits[0].gameObject.name, afterVersion = session.Version, exactBoardRestored = true, passed = true });
                await Frames(2, token);
                Require(Time.frameCount > firstFrame, "No game frames advanced.");
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
            }
            finally
            {
                if (presenter && presenter.BoardView) presenter.BoardView.ResetPointer();
                if (module && module.actionsAsset) module.actionsAsset.devices = oldDevices;
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                foreach (var device in suspended) if (device.added) InputSystem.EnableDevice(device);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                Application.runInBackground = oldRunInBackground;
                Application.logMessageReceived -= observe;
            }
        }
        var savesAfter = SaveHashes(saveDirectory);
        var savesUnchanged = savesBefore.Count == savesAfter.Count && savesBefore.All(pair => savesAfter.TryGetValue(pair.Key, out var value) && value == pair.Value);
        var result = new {
            label, passed = failure == null && errors.Count == 0 && savesUnchanged && isolated,
            failure, errors, unityVersion = Application.unityVersion,
            pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in",
            firstFrame, lastFrame = Time.frameCount, elapsedMilliseconds = timer.ElapsedMilliseconds,
            screen = new { Screen.width, Screen.height }, views, interactions,
            storage = new { isolatedInMemory = isolated, physicalDirectory = saveDirectory,
                hashesBefore = savesBefore, hashesAfter = savesAfter, unchanged = savesUnchanged,
                note = "Physical save hashes cover this probe only; ordinary Bootstrap boot occurred before the probe. Presenter remains in-memory until Play Mode stops." }
        };
        File.WriteAllText(Path.Combine(output, label + "-visual.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }

    private static async Task<object> Capture(string label, string screen, string output, GamePresenter presenter, CancellationToken token)
    {
        Canvas.ForceUpdateCanvases();
        presenter.Render();
        await UnityEngine.Awaitable.EndOfFrameAsync(token);
        var graphics = UnityEngine.Object.FindObjectsByType<Graphic>()
            .Where(g => g.isActiveAndEnabled).ToArray();
        var materials = graphics.Select(g => g.materialForRendering).Where(m => m).Distinct().Select(m => new {
            material = m.name, shader = m.shader ? m.shader.name : "MISSING",
            supported = m.shader && m.shader.isSupported,
            errorShader = m.shader && m.shader.name.Contains("InternalErrorShader")
        }).ToArray();
        Require(materials.All(m => m.supported && !m.errorShader), screen + " has an unsupported/error UI shader.");
        var missing = new HashSet<string>();
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>())
            if (text.isActiveAndEnabled && text.font)
                foreach (var character in text.text)
                    if (!char.IsWhiteSpace(character) && !text.font.HasCharacter(character)) missing.Add(character.ToString());
        Require(missing.Count == 0, screen + " missing glyphs: " + string.Join(",", missing));
        Texture2D texture = null;
        var path = Path.Combine(output, label + "-" + screen + ".png");
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            Require(texture && texture.width == Screen.width && texture.height == Screen.height, "Screenshot dimensions differ from Game View.");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { if (texture) UnityEngine.Object.Destroy(texture); }
        return new { screen, flow = presenter.Flow.Screen.ToString(), frame = Time.frameCount,
            width = Screen.width, height = Screen.height, path, screenshotSha256 = FileHash(path),
            activeGraphics = graphics.Length, materials, missingGlyphs = missing.ToArray() };
    }

    private static async Task Frames(int count, CancellationToken token)
    {
        for (var i = 0; i < count; i++) await UnityEngine.Awaitable.NextFrameAsync(token);
    }

    private static async Task Click(Mouse mouse, Vector2 point, CancellationToken token)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
        await Frames(2, token);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
        await Frames(2, token);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
        await Frames(3, token);
    }

    private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    private static List<RaycastResult> Raycast(Vector2 point)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits;
    }
    private static Button FindButton(string label) => UnityEngine.Object.FindObjectsByType<Button>()
        .FirstOrDefault(button => button.isActiveAndEnabled && button.GetComponentInChildren<TMP_Text>()?.text == label);
    private static SortedDictionary<string, string> SaveHashes(string directory)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(directory))
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                result[Path.GetRelativePath(directory, file)] = FileHash(file);
        return result;
    }
    private static string FileHash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
