using System;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LeagueSandbox.GameServerTests.Tests
{
    [TestClass]
    public class TestStatModificator
    {
        [TestMethod]
        public void TestStatModificator1()
        {
            //Create new stat modificator with all value to 0
            var statMod = new StatMod();

            //Change values
            statMod.BaseBonus = 1;
            statMod.PercentBaseBonus = 2;
            statMod.FlatBonus = 3;
            statMod.PercentBonus = 4;

            //Test values
            Assert.AreEqual(1, statMod.BaseBonus);
            Assert.AreEqual(2, statMod.PercentBaseBonus);
            Assert.AreEqual(3, statMod.FlatBonus);
            Assert.AreEqual(4, statMod.PercentBonus);

            //Create a normal stat whose base value is 0
            var stat = new Stat(0);

            //Apply the statmod to this stat
            stat.ApplyModifier(statMod);

            //Test the total value
            Assert.AreEqual(30, stat.Total);
        }
    }
}
