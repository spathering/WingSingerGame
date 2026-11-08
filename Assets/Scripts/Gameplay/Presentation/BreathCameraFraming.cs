using Cinemachine;
using UnityEngine;

namespace WingSinger {
    // Keep the existing Cinemachine horizontal follow and add vertical composition/safety.
    [DisallowMultipleComponent]
    public sealed class BreathCameraFraming : CinemachineExtension {
        public BreathLevelManager manager;
        public LoopingPanorama worldBackground;
        [Min(1)] public float minimumOrthographicSize = 5.6f;
        [Range(0, 1)] public float verticalFollow = .65f;
        [Min(.01f)] public float followSmoothSeconds = .22f;
        [Range(.02f, .2f)] public float viewportMargin = .08f;
        BreathSession previousSession;
        float centerY, velocityY, size, sizeVelocity;

        public float EntranceLeftEdge(BreathLevel level, float aspect) {
            aspect = Mathf.Max(.1f, aspect);
            float travel = level.settings.playerEntranceSeconds > 0
                ? (float)level.settings.CreatePattern().Lead * level.settings.moveSpeed : 0;
            float playerX = manager.player.transform.position.x - travel;
            float minX = playerX - manager.player.characterAnimation.target.bounds.extents.x - .1f;
            float maxX = level.guideVisual.bounds.max.x + .1f;
            float initialSize = Mathf.Max(minimumOrthographicSize, (maxX - minX) / (2 * aspect * (1 - 2 * viewportMargin)));
            float initialCenter = Mathf.Min(playerX + manager.cameraHorizontalOffset, (minX + maxX) * .5f);
            // BeginEntrance runs before the player's root is advanced into its preview position.
            return initialCenter - initialSize * aspect + travel;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime) {
            if (stage != CinemachineCore.Stage.Finalize || manager == null) return;
            var session = manager.Session;
            var level = manager.CurrentLevel;
            if (session == null || level == null) { previousSession = null; return; }
            var player = manager.player;
            bool reset = !ReferenceEquals(previousSession, session) || deltaTime < 0 || !vcam.PreviousStateIsValid;
            previousSession = session;
            float desiredY = level.spawnPoint.position.y + (player.body.position.y
                - level.spawnPoint.position.y - level.settings.LaneHeight(0)) * verticalFollow;
            if (reset) { centerY = desiredY; velocityY = sizeVelocity = 0; size = minimumOrthographicSize; }
            else if (!manager.IsPaused && deltaTime > 0)
                centerY = Mathf.SmoothDamp(centerY, desiredY, ref velocityY, followSmoothSeconds, Mathf.Infinity, deltaTime);

            var bounds = player.characterAnimation.target.bounds;
            // Entrance deliberately starts off-screen. Frame the landing position until it finishes.
            if (player.IsEntering) { var center = bounds.center; center.x += player.transform.position.x - player.body.position.x; bounds.center = center; }
            var playerBounds = bounds;
            bounds.Encapsulate(level.guideVisual.bounds);
            float safeFraction = 1 - 2 * viewportMargin;
            float aspect = Mathf.Max(.1f, state.Lens.Aspect);
            float requiredSize = Mathf.Max(minimumOrthographicSize,
                playerBounds.extents.y / safeFraction, bounds.extents.x / (aspect * safeFraction));
            // Expand immediately for safety; ease back in when the pair fit again.
            if (reset || requiredSize >= size) { size = requiredSize; sizeVelocity = 0; }
            else if (!manager.IsPaused && deltaTime > 0)
                size = Mathf.SmoothDamp(size, requiredSize, ref sizeVelocity, .4f, Mathf.Infinity, deltaTime);
            var lens = state.Lens; lens.OrthographicSize = size; state.Lens = lens;
            float halfY = size * safeFraction, halfX = halfY * aspect;
            var position = state.CorrectedPosition;
            position.x = Mathf.Clamp(position.x, bounds.max.x - halfX, bounds.min.x + halfX);
            // Only the player affects vertical framing. The crane is protected on X alone.
            float minY = playerBounds.max.y - halfY, maxY = playerBounds.min.y + halfY;
            if (worldBackground != null && worldBackground.worldSpaceVertical) {
                float backgroundHalfHeight = worldBackground.transform.localScale.y * .5f;
                float backgroundY = worldBackground.transform.position.y;
                float coveredMin = Mathf.Max(minY, backgroundY - backgroundHalfHeight + size);
                float coveredMax = Mathf.Min(maxY, backgroundY + backgroundHalfHeight - size);
                // Keep the player protected if an unsupported extreme viewport cannot satisfy both limits.
                if (coveredMin <= coveredMax) { minY = coveredMin; maxY = coveredMax; }
            }
            position.y = Mathf.Clamp(centerY, minY, maxY);
            centerY = position.y;
            state.PositionCorrection += position - state.CorrectedPosition;
        }
    }
}
