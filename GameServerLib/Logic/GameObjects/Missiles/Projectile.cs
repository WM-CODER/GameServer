using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Core.Logic;
using Newtonsoft.Json.Linq;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Projectile : ObjMissile
    {
        public List<GameObject> ObjectsHit { get; private set; }
        public ObjAIBase Owner { get; private set; }
        public int ProjectileId { get; private set; }
        public SpellData SpellData { get; private set; }
        public float ProjectileSpeed { get; protected set; }
        protected Spell _originSpell;
        private Logger _logger = Program.ResolveDependency<Logger>();

        private bool _toRemove;
        public override bool SetToRemove
        {
            get => _toRemove;
            set
            {
                if (value)
                {
                    _game.PacketNotifier.NotifyProjectileDestroy(this);
                }

                _toRemove = value;
            }
        }
        
        public Projectile(
            float x,
            float y,
            int collisionRadius,
            ObjAIBase owner,
            Target target,
            Spell originSpell,
            float projectileSpeed,
            string projectileName,
            int flags = 0,
            uint netId = 0
        ) : base(x, y, collisionRadius, 0, netId)
        {
            SpellData = _game.Config.ContentManager.GetSpellData(projectileName);
            _originSpell = originSpell;
            ProjectileSpeed = projectileSpeed;
            Owner = owner;
            Team = owner.Team;
            ProjectileId = (int)HashFunctions.HashString(projectileName);
            if (!string.IsNullOrEmpty(projectileName))
            {
                VisionRadius = SpellData.MissilePerceptionBubbleRadius;
            }
            ObjectsHit = new List<GameObject>();

            Target = target;

            if (!target.IsSimpleTarget)
            {
                ((ObjAIBase)target).IncrementAttackerCount();
            }

            owner.IncrementAttackerCount();
        }

        public override void Update(float diff)
        {
            if (Target == null)
            {
                SetToRemove = true;
                return;
            }

            base.Update(diff);
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

            var deltaMovement = ProjectileSpeed * 0.001f * diff;

            var xx = _direction.X * deltaMovement;
            var yy = _direction.Y * deltaMovement;

            X += xx;
            Y += yy;

            // If the target was a simple point, stop when it is reached

            if (GetDistanceTo(Target) < deltaMovement * 2)
            {
                if (++CurWaypoint >= Waypoints.Count)
                {
                    Target = null;
                }
                else
                {
                    Target = new Target(Waypoints[CurWaypoint]);
                }
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

        public override void OnCollision(GameObject collider)
        {
            base.OnCollision(collider);
            if (Target != null && Target.IsSimpleTarget && !SetToRemove)
            {
                CheckFlagsForUnit(collider as AttackableUnit);
            }
            else
            {
                if (Target == collider)
                {
                    CheckFlagsForUnit(collider as AttackableUnit);
                }
            }
        }

        protected virtual void CheckFlagsForUnit(AttackableUnit unit)
        {
            if (Target == null)
            {
                return;
            }

            if (Target.IsSimpleTarget)
            { // Skillshot
                if (unit == null || ObjectsHit.Contains(unit))
                {
                    return;
                }

                if (unit.Team == Owner.Team
                    && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectFriends) > 0))
                {
                    return;
                }

                if (unit.Team == TeamId.TEAM_NEUTRAL
                    && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectNeutral) > 0))
                {
                    return;
                }

                if (unit.Team != Owner.Team
                    && unit.Team != TeamId.TEAM_NEUTRAL
                    && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectEnemies) > 0))
                {
                    return;
                }

                if (!unit.IsTargetable && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_NonTargetableAll) > 0))
                {
                    return;
                }

                if (unit.Team == Team &&
                    unit.IsTargetableToTeam.HasFlag(IsTargetableToTeamFlags.NonTargetableAlly) &&
                    !((SpellData.Flags & (int) SpellFlag.SPELL_FLAG_NonTargetableAlly) > 0))
                {
                    return;
                }

                if (unit.Team == CustomConvert.GetEnemyTeam(Team) &&
                    unit.IsTargetableToTeam.HasFlag(IsTargetableToTeamFlags.NonTargetableEnemy) &&
                    !((SpellData.Flags & (int) SpellFlag.SPELL_FLAG_NonTargetableEnemy) > 0))
                {
                    return;
                }

                if (unit.IsDead && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectDead) > 0))
                {
                    return;
                }

                if (unit is Minion && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectMinions) > 0))
                {
                    return;
                }

                if (unit is Placeable && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectUseable) > 0))
                {
                    return;
                }

                if (unit is BaseTurret && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectTurrets) > 0))
                {
                    return;
                }

                if ((unit is Inhibitor || unit is Nexus) &&
                    !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectBuildings) > 0))
                {
                    return;
                }

                if (unit is Champion && !((SpellData.Flags & (int)SpellFlag.SPELL_FLAG_AffectHeroes) > 0))
                {
                    return;
                }

                ObjectsHit.Add(unit);
                if (unit is AttackableUnit attackableUnit)
                {
                    _originSpell.applyEffects(attackableUnit, this);
                }
            }
            else
            {
                if (Target is AttackableUnit u)
                { // Autoguided spell
                    if (_originSpell != null)
                    {
                        _originSpell.applyEffects(u, this);
                    }
                    else
                    { // auto attack
                        Owner.AutoAttackHit(u);
                        SetToRemove = true;
                    }
                }
            }
        }
    }
}
