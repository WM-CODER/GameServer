using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LeagueSandbox.GameServer.Core.Logic;
using LeagueSandbox.GameServer.Logic.API;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;
using LeagueSandbox.GameServer.Logic.Items;
using LeagueSandbox.GameServer.Logic.Players;
using LeagueSandbox.GameServer.Logic.Scripting.CSharp;

namespace LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits
{
    public class AttackableUnit : GameObject
    {
        internal const float DETECT_RANGE = 475.0f;
        internal const int EXP_RANGE = 1400;
        internal const long UPDATE_TIME = 500;

        public InventoryManager Inventory { get; protected set; }
        protected ItemManager _itemManager = Program.ResolveDependency<ItemManager>();
        protected PlayerManager _playerManager = Program.ResolveDependency<PlayerManager>();

        protected Random _random = new Random();

        public CharData CharData { get; protected set; }
        public SpellData AASpellData { get; protected set; }
        public float AutoAttackDelay { get; set; }
        public float AutoAttackProjectileSpeed { get; set; }
        public ReplicationManager ReplicationManager { get; private set; }
        public bool IsAttacking { protected get; set; }
        public bool IsModelUpdated { get; set; }
        public bool IsMelee { get; set; }
        protected internal bool _hasMadeInitialAttack;
        public AttackableUnit DistressCause { get; protected set; }
        private float _statUpdateTimer;
        public MoveOrder MoveOrder { get; set; }

        public Health HealthPoints { get; set; } = new Health(0);
        public Health ManaPoints { get; set; } = new Health(0);
        public bool IsInvulnerable { get; set; }
        public bool IsPhysicalImmune { get; set; }
        public bool IsMagicImmune { get; set; }
        public bool IsTargetable { get; set; } = true;
        public IsTargetableToTeamFlags IsTargetableToTeam { get; set; } =
            IsTargetableToTeamFlags.TargetableToAll;

        /// <summary>
        /// Unit we want to attack as soon as in range
        /// </summary>
        public AttackableUnit TargetUnit { get; set; }
        public AttackableUnit AutoAttackTarget { get; set; }

        public bool IsDead { get; protected set; }

        private string _model;
        public string Model
        {
            get => _model;
            set
            {
                _model = value;
                IsModelUpdated = true;
            }
        }

        protected CSharpScriptEngine _scriptEngine = Program.ResolveDependency<CSharpScriptEngine>();
        protected Logger _logger = Program.ResolveDependency<Logger>();

        public int KillDeathCounter { get; protected set; }

        private float _timerUpdate;

        public bool IsCastingSpell { get; set; }

        private List<UnitCrowdControl> _crowdControlList = new List<UnitCrowdControl>();

