using Cinemachine;
using UnityEngine;

namespace WingSinger {
    public sealed class BreathLevelManager : MonoBehaviour {
        public BreathLevel[] levels;
        public BreathLevelCatalog catalog;
        public int LevelCount => catalog != null ? catalog.Count : levels == null ? 0 : levels.Length;
        public BreathLevelDefinition DefinitionAt(int index) => catalog == null ? null : catalog.At(index);
        public BreathPlayer player;
        public BreathInput input;
        public BreathHud hud;
        public CinemachineVirtualCamera virtualCamera;
        public int initialLevel;
        public bool startAutomatically = true;
        public bool allowKeyboardShortcuts = true;
        public bool IsPaused { get; private set; }
        public BreathSession Session { get; private set; }
        public int CurrentIndex { get; private set; } = -1;
        public BreathLevel CurrentLevel => CurrentIndex >= 0 && levels != null ? levels[CurrentIndex] : null;
        bool switching;
        Vector3 previousBodyPosition;

        void Start() {
            if (player == null || input == null || virtualCamera == null || levels == null || levels.Length == 0) {
                Debug.LogError("WingSinger: missing shared systems or level instances.", this);
                enabled = false; return;
            }
            foreach (var level in levels) if (level != null) level.gameObject.SetActive(false);
            try { ValidateCatalog(); }
            catch (System.Exception error) { Debug.LogException(error, this); enabled = false; return; }
            if (hud != null) {
                hud.restartButton.onClick.AddListener(Restart);
                hud.nextButton.onClick.AddListener(Next);
                hud.resultRestartButton.onClick.AddListener(Restart);
                hud.resultNextButton.onClick.AddListener(Next);
            }
            if (startAutomatically) SwitchLevel(initialLevel);
            else StopSession();
        }

        void LateUpdate() {
            if (IsPaused || switching || Session == null || CurrentLevel == null) return;
            Vector3 current = player.body.position;
            if (!player.IsEntering && CurrentLevel.CollectBetween(Session, previousBodyPosition, current) > 0) player.PlayCollectionFeedback();
            previousBodyPosition = current;
        }

        void Update() {
            if (IsPaused) return;
            if (allowKeyboardShortcuts) {
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                else if (Input.GetKeyDown(KeyCode.N)) Next();
                else if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchLevel(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchLevel(1);
            }
            if (switching || Session == null || CurrentLevel == null) return;
            bool finished = Session.Phase == BreathPhase.Finished;
            Session.Advance(Time.deltaTime, finished ? Session.PlayerLane : Session.Phase == BreathPhase.Preview ? BreathLane.Middle : input.ReadLane());
            player.Present(Session, CurrentLevel.settings);
            CurrentLevel.Present(Session);
            if (hud != null) hud.Present(Session);
            if (!finished && Session.Phase == BreathPhase.Finished) player.StopAnimation();
        }

        public void Restart() { if (CurrentIndex >= 0) SwitchLevel(CurrentIndex); }
        public void Next() { if (levels != null && levels.Length > 0) SwitchLevel((CurrentIndex + 1) % levels.Length); }
        public void SetPaused(bool paused) { IsPaused = paused; }
        public void ValidateCatalog() {
            if (catalog == null) return;
            catalog.Validate();
            if (levels == null || levels.Length != catalog.Count)
                throw new System.InvalidOperationException("Catalog and main-scene level registrations differ.");
            var registered = new System.Collections.Generic.HashSet<BreathLevel>();
            for (int i = 0; i < levels.Length; i++) {
                if (levels[i] == null || !registered.Add(levels[i]) || levels[i].settings != catalog.At(i).settings)
                    throw new System.InvalidOperationException("Missing, duplicated or mismatched level registration at index " + i);
            }
        }
        public void StopSession() {
            if (player != null) player.StopAnimation();
            if (CurrentLevel != null) CurrentLevel.Exit();
            if (levels != null) foreach (var level in levels) if (level != null) level.gameObject.SetActive(false);
            Session = null; CurrentIndex = -1; IsPaused = false;
            if (player != null) player.gameObject.SetActive(false);
        }

        public bool SwitchLevel(int index) {
            if (switching || levels == null || index < 0 || index >= levels.Length || levels[index] == null) return false;
            var target = levels[index];
            // Validate before disabling the working level.
            try {
                target.settings.Validate();
                if (catalog != null && (catalog.At(index) == null || catalog.At(index).settings != target.settings))
                    throw new System.InvalidOperationException("Selected level does not match its catalog definition.");
            }
            catch (System.Exception error) { Debug.LogException(error, target); return false; }
            switching = true;
            try {
                player.StopAnimation();
                if (CurrentLevel != null) { CurrentLevel.Exit(); CurrentLevel.gameObject.SetActive(false); }
                Session = null; CurrentIndex = -1;
                target.Prepare();
                player.gameObject.SetActive(true);
                IsPaused = false;
                player.ResetPlayer(target.spawnPoint.position);
                virtualCamera.PreviousStateIsValid = false;
                Vector3 cameraPosition = player.cameraTarget.position + new Vector3(4, 0, -10);
                var outputCamera = Camera.main;
                float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;
                player.BeginEntrance(cameraPosition.x - virtualCamera.m_Lens.OrthographicSize * aspect, target.settings.playerEntranceSeconds);
                target.gameObject.SetActive(true);
                CurrentIndex = index;
                Session = new BreathSession(target.settings.CreatePattern());
                if (hud != null) hud.ResetHud(target.settings.displayName);
                player.Present(Session, target.settings);
                previousBodyPosition = player.body.position;
                virtualCamera.ForceCameraPosition(player.cameraTarget.position + new Vector3(4, 0, -10), Quaternion.identity);
                target.Present(Session);
                if (hud != null) hud.Present(Session);
                return true;
            } catch (System.Exception error) {
                target.gameObject.SetActive(false);
                Session = null; CurrentIndex = -1;
                player.gameObject.SetActive(false);
                Debug.LogException(error, this);
                return false;
            } finally { switching = false; }
        }

        void OnDisable() {
            StopSession();
        }

        void OnDestroy() {
            if (hud == null) return;
            hud.restartButton.onClick.RemoveListener(Restart);
            hud.nextButton.onClick.RemoveListener(Next);
            hud.resultRestartButton.onClick.RemoveListener(Restart);
            hud.resultNextButton.onClick.RemoveListener(Next);
        }
    }
}
