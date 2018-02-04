using System;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Inhibitor : ObjAnimatedBuilding
    {
        private System.Timers.Timer RespawnTimer;
        public InhibitorState State { get; private set; }
        private const double RESPAWN_TIMER = 5 * 60 * 1000;
        private const double RESPAWN_ANNOUNCE = 1 * 60 * 1000;
        private const float GOLD_WORTH = 50.0f;
        private DateTime TimerStartTime;
        public bool RespawnAnnounced { get; private set; } = true;

        // TODO assists
        public Inhibitor(
            string model,
            TeamId team,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(model, collisionRadius, x, y, visionRadius, netId)
        {
            HealthPoints = new Health(4000);
            IsTargetable = false;
            IsInvulnerable = true;
            State = InhibitorState.Alive;
            SetTeam(team);
        }
        public override void OnAdded()
        {
            base.OnAdded();
            _game.ObjectManager.AddInhibitor(this);
        }

        public override void Die(AttackableUnit killer)
        {
            var objects = _game.ObjectManager.GetObjects().Values;
            foreach (var obj in objects)
            {
                if (obj is AttackableUnit u && u.TargetUnit == this)
                {
                    u.SetTargetUnit(null);
                    u.AutoAttackTarget = null;
                    u.IsAttacking = false;
                    _game.PacketNotifier.NotifySetTarget(u, null);
                    u._hasMadeInitialAttack = false;
                }
            }

            if (RespawnTimer != null) //?
            {
                RespawnTimer.Stop();
            }

            RespawnTimer = new System.Timers.Timer(RESPAWN_TIMER) {AutoReset = false};

            RespawnTimer.Elapsed += (a, b) =>
            {
                HealthPoints.Current = HealthPoints.Total;
                SetState(InhibitorState.Alive);
                IsDead = false;
            };
            RespawnTimer.Start();
            TimerStartTime = DateTime.Now;

            if (killer is Champion c)
            {
                c.Stats.Gold += GOLD_WORTH;
                c.Stats.TotalGold += GOLD_WORTH;
                _game.PacketNotifier.NotifyAddGold(c, this, GOLD_WORTH);
            }

            SetState(InhibitorState.Dead, killer);
            RespawnAnnounced = false;

            base.Die(killer);
        }

        public override void UpdateReplication()
        {
            ReplicationManager.Update(HealthPoints.Current, 1, 0);
            ReplicationManager.Update(IsInvulnerable, 1, 1);
            ReplicationManager.Update(IsTargetable, 5, 0);
            ReplicationManager.Update((uint)IsTargetableToTeam, 5, 1);
        }

        public void SetState(InhibitorState state, GameObject killer = null)
        {
            if (RespawnTimer != null && state == InhibitorState.Alive)
            {
                RespawnTimer.Stop();
            }

            State = state;
            _game.PacketNotifier.NotifyInhibitorState(this, killer);
        }

        public double GetRespawnTimer()
        {
            var diff = DateTime.Now - TimerStartTime;
            return RESPAWN_TIMER - diff.TotalMilliseconds;
        }

        public override void Update(float diff)
        {
            if (!RespawnAnnounced && State == InhibitorState.Dead && GetRespawnTimer() <= RESPAWN_ANNOUNCE)
            {
                _game.PacketNotifier.NotifyInhibitorSpawningSoon(this);
                RespawnAnnounced = true;
            }

            base.Update(diff);
        }

        public override void RefreshWaypoints()
        {

        }
    }

    public enum InhibitorState : byte
    {
        Dead = 0x00,
        Alive = 0x01
    }
}
