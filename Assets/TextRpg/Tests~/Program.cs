using System;
using System.Collections.Generic;
using MapGame.TextRpg;

static class Program
{
    static int passed;
    static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    static void Check(string name, Action action)
    { action(); passed++; Console.WriteLine("PASS " + name); }
    static (RpgPlayerState p, RpgEvent e, RpgEventMap m, RpgEventSession s) Setup()
    {
        var p = new RpgPlayerState();
        var e = new RpgEvent();
        var m = new RpgEventMap { defaultEvent = e };
        return (p, e, m, new RpgEventSession(p));
    }
    static void Enter(RpgEventSession s, RpgEventMap m, int col = 0, int row = 1)
    { Assert(s.TryEnter(m, col, row, out string error), error); }
    static void Choose(RpgEventSession s, int index = 0)
    { Assert(s.TryChoose(index, out string error), error); }
    static void Main()
    {
        Check("all 150 cells have a default event", () => {
            var x = Setup(); Assert(x.m.IsValid(out _), "map invalid");
            for (int r = 1; r <= 10; r++) for (int c = 0; c < 15; c++)
                Assert(x.m.GetEvent(c, r) == x.e, "cell missing");
            Assert(x.m.GetEvent(-1, 1) == null && x.m.GetEvent(0, 11) == null, "bounds");
        });
        Check("at least two choices enforced", () => {
            var x = Setup(); x.e.choices.RemoveAt(1);
            Assert(!x.s.TryEnter(x.m, 0, 1, out _), "invalid event entered");
        });




        Check("stat equality succeeds and stats change", () => {
            var x = Setup(); var c = x.e.choices[0]; c.useStatCheck = true; c.minimumStat = 5;
            c.success.stats.Add(new StatChange { stat = RpgStat.Agility, amount = 2 });
            Enter(x.s, x.m); Choose(x.s);
            Assert(x.s.LastCheckSucceeded && x.p.GetStat(RpgStat.Agility) == 7, "threshold or stats");
        });
        Check("duplicate submissions cannot grant rewards twice", () => {
            var x = Setup(); var item = new RpgItem();
            x.e.choices[0].success.rewards.Add(new ItemAmount { item = item });
            Enter(x.s, x.m); Choose(x.s);
            Assert(!x.s.TryChoose(0, out _) && x.p.CountItem(item) == 1, "double reward");
        });
        Check("completion is per cell, not shared event asset", () => {
            var x = Setup(); Enter(x.s, x.m); Choose(x.s);
            Assert(!x.s.TryEnter(x.m, 0, 1, out _), "completed cell reopened");
            Enter(x.s, x.m, 1, 1); Choose(x.s);
            Assert(x.e.choices.Count == 2, "shared asset changed");
        });
        Check("pending event cannot be replaced", () => {
            var x = Setup(); Enter(x.s, x.m);
            Assert(!x.s.TryEnter(x.m, 1, 1, out _) && x.s.ActiveEvent == x.e, "pending overwritten");
        });
        Check("repeatable events can reopen", () => {
            var x = Setup(); x.e.repeatable = true;
            Enter(x.s, x.m); Choose(x.s); Enter(x.s, x.m); Choose(x.s);
        });
        Check("health/stat floors and dead-player gate", () => {
            var x = Setup(); var o = x.e.choices[0].success;
            o.healthChange = -200; o.maxHealthChange = -200;
            o.stats.Add(new StatChange { stat = RpgStat.Strength, amount = -100 });
            Enter(x.s, x.m); Choose(x.s);
            Assert(x.p.Health == 0 && x.p.MaxHealth == 1 && x.p.GetStat(RpgStat.Strength) == 0, "bounds");
            Assert(!x.s.TryEnter(x.m, 1, 1, out _), "dead player entered");
        });
        Check("maximum health applied before healing and healing capped", () => {
            var x = Setup(); var o = x.e.choices[0].success;
            o.maxHealthChange = 5; o.healthChange = 200; Enter(x.s, x.m); Choose(x.s);
            Assert(x.p.Health == 105 && x.p.MaxHealth == 105, "healing order");
        });
        Check("invalid overrides and invalid rewards rejected", () => {
            var x = Setup(); x.m.overrides.Add(new CellEvent { encounter = x.e });
            x.m.overrides.Add(new CellEvent { encounter = x.e });
            Assert(!x.m.IsValid(out _), "duplicate cell allowed");
            x.m.overrides.Clear(); x.e.choices[0].success.rewards.Add(new ItemAmount());
            Assert(!x.s.TryEnter(x.m, 0, 1, out _), "invalid reward entered");
        });

        Check("inventory overflow saturates instead of wrapping", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, int.MaxValue); x.p.AddItem(item, 1);
            Assert(x.p.CountItem(item) == int.MaxValue, "inventory overflow");
        });
        Check("all eight adjacent cells are reachable", () => {
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var x = Setup(); var travel = new RpgMapTravel(x.s, x.m, 7, 5, true);
                Assert(travel.TryMoveTo(7 + dx, 5 + dy, out _), "neighbour rejected");
                Assert(travel.Column == 7 + dx && travel.Row == 5 + dy && x.s.HasPendingChoice, "move/event mismatch");
            }
        });
        Check("distant, current and out-of-bounds cells cannot move player", () => {
            var x = Setup(); var t = new RpgMapTravel(x.s, x.m, 0, 1, true);
            Assert(!t.TryMoveTo(0, 1, out _) && !t.TryMoveTo(2, 1, out _) && !t.TryMoveTo(-1, 1, out _), "bad move accepted");
            Assert(t.Column == 0 && t.Row == 1 && !x.s.HasPendingChoice, "bad move changed state");
        });
        Check("four-way movement can disable diagonals", () => {
            var x = Setup(); var t = new RpgMapTravel(x.s, x.m, 7, 5, false);
            Assert(!t.IsNeighbour(8, 6) && t.IsNeighbour(7, 6), "four-way setting ignored");
        });
        Check("pending event prevents movement without changing coordinates", () => {
            var x = Setup(); var t = new RpgMapTravel(x.s, x.m, 0, 1, true); Enter(x.s, x.m);
            Assert(!t.TryMoveTo(1, 1, out _) && t.Column == 0, "moved during event");
            Choose(x.s); Assert(t.TryMoveTo(1, 1, out _), "did not unlock after choice");
        });
        Check("resolved cells are traversable without reopening or duplicate rewards", () => {
            var x = Setup(); var item = new RpgItem(); x.e.choices[0].success.rewards.Add(new ItemAmount { item = item });
            var t = new RpgMapTravel(x.s, x.m, 0, 1, true); Enter(x.s, x.m); Choose(x.s);
            Assert(t.TryMoveTo(1, 1, out _), "forward failed"); Choose(x.s);
            Assert(t.TryMoveTo(0, 1, out _) && !x.s.HasPendingChoice && x.p.CountItem(item) == 2, "revisit opened/rewarded");
            Assert(t.TryMoveTo(1, 1, out _) && !x.s.HasPendingChoice, "second revisit failed");
        });
        Check("invalid target event keeps player at previous position", () => {
            var x = Setup(); var t = new RpgMapTravel(x.s, x.m, 0, 1, true);
            x.m.overrides.Add(new CellEvent { column = 1, row = 1, encounter = null });
            Assert(!t.TryMoveTo(1, 1, out _) && t.Column == 0, "invalid map teleported player");
        });
        Check("dead player cannot move", () => {
            var x = Setup(); var t = new RpgMapTravel(x.s, x.m, 0, 1, true);
            x.e.choices[0].success.healthChange = -200; Enter(x.s, x.m); Choose(x.s);
            Assert(!t.TryMoveTo(1, 1, out _) && t.Column == 0, "dead player moved");
        });
        Check("normalized clicks map correctly to A1, B2 and O10", () => {
            Assert(RpgMapTravel.TryGetCell(0, 0, out int c, out int r) && c == 0 && r == 1, "A1");
            Assert(RpgMapTravel.TryGetCell(1.5f / 15, 1.5f / 10, out c, out r) && c == 1 && r == 2, "B2");
            Assert(RpgMapTravel.TryGetCell(1, 1, out c, out r) && c == 14 && r == 10, "O10");
            Assert(!RpgMapTravel.TryGetCell(float.NaN, 0, out c, out r) && !RpgMapTravel.TryGetCell(1.1f, 0, out c, out r), "outside accepted");
        });
        Check("base choices never consume inventory", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 2);
            x.e.itemInteractions.Add(new ItemInteraction { item = item });
            Enter(x.s, x.m); Choose(x.s);
            Assert(x.p.CountItem(item) == 2 && !x.s.HasPendingChoice, "base choice consumed item");
        });
        Check("unsupported item is rejected without changes", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 2); Enter(x.s, x.m);
            Assert(!x.s.TryUseItem(item, out _) && x.p.CountItem(item) == 2 && !x.s.IsSpecialInteraction, "unsupported item changed state");
        });
        Check("insufficient special cost is atomic", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 1);
            x.e.itemInteractions.Add(new ItemInteraction { item = item, amount = 2 }); Enter(x.s, x.m);
            Assert(!x.s.TryUseItem(item, out _) && x.p.CountItem(item) == 1 && !x.s.IsSpecialInteraction, "partial consume");
        });
        Check("availability check never consumes item", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 1);
            x.e.itemInteractions.Add(new ItemInteraction { item = item }); Enter(x.s, x.m);
            Assert(x.s.CanUseItem(item, out _) && x.s.CanUseItem(item, out _) && x.p.CountItem(item) == 1, "preview consumed");
        });
        Check("item use enters a distinct branch without granting rewards", () => {
            var x = Setup(); var item = new RpgItem(); var reward = new RpgItem(); x.p.AddItem(item, 1);
            var branch = new ItemInteraction { item = item, title = "special", narrative = "branch text" };
            branch.choices[0].success.rewards.Add(new ItemAmount { item = reward, amount = 3 });
            x.e.itemInteractions.Add(branch); Enter(x.s, x.m);
            Assert(x.s.TryUseItem(item, out _) && x.s.IsSpecialInteraction && x.s.HasPendingChoice, "branch not entered");
            Assert(x.p.CountItem(item) == 0 && x.p.CountItem(reward) == 0, "wrong cost/reward timing");
            Assert(x.s.CurrentChoices == branch.choices && x.s.CurrentTitle == "special" && x.s.LastNarrative == "branch text", "base view still active");
            Choose(x.s); Assert(x.p.CountItem(reward) == 3 && !x.s.IsSpecialInteraction, "branch result missing");
        });
        Check("special branch cannot consume twice or switch items", () => {
            var x = Setup(); var item = new RpgItem(); var other = new RpgItem(); x.p.AddItem(item, 2); x.p.AddItem(other, 1);
            x.e.itemInteractions.Add(new ItemInteraction { item = item }); x.e.itemInteractions.Add(new ItemInteraction { item = other });
            Enter(x.s, x.m); Assert(x.s.TryUseItem(item, out _), "entry failed");
            Assert(!x.s.TryUseItem(item, out _) && !x.s.TryUseItem(other, out _), "branch switched");
            Assert(x.p.CountItem(item) == 1 && x.p.CountItem(other) == 1, "double consume");
        });
        Check("non-consumed key remains through special outcome", () => {
            var x = Setup(); var key = new RpgItem(); x.p.AddItem(key, 1);
            x.e.itemInteractions.Add(new ItemInteraction { item = key, consume = false }); Enter(x.s, x.m);
            Assert(x.s.TryUseItem(key, out _), "key rejected"); Choose(x.s);
            Assert(x.p.CountItem(key) == 1, "key consumed");
        });
        Check("special failure can be worse and still costs the item", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 1);
            var branch = new ItemInteraction { item = item };
            branch.choices[0].useStatCheck = true; branch.choices[0].minimumStat = 7;
            branch.choices[0].success.healthChange = 10; branch.choices[0].failure.healthChange = -22;
            x.e.itemInteractions.Add(branch); Enter(x.s, x.m); Assert(x.s.TryUseItem(item, out _), "entry"); Choose(x.s);
            Assert(!x.s.LastCheckSucceeded && x.p.Health == 78 && x.p.CountItem(item) == 0, "failure cost/outcome");
        });
        Check("special success can improve rewards over basic choices", () => {
            var x = Setup(); var item = new RpgItem(); var reward = new RpgItem(); x.p.AddItem(item, 1);
            var branch = new ItemInteraction { item = item };
            branch.choices[0].success.rewards.Add(new ItemAmount { item = reward, amount = 5 });
            x.e.itemInteractions.Add(branch); Enter(x.s, x.m); x.s.TryUseItem(item, out _); Choose(x.s);
            Assert(x.p.CountItem(reward) == 5, "special reward missing");
        });
        Check("special outcome completes the cell exactly once", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 2);
            x.e.itemInteractions.Add(new ItemInteraction { item = item }); Enter(x.s, x.m); x.s.TryUseItem(item, out _); Choose(x.s);
            Assert(!x.s.TryUseItem(item, out _) && !x.s.TryChoose(0, out _) && !x.s.TryEnter(x.m, 0, 1, out _), "duplicate outcome allowed");
            Assert(x.p.CountItem(item) == 1, "post-completion consume");
        });
        Check("special branch blocks travel until its choice resolves", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 1);
            x.e.itemInteractions.Add(new ItemInteraction { item = item });
            var t = new RpgMapTravel(x.s, x.m, 0, 1, true); Enter(x.s, x.m); x.s.TryUseItem(item, out _);
            Assert(!t.TryMoveTo(1, 1, out _) && t.Column == 0, "escaped branch");
            Choose(x.s); Assert(t.TryMoveTo(1, 1, out _), "travel did not unlock");
        });
        Check("invalid or duplicate item branches are rejected before costs", () => {
            var x = Setup(); var item = new RpgItem(); x.p.AddItem(item, 1);
            var branch = new ItemInteraction { item = item }; branch.choices.RemoveAt(1); x.e.itemInteractions.Add(branch);
            Assert(!x.s.TryEnter(x.m, 0, 1, out _) && x.p.CountItem(item) == 1, "one-choice branch accepted");
            branch.choices.Add(new EventChoice()); x.e.itemInteractions.Add(new ItemInteraction { item = item });
            Assert(!x.e.IsValid(out _), "duplicate item branch accepted");
        });
        Check("null item cannot open special interaction", () => {
            var x = Setup(); Enter(x.s, x.m); Assert(!x.s.TryUseItem(null, out _) && x.s.HasPendingChoice, "null item accepted");
        });
        Check("herb rewards create and accumulate a bag stack", () => {
            var x = Setup(); var herb = new RpgItem { displayName = "치유 약초" };
            x.e.choices[0].success.rewards.Add(new ItemAmount { item = herb, amount = 2 });
            Enter(x.s, x.m); Choose(x.s);
            Assert(x.p.CountItem(herb) == 2 && x.p.Inventory.Count == 1, "first herb reward missing");
            Enter(x.s, x.m, 1, 1); Choose(x.s);
            Assert(x.p.CountItem(herb) == 4 && x.p.Inventory.Count == 1, "herbs did not stack");
        });
        Console.WriteLine($"{passed} domain checks passed. Unity serialization/UI require Editor verification.");
    }
}
