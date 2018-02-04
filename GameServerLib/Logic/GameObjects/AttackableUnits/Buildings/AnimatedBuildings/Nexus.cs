using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Nexus : ObjAnimatedBuilding
    {
        public Nexus(
            string model,
            TeamId team,
            int collisionRadius = 40,
            float x = 0,
            float y = 0,
            int visionRadius = 0,
            uint netId = 0
        ) : base(model, collisionRadius, x, y, visionRadius, netId)
        {
            HealthPoints = new Health(5500);
            IsTargetable = false;
            IsInvulnerable = true;

            SetTeam(team);
        }

        public override void Die(AttackableUnit killer)
        {
            _game.Stop();
            _game.PacketNotifier.NotifyGameEnd(this);
        }

        public override void UpdateReplication()
        {
            ReplicationManager.Update(HealthPoints.Current, 1, 0);
            ReplicationManager.Update(IsInvulnerable, 1, 1);
            ReplicationManager.Update(IsTargetable, 5, 0);
            ReplicationManager.Update((uint)IsTargetableToTeam, 5, 1);
        }

        public override void RefreshWaypoints()
        {

        }
    }
}
