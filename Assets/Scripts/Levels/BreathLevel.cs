using System.Collections.Generic;
using UnityEngine;

namespace WingSinger {
    public sealed class BreathLevel : MonoBehaviour {
        public BreathLevelSettings settings;
        public Transform spawnPoint;
        public Transform guide;
        public LineRenderer routeCore;
        public LineRenderer routeGlow;
        public SpriteRenderer guideVisual;
        public CharacterSpriteClip guideClip;
        public PaperCollectible collectiblePrefab;
        public bool reduceMotion;
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
            if (guideVisual == null || guideClip == null || collectiblePrefab == null)
                throw new System.InvalidOperationException($"Level {name} needs paper and guide animation assets.");
            var pattern = settings.CreatePattern();
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
            routeNodes = nodes.ToArray();
            visibleNodes = new Vector3[routeNodes.Length + 1];
            routeCore.positionCount = routeGlow.positionCount = 0;
            guide.localPosition = Point(0, pattern.HeightAt(0));
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
            + (float)time * settings.moveSpeed, spawnPoint.localPosition.y + (float)height * settings.laneSpacing, -0.2f);

        public void Present(BreathSession session) {
            if (!prepared) return;
            double guideTime = System.Math.Min(session.Elapsed, session.Pattern.Duration);
            float x = spawnPoint.localPosition.x + (float)guideTime * settings.moveSpeed;
            var head = Point(guideTime, session.Pattern.HeightAt(guideTime));
            guide.localPosition = head;
            guideVisual.sprite = guideClip.Sample(session.Elapsed, true);
            for (int i = 0; i < PaperLayout.Length; i++) papers[i].Present(guideTime, session.Elapsed);
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

        public int CollectBetween(BreathSession session, Vector3 from, Vector3 to) {
            if (!prepared || !session.CanCollectThisFrame || !gameObject.activeInHierarchy) return 0;
            int count = 0;
            for (int i = 0; i < PaperLayout.Length; i++) {
                var paper = papers[i];
                if (paper.IntersectsSweep(from, to, settings.collectionRadius) && session.TryCollect(paper.Id)) {
                    paper.Collect(); count++;
                } else if (!paper.Collected && to.x > paper.transform.position.x + settings.collectionRadius) paper.MarkMissed();
            }
            return count;
        }

        public void Exit() {
            prepared = false;
            routeCore.positionCount = routeGlow.positionCount = 0;
            guide.localScale = Vector3.one;
            foreach (var paper in papers) { paper.StopFeedback(); paper.gameObject.SetActive(false); }
        }
    }
}