        public AttackableUnit(
            string model,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(x, y, collisionRadius, visionRadius, netId)
        {
            Model = model;
            CharData = _game.Config.ContentManager.GetCharData(Model);
            AutoAttackDelay = 0;
            AutoAttackProjectileSpeed = 500;
            IsMelee = CharData.IsMelee;
            ReplicationManager = new ReplicationManager();

            if (CharData.PathfindingCollisionRadius > 0)
            {
                CollisionRadius = CharData.PathfindingCollisionRadius;
            }
            else if (collisionRadius > 0)
            {
                CollisionRadius = collisionRadius;
            }
            else
            {
                CollisionRadius = 40;
            }
        }

        public override void OnAdded()
        {
            base.OnAdded();
            _game.ObjectManager.AddVisionUnit(this);
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
            _game.ObjectManager.RemoveVisionUnit(this);
        }

        public void ApplyCrowdControl(UnitCrowdControl cc)
        {
            if (cc.IsTypeOf(CrowdControlType.Stun) || cc.IsTypeOf(CrowdControlType.Root))
            {
                StopMovement();
            }

            _crowdControlList.Add(cc);
        }

        public void RemoveCrowdControl(UnitCrowdControl cc)
        {
            _crowdControlList.Remove(cc);
        }

        public void ClearAllCrowdControl()
        {
            _crowdControlList.Clear();
        }

        public bool HasCrowdControl(CrowdControlType ccType)
        {
            return _crowdControlList.FirstOrDefault(cc => cc.IsTypeOf(ccType)) != null;
        }

        public void StopMovement()
        {
            var position = GetPosition();
            SetWaypoints(new List<Vector2> { position, position });
        }

        public override void Update(float diff)
        {
            _timerUpdate += diff;
            if (_timerUpdate >= UPDATE_TIME)
            {
                _timerUpdate = 0;
            }

            foreach (var cc in _crowdControlList)
            {
                cc.Update(diff);
            }

            _crowdControlList.RemoveAll(cc => cc.IsDead());

            var onUpdate = _scriptEngine.GetStaticMethod<Action<AttackableUnit, double>>(Model, "Passive", "OnUpdate");
            onUpdate?.Invoke(this, diff);

            base.Update(diff);

            _statUpdateTimer += diff;

            if (_statUpdateTimer >= 500)
            { // Update Stats (hpregen, manaregen) every 0.5 seconds
                UpdateReplication();
                _statUpdateTimer = 0;
            }
        }

        public virtual void UpdateReplication()
        {

        }

        public virtual bool GetTargetableToTeam(TeamId team)
        {
            if (IsTargetableToTeam.HasFlag(IsTargetableToTeamFlags.TargetableToAll))
            {
                return true;
            }

            if (team == TeamId.TEAM_NEUTRAL)
            {
                return true;
            }

            if (!IsTargetable)
            {
                return false;
            }

            if (team == Team)
            {
                return !IsTargetableToTeam.HasFlag(IsTargetableToTeamFlags.NonTargetableAlly);
            }

            return !IsTargetableToTeam.HasFlag(IsTargetableToTeamFlags.NonTargetableEnemy);
        }

        public virtual void SetTargetableToTeam(TeamId team, bool targetable)
        {
            var dictionary = new Dictionary<TeamId, bool>
            {
                {TeamId.TEAM_NEUTRAL, true},
                {TeamId.TEAM_BLUE, GetTargetableToTeam(TeamId.TEAM_BLUE)},
                {TeamId.TEAM_PURPLE, GetTargetableToTeam(TeamId.TEAM_PURPLE)}
            };

            dictionary[team] = targetable;

            IsTargetableToTeam = 0;
            if (dictionary[TeamId.TEAM_BLUE] && dictionary[TeamId.TEAM_PURPLE])
            {
                IsTargetableToTeam = IsTargetableToTeamFlags.TargetableToAll;
            }
            else
            {
                if (!dictionary[Team])
                {
                    IsTargetableToTeam |= IsTargetableToTeamFlags.NonTargetableAlly;
                }

                if (!dictionary[CustomConvert.GetEnemyTeam(Team)])
                {
                    IsTargetableToTeam |= IsTargetableToTeamFlags.NonTargetableEnemy;
                }
            }
        }

        public override void OnCollision(GameObject collider)
        {
            base.OnCollision(collider);
            if (collider == null)
            {
                var onCollideWithTerrain = _scriptEngine.GetStaticMethod<Action<AttackableUnit>>(Model, "Passive", "onCollideWithTerrain");
                onCollideWithTerrain?.Invoke(this);
            }
            else
            {
                var onCollide = _scriptEngine.GetStaticMethod<Action<AttackableUnit, AttackableUnit>>(Model, "Passive", "onCollide");
                onCollide?.Invoke(this, collider as AttackableUnit);
            }
        }
        
        public virtual void Die(AttackableUnit killer)
        {
            SetToRemove = true;
            _game.ObjectManager.StopTargeting(this);

            _game.PacketNotifier.NotifyNpcDie(this, killer);

            var onDie = _scriptEngine.GetStaticMethod<Action<AttackableUnit, AttackableUnit>>(Model, "Passive", "OnDie");
            onDie?.Invoke(this, killer);

            var exp = _game.Map.MapGameScript.GetExperienceFor(this);
            var champs = _game.ObjectManager.GetChampionsInRange(this, EXP_RANGE, true);
            //Cull allied champions
            champs.RemoveAll(l => l.Team == Team);

            if (champs.Count > 0)
            {
                var expPerChamp = exp / champs.Count;
                foreach (var c in champs)
                {
                    c.Stats.Experience += expPerChamp;
                    _game.PacketNotifier.NotifyAddXP(c, expPerChamp);
                }
            }

            if (killer != null)
            {
                if (!(killer is Champion cKiller))
                {
                    return;
                }

                var gold = _game.Map.MapGameScript.GetGoldFor(this);
                if (gold <= 0)
                {
                    return;
                }

                cKiller.Stats.Gold += gold;
                cKiller.Stats.TotalGold += gold;
                _game.PacketNotifier.NotifyAddGold(cKiller, this, gold);

                if (cKiller.KillDeathCounter < 0)
                {
                    cKiller.ChampionGoldFromMinions += gold;
                    _logger.LogCoreInfo($"Adding gold form minions to reduce death spree: {cKiller.ChampionGoldFromMinions}");
                }

                if (cKiller.ChampionGoldFromMinions >= 50 && cKiller.KillDeathCounter < 0)
                {
                    cKiller.ChampionGoldFromMinions = 0;
                    cKiller.KillDeathCounter += 1;
                }
            }

            if (IsDashing)
            {
                IsDashing = false;
            }
        }

        public virtual bool IsInDistress()
        {
            return false; //return DistressCause;
        }

        public void SetTargetUnit(AttackableUnit target)
        {
            if (target == null) // If we are unsetting the target (moving around)
            {
                if (TargetUnit != null) // and we had a target
                {
                    TargetUnit.DistressCause = null; // Unset the distress call
                }
                // TODO: Replace this with a delay?

                IsAttacking = false;
            }
            else
            {
                target.DistressCause = this; // Otherwise set the distress call
            }

            TargetUnit = target;
            RefreshWaypoints();
        }

        public virtual void RefreshWaypoints()
        {
            
        }

        public ClassifyUnit ClassifyTarget(AttackableUnit target)
        {
            if (target.TargetUnit != null && target.TargetUnit.IsInDistress()) // If an ally is in distress, target this unit. (Priority 1~5)
            {
                if (target is Champion && target.TargetUnit is Champion) // If it's a champion attacking an allied champion
                {
                    return ClassifyUnit.ChampionAttackingChampion;
                }

                if (target is Minion && target.TargetUnit is Champion) // If it's a minion attacking an allied champion.
                {
                    return ClassifyUnit.MinionAttackingChampion;
                }

                if (target is Minion && target.TargetUnit is Minion) // Minion attacking minion
                {
                    return ClassifyUnit.MinionAttackingMinion;
                }

                if (target is BaseTurret && target.TargetUnit is Minion) // Turret attacking minion
                {
                    return ClassifyUnit.TurretAttackingMinion;
                }

                if (target is Champion && target.TargetUnit is Minion) // Champion attacking minion
                {
                    return ClassifyUnit.ChampionAttackingMinion;
                }
            }

            if (target is Placeable)
            {
                return ClassifyUnit.Placeable;
            }

            if (target is Minion m)
            {
                switch (m.getType())
                {
                    case MinionSpawnType.MINION_TYPE_MELEE:
                        return ClassifyUnit.MeleeMinion;
                    case MinionSpawnType.MINION_TYPE_CASTER:
                        return ClassifyUnit.CasterMinion;
                    case MinionSpawnType.MINION_TYPE_CANNON:
                    case MinionSpawnType.MINION_TYPE_SUPER:
                        return ClassifyUnit.SuperOrCannonMinion;
                }
            }

            if (target is BaseTurret)
            {
                return ClassifyUnit.Turret;
            }

            if (target is Champion)
            {
                return ClassifyUnit.Champion;
            }

            if (target is Inhibitor && !target.IsDead)
            {
                return ClassifyUnit.Inhibitor;
            }

            if (target is Nexus)
            {
                return ClassifyUnit.Nexus;
            }

            return ClassifyUnit.Default;
        }

        public virtual void TakeDamage(ObjAIBase attacker, float damage, DamageType type, DamageSource source,
            DamageText damageText)
        {
            if (HasCrowdControl(CrowdControlType.Invulnerable))
            {
                damage = 0;
                damageText = DamageText.DAMAGE_TEXT_INVULNERABLE;
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

        public virtual void TakeDamage(ObjAIBase attacker, float damage, DamageType type, DamageSource source,
            bool isCrit)
        {
            var text = DamageText.DAMAGE_TEXT_NORMAL;

            if (isCrit)
            {
                text = DamageText.DAMAGE_TEXT_CRITICAL;
            }

            TakeDamage(attacker, damage, type, source, text);
        }
    }

    public enum DamageType : byte
    {
        DAMAGE_TYPE_PHYSICAL = 0x0,
        DAMAGE_TYPE_MAGICAL = 0x1,
        DAMAGE_TYPE_TRUE = 0x2
    }

    public enum DamageText : byte
    {
        DAMAGE_TEXT_INVULNERABLE = 0x0,
        DAMAGE_TEXT_DODGE = 0x2,
        DAMAGE_TEXT_CRITICAL = 0x3,
        DAMAGE_TEXT_NORMAL = 0x4,
        DAMAGE_TEXT_MISS = 0x5
    }

    public enum DamageSource
    {
        DAMAGE_SOURCE_ATTACK,
        DAMAGE_SOURCE_SPELL,
        DAMAGE_SOURCE_SUMMONER_SPELL, // Ignite shouldn't destroy Banshee's
        DAMAGE_SOURCE_PASSIVE // Red/Thornmail shouldn't as well
    }

    public enum AttackType : byte
    {
        ATTACK_TYPE_RADIAL,
        ATTACK_TYPE_MELEE,
        ATTACK_TYPE_TARGETED
    }

    public enum MoveOrder
    {
        MOVE_ORDER_MOVE,
        MOVE_ORDER_ATTACKMOVE
    }

    public enum ShieldType : byte
    {
        GreenShield = 0x01,
        MagicShield = 0x02,
        NormalShield = 0x03
    }

    public enum UnitAnnounces : byte
    {
        Death = 0x04,
        InhibitorDestroyed = 0x1F,
        InhibitorAboutToSpawn = 0x20,
        InhibitorSpawned = 0x21,
        TurretDestroyed = 0x24,
        SummonerDisconnected = 0x47,
        SummonerReconnected = 0x48
    }

    public enum ClassifyUnit
    {
        ChampionAttackingChampion = 1,
        MinionAttackingChampion = 2,
        MinionAttackingMinion = 3,
        TurretAttackingMinion = 4,
        ChampionAttackingMinion = 5,
        Placeable = 6,
        MeleeMinion = 7,
        CasterMinion = 8,
        SuperOrCannonMinion = 9,
        Turret = 10,
        Champion = 11,
        Inhibitor = 12,
        Nexus = 13,
        Default = 14
    }
}
