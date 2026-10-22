using System;
using System.Linq;
using WingSinger;

int assertions = 0;
void Check(bool condition, string name) {
    if (!condition) throw new Exception(name);
    assertions++;
}
bool Near(double a, double b) => Math.Abs(a - b) < 0.000001;
var p = new BreathPattern(3, 4, 18, 10, 2, 0.16, 3, 1);
var route = BreathCollectibleLayout.Create(p, 0.5);
bool Contains(double time, BreathLane lane) => route.Any(x => Near(x.Time, time) && x.Lane == lane);
Check(Contains(0, BreathLane.Middle) && Contains(3, BreathLane.Middle), "Preparation endpoints");
Check(Contains(4, BreathLane.Upper) && Contains(7, BreathLane.Upper), "Upper endpoints");
Check(Contains(8, BreathLane.Lower) && Contains(12, BreathLane.Lower), "Lower endpoints");
Check(Contains(7.5, BreathLane.Middle) && Contains(12.5, BreathLane.Middle), "Both curve directions cross middle");
Check(Contains(16.5, BreathLane.Middle), "Next cycle crossing");
foreach (var point in route) {
    Check(Near(p.HeightAt(point.Time), (int)point.Lane), "Object lies exactly on a lane and on route");
    Check(point.Time >= 0 && point.Time <= p.Duration, "No object beyond session bounds");
}
for (int i = 1; i < route.Length; i++) Check(route[i].Time > route[i - 1].Time, "Ordered and deduplicated times");
foreach (var section in new[] { (0d, 3d), (4d, 7d), (8d, 12d) }) {
    var group = route.Where(x => x.Time >= section.Item1 && x.Time <= section.Item2).ToArray();
    for (int i = 1; i < group.Length; i++) Check(Near(group[i].Time - group[i - 1].Time, 0.5), "Constant spacing");
}
var awkward = new BreathPattern(3.2, 2.7, 16, 10, 0, 0.16, 2.3, 0.7);
var uneven = BreathCollectibleLayout.Create(awkward, 0.6).Where(x => x.Lane == BreathLane.Upper && x.Time < 6.3).ToArray();
Check(Near(uneven[0].Time, 3) && Near(uneven[^1].Time, 6.2), "Non-divisible section retains both endpoints");
double gap = uneven[1].Time - uneven[0].Time;
for (int i = 2; i < uneven.Length; i++) Check(Near(uneven[i].Time - uneven[i - 1].Time, gap), "Redistributed section remains uniform");
var shortRoute = BreathCollectibleLayout.Create(new BreathPattern(3, 4, 0.8, 10, 0.1, 0.16, 0.8, 0.65), 0.4);
Check(shortRoute.Length == 3 && Near(shortRoute[^1].Time, 0.8), "Truncated straight section endpoint");
var noPrep = BreathCollectibleLayout.Create(new BreathPattern(3, 4, 10, 10, 0, 0.16, 0, 0.65), 0.5);
Check(noPrep.Count(x => Near(x.Time, 0)) == 1, "No duplicate first object when preparation is zero");
var session = new BreathSession(p);
Check(!session.TryCollect(1), "No award before first frame");
session.Advance(1, BreathLane.Middle);
Check(!session.TryCollect(1), "No award during preview");
session.Advance(2, BreathLane.Middle);
Check(session.TryCollect(0) && session.TryCollect(1), "Separate object awards");
Check(!session.TryCollect(1) && !session.TryCollect(-1), "Duplicate and invalid IDs rejected");
Check(session.CollectedCount == 2 && session.Score == 20, "Score is number times per-object value");
session.Advance(10, BreathLane.Upper);
Check(session.Score == 20, "Following a lane never increases score by itself");
session.Advance(100, BreathLane.Lower);
Check(session.TryCollect(2) && session.Score == 30, "Final movement frame still collects");
session.Advance(0, BreathLane.Upper);
Check(!session.TryCollect(3) && session.Score == 30, "Later finished frames cannot collect");
var reset = new BreathSession(p);
reset.Advance(3, BreathLane.Middle);
Check(reset.CollectedCount == 0 && reset.TryCollect(1), "New session resets collected IDs");
var free = new BreathSession(new BreathPattern(3, 4, 10, 0, 0, 0));
free.Advance(1, BreathLane.Middle);
Check(free.TryCollect(0) && free.CollectedCount == 1 && free.Score == 0, "Zero point configuration remains collectable");
bool invalid = false;
try { BreathCollectibleLayout.Create(p, double.NaN); } catch (ArgumentOutOfRangeException) { invalid = true; }
Check(invalid, "Invalid interval rejected");
Console.WriteLine($"PASS: {assertions} assertions for endpoints, uniform spacing, crossings, bounds, discrete score, duplicates, final frame and reset.");
