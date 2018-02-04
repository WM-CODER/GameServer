using System;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Stats
{
    public class ReplicationManager
    {
        public uint?[,] Values { get; private set; }
        public uint[] ChangedValues { get; private set; }

        public ReplicationManager()
        {
            Values = new uint?[6, 32];
            ChangedValues = new uint[6];
        }

        public uint?[,] GetUpdatedStats()
        {
            var ret = new uint?[6, 32];
            for (var i = 0; i < ChangedValues.Length; i++)
            {
                for (var j = 0; j < 32; j++)
                {
                    if ((ChangedValues[i] & (1 << j)) > 0)
                    {
                        ret[i, j] = Values[i, j];
                    }
                }
            }

            return ret;
        }

        public void ResetChanged()
        {
            ChangedValues = new uint[6];
        }

        public void Update(uint value, int primary, int secondary)
        {
            if (Values[primary, secondary] == null || Values[primary, secondary] != value)
            {
                Values[primary, secondary] = value;
                ChangedValues[primary] |= 1u << secondary;
            }
        }

        public void Update(float value, int primary, int secondary)
        {
            Update(BitConverter.ToUInt32(BitConverter.GetBytes(value), 0), primary, secondary);
        }

        public void Update(int value, int primary, int secondary)
        {
            Update(BitConverter.ToUInt32(BitConverter.GetBytes(value), 0), primary, secondary);
        }

        public void Update(bool value, int primary, int secondary)
        {
            Update(value ? 1u : 0u, primary, secondary);
        }
    }
}
