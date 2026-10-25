using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WingSinger.Editor {
    // Runs in the existing editor and restores Edit mode without changing production settings.
    [InitializeOnLoad]
    public static class PlayerPresentationValidation {
        const string Request = "Temp/WingSingerPlayerValidate.request";
        const string Key = "WingSingerPlayerValidation";
        const string Report = "Logs/PlayerPresentationValidation.txt";
        static BreathLevelManager manager;
        static ExternalBreathInput input;
        static int stage;
        static double started;
        static float startX, previewRootX, previewCameraX;

        static PlayerPresentationValidation() {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false)) {
                    Application.runInBackground = SessionState.GetBool(Key + "Background", false);
                    SessionState.SetBool(Key, false);
                }
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) {
                    SessionState.SetBool(Key + "Background", Application.runInBackground);
                    Application.runInBackground = true;
                    stage = 0; manager = null; started = EditorApplication.timeSinceStartup;
                }
            };
        }

        [MenuItem("WingSinger/Validate Player Presentation")]
        public static void Begin() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != BreathDemoBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved BreathDemo scene outside Play mode first.");
            var animator = UnityEngine.Object.FindObjectOfType<BreathCharacterAnimator>();
            animator.cruiseToDescend = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>("Assets/Art/Character/04_Cruise_to_Descend.asset");
            animator.cruiseToRise = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>("Assets/Art/Character/05_Cruise_to_Rise.asset");
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, "Player presentation verification\n");
            SessionState.SetBool(Key, true);
            stage = 0; manager = null; started = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }

        static void Check(bool ok, string message) {
            if (!ok) throw new Exception(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }

        static void CheckAnimation(BreathCharacterAnimator animator, float direction, CharacterSpriteClip moving, CharacterSpriteClip transition) {
            animator.ResetAnimation();
            animator.Present(0.05f, direction);
            Check(animator.CurrentClip == moving, "Actual movement selects " + moving.name);
            float step = animator.returnToCruiseSeconds / 4;
            animator.Present(step, 0);
            Check(animator.IsReturning && animator.CurrentClip == transition
                && animator.target.sprite == transition.Sample(transition.Duration * 0.75, false), "Arrival reverses " + transition.name);
            animator.Present(animator.returnToCruiseSeconds, 0);
            Check(!animator.IsReturning && animator.CurrentClip == animator.cruise, "Return finishes in cruise");
            animator.Present(0.5f, 0);
            Check(animator.CurrentClip == animator.cruise, "Lane dwell stays in cruise");
            animator.Present(0.05f, direction);
            animator.Present(step, 0);
            animator.Present(0.01f, -direction);
            Check(!animator.IsReturning && animator.CurrentClip == (direction > 0 ? animator.descend : animator.rise), "New opposite movement interrupts return");
        }

        static float SamplePlayerAt(double elapsed) {
            manager.Session.Advance(elapsed - manager.Session.Elapsed, BreathLane.Middle);
            manager.player.Present(manager.Session, manager.CurrentLevel.settings);
            return manager.player.body.position.x;
        }

        static void CheckContinuousFlight() {
            manager.Restart();
            var player = manager.player;
            var settings = manager.CurrentLevel.settings;
            double entry = settings.playerEntranceSeconds;
            const double step = 0.002;
            float beforeEntry = SamplePlayerAt(entry - step);
            float atEntry = SamplePlayerAt(entry);
            float afterEntry = SamplePlayerAt(entry + step);
            Check(Math.Abs((atEntry - beforeEntry) / step - settings.moveSpeed) < 0.1
                && Math.Abs((afterEntry - atEntry) / step - settings.moveSpeed) < 0.1,
                "Entrance joins normal forward velocity without stopping or jumping");
            manager.Restart();
            float beforeStart = SamplePlayerAt(manager.Session.Pattern.Lead - step);
            float cameraBefore = player.cameraTarget.position.x;
            float afterStart = SamplePlayerAt(manager.Session.Pattern.Lead + step);
            Check(Math.Abs((afterStart - beforeStart) / (2 * step) - settings.moveSpeed) < 0.01,
                "World movement stays continuous across gameplay start");
            Check(Math.Abs((player.cameraTarget.position.x - cameraBefore) / (2 * step) - settings.moveSpeed) < 0.01,
                "Camera follow target stays continuous across gameplay start");
            Check(manager.Session.Score == 0, "Approach cannot award preview points");
            manager.Restart();
        }

        static void Tick() {
            if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(Request);
                try { Begin(); } catch (Exception error) { Debug.LogException(error); File.WriteAllText(Report, "FAIL: " + error); }
            }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                if (manager == null) manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                if (manager == null || manager.Session == null) return;
                if (EditorApplication.timeSinceStartup - started > 25) throw new Exception("Presentation test timed out");
                var player = manager.player;
                var animator = player.characterAnimation;
                switch (stage) {
                    case 0:
                        input = manager.gameObject.AddComponent<ExternalBreathInput>(); manager.input = input;
                        CheckAnimation(animator, 1, animator.rise, animator.cruiseToRise);
                        CheckAnimation(animator, -1, animator.descend, animator.cruiseToDescend);
                        CheckContinuousFlight();
                        manager.Restart();
                        startX = player.body.localPosition.x;
                        float left = player.cameraTarget.position.x + 4 - manager.virtualCamera.m_Lens.OrthographicSize * Camera.main.aspect;
                        Check(player.IsEntering && animator.target.bounds.max.x < left, "Restart starts the whole character outside the left edge");
                        Check(animator.CurrentClip == animator.cruise && !animator.IsReturning, "Restart clears pose transition");
                        stage = 1; started = EditorApplication.timeSinceStartup; break;
                    case 1:
                        if (manager.Session.Elapsed < 0.55) return;
                        Check(player.body.localPosition.x > startX && player.body.localPosition.x < 0 && player.IsEntering, "Character visibly travels right into the frame");
                        Check(player.cameraTarget.position.x == player.transform.position.x && manager.Session.PlayTime == 0 && manager.Session.Score == 0, "Entrance preserves camera framing, game time and score");
                        previewRootX = player.transform.position.x;
                        previewCameraX = Camera.main.transform.position.x;
                        BreathArtSetup.Capture("Docs/Images/PlayerEntrance.png");
                        stage = 2; break;
                    case 2:
                        if (manager.Session.Elapsed < manager.CurrentLevel.settings.playerEntranceSeconds + 0.08) return;
                        Check(!player.IsEntering && Mathf.Abs(player.body.localPosition.x) < 0.0001f, "Entrance ends exactly at the normal player anchor");
                        Check(manager.Session.PlayTime == 0 && manager.Session.Score == 0, "Preview still excludes score and game time after entry");
                        Check(player.transform.position.x > previewRootX && Camera.main.transform.position.x > previewCameraX,
                            "Live forward motion and camera scrolling continue throughout preview");
                        manager.Session.Advance(manager.Session.Pattern.Lead - manager.Session.Elapsed + 0.1, BreathLane.Upper);
                        input.Submit(BreathLane.Upper);
                        stage = 3; started = EditorApplication.timeSinceStartup; break;
                    case 3:
                        if (EditorApplication.timeSinceStartup - started < manager.CurrentLevel.settings.laneTransitionSeconds + animator.returnToCruiseSeconds + 0.15) return;
                        Check(Mathf.Abs(player.body.localPosition.y - manager.CurrentLevel.settings.laneSpacing) < 0.001f && animator.CurrentClip == animator.cruise, "Real upper-lane arrival completes the return to cruise");
                        input.Submit(BreathLane.Lower);
                        stage = 4; started = EditorApplication.timeSinceStartup; break;
                    case 4:
                        if (EditorApplication.timeSinceStartup - started < manager.CurrentLevel.settings.laneTransitionSeconds + animator.returnToCruiseSeconds + 0.15) return;
                        Check(Mathf.Abs(player.body.localPosition.y + manager.CurrentLevel.settings.laneSpacing) < 0.001f && animator.CurrentClip == animator.cruise, "Real lower-lane arrival completes the return to cruise");
                        var old = manager.CurrentLevel;
                        manager.Next();
                        Check(!old.gameObject.activeSelf && manager.levels.Count(l => l.gameObject.activeSelf) == 1 && player.IsEntering, "Switch replays entry with exactly one active level");
                        input.Submit(BreathLane.Middle);
                        manager.Restart();
                        Check(player.IsEntering && player.body.localPosition.x < 0 && manager.Session.Elapsed == 0 && manager.Session.Score == 0, "Rapid restart resets entry and score without stale motion");
                        var settings = UnityEngine.Object.Instantiate(manager.CurrentLevel.settings);
                        settings.guideLeadSeconds = 0;
                        Check(settings.CreatePattern().Lead >= settings.playerEntranceSeconds, "A short preview extends to finish entry before gameplay");
                        settings.playerEntranceSeconds = 0;
                        Check(settings.CreatePattern().Lead == 0, "Zero entrance and zero preview remain supported");
                        UnityEngine.Object.Destroy(settings);
                        Check(manager.CurrentLevel.guideClip.sourceFile == "craneFlat (1).gif" && manager.CurrentLevel.collectiblePrefab.clip.sourceFile == "star.gif", "Guide and collectible GIF roles remain correct");
                        Check(UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length == 0, "No HUD added");
                        File.AppendAllText(Report, "SUCCESS\n");
                        Finish(); break;
                }
            } catch (Exception error) {
                File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); Finish();
            }
        }

        static void Finish() {
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            SessionState.SetBool(Key, false); EditorApplication.isPlaying = false;
        }
    }
}
