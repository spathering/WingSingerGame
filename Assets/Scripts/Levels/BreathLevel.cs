using System.Collections.Generic;
using UnityEngine;

namespace WingSinger {
    // Recycle only after Cinemachine has finalized the actual output camera for this frame.
    [DefaultExecutionOrder(10002)]
    public sealed class BreathLevel : MonoBehaviour {
        public BreathLevelSettings settings;
        public Transform spawnPoint;
        public Transform guide;
        public float guideHorizontalOffset;
        public LineRenderer routeCore;
        public LineRenderer routeGlow;
        public SpriteRenderer guideVisual;
        public CharacterSpriteClip guideClip;
        public PaperCollectible collectiblePrefab;
        public LevelForegroundParallax foreground;
        public bool reduceMotion;
        public FirstFlightSequence sequence;
        readonly List<PaperCollectible> papers = new List<PaperCollectible>();
        public IReadOnlyList<PaperCollectible> Papers => papers;
        public PaperRoutePoint[] PaperLayout { get; private set; }
        Vector3[] routeNodes;
        Vector3[] visibleNodes;
        bool prepared;

        public void Prepare() {
            if (settings == null || spawnPoint == null || guide == null || routeCore == null || routeGlow == null)
                throw new System.InvalidOperationException($"Level {name} has missing references.");
            settings.Validate();
            if (foreground != null) foreground.ResetPresentation();
            if (guideVisual == null || guideClip == null || collectiblePrefab == null)
                throw new System.InvalidOperationException($"Level {name} needs paper and guide animation assets.");
            var pattern = settings.CreatePattern();
            var cues = GetComponent<FlightHazardCues>();
            if (cues != null) cues.Prepare(this);
            routeCore.enabled = routeGlow.enabled = settings.showGuideLine;
            PaperLayout = BreathCollectibleLayout.Create(pattern, settings.collectibleIntervalSeconds);
            while (papers.Count < PaperLayout.Length) papers.Add(Instantiate(collectiblePrefab, transform));
            for (int i = 0; i < papers.Count; i++) {
                if (i < PaperLayout.Length) papers[i].ResetPaper(i, PaperLayout[i].Time, Point(PaperLayout[i].Time, (int)PaperLayout[i].Lane));
                else { papers[i].StopFeedback(); papers[i].gameObject.SetActive(false); }
            }
            guideVisual.sprite = guideClip.Sample(0, true);
            var nodes = new List<Vector3>();
            double t = 0;
            double length = pattern.Duration + pattern.Lead;
            nodes.Add(Point(0, pattern.HeightAt(0)));
            AddSegment(nodes, pattern, ref t, length, pattern.Preparation, 1);
            AddSegment(nodes, pattern, ref t, length, pattern.GuideTransition, 24);
            while (t < length) {
                AddSegment(nodes, pattern, ref t, length, pattern.Inhale, 1);
                AddSegment(nodes, pattern, ref t, length, pattern.GuideTransition, 24);
                AddSegment(nodes, pattern, ref t, length, pattern.Exhale, 1);
                AddSegment(nodes, pattern, ref t, length, pattern.GuideTransition, 24);
            }
            if(pattern.Segmented){
                nodes.Clear();
                for(double sample=0;sample<length;sample+=.05)nodes.Add(Point(sample,pattern.HeightAt(sample)));
                nodes.Add(Point(length,pattern.HeightAt(length)));
            }
            routeNodes = nodes.ToArray();
            visibleNodes = new Vector3[routeNodes.Length + 1];
            routeCore.positionCount = routeGlow.positionCount = 0;
            double initialGuideTime = System.Math.Min(GuideOffsetSeconds, pattern.Duration);
            guide.localPosition = Point(initialGuideTime, pattern.HeightAt(initialGuideTime));
            prepared = true;
        }

