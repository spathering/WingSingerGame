using System.Collections.Generic;
using UnityEngine;

namespace WingSinger {
    public sealed class BreathLevel : MonoBehaviour {
        public BreathLevelSettings settings;
        public Transform spawnPoint;
        public Transform guide;
        public LineRenderer routeCore;
        public LineRenderer routeGlow;
        Vector3[] routeNodes;
        Vector3[] visibleNodes;
        bool prepared;

        public void Prepare() {
            if (settings == null || spawnPoint == null || guide == null || routeCore == null || routeGlow == null)
                throw new System.InvalidOperationException($"Level {name} has missing references.");
            settings.Validate();
            var pattern = settings.CreatePattern();
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
            float x = spawnPoint.localPosition.x + (float)session.Elapsed * settings.moveSpeed;
            var head = Point(session.Elapsed, session.Pattern.HeightAt(session.Elapsed));
            guide.localPosition = head;
            guide.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin((float)session.Elapsed * 5));
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

        public void Exit() {
            prepared = false;
            routeCore.positionCount = routeGlow.positionCount = 0;
            guide.localScale = Vector3.one;
        }
    }
}
