using NUnit.Framework;
using Roughlike.Core;

namespace Roughlike.Tests.EditMode
{
    public class StatSheetTests
    {
        [Test]
        public void DefaultsAreNeutral()
        {
            var s = new StatSheet();
            Assert.AreEqual(100f, s.Get(StatKind.MaxHealth));
            Assert.AreEqual(1f, s.Get(StatKind.DamageDealt));
            Assert.AreEqual(1f, s.Get(StatKind.DamageTaken));
            Assert.AreEqual(0.05f, s.Get(StatKind.CritChance), 0.0001f);
            Assert.AreEqual(1.5f, s.Get(StatKind.CritDamage));
        }

        [Test]
        public void FlatThenAdditivePercentThenMultiplicative()
        {
            var s = new StatSheet();
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.Flat, 20f));          // 120
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.PercentAdd, 0.10f));  // +10%
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.PercentAdd, 0.15f));  // +15%  => x1.25 = 150
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.PercentMult, 0.20f)); // x1.2 = 180
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.PercentMult, 0.50f)); // x1.5 = 270
            Assert.AreEqual(270f, s.Get(StatKind.MaxHealth), 0.001f);
        }

        [Test]
        public void ModifiersRemoveTogetherBySource()
        {
            var s = new StatSheet();
            var sword = new object();
            var amulet = new object();
            s.Add(new StatModifier(StatKind.DamageDealt, ModOp.PercentAdd, 0.2f, sword));
            s.Add(new StatModifier(StatKind.MoveSpeed, ModOp.PercentAdd, 0.1f, sword));
            s.Add(new StatModifier(StatKind.DamageDealt, ModOp.PercentAdd, 0.1f, amulet));
            Assert.AreEqual(1.3f, s.Get(StatKind.DamageDealt), 0.0001f);

            Assert.AreEqual(2, s.RemoveBySource(sword));
            Assert.AreEqual(1.1f, s.Get(StatKind.DamageDealt), 0.0001f);
            Assert.AreEqual(1f, s.Get(StatKind.MoveSpeed), 0.0001f);
        }

        [Test]
        public void ValuesAreClampedToSaneRanges()
        {
            var s = new StatSheet();
            s.Add(new StatModifier(StatKind.CritChance, ModOp.Flat, 5f));
            s.Add(new StatModifier(StatKind.DamageTaken, ModOp.PercentAdd, -5f));
            s.Add(new StatModifier(StatKind.MaxHealth, ModOp.Flat, -500f));
            Assert.AreEqual(1f, s.Get(StatKind.CritChance));
            Assert.AreEqual(0.1f, s.Get(StatKind.DamageTaken), 0.0001f);
            Assert.AreEqual(10f, s.Get(StatKind.MaxHealth));
        }

        [Test]
        public void ChangedFiresOnEveryEdit()
        {
            var s = new StatSheet();
            int fired = 0;
            s.Changed += () => fired++;
            var src = new object();
            s.Add(new StatModifier(StatKind.GoldFind, ModOp.PercentAdd, 0.3f, src));
            s.RemoveBySource(src);
            s.SetBase(StatKind.MaxHealth, 120f);
            Assert.AreEqual(3, fired);
            Assert.AreEqual(120f, s.Get(StatKind.MaxHealth));
        }
    }
}
