namespace LeagueSandbox.GameServer.Logic.GameObjects.Stats
{
    public class StatMod
    {
        /// <summary>
        /// Flat base mod. Calculated before everything else.
        /// </summary>
        public virtual float BaseBonus { get; set; }

        /// <summary>
        /// Percent base mod. Calculated after flat base mod. Should be between 0-1. A value of 0.1 means 10%.
        /// </summary>
        public virtual float PercentBaseBonus { get; set; }

        /// <summary>
        /// Flat bonus mod. Calculated after percent base mod.
        /// </summary>
        public virtual float FlatBonus { get; set; }

        /// <summary>
        /// Percent bonus mod. Calculated after everything else. A value of 0.1 means 10%.
        /// </summary>
        public virtual float PercentBonus { get; set; }
    }
}
