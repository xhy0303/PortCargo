#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PortCargo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    /// <summary>
    /// Explicitly invoked development harness. Real UI pointer events exercise gameplay with a
    /// manually completed in-memory ad provider. This file and its fake are absent from release builds.
    /// No Awake/Start hook installs or runs this harness; an operator must call Begin.
    /// </summary>
    public sealed class DevelopmentAdChecks : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string result, failure, startedUtc, finishedUtc;
            public string scope = "Unity UI and reward transaction checks with injected fake providers; not WeChat SDK/device verification";
            public string saveIsolation = "All fixture writes use in-memory repositories; original repository is retained untouched";
            public int pointerClicks, hintAdRequests, skipAdRequests;
            public bool originalStateRestored;
            public List<string> checks = new List<string>();
            public List<string> screenshots = new List<string>();
        }

        private sealed class MemoryStorage : IStoragePort
        {
            private readonly Dictionary<string, string> data = new Dictionary<string, string>();
            public string ReadString(string key) => data.TryGetValue(key, out var value) ? value : null;
            public void WriteString(string key, string value) { data[key] = value; }
        }

        // Only this development-only component can create this fake. Callbacks remain available
        // intentionally, so duplicate and obsolete platform callbacks can be tested faithfully.
        private sealed class FakeProvider : ICancellableRewardedVideoProvider
        {
            private readonly List<Action<RewardedVideoResult>> requests = new List<Action<RewardedVideoResult>>();
            public bool Available = true;
            public bool ThrowOnShow;
            public bool NativeAdOpen;
            public bool IsAvailable => Available;
            public bool CanCancel => !NativeAdOpen;
            public int CancelCount { get; private set; }
            public int Count => requests.Count;
            public void Show(Action<RewardedVideoResult> completed)
            {
                if (ThrowOnShow) throw new InvalidOperationException("Injected ad adapter failure");
                requests.Add(completed);
            }
            public void Complete(int request, RewardedVideoResult result) => requests[request](result);
            public void CompleteLatest(RewardedVideoResult result) => Complete(Count - 1, result);
            public void Cancel()
            {
                CancelCount++;
                if (Count > 0) CompleteLatest(RewardedVideoResult.Cancelled);
            }
        }

        private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Evidence evidence = new Evidence();
        private readonly List<InputDevice> suspendedDevices = new List<InputDevice>();
        private GamePresenter presenter;
        private LevelDefinition[] levels;
        private GameTuning tuning;
        private SaveRepository originalRepository, testRepository;
        private MemoryStorage testStorage;
        private PlayerSave originalSave;
        private IRewardedVideoProvider originalHints, originalSkip;
        private FakeProvider hints, skip;
        private Mouse mouse;
        private InputSystemUIInputModule inputModule;
        private UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private bool oldRunInBackground, inputCaptured, stateCaptured, started;
        private string output;

        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }

        /// <param name="evidencePath">Absolute output prefix; .txt/.json suffixes are optional.</param>
        public void Begin(string evidencePath)
        {
            if (started) throw new InvalidOperationException("Ad checks may only be started once per component");
            if (string.IsNullOrWhiteSpace(evidencePath) || !Path.IsPathRooted(evidencePath))
                throw new ArgumentException("An absolute evidence path is required", nameof(evidencePath));
            started = true;
            output = evidencePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || evidencePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Path.GetDirectoryName(evidencePath), Path.GetFileNameWithoutExtension(evidencePath))
                : evidencePath;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            evidence.startedUtc = DateTime.UtcNow.ToString("O");
            StartCoroutine(Guarded(Run()));
        }

        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object value = null;
                bool moved;
                try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; }
                catch (Exception ex) { Failure = ex.ToString(); break; }
                if (!moved) { stack.Pop(); continue; }
                if (value is IEnumerator child) { stack.Push(child); continue; }
                yield return value;
            }
            try { RestoreState(); }
            catch (Exception ex) { Failure = (Failure ?? "") + "\nRestore failure: " + ex; }
            try { RestoreInput(); }
            catch (Exception ex) { Failure = (Failure ?? "") + "\nInput restore failure: " + ex; }
            evidence.hintAdRequests = hints?.Count ?? 0;
            evidence.skipAdRequests = skip?.Count ?? 0;
            evidence.failure = Failure;
            evidence.finishedUtc = DateTime.UtcNow.ToString("O");
            Passed = string.IsNullOrEmpty(Failure);
            evidence.result = Passed ? "PASS" : "FAIL";
            File.WriteAllText(output + ".json", JsonUtility.ToJson(evidence, true));
            var lines = new List<string> { evidence.scope, evidence.saveIsolation };
            lines.AddRange(evidence.checks);
            if (!Passed) lines.Add("FAIL " + Failure);
            lines.Add("RESULT " + evidence.result + "; pointerClicks=" + evidence.pointerClicks
                + "; hints=" + evidence.hintAdRequests + "; skips=" + evidence.skipAdRequests
                + "; originalStateRestored=" + evidence.originalStateRestored);
            File.WriteAllLines(output + ".txt", lines);
            Finished = true;
            Debug.Log("PORTCARGO_AD_CHECKS " + evidence.result + " " + output + ".json");
        }

        private IEnumerator Run()
        {
            yield return null;
            presenter = FindFirstObjectByType<GamePresenter>();
            Require(presenter && presenter.Session != null, "A running initialized GamePresenter is required");
            Require(!presenter.RewardedRequestActive, "Do not start checks while a real ad request is active");
            CaptureState();
            ConfigureInput();
            hints = new FakeProvider(); skip = new FakeProvider();
            testStorage = new MemoryStorage();
            testRepository = MakeRepository(testStorage);
            var fixture = new PlayerSave {
                profile = new PlayerProfile { tutorialCompleted = true, sound = false, music = false, reducedMotion = true },
                activeRun = new GameSession(levels[0], "ad-checks-" + Guid.NewGuid().ToString("N")).Export(),
                mode = "campaign"
            };
            presenter.Initialize(levels, tuning, testRepository, new LoadResult { Value = fixture });
            presenter.BindRewardedAds(hints, skip);
            yield return Press("开始闯关");
            Require(presenter.Flow.Screen == FlowScreen.Playing && !presenter.Session.Busy, "Campaign UI opens");
            var initialRun = presenter.Session.RunId;
            var initialBoard = presenter.Session.Board.ExactKey();

            yield return Press("提示");
            yield return Until(() => HintCount == 1, "First free hint arrives");
            Require(presenter.Session.FreeHintUsed && hints.Count == 0, "First hint must remain free");
            Check("A01", "First real HUD hint click grants the original free hint without requesting an ad");

            yield return OpenHintConfirmation();
            yield return Capture("hint-confirm");
            yield return Press("继续游戏");
            Require(HintCount == 1 && hints.Count == 0, "Declining confirmation must not request an ad");
            Check("A02", "Declining the optional hint confirmation leaves the board and reward count unchanged");

            for (var i = 0; i < 5; i++)
            {
                var before = HintCount;
                yield return StartHintAd();
                if (i == 0)
                {
                    yield return Capture("hint-pending");
                    var count = hints.Count;
                    var blockedHint = FindButton("提示", false);
                    Require(blockedHint && !blockedHint.interactable, "Underlying hint is disabled while ad is pending");
                    var position = Center((RectTransform)blockedHint.transform);
                    yield return Click(position); yield return Click(position);
                    Require(hints.Count == count && presenter.RewardedRequestActive && HintCount == before,
                        "Double clicking the HUD under the ad overlay must not start a second request");
                    Check("A03", "Two real pointer clicks on the blocked HUD during ad wait do not request or grant twice");
                }
                hints.CompleteLatest(RewardedVideoResult.Completed);
                yield return Until(() => !presenter.RewardedRequestActive, "Hint completion releases wait");
                Require(HintCount == before + 1 && presenter.Flow.Screen == FlowScreen.Playing && !presenter.Session.Busy,
                    "One completed ad grants exactly one hint and resumes the board");
                hints.CompleteLatest(RewardedVideoResult.Completed);
                Require(HintCount == before + 1, "Duplicate hint callback must be ignored");
            }
            Require(presenter.Session.RunId == initialRun && presenter.Session.Board.ExactKey() == initialBoard,
                "Hint ads must not change campaign board or run");
            Check("A04", "Five rewarded hints work in the same run; each duplicate callback is ignored; no one-ad quota remains");

            foreach (var result in new[] { RewardedVideoResult.Cancelled, RewardedVideoResult.Failed, RewardedVideoResult.Unavailable })
            {
                var before = HintCount;
                yield return StartHintAd();
                hints.CompleteLatest(result); yield return null;
                Require(HintCount == before && !presenter.RewardedRequestActive && !presenter.Session.Busy,
                    result + " must not grant a hint or leave the game blocked");
            }
            Check("A05", "Cancelled, failed, and unavailable provider results grant nothing and restore playable UI");

            var countBefore = hints.Count;
            hints.Available = false;
            yield return Press("提示");
            Require(hints.Count == countBefore && presenter.Flow.Screen == FlowScreen.Playing, "Unavailable provider fails before confirmation");
            hints.Available = true;
            yield return OpenHintConfirmation();
            hints.Available = false;
            yield return Press("看广告获取提示");
            Require(hints.Count == countBefore && !presenter.RewardedRequestActive && !presenter.Session.Busy,
                "Provider becoming unavailable after confirmation must return safely");
            hints.Available = true; hints.ThrowOnShow = true;
            var beforeThrow = HintCount;
            yield return OpenHintConfirmation(); yield return Press("看广告获取提示");
            Require(HintCount == beforeThrow && !presenter.RewardedRequestActive && !presenter.Session.Busy,
                "Synchronous platform exception must fail safely");
            hints.ThrowOnShow = false;
            Check("A06", "Unavailable-before/after-confirmation and synchronous adapter exceptions preserve free play");

            yield return StartHintAd();
            var beforeHidden = HintCount;
            presenter.HandleHidden(); yield return null;
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement && presenter.Session.Busy,
                "App background callback preserves the pending ad transaction and blocked board");
            presenter.HandleShown(); yield return null;
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement,
                "Foreground callback must not cancel or prematurely reward an ad");
            hints.CompleteLatest(RewardedVideoResult.Completed); yield return null;
            Require(HintCount == beforeHidden + 1 && !presenter.Session.Busy, "Reward after background/foreground arrives once");
            Check("A07", "Simulated platform hide/show while pending preserves transaction; completed callback resumes play once");

            yield return StartHintAd();
            hints.NativeAdOpen = true;
            var beforeNative = HintCount;
            var cancelsBeforeNative = hints.CancelCount;
            yield return Press("返回游戏");
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement
                && HintCount == beforeNative && hints.CancelCount == cancelsBeforeNative,
                "Game return action must not cancel a native advertisement already opening or playing");
            presenter.Flow.ConfirmQuit();
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement,
                "System quit confirmation must not replace an active advertisement screen");
            hints.CompleteLatest(RewardedVideoResult.Completed); yield return null;
            hints.NativeAdOpen = false;
            Require(HintCount == beforeNative + 1 && !presenter.RewardedRequestActive,
                "A blocked return click must not discard the later native completion reward");
            Check("A07b", "Native-stage fake rejects game return cancellation and system quit overlay; later completion still grants the reward");

            yield return StartHintAd();
            var stale = hints.Count - 1;
            var beforeStale = HintCount;
            var cancelsBeforeReturn = hints.CancelCount;
            yield return Press("返回游戏");
            Require(!presenter.RewardedRequestActive && HintCount == beforeStale && hints.CancelCount == cancelsBeforeReturn + 1,
                "Closing loading UI must call adapter Cancel exactly once and grant no reward");
            yield return StartHintAd();
            hints.Complete(stale, RewardedVideoResult.Completed); yield return null;
            Require(presenter.RewardedRequestActive && HintCount == beforeStale && presenter.Flow.Screen == FlowScreen.Advertisement,
                "Old callback must not resolve the newer request");
            hints.CompleteLatest(RewardedVideoResult.Completed); yield return null;
            Require(HintCount == beforeStale + 1 && !presenter.RewardedRequestActive, "Current callback resolves after old callback is ignored");
            Check("A08", "Close and reopen through real buttons: stale completion cannot grant or close a newer ad request");

            var campaign = JsonUtility.ToJson(presenter.CampaignRun);
            var completed = presenter.Profile.completed.Count;
            var unlocked = presenter.Profile.unlockedCount;
            yield return Press("暂停"); yield return Press("返回首页"); yield return Press("开启无尽");
            Require(presenter.IsEndless && presenter.EndlessRound == 1, "Home starts endless mode");
            yield return OpenSkipConfirmation();
            yield return Capture("skip-confirm");
            var skipRun = presenter.Session.RunId;
            yield return Press("继续本程");
            Require(skip.Count == 0 && presenter.EndlessRound == 1 && presenter.Session.RunId == skipRun,
                "Declining skip must preserve the round");
            foreach (var result in new[] { RewardedVideoResult.Cancelled, RewardedVideoResult.Failed })
            {
                yield return StartSkipAd(); skip.CompleteLatest(result); yield return null;
                Require(presenter.EndlessRound == 1 && presenter.Session.RunId == skipRun && !presenter.RewardedRequestActive,
                    result + " skip must preserve the round and run");
            }
            Check("A09", "Declined, cancelled, and failed endless skips preserve the current round and exact run");

            for (var i = 0; i < 2; i++)
            {
                var round = presenter.EndlessRound;
                var run = presenter.Session.RunId;
                yield return StartSkipAd();
                if (i == 0) yield return Capture("skip-pending");
                skip.CompleteLatest(RewardedVideoResult.Completed); yield return null;
                Require(presenter.EndlessRound == round + 1 && presenter.Session.RunId != run
                    && presenter.Session.Version == 0 && presenter.Flow.Screen == FlowScreen.Playing,
                    "Completed skip creates only the next initial endless board");
                skip.CompleteLatest(RewardedVideoResult.Completed);
                Require(presenter.EndlessRound == round + 1, "Duplicate skip completion must not advance twice");
                Require(JsonUtility.ToJson(presenter.CampaignRun) == campaign && presenter.Profile.completed.Count == completed
                    && presenter.Profile.unlockedCount == unlocked, "Endless skip must not modify campaign progress or unlocks");
            }
            var persisted = testRepository.Load().Value;
            Require(persisted != null && persisted.mode == "endless" && persisted.endless.round == 3
                && JsonUtility.ToJson(persisted.activeRun) == campaign, "In-memory committed save preserves independent modes");
            Check("A10", "Two completed endless skips advance one round each, duplicate callbacks do nothing, and campaign save/unlocks remain identical");

            var skippedRun = JsonUtility.ToJson(presenter.Session.Export());
            var skippedLevel = JsonUtility.ToJson(presenter.Endless.level);
            var skippedHistory = string.Join("/", presenter.Endless.recentLayouts);
            var savedRevision = testRepository.CommittedRevision;
            // Copy the serialized A/B envelope bytes into a distinct storage and repository.
            // This validates reload through production decoding/validation, in this same process.
            var reloadStorage = new MemoryStorage();
            for (var slot = 0; slot < 2; slot++)
            {
                var key = SaveRepository.Key(slot);
                var bytes = testStorage.ReadString(key);
                if (bytes != null) reloadStorage.WriteString(key, bytes);
            }
            testStorage = reloadStorage; testRepository = MakeRepository(testStorage);
            var reloaded = testRepository.Load();
            Require(reloaded.Value != null && !reloaded.Reset && !reloaded.RecoveredOlderSlot
                && reloaded.Revision == savedRevision, "Serialized skip save reload selects the latest valid slot");
            presenter.Initialize(levels, tuning, testRepository, reloaded);
            presenter.BindRewardedAds(hints, skip);
            Require(presenter.Flow.Screen == FlowScreen.Home, "Reload enters the standard home screen");
            yield return Press("继续无尽");
            Require(presenter.IsEndless && presenter.EndlessRound == 3 && JsonUtility.ToJson(presenter.Session.Export()) == skippedRun
                && JsonUtility.ToJson(presenter.Endless.level) == skippedLevel
                && string.Join("/", presenter.Endless.recentLayouts) == skippedHistory
                && JsonUtility.ToJson(presenter.CampaignRun) == campaign,
                "Continue endless after serialized reload must restore exact skipped round/run/board/history and independent campaign");
            yield return Capture("skip-reloaded");
            Check("A12", "Copied serialized save-slot JSON to a fresh repository, reloaded, reinitialized presenter and clicked Continue Endless; exact round 3 board/run/history and campaign restored (same process)");

            hints.Available = false; skip.Available = false;
            yield return Press("暂停");
            Require(!FindButton("看广告跳过本程"), "Unavailable skip provider must not advertise a working action");
            yield return Press("返回首页"); yield return Press("开始闯关");
            Require(JsonUtility.ToJson(presenter.Session.Export()) == campaign, "Return to campaign restores the exact original campaign run");
            var originalKey = presenter.Session.Board.ExactKey();
            yield return OneMove(); yield return Press("撤销");
            Require(presenter.Session.Board.ExactKey() == originalKey && presenter.Session.FreeHintUsed,
                "Free undo restores board without restoring consumed free hint");
            yield return Press("暂停"); yield return Press("重新开始"); yield return Press("重新开始");
            Require(!presenter.Session.FreeHintUsed && presenter.Session.Board.ExactKey() == originalKey, "Free restart remains available");
            countBefore = hints.Count;
            yield return Press("提示"); yield return Until(() => HintCount == 1, "Restarted free hint works without any ad availability");
            Require(hints.Count == countBefore, "Free hint after restart must not request an unavailable ad");
            yield return Capture("free-play");
            Check("A11", "With both ad providers unavailable, campaign return, real box move, free undo, free restart and first free hint all work");
            hints.Available = true; skip.Available = true;
            yield return DeadlockEntry();
        }

        private int HintCount => presenter.Session.Export().hintCount;

        private IEnumerator OpenHintConfirmation()
        {
            yield return Press("提示");
            yield return Until(() => presenter.Flow.Screen == FlowScreen.Confirm, "Hint confirmation appears");
            Require(FindButton("看广告获取提示"), "Optional hint confirmation contains a watch action");
        }

        private IEnumerator StartHintAd()
        {
            yield return OpenHintConfirmation(); yield return Press("看广告获取提示");
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement,
                "Watch hint button opens a pending ad transaction");
        }

        private IEnumerator OpenSkipConfirmation()
        {
            if (presenter.Flow.Screen == FlowScreen.Playing) yield return Press("暂停");
            yield return Press("看广告跳过本程");
            Require(presenter.Flow.Screen == FlowScreen.Confirm && FindButton("继续本程"), "Skip asks for player confirmation");
        }

        private IEnumerator StartSkipAd()
        {
            yield return OpenSkipConfirmation(); yield return Press("看广告跳过本程");
            Require(presenter.RewardedRequestActive && presenter.Flow.Screen == FlowScreen.Advertisement,
                "Confirmed skip opens a pending ad transaction");
        }

        private IEnumerator OneMove()
        {
            var session = presenter.Session;
            using (var solver = new Solver(session.Level, session.Board, session.Capacity, session.Version, new SearchBudget(200000, 3000)))
            {
                while (solver.Step(100) == SearchStatus.Searching) yield return null;
                Require(solver.Status == SearchStatus.Found && solver.Solution.Length > 0, "Fixture has a legal solvable move");
                BoxView box = null;
                foreach (var candidate in presenter.BoardView.Boxes)
                    if (candidate.gameObject.activeInHierarchy && candidate.BoxId == solver.Solution[0]) { box = candidate; break; }
                Require(box, "Fixture box exists in rendered board");
                var version = session.Version;
                yield return Click(Center(box.Rect));
                Require(session.Version == version + 1, "Real board pointer accepted exactly one move");
            }
        }

        private IEnumerator DeadlockEntry()
        {
            var picks = new List<string>();
            GeneratedLevel generated = null;
            LevelDefinition definition = null;
            var seed = 1701;
            for (; seed < 1717; seed++)
            {
                var candidate = EndlessGenerator.Generate(seed, 3);
                var candidateDefinition = new LevelDefinition(candidate.Data);
                picks.Clear();
                if (FindDeadlock(candidateDefinition, GameRules.Initial(candidateDefinition), picks, new HashSet<string>()))
                { generated = candidate; definition = candidateDefinition; break; }
                yield return null;
            }
            Require(generated != null && picks.Count > 0, "Find a deterministic generated endless fixture with a reachable deadlock");
            var campaign = JsonUtility.ToJson(presenter.CampaignRun);
            var fixture = new PlayerSave {
                profile = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(presenter.Profile)),
                activeRun = JsonUtility.FromJson<RunData>(campaign), mode = "endless",
                endless = new EndlessProgress {
                    generatorVersion = generated.GeneratorVersion, seed = generated.Seed, round = 3,
                    level = generated.Data,
                    run = new GameSession(definition, "ad-deadlock-" + Guid.NewGuid().ToString("N")).Export(),
                    recentLayouts = new List<string> { EndlessGenerator.LayoutSignature(generated.Data) }
                }
            };
            presenter.Initialize(levels, tuning, testRepository, new LoadResult { Value = fixture });
            presenter.BindRewardedAds(hints, skip);
            yield return Press("继续无尽");
            foreach (var pick in picks)
            {
                BoxView box = null;
                foreach (var candidate in presenter.BoardView.Boxes)
                    if (candidate.gameObject.activeInHierarchy && candidate.BoxId == pick) { box = candidate; break; }
                Require(box, "Generated deadlock path box is visible: " + pick);
                var version = presenter.Session.Version;
                yield return Click(Center(box.Rect));
                Require(presenter.Session.Version == version + 1, "Real pointer must advance each generated deadlock path move");
            }
            Require(presenter.Session.Status == BoardStatus.Deadlocked && presenter.Flow.Screen == FlowScreen.Deadlocked
                && FindButton("看广告跳过本程") && FindButton("撤销") && FindButton("重新开始"),
                "Deadlock dialog offers optional ad skip alongside original free undo and restart");
            yield return Capture("endless-deadlock");
            var deadlockedRun = JsonUtility.ToJson(presenter.Session.Export());
            var requests = skip.Count;
            yield return OpenSkipConfirmation(); yield return Press("继续本程");
            Require(presenter.Flow.Screen == FlowScreen.Deadlocked && JsonUtility.ToJson(presenter.Session.Export()) == deadlockedRun
                && skip.Count == requests, "Declining deadlock skip returns to unchanged deadlock with no ad request");
            yield return StartSkipAd(); skip.CompleteLatest(RewardedVideoResult.Cancelled); yield return null;
            Require(presenter.Flow.Screen == FlowScreen.Deadlocked && JsonUtility.ToJson(presenter.Session.Export()) == deadlockedRun,
                "Cancelled deadlock ad preserves the same board and free recovery options");
            yield return StartSkipAd(); skip.CompleteLatest(RewardedVideoResult.Completed); yield return null;
            Require(presenter.EndlessRound == 4 && presenter.Flow.Screen == FlowScreen.Playing && !presenter.Session.Busy
                && presenter.Session.Version == 0 && JsonUtility.ToJson(presenter.CampaignRun) == campaign,
                "Completed ad from deadlock creates round 4 without changing campaign");
            Check("A13", "Generated endless seed=" + seed + " reaches deadlock via " + picks.Count
                + " real box clicks; optional skip decline/cancel preserve board, complete advances one round, and free undo/restart remain available");
        }

        private static bool FindDeadlock(LevelDefinition level, BoardSnapshot board, List<string> path, HashSet<string> visited)
        {
            if (GameRules.Status(level, board, 3) == BoardStatus.Deadlocked) return true;
            if (visited.Count >= 20000 || !visited.Add(board.ExactKey())) return false;
            for (var stack = 0; stack < level.Stacks.Count; stack++)
            {
                var top = GameRules.Top(level, board, stack);
                if (top == null) continue;
                var move = GameRules.Apply(level, board, 3, top);
                if (!move.Accepted) continue;
                path.Add(top);
                if (FindDeadlock(level, move.Board, path, visited)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        private IEnumerator Until(Func<bool> condition, string message)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (!condition()) { Require(Time.realtimeSinceStartup < deadline, "Timed out: " + message); yield return null; }
        }

        private IEnumerator Press(string label)
        {
            Canvas.ForceUpdateCanvases();
            var button = FindButton(label);
            Require(button && button.interactable, "Missing enabled visible button: " + label);
            yield return Click(Center((RectTransform)button.transform));
        }

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, true));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            evidence.pointerClicks++;
        }

        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static Button FindButton(string label, bool onlyTopLayer = true)
        {
            var dialog = FindFirstObjectByType<DialogView>();
            var panel = dialog ? dialog.transform.Find("Dialog") : null;
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (onlyTopLayer && dialog && dialog.Visible && panel && !button.transform.IsChildOf(panel)) continue;
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text && text.text.Replace("\n", "  ") == label) return button;
            }
            return null;
        }

        private IEnumerator Capture(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var path = output + "." + name + ".png";
            var dialog = FindFirstObjectByType<DialogView>();
            var panel = dialog ? dialog.transform.Find("Dialog") : null;
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (dialog && dialog.Visible && panel && !text.transform.IsChildOf(panel)) continue;
                foreach (var character in text.text)
                    Require(char.IsWhiteSpace(character) || text.font.HasCharacter(character), "Missing visible glyph: " + character);
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, "Visible text overflows: " + text.name + " " + text.text);
            }
            ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path), "Screenshot saved: " + name);
            evidence.screenshots.Add(path);
        }

        private void CaptureState()
        {
            levels = ReadField<LevelDefinition[]>("levels"); tuning = ReadField<GameTuning>("tuning");
            originalRepository = ReadField<SaveRepository>("saves");
            originalHints = ReadField<IRewardedVideoProvider>("rewardedHints");
            originalSkip = ReadField<IRewardedVideoProvider>("rewardedSkip");
            var snapshot = new PlayerSave { profile = presenter.Profile, activeRun = presenter.CampaignRun,
                mode = presenter.IsEndless ? "endless" : "campaign", endless = presenter.Endless };
            originalSave = JsonUtility.FromJson<PlayerSave>(JsonUtility.ToJson(snapshot));
            if (presenter.IsEndless) originalSave.endless.run = presenter.Session.Export();
            stateCaptured = true;
        }

        private T ReadField<T>(string name) => (T)typeof(GamePresenter).GetField(name, PrivateFields).GetValue(presenter);
        private SaveRepository MakeRepository(MemoryStorage storage = null) => new SaveRepository(storage ?? new MemoryStorage(), new PortCargo.Platform.UnityJsonCodec(),
            save => ProgressValidator.Validate(save, levels));

        private void RestoreState()
        {
            if (!stateCaptured || !presenter) return;
            presenter.CancelRewardedRequest();
            // Initialize itself saves. Keep a disposable in-memory repository installed until
            // all restoration/UI initialization has completed; never write test data to disk.
            presenter.Initialize(levels, tuning, MakeRepository(), new LoadResult { Value = originalSave });
            presenter.BindRewardedAds(originalHints, originalSkip);
            typeof(GamePresenter).GetField("saves", PrivateFields).SetValue(presenter, originalRepository);
            presenter.HandleShown();
            evidence.originalStateRestored = true;
            stateCaptured = false;
        }

        private void ConfigureInput()
        {
            inputModule = FindFirstObjectByType<InputSystemUIInputModule>();
            Require(inputModule && inputModule.actionsAsset, "A configured InputSystemUIInputModule is required");
            oldDevices = inputModule.actionsAsset.devices;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldRunInBackground = Application.runInBackground;
            inputCaptured = true;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices)
                if (device.enabled && device is UnityEngine.InputSystem.Pointer) { suspendedDevices.Add(device); InputSystem.DisableDevice(device); }
            mouse = InputSystem.AddDevice<Mouse>("PortCargoAdCheckMouse"); mouse.MakeCurrent();
            inputModule.actionsAsset.devices = new InputDevice[] { mouse };
        }

        private void RestoreInput()
        {
            if (!inputCaptured) return;
            if (inputModule && inputModule.actionsAsset) inputModule.actionsAsset.devices = oldDevices;
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            foreach (var device in suspendedDevices) if (device.added) InputSystem.EnableDevice(device);
            suspendedDevices.Clear();
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            Application.runInBackground = oldRunInBackground;
            inputCaptured = false;
        }

        private void Check(string id, string detail) => evidence.checks.Add(id + " PASS " + detail);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void OnDestroy()
        {
            if (!started || Finished) return;
            try { RestoreState(); } finally { RestoreInput(); }
        }
    }
}
#endif