        void AddSegment(List<Vector3> nodes, BreathPattern pattern, ref double time, double end, double duration, int samples) {
            if (duration <= 0 || time >= end) return;
            double start = time;
            time = System.Math.Min(end, start + duration);
            for (int i = 1; i <= samples; i++) {
                double sampleTime = start + (time - start) * i / samples;
                nodes.Add(Point(sampleTime, pattern.HeightAt(sampleTime)));
            }
        }

        Vector3 Point(double time, double height) => new Vector3(spawnPoint.localPosition.x
            + (float)time * settings.moveSpeed, spawnPoint.localPosition.y + settings.LaneHeight((float)height), -0.2f);

        // The composition offset is a time lead along the shared route, not a separate translated path.
        double GuideOffsetSeconds => System.Math.Max(0, guideHorizontalOffset) / settings.moveSpeed;

        public void Present(BreathSession session) {
            if (!prepared) return;
            var cues = GetComponent<FlightHazardCues>();
            if (cues != null) cues.Present(session);
            double guideClock = session.Elapsed + GuideOffsetSeconds;
            double limit = session.Pattern.Segmented
                ? System.Math.Min(session.Pattern.Duration, ((int)(session.PlayTime / session.Pattern.LoopSeconds) + 1) * session.Pattern.LoopSeconds)
                : session.Pattern.Duration;
            double guideTime = System.Math.Min(guideClock, limit);
            float x = spawnPoint.localPosition.x + (float)guideTime * settings.moveSpeed;
            var head = Point(guideTime, session.Pattern.HeightAt(guideTime));
            guide.localPosition = head;
            guideVisual.sprite = guideClip.Sample(session.Elapsed, true);
            for (int i = 0; i < PaperLayout.Length; i++) {
                papers[i].Present(guideTime, guideClock);
            }
            guide.localScale = reduceMotion ? Vector3.one : Vector3.one * (1 + 0.08f * Mathf.Sin((float)session.Elapsed * 5));
            int count = 0;
            for (int i = 0; i < routeNodes.Length && routeNodes[i].x <= x; i++) visibleNodes[count++] = routeNodes[i];
            if (count == 0 || visibleNodes[count - 1] != head) visibleNodes[count++] = head;
            routeCore.positionCount = routeGlow.positionCount = count;
            // SetPositions requires an exact-sized array; SetPosition avoids allocating every frame.
            for (int i = 0; i < count; i++) {
                routeCore.SetPosition(i, visibleNodes[i]);
                routeGlow.SetPosition(i, visibleNodes[i]);
            }
        }

        void LateUpdate() {
            if (!prepared) return;
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return;
            float cameraLeft = camera.transform.position.x - camera.orthographicSize * camera.aspect;
            // Passing the player is not a reason to hide a star. Its entire visual must leave the viewport.
            for (int i = 0; i < PaperLayout.Length; i++) papers[i].RecycleIfBehindCamera(cameraLeft);
        }

        public int CollectBetween(BreathSession session, Vector3 from, Vector3 to, Transform absorptionTarget = null) {
            if (!prepared || !session.CanCollectThisFrame || !gameObject.activeInHierarchy) return 0;
            int count = 0;
            for (int i = 0; i < PaperLayout.Length; i++) {
                var paper = papers[i];
                if (paper.IntersectsSweep(from, to, Mathf.Max(settings.collectionRadius, settings.attractionRadius)) && session.TryCollect(paper.Id)) {
                    paper.Collect(absorptionTarget); count++;
                }
            }
            return count;
        }

        public void Exit() {
            var cues = GetComponent<FlightHazardCues>();
            if (cues != null) cues.ResetPresentation();
            if (foreground != null) foreground.ResetPresentation();
            prepared = false;
            routeCore.positionCount = routeGlow.positionCount = 0;
            guide.localScale = Vector3.one;
            foreach (var paper in papers) { paper.StopFeedback(); paper.gameObject.SetActive(false); }
        }
    }
}
