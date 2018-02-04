using LeagueSandbox.GameServer.Logic.Content;

namespace LeagueSandbox.GameServer.Logic.GameObjects.Stats
{
    public class TowerStats
    {
        private BaseTurret _owner;

        public float MaxMana => _owner.ManaPoints.Total; // mMaxMP (2-1)
        public float CurrentMana => _owner.ManaPoints.Current; // mMP (2-2)
        public ActionState ActionState { get; set; } // ActionState (2-3)
        public bool MagicImmune { get; set; } // MagicImmune (2-4)
        public bool IsInvulnerable => _owner.IsInvulnerable; // IsInvulnerable (2-5)
        public bool IsPhysicalImmune => _owner.IsPhysicalImmune; // IsPhysicalImmune (2-6)
        public bool IsLifestealImmune { get; set; } // IsLifestealImmune (2-7)
        public float BaseAttackDamage { get; set; } // mBaseAttackDamage (2-8)
        public float TotalArmor => _owner.Armor.Total; // mArmor (2-9)
        public float SpellBlock { get; set; } // mSpellBlock (2-10)
        public float AttackSpeedMod { get; set; } // mAttackSpeedMod (2-11)
        public float FlatPhysicalDamageMod { get; set; } // mFlatPhysicalDamageMod (2-12)
        public float PercentPhysicalDamageMod { get; set; } // mPercentPhysicalDamageMod (2-13)
        public float FlatMagicDamageMod { get; set; } // mFlatMagicDamageMod (2-14)
        public float PercentMagicDamageMod { get; set; } // mPercentPhysicalDamageMod (2-15)
        public float HealthRegenRate { get; set; } // mHPRegenRate (2-16)
        public float CurrentHealth => _owner.HealthPoints.Current; // mHP (8-1)
        public float MaxHealth => _owner.HealthPoints.Total; // mMaxHP (8-2)
        public float FlatVisionRadiusMod { get; set; } // mFlatBubbleRadiusMod (8-3)
        public float PercentVisionRadiusMod { get; set; } // mPercentBubbleRadiusMod (8-4)
        public float TotalMovementSpeed => _owner.MovementSpeed.Total; // mMoveSpeed (8-5)
        public float TotalCriticalChance => _owner.CriticalChance.Total; // mCrit (8-6)
        public bool IsTargetable => _owner.IsTargetable; // mIsTargetable (32-1)
        public IsTargetableToTeamFlags IsTargetableToTeamFlags => _owner.IsTargetableToTeam; // mIsTargetableToTeamFlags (32-2)

        public TowerStats(BaseTurret owner)
        {
            _owner = owner;
        }

        public bool GetActionState(ActionState state)
        {
            return ActionState.HasFlag(state);
        }

        public void SetActionState(ActionState state, bool value)
        {
            if (value)
            {
                ActionState |= state;
            }
            else
            {
                ActionState &= ~state;
            }
        }
    }
}
