using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public enum MinionSpawnPosition : uint
    {
        SPAWN_BLUE_TOP = 0xeb364c40,
        SPAWN_BLUE_BOT = 0x53b83640,
        SPAWN_BLUE_MID = 0xb7717140,
        SPAWN_RED_TOP = 0xe647d540,
        SPAWN_RED_BOT = 0x5ec9af40,
        SPAWN_RED_MID = 0xba00e840
    };

    public enum MinionSpawnType : byte
    {
        MINION_TYPE_MELEE = 0x00,
        MINION_TYPE_CASTER = 0x03,
        MINION_TYPE_CANNON = 0x02,
        MINION_TYPE_SUPER = 0x01
    };

    public class Minion : ObjAIBase
    {
        /// <summary>
        /// Const waypoints that define the minion's route
        /// </summary>
        protected List<Vector2> mainWaypoints;
        protected int curMainWaypoint = 0;
        public MinionSpawnPosition SpawnPosition { get; private set; }
        protected MinionSpawnType minionType;
        protected bool _AIPaused;
        public MinionStats Stats;

        public Minion(
            MinionSpawnType type,
            MinionSpawnPosition position,
            List<Vector2> mainWaypoints,
            uint netId = 0
        ) : base("", 40, 0, 0, 1100, netId)
        {
            minionType = type;
            SpawnPosition = position;
            this.mainWaypoints = mainWaypoints;
            curMainWaypoint = 0;
            _AIPaused = false;
            Stats = new MinionStats(this);

            var spawnSpecifics = _game.Map.MapGameScript.GetMinionSpawnPosition(SpawnPosition);
            SetTeam(spawnSpecifics.Item1);
            SetPosition(spawnSpecifics.Item2.X, spawnSpecifics.Item2.Y);

            _game.Map.MapGameScript.SetMinionStats(this); // Let the map decide how strong this minion has to be.

            // Set model
            Model = _game.Map.MapGameScript.GetMinionModel(spawnSpecifics.Item1, type);
            
            // Fix issues induced by having an empty model string
            CollisionRadius = _game.Config.ContentManager.GetCharData(Model).PathfindingCollisionRadius;

            // If we have lane path instructions from the map
            if (mainWaypoints.Count > 0)
            {
                // Follow these instructions
                SetWaypoints(new List<Vector2> { mainWaypoints[0], mainWaypoints[1] });
            }
            else
            {
                // Otherwise path to own position. (Stand still)
                SetWaypoints(new List<Vector2> { new Vector2(X, Y), new Vector2(X, Y) });
            }

            MoveOrder = MoveOrder.MOVE_ORDER_ATTACKMOVE;
        }

        public Minion(
            MinionSpawnType type,
            MinionSpawnPosition position,
            uint netId = 0
        ) : this(type, position, new List<Vector2>(), netId)
        {

        }

        public override void UpdateReplication()
        {
            ReplicationManager.Update(Stats.CurrentHealth, 1, 0);
            ReplicationManager.Update(Stats.MaxHealth, 1, 1);
            ReplicationManager.Update(Stats.LifeTime, 1, 2);
            ReplicationManager.Update(Stats.MaxLifeTime, 1, 3);
            ReplicationManager.Update(Stats.LifeTimeTicks, 1, 4);
            ReplicationManager.Update((uint)Stats.ActionState, 1, 6);
            ReplicationManager.Update(Stats.MagicImmune, 1, 7);
            ReplicationManager.Update(Stats.IsInvulnerable, 1, 8);
            ReplicationManager.Update(Stats.IsPhysicalImmune, 1, 9);
            ReplicationManager.Update(Stats.IsLifestealImmune, 1, 10);
            ReplicationManager.Update(Stats.BaseAttackDamage, 1, 11);
            ReplicationManager.Update(Stats.TotalArmor, 1, 12);
            ReplicationManager.Update(Stats.SpellBlock, 1, 13);
            ReplicationManager.Update(Stats.AttackSpeedMod, 1, 14);
            ReplicationManager.Update(Stats.FlatPhysicalDamageMod, 1, 15);
            ReplicationManager.Update(Stats.PercentPhysicalDamageMod, 1, 16);
            ReplicationManager.Update(Stats.FlatMagicalDamageMod, 1, 17);
            ReplicationManager.Update(Stats.PercentMagicalDamageMod, 1, 18);
            ReplicationManager.Update(Stats.HealthRegenRate, 1, 19);
            ReplicationManager.Update(Stats.ManaRegenRate, 1, 20);
            ReplicationManager.Update(Stats.ManaRegenRate2, 1, 21);
            ReplicationManager.Update(Stats.FlatMagicReduction, 1, 22);
            ReplicationManager.Update(Stats.PercentMagicReduction, 1, 23);
            ReplicationManager.Update(Stats.CurrentMana, 3, 0);
            ReplicationManager.Update(Stats.FlatVisionRadiusMod, 3, 1);
            ReplicationManager.Update(Stats.PercentVisionRadiusMod, 3, 2);
            ReplicationManager.Update(Stats.TotalMovementSpeed, 3, 3);
            ReplicationManager.Update(Stats.TotalCriticalChance, 3, 4);
            ReplicationManager.Update(Stats.IsTargetable, 3, 5);
            ReplicationManager.Update((uint)Stats.IsTargetableToTeamFlags, 3, 6);
        }

        public MinionSpawnType getType()
        {
            return minionType;
        }

        public void PauseAI(bool b)
        {
            _AIPaused = b;
        }
        public override void OnAdded()
        {
            base.OnAdded();
            _game.PacketNotifier.NotifyMinionSpawned(this, Team);
        }
        public override void Update(float diff)
        {
            base.Update(diff);

            if (!IsDead)
            {
                if (IsDashing || _AIPaused)
                {
                    return;
                }

                if (scanForTargets()) // returns true if we have a target
                {
                    keepFocussingTarget(); // fight target
                }
                else
                {
                    walkToDestination(); // walk to destination (or target)
                }
            }
        }

        public override void OnCollision(GameObject collider)
        {
            if (collider == TargetUnit) // If we're colliding with the target, don't do anything.
            {
                return;
            }

            base.OnCollision(collider);
        }

        public override bool IsInDistress()
        {
            return DistressCause != null;
        }

        // AI tasks
        protected bool scanForTargets()
        {
            AttackableUnit nextTarget = null;
            var nextTargetPriority = 14;

            var objects = _game.ObjectManager.GetObjects();
            foreach (var it in objects)
            {
                var u = it.Value as AttackableUnit;

                // Targets have to be:
                if (u == null ||                          // a unit
                    u.IsDead ||                          // alive
                    u.Team == Team ||                    // not on our team
                    GetDistanceTo(u) > DETECT_RANGE ||   // in range
                    !_game.ObjectManager.TeamHasVisionOn(Team, u)) // visible to this minion
                    continue;                             // If not, look for something else

                var priority = (int)ClassifyTarget(u);  // get the priority.
                if (priority < nextTargetPriority) // if the priority is lower than the target we checked previously
                {
                    nextTarget = u;                // make him a potential target.
                    nextTargetPriority = priority;
                }
            }

            if (nextTarget != null) // If we have a target
            {
                TargetUnit = nextTarget; // Set the new target and refresh waypoints
                _game.PacketNotifier.NotifySetTarget(this, nextTarget);
                return true;
            }

            return false;
        }

        protected void walkToDestination()
        {
            if (mainWaypoints.Count > curMainWaypoint + 1)
            {
                if ((Waypoints.Count == 1) || (CurWaypoint == 2 && ++curMainWaypoint < mainWaypoints.Count))
                {
                    //CORE_INFO("Minion reached a point! Going to %f; %f", mainWaypoints[curMainWaypoint].X, mainWaypoints[curMainWaypoint].Y);
                    List<Vector2> newWaypoints = new List<Vector2> { new Vector2(X, Y), mainWaypoints[curMainWaypoint] };
                    SetWaypoints(newWaypoints);
                }
            }
        }
        protected void keepFocussingTarget()
        {
            if (IsAttacking && (TargetUnit == null || GetDistanceTo(TargetUnit) > AttackRange.Total))
            // If target is dead or out of range
            {
                _game.PacketNotifier.NotifyStopAutoAttack(this);
                IsAttacking = false;
            }
        }
    }
}
