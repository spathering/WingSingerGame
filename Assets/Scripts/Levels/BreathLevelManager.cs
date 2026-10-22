using Cinemachine;
using UnityEngine;

namespace WingSinger {
    public sealed class BreathLevelManager : MonoBehaviour {
        public BreathLevel[] levels;
        public BreathPlayer player;
        public BreathInput input;
        public BreathHud hud;
        public CinemachineVirtualCamera virtualCamera;
        public int initialLevel;
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
            if (hud != null) {
                hud.restartButton.onClick.AddListener(Restart);
                hud.nextButton.onClick.AddListener(Next);
                hud.resultRestartButton.onClick.AddListener(Restart);
                hud.resultNextButton.onClick.AddListener(Next);
            }
            SwitchLevel(initialLevel);
        }

        void LateUpdate() {
            if (switching || Session == null || CurrentLevel == null) return;
            Vector3 current = player.body.position;
            if (!player.IsEntering && CurrentLevel.CollectBetween(Session, previousBodyPosition, current) > 0) player.PlayCollectionFeedback();
            previousBodyPosition = current;
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.N)) Next();
            else if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchLevel(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchLevel(1);
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

        public bool SwitchLevel(int index) {
            if (switching || levels == null || index < 0 || index >= levels.Length || levels[index] == null) return false;
            var target = levels[index];
            // Validate before disabling the working level.
            try { target.settings.Validate(); }
            catch (System.Exception error) { Debug.LogException(error, target); return false; }
            switching = true;
            try {
                player.StopAnimation();
                if (CurrentLevel != null) { CurrentLevel.Exit(); CurrentLevel.gameObject.SetActive(false); }
                Session = null; CurrentIndex = -1;
                target.Prepare();
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
                Debug.LogException(error, this);
                return false;
            } finally { switching = false; }
        }

        void OnDisable() {
            if (player != null) player.StopAnimation();
            if (CurrentLevel != null) CurrentLevel.Exit();
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
