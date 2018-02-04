namespace LeagueSandbox.GameServer.Logic.GameObjects.Stats
{
    public class MinionStats
    {
        private Minion _owner;

        public float CurrentHealth => _owner.HealthPoints.Current; // mHP (2-1)
        public float MaxHealth => _owner.HealthPoints.Total; // mMaxHP (2-2)
        public float LifeTime { get; set; } // mLifetime (2-3)
        public float MaxLifeTime { get; set; } // mMaxLifetime (2-4)
        public float LifeTimeTicks { get; set; } // mLifetimeTicks (2-5)
        // public float CurrentMana => _owner.ManaPoints.Current; // mMP (2-6)
        public ActionState ActionState { get; set; } // ActionState (2-7)
        public bool MagicImmune { get; set; } // MagicImmune (2-8)
        public bool IsInvulnerable => _owner.IsInvulnerable; // IsInvulnerable (2-9)
        public bool IsPhysicalImmune => _owner.IsPhysicalImmune; // IsPhysicalImmune (2-10)
        public bool IsLifestealImmune { get; set; } // IsLifestealImmune (2-11)
        public float BaseAttackDamage { get; set; } // mBaseAttackDamage (2-12)
        public float TotalArmor => _owner.Armor.Total; // mArmor (2-13)
        public float SpellBlock { get; set; } // mSpellBlock (2-14)
        public float AttackSpeedMod { get; set; } // mAttackSpeedMod (2-15)
        public float FlatPhysicalDamageMod { get; set; } // mFlatPhysicalDamageMod (2-16)
        public float PercentPhysicalDamageMod { get; set; } // mPercentPhysicalDamageMod (2-17)
        public float FlatMagicalDamageMod { get; set; } // mFlatMagicDamageMod (2-18)
        public float PercentMagicalDamageMod { get; set; } // mPercentPhysicalDamageMod (2-19)
        public float HealthRegenRate { get; set; } // mHPRegenRate (2-20)
        public float ManaRegenRate { get; set; } // mPARRegenRate (2-21)
        public float ManaRegenRate2 { get; set; } // mPARRegenRate (2-22)
        public float FlatMagicReduction { get; set; } // mFlatMagicReduction (2-23)
        public float PercentMagicReduction { get; set; } // mPercentMagicReduction (2-24)
/* ?? */public float CurrentMana => _owner.ManaPoints.Current; // mMP (8-1)
        public float FlatVisionRadiusMod { get; set; } // mFlatBubbleRadiusMod (8-2)
        public float PercentVisionRadiusMod { get; set; } // mPercentBubbleRadiusMod (8-3)
        public float TotalMovementSpeed => _owner.MovementSpeed.Total; // mMoveSpeed (8-4)
        public float TotalCriticalChance => _owner.CriticalChance.Total; // mCrit (8-5)
        public bool IsTargetable => _owner.IsTargetable; // mIsTargetable (8-6)
        public IsTargetableToTeamFlags IsTargetableToTeamFlags => _owner.IsTargetableToTeam; // mIsTargetableToTeamFlags (8-7)

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

        public MinionStats(Minion owner)
        {
            _owner = owner;
        }
    }
}
