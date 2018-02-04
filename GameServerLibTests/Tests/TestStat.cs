using System;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LeagueSandbox.GameServerTests.Tests
{
    [TestClass]
    public class TestStat
    {
        [TestMethod]
        public void TestTotal()
        {
            //Create new Stat object with everything set to 0
            var stat = new Stat();
            //Make sure total is equal to 0
            Assert.AreEqual(0, stat.Total);
            
            //Set base value to 1
            stat = new Stat(1);
            Assert.AreEqual(1, stat.Total);
            
            //Add 1 to modifed base value
            stat.BaseBonus += 1;
            Assert.AreEqual(2, stat.Total);

            stat.PercentBaseBonus = 1.0f;
            Assert.AreEqual(4, stat.Total);

            //Add 1 to bonus value
            stat.FlatBonus += 1;
            Assert.AreEqual(5, stat.Total);

            //Add set to 100% bonus value
            stat.PercentBonus = 1.0f;
            Assert.AreEqual(10, stat.Total);
            
            //Reset everything to 0
            stat = new Stat();

            Assert.AreEqual(0, stat.Total);

            //Set stat to a basic attack speed value
            stat = new Stat(0.625f, 0.2f, 2.5f);
            Assert.AreEqual(0.625f, stat.Total);

            //Subtract 0.6 from base value
            stat.BaseBonus -= 0.6f;
            Assert.AreEqual(0.2f, stat.Total);

            //Reset base value and set percent bonus to 500%
            stat.BaseBonus = 0;
            stat.PercentBaseBonus = 5.0f;
            Assert.AreEqual(2.5f, stat.Total);

            // reset percent bonus
            stat.PercentBaseBonus = 0;
            Assert.AreEqual(0.625f, stat.Total);
        }
    }
}
