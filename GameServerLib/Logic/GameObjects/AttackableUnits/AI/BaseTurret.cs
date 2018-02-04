using System;
using LeagueSandbox.GameServer.Logic.API;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;
using LeagueSandbox.GameServer.Logic.Items;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class BaseTurret : ObjAIBase
    {
        public string Name { get; private set; }
        protected float globalGold = 250.0f;
        protected float globalExp = 0.0f;

        public TowerStats Stats { get; protected set; }

        public BaseTurret(
            string name,
            string model,
            float x = 0,
            float y = 0,
            TeamId team = TeamId.TEAM_BLUE,
            uint netId = 0
        ) : base(model, 50, x, y, 1200, netId)
        {
            Name = name;
            SetTeam(team);
            Inventory = InventoryManager.CreateInventory(this);
            Stats = new TowerStats(this);
        }

        public override void UpdateReplication()
        {
            ReplicationManager.Update(Stats.MaxMana, 1, 0);
            ReplicationManager.Update(Stats.CurrentMana, 1, 1);
            ReplicationManager.Update((uint)Stats.ActionState, 1, 2);
            ReplicationManager.Update(Stats.MagicImmune, 1, 3);
            ReplicationManager.Update(Stats.IsInvulnerable, 1, 4);
            ReplicationManager.Update(Stats.IsPhysicalImmune, 1, 5);
            ReplicationManager.Update(Stats.IsLifestealImmune, 1, 6);
            ReplicationManager.Update(Stats.BaseAttackDamage, 1, 7);
            ReplicationManager.Update(Stats.TotalArmor, 1, 9);
            ReplicationManager.Update(Stats.SpellBlock, 1, 10);
            ReplicationManager.Update(Stats.AttackSpeedMod, 1, 11);
            ReplicationManager.Update(Stats.FlatPhysicalDamageMod, 1, 12);
            ReplicationManager.Update(Stats.PercentPhysicalDamageMod, 1, 13);
            ReplicationManager.Update(Stats.FlatMagicDamageMod, 1, 14);
            ReplicationManager.Update(Stats.PercentMagicDamageMod, 1, 15);
            ReplicationManager.Update(Stats.HealthRegenRate, 1, 16);
            ReplicationManager.Update(Stats.CurrentHealth, 4, 0);
            ReplicationManager.Update(Stats.MaxHealth, 4, 1);
            ReplicationManager.Update(Stats.FlatVisionRadiusMod, 4, 2);
            ReplicationManager.Update(Stats.PercentVisionRadiusMod, 4, 3);
            ReplicationManager.Update(Stats.TotalMovementSpeed, 4, 4);
            ReplicationManager.Update(Stats.TotalCriticalChance, 4, 5);
            ReplicationManager.Update(Stats.IsTargetable, 5, 0);
            ReplicationManager.Update((uint)Stats.IsTargetableToTeamFlags, 5, 1);
        }

        public void CheckForTargets()
        {
            var objects = _game.ObjectManager.GetObjects();
            AttackableUnit nextTarget = null;
            var nextTargetPriority = 14;

            foreach (var it in objects)
            {
                if (!(it.Value is AttackableUnit u) || u.IsDead || u.Team == Team ||
                    GetDistanceTo(u) > AttackRange.Total)
                {
                    continue;
                }

                // Note: this method means that if there are two champions within turret range,
                // The player to have been added to the game first will always be targeted before the others
                if (TargetUnit == null)
                {
                    var priority = (int)ClassifyTarget(u);
                    if (priority < nextTargetPriority)
                    {
                        nextTarget = u;
                        nextTargetPriority = priority;
                    }
                }
                else
                {
                    // Is the current target a champion? If it is, don't do anything
                    if (TargetUnit is Champion)
                    {
                        // Find the next champion in range targeting an enemy champion who is also in range
                        if (u is Champion enemyChamp && enemyChamp.TargetUnit != null)
                        {
                            if (enemyChamp.TargetUnit is Champion enemyChampTarget && // Enemy Champion is targeting an ally
                                enemyChamp.GetDistanceTo(enemyChampTarget) <= enemyChamp.AttackRange.Total && // Enemy within range of ally
                                GetDistanceTo(enemyChampTarget) <= AttackRange.Total) // Enemy within range of this turret
                            {
                                nextTarget = enemyChamp; // No priority required
                                break;
                            }
                        }
                    }
                }
            }

            if (nextTarget != null)
            {
                TargetUnit = nextTarget;
                _game.PacketNotifier.NotifySetTarget(this, nextTarget);
            }
        }

        public override void Update(float diff)
        {
            if (!IsAttacking)
            {
                CheckForTargets();
            }

            // Lose focus of the unit target if the target is out of range
            if (TargetUnit != null && GetDistanceTo(TargetUnit) > AttackRange.Total)
            {
                TargetUnit = null;
                _game.PacketNotifier.NotifySetTarget(this, null);
            }

            base.Update(diff);
        }

        public override void TakeDamage(ObjAIBase attacker, float damage, DamageType type, DamageSource source,
            DamageText damageText)
        {
            float defense = 0;

            switch (type)
            {
                case DamageType.DAMAGE_TYPE_PHYSICAL:
                    defense = Armor.Total;
                    break;
                case DamageType.DAMAGE_TYPE_MAGICAL:
                    defense = MagicResist.Total;
                    break;
                case DamageType.DAMAGE_TYPE_TRUE:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            //Damage dealing. (based on leagueoflegends' wikia)
            damage = defense >= 0 ? (100 / (100 + defense)) * damage : (2 - (100 / (100 - defense))) * damage;

            if (HasCrowdControl(CrowdControlType.Invulnerable))
            {
                var attackerIsFountainTurret = false;

                if (attacker is LaneTurret laneTurret)
                {
                    attackerIsFountainTurret = laneTurret.Type == TurretType.FountainTurret;
                }

                if (!attackerIsFountainTurret)
                {
                    damage = 0;
                    damageText = DamageText.DAMAGE_TEXT_INVULNERABLE;
                }
            }

            ApiEventManager.OnUnitDamageTaken.Publish(this);

            HealthPoints.Current = Math.Max(0.0f, HealthPoints.Current - damage);
            if (!IsDead && HealthPoints.Current <= 0)
            {
                IsDead = true;
                Die(attacker);
            }

            _game.PacketNotifier.NotifyDamageDone(attacker, this, damage, type, damageText);
        }

        public override void Die(AttackableUnit killer)
        {
            foreach (var player in _game.ObjectManager.GetAllChampionsFromTeam(killer.Team))
            {
                var goldEarn = globalGold;

                // Champions in Range within TURRET_RANGE * 1.5f will gain 150% more (obviously)
                if (player.GetDistanceTo(this) <= AttackRange.Total * 1.5f && !player.IsDead)
                {
                    goldEarn = globalGold * 2.5f;
                    if (globalExp > 0)
                    {
                        player.Stats.Experience += globalExp;
                    }
                }


                player.Stats.Gold += goldEarn;
                player.Stats.TotalGold += goldEarn;
                _game.PacketNotifier.NotifyAddGold(player, this, goldEarn);
            }

            _game.PacketNotifier.NotifyUnitAnnounceEvent(UnitAnnounces.TurretDestroyed, this, killer);
            base.Die(killer);
        }

        public override void RefreshWaypoints()
        {
        }
    }
}
