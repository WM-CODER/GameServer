using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.API;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;
using LeagueSandbox.GameServer.Logic.Scripting.CSharp;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class ObjAIBase : AttackableUnit
    {
        private Buff[] AppliedBuffs { get; }
        private List<BuffGameScriptController> BuffGameScriptControllers { get; }
        private object BuffsLock { get; }
        private Dictionary<string, Buff> Buffs { get; }
        public int AttackerCount { get; protected set; }
        protected float _autoAttackCurrentCooldown;
        protected float _autoAttackCurrentDelay;
        protected uint _autoAttackProjId;
        protected bool _isNextAutoCrit;
        protected bool _nextAttackFlag;

        public Stat MovementSpeed { get; set; } = new Stat(0, 0);
        public Stat AttackSpeed { get; set; } = new Stat(0.625f, 0.2f, 2.5f);
        public Stat AttackRange { get; set; } = new Stat(0, 0);
        public Stat CriticalChance { get; set; } = new Stat(0, 0, 1);
        public Stat CriticalDamage { get; set; } = new Stat(2);
        public Stat AttackDamage { get; set; } = new Stat(0, 0);
        public Stat Armor { get; set; } = new Stat();
        public Stat ArmorPenetration { get; set; } = new Stat();
        public Stat MagicResist { get; set; } = new Stat();
        public Stat MagicPenetration { get; set; } = new Stat();
        public Stat LifeSteal { get; set; } = new Stat();
        public Stat SpellVamp { get; set; } = new Stat();
        public Stat AbilityPower { get; set; } = new Stat();
        public Stat DodgeChance { get; set; } = new Stat(0, 0, 1);
        public Stat HealthRegeneration { get; set; } = new Stat(0, 0);
        public Stat ManaRegeneration { get; set; } = new Stat();
        public Stat CooldownReduction { get; set; } = new Stat(0, 0, 0.4f);
        public Stat Tenacity { get; set; } = new Stat(0, 0, 1);
        public Stat VisionRange { get; set; } = new Stat(0, 0);
        public Stat PathfindingRadius { get; set; } = new Stat(0, 0);
        public Stat Size { get; set; } = new Stat(1, 0);
        
        public ObjAIBase(string model, int collisionRadius = 40,
            float x = 0, float y = 0, int visionRadius = 0, uint netId = 0) :
            base(model, collisionRadius, x, y, visionRadius, netId)
        {
            if (!string.IsNullOrEmpty(model))
            {
                AASpellData = _game.Config.ContentManager.GetSpellData(model + "BasicAttack");
                AutoAttackDelay = AASpellData.CastFrame / 30.0f;
                AutoAttackProjectileSpeed = AASpellData.MissileSpeed;
            }
            
            AppliedBuffs = new Buff[256];
            BuffGameScriptControllers = new List<BuffGameScriptController>();
            BuffsLock = new object();
            Buffs = new Dictionary<string, Buff>();
        }
        
        public BuffGameScriptController AddBuffGameScript(string buffNamespace, string buffClass, Spell ownerSpell, float removeAfter = -1f, bool isUnique = false)
        {
            if (isUnique)
            {
                RemoveBuffGameScriptsWithName(buffNamespace, buffClass);
            }

            var buffController = 
                new BuffGameScriptController(this, buffNamespace, buffClass, ownerSpell, removeAfter);
            BuffGameScriptControllers.Add(buffController);
            buffController.ActivateBuff();

            return buffController;
        }

        public void IncrementAttackerCount()
        {
            ++AttackerCount;
        }

        public void DecrementAttackerCount()
        {
            --AttackerCount;
        }

        public override void Move(float diff)
        {
            if (Target == null)
            {
                _direction = new Vector2();
                return;
            }

            var to = new Vector2(Target.X, Target.Y);
            var cur = new Vector2(X, Y); //?

            var goingTo = to - cur;
            _direction = Vector2.Normalize(goingTo);
            if (float.IsNaN(_direction.X) || float.IsNaN(_direction.Y))
            {
                _direction = new Vector2(0, 0);
            }

            var moveSpeed = MovementSpeed.Total;
            if (IsDashing)
            {
                moveSpeed = _dashSpeed;
            }

            var deltaMovement = moveSpeed * 0.001f * diff;

            var xx = _direction.X * deltaMovement;
            var yy = _direction.Y * deltaMovement;

            X += xx;
            Y += yy;

            // If the target was a simple point, stop when it is reached

            if (GetDistanceTo(Target) < deltaMovement * 2)
            {
                if (IsDashing)
                {
                    if (this is AttackableUnit u)
                    {
                        var animList = new List<string>();
                        _game.PacketNotifier.NotifySetAnimation(u, animList);
                    }

                    Target = null;
                }
                else if (++CurWaypoint >= Waypoints.Count)
                {
                    Target = null;
                }
                else
                {
                    Target = new Target(Waypoints[CurWaypoint]);
                }

                if (IsDashing)
                {
                    IsDashing = false;
                }
            }
        }

        public override void RefreshWaypoints()
        {
            if (TargetUnit == null || (GetDistanceTo(TargetUnit) <= AttackRange.Total && Waypoints.Count == 1))
            {
                return;
            }

            if (GetDistanceTo(TargetUnit) <= AttackRange.Total - 2.0f)
            {
                SetWaypoints(new List<Vector2> { new Vector2(X, Y) });
            }
            else
            {
                var t = new Target(Waypoints[Waypoints.Count - 1]);
                if (t.GetDistanceTo(TargetUnit) >= 25.0f)
                {
                    SetWaypoints(new List<Vector2> { new Vector2(X, Y), new Vector2(TargetUnit.X, TargetUnit.Y) });
                }
            }
        }

        public override void TakeDamage(ObjAIBase attacker, float damage, DamageType type, DamageSource source,
            DamageText damageText)
        {
            float defense = 0;
            float regain = 0;

            switch (type)
            {
                case DamageType.DAMAGE_TYPE_PHYSICAL:
                    defense = Armor.Total;
                    defense -= defense > 0 ? attacker.ArmorPenetration.PercentBonus * defense : 0;
                    defense = Math.Max(0, defense - attacker.ArmorPenetration.FlatBonus);
                    break;
                case DamageType.DAMAGE_TYPE_MAGICAL:
                    defense = MagicResist.Total;
                    defense -= defense > 0 ? attacker.MagicPenetration.PercentBonus * defense : 0;
                    defense = Math.Max(0, defense - attacker.MagicPenetration.FlatBonus);
                    break;
                case DamageType.DAMAGE_TYPE_TRUE:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            switch (source)
            {
                case DamageSource.DAMAGE_SOURCE_SPELL:
                    regain = attacker.SpellVamp.Total;
                    break;
                case DamageSource.DAMAGE_SOURCE_ATTACK:
                    regain = attacker.LifeSteal.Total;
                    break;
                case DamageSource.DAMAGE_SOURCE_SUMMONER_SPELL:
                    break;
                case DamageSource.DAMAGE_SOURCE_PASSIVE:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
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

            // Get health from lifesteal/spellvamp
            if (regain > 0)
            {
                attacker.HealthPoints.Current = Math.Min(HealthPoints.Total,
                    attacker.HealthPoints.Current + regain * damage);
            }
        }

        public override void SetWaypoints(List<Vector2> newWaypoints)
        {
            Waypoints = newWaypoints;

            SetPosition(Waypoints[0].X, Waypoints[0].Y);
            _movementUpdated = true;
            if (Waypoints.Count == 1)
            {
                Target = null;
                return;
            }

            Target = new Target(Waypoints[1]);
            CurWaypoint = 1;
        }

        public override bool IsMovementUpdated()
        {
            return _movementUpdated;
        }

        public override void ClearMovementUpdated()
        {
            _movementUpdated = false;
        }

        public void UpdateAutoAttackTarget(float diff)
        {
            if (HasCrowdControl(CrowdControlType.Disarm) || HasCrowdControl(CrowdControlType.Stun))
            {
                return;
            }

            if (IsDead)
            {
                if (TargetUnit != null)
                {
                    SetTargetUnit(null);
                    AutoAttackTarget = null;
                    IsAttacking = false;
                    _game.PacketNotifier.NotifySetTarget(this, null);
                    _hasMadeInitialAttack = false;
                }
                return;
            }

            if (TargetUnit != null)
            {
                if (TargetUnit.IsDead || !_game.ObjectManager.TeamHasVisionOn(Team, TargetUnit))
                {
                    SetTargetUnit(null);
                    IsAttacking = false;
                    _game.PacketNotifier.NotifySetTarget(this, null);
                    _hasMadeInitialAttack = false;

                }
                else if (IsAttacking && AutoAttackTarget != null)
                {
                    _autoAttackCurrentDelay += diff / 1000.0f;
                    if (_autoAttackCurrentDelay >= AutoAttackDelay / AttackSpeed.PercentBonus)
                    {
                        if (!IsMelee)
                        {
                            var p = new Projectile(
                                X,
                                Y,
                                5,
                                this,
                                AutoAttackTarget,
                                null,
                                AutoAttackProjectileSpeed,
                                "",
                                0,
                                _autoAttackProjId
                            );
                            _game.ObjectManager.AddObject(p);
                            _game.PacketNotifier.NotifyShowProjectile(p);
                        }
                        else
                        {
                            AutoAttackHit(AutoAttackTarget);
                        }
                        _autoAttackCurrentCooldown = 1.0f / AttackSpeed.Total;
                        IsAttacking = false;
                    }

                }
                else if (GetDistanceTo(TargetUnit) <= AttackRange.Total)
                {
                    RefreshWaypoints();
                    _isNextAutoCrit = _random.Next(0, 100) < CriticalChance.Total * 100;
                    if (_autoAttackCurrentCooldown <= 0)
                    {
                        IsAttacking = true;
                        _autoAttackCurrentDelay = 0;
                        _autoAttackProjId = _networkIdManager.GetNewNetID();
                        AutoAttackTarget = TargetUnit;

                        if (!_hasMadeInitialAttack)
                        {
                            _hasMadeInitialAttack = true;
                            _game.PacketNotifier.NotifyBeginAutoAttack(
                                this,
                                TargetUnit,
                                _autoAttackProjId,
                                _isNextAutoCrit
                            );
                        }
                        else
                        {
                            _nextAttackFlag = !_nextAttackFlag; // The first auto attack frame has occurred
                            _game.PacketNotifier.NotifyNextAutoAttack(
                                this,
                                TargetUnit,
                                _autoAttackProjId,
                                _isNextAutoCrit,
                                _nextAttackFlag
                                );
                        }

                        var attackType = IsMelee ? AttackType.ATTACK_TYPE_MELEE : AttackType.ATTACK_TYPE_TARGETED;
                        _game.PacketNotifier.NotifyOnAttack(this, TargetUnit, attackType);
                    }

                }
                else
                {
                    RefreshWaypoints();
                }

            }
            else if (IsAttacking)
            {
                if (AutoAttackTarget == null
                    || AutoAttackTarget.IsDead
                    || !_game.ObjectManager.TeamHasVisionOn(Team, AutoAttackTarget)
                )
                {
                    IsAttacking = false;
                    _hasMadeInitialAttack = false;
                    AutoAttackTarget = null;
                }
            }

            if (_autoAttackCurrentCooldown > 0)
            {
                _autoAttackCurrentCooldown -= diff / 1000.0f;
            }
        }

        /// <summary>
        /// This is called by the AA projectile when it hits its target
        /// </summary>
        public virtual void AutoAttackHit(AttackableUnit target)
        {
            if (HasCrowdControl(CrowdControlType.Blind)) {
                target.TakeDamage(this, 0, DamageType.DAMAGE_TYPE_PHYSICAL,
                    DamageSource.DAMAGE_SOURCE_ATTACK,
                    DamageText.DAMAGE_TEXT_MISS);
                return;
            }

            var damage = AttackDamage.Total;
            if (_isNextAutoCrit)
            {
                damage *= CriticalDamage.Total;
            }

            var onAutoAttack = _scriptEngine.GetStaticMethod<Action<AttackableUnit, AttackableUnit>>(Model, "Passive", "OnAutoAttack");
            onAutoAttack?.Invoke(this, target);

            target.TakeDamage(this, damage, DamageType.DAMAGE_TYPE_PHYSICAL,
                DamageSource.DAMAGE_SOURCE_ATTACK,
                _isNextAutoCrit);
        }

        public void RemoveBuffGameScript(BuffGameScriptController buffController)
        {
            buffController.DeactivateBuff();
            BuffGameScriptControllers.Remove(buffController);
        }

        public bool HasBuffGameScriptActive(string buffNamespace, string buffClass)
        {
            foreach (var b in BuffGameScriptControllers)
            {
                if (b.IsBuffSame(buffNamespace, buffClass)) return true;
            }
            return false;
        }

        public void RemoveBuffGameScriptsWithName(string buffNamespace, string buffClass)
        {
            foreach (var b in BuffGameScriptControllers)
            {
                if (b.IsBuffSame(buffNamespace, buffClass)) b.DeactivateBuff();
            }
            BuffGameScriptControllers.RemoveAll((b) => b.NeedsRemoved());
        }

        public List<BuffGameScriptController> GetBuffGameScriptController()
        {
            return BuffGameScriptControllers;
        }
        
        public Dictionary<string, Buff> GetBuffs()
        {
            var toReturn = new Dictionary<string, Buff>();
            lock (BuffsLock)
            {
                foreach (var buff in Buffs)
                    toReturn.Add(buff.Key, buff.Value);

                return toReturn;
            }
        }
        
        public int GetBuffsCount()
        {
            return Buffs.Count;
        }
        
        //todo: use statmods
        public Buff GetBuff(string name)
        {
            lock (BuffsLock)
            {
                if (Buffs.ContainsKey(name))
                    return Buffs[name];
                return null;
            }
        }
        
        public void AddBuff(Buff b)
        {
            lock (BuffsLock)
            {
                if (!Buffs.ContainsKey(b.Name))
                {
                    Buffs.Add(b.Name, b);
                }
                else
                {
                    Buffs[b.Name].TimeElapsed = 0; // if buff already exists, just restart its timer
                }
            }
        }

        public void RemoveBuff(Buff b)
        {
            //TODO add every stat
            RemoveBuff(b.Name);
            RemoveBuffSlot(b);
        }

        public void RemoveBuff(string b)
        {
            lock (BuffsLock)
                Buffs.Remove(b);
        }
        
        public byte GetNewBuffSlot(Buff b)
        {
            byte slot = GetBuffSlot();
            AppliedBuffs[slot] = b;
            return slot;
        }

        public void RemoveBuffSlot(Buff b)
        {
            byte slot = GetBuffSlot(b);
            AppliedBuffs[slot] = null;
        }

        private byte GetBuffSlot(Buff buffToLookFor = null)
        {
            for (byte i = 1; i < AppliedBuffs.Length; i++) // Find the first open slot or the slot corresponding to buff
            {
                if (AppliedBuffs[i] == buffToLookFor)
                {
                    return i;
                }
            }
            throw new Exception("No slot found with requested value"); // If no open slot or no corresponding slot
        }

        public override void Update(float diff)
        {
            UpdateAutoAttackTarget(diff);
            BuffGameScriptControllers.RemoveAll((b) => b.NeedsRemoved());
            base.Update(diff);
        }
    }
}
