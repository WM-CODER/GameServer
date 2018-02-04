using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Stats
{
    public class ChampionStatMod
    {
        public StatMod HealthPoints { get; set; } = new StatMod();
        public StatMod HealthRegeneration { get; set; } = new StatMod();
        public StatMod AttackDamage { get; set; } = new StatMod();
        public StatMod AbilityPower { get; set; } = new StatMod();
        public StatMod CriticalChance { get; set; } = new StatMod();
        public StatMod Armor { get; set; } = new StatMod();
        public StatMod MagicResist { get; set; } = new StatMod();
        public StatMod AttackSpeed { get; set; } = new StatMod();
        public StatMod ArmorPenetration { get; set; } = new StatMod();
        public StatMod MagicPenetration { get; set; } = new StatMod();
        public StatMod ManaPoints { get; set; } = new StatMod();
        public StatMod ManaRegeneration { get; set; } = new StatMod();
        public StatMod LifeSteal { get; set; } = new StatMod();
        public StatMod SpellVamp { get; set; } = new StatMod();
        public StatMod Tenacity { get; set; } = new StatMod();
        public StatMod Size { get; set; } = new StatMod();
        public StatMod AttackRange { get; set; } = new StatMod();
        public StatMod MovementSpeed { get; set; } = new StatMod();
        public StatMod GoldPerSecond { get; set; } = new StatMod();
        public StatMod CriticalDamage { get; set; } = new StatMod();
        public StatMod CooldownReduction { get; set; } = new StatMod();
    }
}
