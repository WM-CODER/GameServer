using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSandbox.GameServer.Logic.GameObjects;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.Packets.PacketHandlers;

namespace LeagueSandbox.GameServer.Logic.Packets.PacketDefinitions.S2C
{
    public class UpdateStats : BasePacket
    {
        public UpdateStats(ObjAIBase u, bool partial = true)
            : base(PacketCmd.PKT_S2C_CharStats)
        {
            var stats = new uint?[6, 32];
            if (partial)
            {
                stats = u.ReplicationManager.GetUpdatedStats();
            }
            else
            {
                stats = u.ReplicationManager.Values;
            }

            buffer.Write(Environment.TickCount); // syncID
            buffer.Write((byte)1); // updating 1 unit

            byte masterMask = 0;

            for (var i = 0; i < 6; i++)
            {
                for (var j = 0; j < 32; j++)
                {
                    if (stats[i, j] != null)
                    {
                        masterMask |= (byte)(1 << i);
                        break;
                    }
                }
            }

            buffer.Write((byte)masterMask);
            buffer.Write((uint)u.NetId);

            for (var i = 0; i < 6; i++)
            {
                uint fieldMask = 0;
                byte size = 0;
                for (var j = 0; j < 32; j++)
                {
                    if (stats[i, j] != null)
                    {
                        fieldMask |= 1u << j;
                        size += 4;
                    }
                }
                buffer.Write((uint)fieldMask);
                buffer.Write((byte)size);
                for (var j = 0; j < 32; j++)
                {
                    if (stats[i, j] != null)
                    {
                        buffer.Write((uint)stats[i, j]);
                    }
                }
            }

            u.ReplicationManager.ResetChanged();
        }
    }
}