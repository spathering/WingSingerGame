using System;
using System.Collections.Generic;

namespace WingSinger {
    public readonly struct PaperRoutePoint {
        public readonly double Time;
        public readonly BreathLane Lane;
        public PaperRoutePoint(double time, BreathLane lane) { Time = time; Lane = lane; }
    }

    public static class BreathCollectibleLayout {
        public static PaperRoutePoint[] Create(BreathPattern pattern, double interval) {
            if (double.IsNaN(interval) || double.IsInfinity(interval) || interval < 0.05)
                throw new ArgumentOutOfRangeException(nameof(interval));
            var points = new List<PaperRoutePoint>();
            for (int loop = 0; loop < pattern.LoopCount; loop++) {
                double offset = loop * pattern.LoopSeconds;
                double limit = offset + (pattern.Segmented ? pattern.ReturnStart : pattern.Duration);
                double start = offset + pattern.Preparation + pattern.GuideTransition;
                while (start < limit) {
                    AddStraight(points, pattern, start, Math.Min(limit, start + pattern.Inhale), BreathLane.Upper, interval);
                    double lower = start + pattern.Inhale + pattern.GuideTransition;
                    if (lower < limit) AddStraight(points, pattern, lower, Math.Min(limit, lower + pattern.Exhale), BreathLane.Lower, interval);
                    start += pattern.Cycle;
                }
            }
            return points.ToArray();
        }

        static void AddStraight(List<PaperRoutePoint> points, BreathPattern p, double start, double end, BreathLane lane, double interval) {
            if (start > p.Duration) return;
            end = Math.Min(end, p.Duration);
            int gaps = Math.Max(1, (int)Math.Ceiling((end - start) / interval));
            for (int i = 0; i <= gaps; i++) Add(points, start + (end - start) * i / gaps, lane);
        }

        static void Add(List<PaperRoutePoint> points, double time, BreathLane lane) {
            // Adjacent route sections can share an endpoint. It must award only once.
            if (points.Count > 0 && Math.Abs(points[points.Count - 1].Time - time) < 0.000001
                && points[points.Count - 1].Lane == lane) return;
            points.Add(new PaperRoutePoint(time, lane));
        }
    }
}
