using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Rooms;

namespace Roughlike.Tests.EditMode
{
    /// <summary>Pure-logic tests for seeds, the RNG and level layout generation. Fast: no scene objects.</summary>
    public class GeneratorTests
    {
        static readonly GeneratorSettings Settings = new GeneratorSettings();

        // ------------------------------------------------------------------ RNG and seed codes

        [Test]
        public void RngIsDeterministicAndRangesAreRespected()
        {
            var a = new SeededRng(123);
            var b = new SeededRng(123);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextULong(), b.NextULong());

            var r = new SeededRng(7);
            for (int i = 0; i < 2000; i++)
            {
                int v = r.Range(3, 9);
                Assert.That(v, Is.InRange(3, 8));
                float f = r.Value;
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void ForkIsStableIndependentAndDoesNotAdvanceParent()
        {
            var parent = new SeededRng(99);
            ulong first = parent.Fork(1).NextULong();
            Assert.AreEqual(first, parent.Fork(1).NextULong(), "same salt, same stream");
            Assert.AreNotEqual(first, parent.Fork(2).NextULong(), "different salt, different stream");

            var other = new SeededRng(99);
            Assert.AreEqual(other.NextULong(), parent.NextULong(), "forking must not consume the parent");
        }

        [Test]
        public void SeedCodesRoundTripAndAcceptSloppyInput()
        {
            var r = new SeededRng(1);
            for (int i = 0; i < 1000; i++)
            {
                uint seed = (uint)r.NextULong();
                string code = SeedCode.Encode(seed);
                Assert.IsTrue(SeedCode.TryParse(code, out uint back));
                Assert.AreEqual(seed, back, code);
            }

            string c = SeedCode.Encode(123456789u);
            Assert.IsTrue(SeedCode.TryParse(c.ToLowerInvariant().Replace("-", " "), out uint lower));
            Assert.AreEqual(123456789u, lower);

            Assert.IsTrue(SeedCode.TryParse("drowned bells", out uint w1));
            Assert.IsTrue(SeedCode.TryParse("drowned bells", out uint w2));
            Assert.AreEqual(w1, w2, "free text hashes deterministically");
            Assert.IsFalse(SeedCode.TryParse("   ", out _));
        }

        // ------------------------------------------------------------------ layout

        [Test]
        public void SameSeedGivesTheSameLayout()
        {
            for (uint seed = 0; seed < 50; seed++)
                Assert.AreEqual(LevelGenerator.Generate(seed, Settings).Signature(), LevelGenerator.Generate(seed, Settings).Signature());
        }

        [Test]
        public void DifferentSeedsGiveVariedLayouts()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1000; seed < 1100; seed++) seen.Add(LevelGenerator.Generate(seed, Settings).Signature());
            Assert.GreaterOrEqual(seen.Count, 90, "layouts should rarely repeat");
        }

        [Test]
        public void EveryLayoutIsStructurallyValid()
        {
            for (uint seed = 0; seed < 1000; seed++)
            {
                LevelLayout l = LevelGenerator.Generate(seed * 2654435761u, Settings);
                string ctx = $"seed {l.Seed}";

                Assert.IsNotNull(l.Start, ctx);
                Assert.IsNotNull(l.Exit, ctx);
                Assert.AreEqual(RoomRole.Start, l.Start.Role, ctx);
                Assert.AreEqual(RoomRole.Exit, l.Exit.Role, ctx);
                Assert.AreEqual(0, l.Start.Pos.x, ctx);
                Assert.GreaterOrEqual(l.Exit.Pos.x - l.Start.Pos.x, Settings.minExitColumn, ctx);
                Assert.That(l.MainPathLength, Is.InRange(Settings.minMainPath, Settings.maxMainPath), ctx);

                // unique cells inside the grid
                var seen = new HashSet<Vector2Int>();
                foreach (var c in l.Cells)
                {
                    Assert.IsTrue(seen.Add(c.Pos), ctx + " duplicate cell " + c.Pos);
                    Assert.That(c.Pos.x, Is.InRange(0, Settings.width - 1), ctx);
                    Assert.That(c.Pos.y, Is.InRange(0, Settings.height - 1), ctx);
                    Assert.That(c.Variant, Is.InRange(0, Settings.variantCount - 1), ctx);
                }

                // masks equal the union of edges and edges are mutual
                foreach (var c in l.Cells)
                {
                    Openings expected = Openings.None;
                    foreach (var (a, b) in l.Edges)
                    {
                        if (a == c.Pos) expected |= LevelLayout.DirectionTo(a, b);
                        if (b == c.Pos) expected |= LevelLayout.DirectionTo(b, a);
                    }
                    Assert.AreEqual(expected, c.Mask, ctx + " mask at " + c.Pos);
                    Assert.AreNotEqual(Openings.None, c.Mask, ctx + " isolated room " + c.Pos);
                }
                foreach (var (a, b) in l.Edges)
                {
                    Assert.AreEqual(1, Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y), ctx + " edges join neighbours only");
                    Assert.IsTrue((l.Get(a).Mask & LevelLayout.DirectionTo(a, b)) != 0, ctx);
                    Assert.IsTrue((l.Get(b).Mask & LevelLayout.DirectionTo(b, a)) != 0, ctx);
                }

                // everything reachable from the start, exit included
                var reached = new HashSet<Vector2Int> { l.Start.Pos };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(l.Start.Pos);
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    foreach (var (a, b) in l.Edges)
                    {
                        if (a == p && reached.Add(b)) queue.Enqueue(b);
                        else if (b == p && reached.Add(a)) queue.Enqueue(a);
                    }
                }
                Assert.AreEqual(l.Cells.Count, reached.Count, ctx + " unreachable rooms");
                Assert.IsTrue(reached.Contains(l.Exit.Pos), ctx);

                // exactly one start and one exit; treasure rooms are dead ends off the main path
                int starts = 0, exits = 0, shops = 0;
                foreach (var c in l.Cells)
                {
                    if (c.Role == RoomRole.Start) starts++;
                    if (c.Role == RoomRole.Exit) exits++;
                    if (c.Role == RoomRole.Shop)
                    {
                        shops++;
                        Assert.IsTrue(c.OnMainPath || ((int)c.Mask & ((int)c.Mask - 1)) == 0, ctx + " shop must be a dead end or on the main path");
                    }
                    if (c.Role == RoomRole.Treasure)
                    {
                        Assert.IsFalse(c.OnMainPath, ctx);
                        int bits = 0;
                        for (int i = 0; i < 4; i++) if (((int)c.Mask & (1 << i)) != 0) bits++;
                        Assert.AreEqual(1, bits, ctx + " treasure room must be a dead end");
                    }
                }
                Assert.AreEqual(1, starts, ctx);
                Assert.AreEqual(1, exits, ctx);
                Assert.AreEqual(1, shops, ctx + " every level has exactly one merchant");
            }
        }

        [Test]
        public void LayoutsUseBranchesAndVerticalConnections()
        {
            int withBranch = 0, withVertical = 0;
            for (uint seed = 0; seed < 200; seed++)
            {
                LevelLayout l = LevelGenerator.Generate(seed, Settings);
                foreach (var c in l.Cells)
                {
                    if (!c.OnMainPath) { withBranch++; break; }
                }
                foreach (var c in l.Cells)
                {
                    if ((c.Mask & (Openings.Up | Openings.Down)) != 0) { withVertical++; break; }
                }
            }
            Assert.Greater(withBranch, 100, "most levels should have a branch");
            Assert.Greater(withVertical, 100, "most levels should have vertical rooms");
        }
    }
}
