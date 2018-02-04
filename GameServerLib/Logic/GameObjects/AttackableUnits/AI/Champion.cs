using LeagueSandbox.GameServer.Logic.Items;
using System;
using System.Collections.Generic;
using System.Numerics;
using LeagueSandbox.GameServer.Logic.Content;
using LeagueSandbox.GameServer.Logic.Enet;
using LeagueSandbox.GameServer.Logic.API;
using LeagueSandbox.GameServer.Logic.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.Logic.GameObjects.Stats;

namespace LeagueSandbox.GameServer.Logic.GameObjects
{
    public class Champion : ObjAIBase
    {
        public Shop Shop { get; protected set; }
        public float RespawnTimer { get; private set; }
        public float ChampionGoldFromMinions { get; set; }
        public RuneCollection RuneList { get; set; }
        public Dictionary<short, Spell> Spells { get; private set; } = new Dictionary<short, Spell>();
        public float GoldPerSecond { get; set; }
        public bool IsGeneratingGold { get; set; }

        private short _skillPoints;
        public int Skin { get; set; }
        private float _championHitFlagTimer;
        /// <summary>
        /// Player number ordered by the config file.
        /// </summary>
        private uint _playerId;
        /// <summary>
        /// Player number in the team ordered by the config file.
        /// Used in nowhere but to set spawnpoint at the game start.
        /// </summary>
        private uint _playerTeamSpecialId;
        private uint _playerHitId;
        public HeroStats Stats;

        public Champion(string model,
                        uint playerId,
                        uint playerTeamSpecialId,
                        RuneCollection runeList,
                        ClientInfo clientInfo,
                        uint netId = 0)
            : base(model, 30, 0, 0, 1200, netId)
        {
            _playerId = playerId;
            _playerTeamSpecialId = playerTeamSpecialId;
            RuneList = runeList;

            Inventory = InventoryManager.CreateInventory(this);
            Shop = Shop.CreateShop(this);

            Stats = new HeroStats(this, CharData);
            HealthPoints = new Health(CharData.BaseHP);
            ManaPoints = new Health(CharData.BaseMP);
            Stats.Gold += 475.0f;
            Stats.TotalGold += 475.0f;
            GoldPerSecond = _game.Map.MapGameScript.GoldPerSecond;
            IsGeneratingGold = false;

            //TODO: automaticaly rise spell levels with CharData.SpellLevelsUp
            for (short i = 0; i < CharData.SpellNames.Length;i++)
            {
                if (CharData.SpellNames[i] != "")
                {
                    Spells[i] = new Spell(this, CharData.SpellNames[i], (byte)i);
                }
            }
            Spells[4] = new Spell(this, clientInfo.SummonerSkills[0], 4);
            Spells[5] = new Spell(this, clientInfo.SummonerSkills[1], 5);
            Spells[13] = new Spell(this, "Recall", 13);

            for (short i = 0; i < CharData.Passives.Length; i++)
            {
                if (!string.IsNullOrEmpty(CharData.Passives[i].PassiveLuaName))
                {
                    Spells[(byte)(i + 14)] = new Spell(this, CharData.Passives[i].PassiveLuaName, (byte)(i + 14));
                }
            }

            for (short i = 0; i < CharData.ExtraSpells.Length; i++)
            {
                if (!string.IsNullOrEmpty(CharData.ExtraSpells[i]))
                {
                    var spell = new Spell(this, CharData.ExtraSpells[i], (byte)(i + 45));
                    Spells[(byte)(i + 45)] = spell;
                    spell.levelUp();
                }
            }

            Spells[4].levelUp();
            Spells[5].levelUp();
        }

        private string GetPlayerIndex()
        {
            return $"player{_playerId}";
        }

        public override void OnAdded()
        {
            base.OnAdded();
            _game.ObjectManager.AddChampion(this);
            _game.PacketNotifier.NotifyChampionSpawned(this, Team);
        }

        public override void OnRemoved()
        {
            base.OnRemoved();
            _game.ObjectManager.RemoveChampion(this);
        }

        public int GetTeamSize()
        {
            var blueTeamSize = 0;
            var purpTeamSize = 0;

            foreach (var player in _game.Config.Players.Values)
            {
                if (player.Team.ToLower() == "blue")
                {
                    blueTeamSize++;
                }
                else
                {
                    purpTeamSize++;
                }
            }

            var playerIndex = GetPlayerIndex();
            if (_game.Config.Players.ContainsKey(playerIndex))
            {
                switch (_game.Config.Players[playerIndex].Team.ToLower())
                {
                    case "blue":
                        return blueTeamSize;
                    default:
                        return purpTeamSize;
                }
            }

            return 0;
        }

        public override void UpdateReplication()
        {
            ReplicationManager.Update(Stats.Gold, 0, 0);
            ReplicationManager.Update(Stats.TotalGold, 0, 1);
            ReplicationManager.Update(Stats.SpellEnabledBitFieldLower1, 0, 2);
            ReplicationManager.Update(Stats.SpellEnabledBitFieldUpper1, 0, 3);
            ReplicationManager.Update(Stats.SpellEnabledBitFieldLower2, 0, 4);
            ReplicationManager.Update(Stats.SpellEnabledBitFieldUpper2, 0, 5);
            ReplicationManager.Update(Stats.EvolvePoints, 0, 6);
            ReplicationManager.Update(Stats.EvolveFlags, 0, 7);
            ReplicationManager.Update(Stats.ManaCost0, 0, 8);
            ReplicationManager.Update(Stats.ManaCost1, 0, 9);
            ReplicationManager.Update(Stats.ManaCost2, 0, 10);
            ReplicationManager.Update(Stats.ManaCost3, 0, 11);
            ReplicationManager.Update(Stats.ManaCostEx0, 0, 12);
            ReplicationManager.Update(Stats.ManaCostEx1, 0, 13);
            ReplicationManager.Update(Stats.ManaCostEx2, 0, 14);
            ReplicationManager.Update(Stats.ManaCostEx3, 0, 15);
            ReplicationManager.Update(Stats.ManaCostEx4, 0, 16);
            ReplicationManager.Update(Stats.ManaCostEx5, 0, 17);
            ReplicationManager.Update(Stats.ManaCostEx6, 0, 18);
            ReplicationManager.Update(Stats.ManaCostEx7, 0, 19);
            ReplicationManager.Update(Stats.ManaCostEx8, 0, 20);
            ReplicationManager.Update(Stats.ManaCostEx9, 0, 21);
            ReplicationManager.Update(Stats.ManaCostEx10, 0, 22);
            ReplicationManager.Update(Stats.ManaCostEx11, 0, 23);
            ReplicationManager.Update(Stats.ManaCostEx12, 0, 24);
            ReplicationManager.Update(Stats.ManaCostEx13, 0, 25);
            ReplicationManager.Update(Stats.ManaCostEx14, 0, 26);
            ReplicationManager.Update(Stats.ManaCostEx15, 0, 27);
            ReplicationManager.Update((uint)Stats.ActionState, 1, 0);
            ReplicationManager.Update(Stats.IsMagicImmune, 1, 1);
            ReplicationManager.Update(Stats.IsInvulnerable, 1, 2);
            ReplicationManager.Update(Stats.IsPhysicalImmune, 1, 3);
            ReplicationManager.Update(Stats.IsLifestealImmune, 1, 4);
            ReplicationManager.Update(Stats.BaseAttackDamage, 1, 5);
            ReplicationManager.Update(Stats.BaseAbilityPower, 1, 6);
            ReplicationManager.Update(Stats.TotalDodgeChance, 1, 7);
            ReplicationManager.Update(Stats.TotalCriticalChance, 1, 8);
            ReplicationManager.Update(Stats.TotalArmor, 1, 9);
            ReplicationManager.Update(Stats.SpellBlock, 1, 10);
            ReplicationManager.Update(Stats.HealthRegenPer5, 1, 11);
            ReplicationManager.Update(Stats.ManaRegenPer5, 1, 12);
            ReplicationManager.Update(Stats.TotalAttackRange, 1, 13);
            ReplicationManager.Update(Stats.FlatAttackDamageMod, 1, 14);
            ReplicationManager.Update(Stats.PercentAttackDamageMod, 1, 15);
            ReplicationManager.Update(Stats.FlatMagicalDamageMod, 1, 16);
            ReplicationManager.Update(Stats.PercentMagicalDamageMod, 1, 17);
            ReplicationManager.Update(Stats.FlatMagicalReduction, 1, 18);
            ReplicationManager.Update(Stats.PercentMagicalReduction, 1, 19);
            ReplicationManager.Update(Stats.AttackSpeedMod, 1, 20);
            ReplicationManager.Update(Stats.FlatAttackRangeMod, 1, 21);
            ReplicationManager.Update(Stats.PercentCdrMod, 1, 22);
            ReplicationManager.Update(Stats.PassiveCooldownEndTime, 1, 23);
            ReplicationManager.Update(Stats.PassiveCooldownTotalTime, 1, 24);
            ReplicationManager.Update(Stats.FlatArmorPenetration, 1, 25);
            ReplicationManager.Update(Stats.PercentArmorPenetration, 1, 26);
            ReplicationManager.Update(Stats.FlatMagicPenetration, 1, 27);
            ReplicationManager.Update(Stats.PercentMagicPenetration, 1, 28);
            ReplicationManager.Update(Stats.PercentLifeStealMod, 1, 29);
            ReplicationManager.Update(Stats.PercentSpellVampMod, 1, 30);
            ReplicationManager.Update(Stats.PercentTenacity, 1, 31);
            ReplicationManager.Update(Stats.PercentBonusArmorPenetration, 2, 0);
            ReplicationManager.Update(Stats.PercentBonusMagicPenetration, 2, 1);
            ReplicationManager.Update(Stats.BaseHealthRegenRate, 2, 2);
            ReplicationManager.Update(Stats.BaseManaRegenRate, 2, 3);
            ReplicationManager.Update(Stats.CurrentHealth, 3, 0);
            ReplicationManager.Update(Stats.CurrentMana, 3, 1);
            ReplicationManager.Update(Stats.MaxHealth, 3, 2);
            ReplicationManager.Update(Stats.MaxMana, 3, 3);
            ReplicationManager.Update(Stats.Experience, 3, 4);
            ReplicationManager.Update(Stats.LifeTime, 3, 5);
            ReplicationManager.Update(Stats.MaxLifeTime, 3, 6);
            ReplicationManager.Update(Stats.LifeTimeTicks, 3, 7);
            ReplicationManager.Update(Stats.FlatVisionRangeMod, 3, 8);
            ReplicationManager.Update(Stats.PercentVisionRangeMod, 3, 9);
            ReplicationManager.Update(Stats.TotalMovementSpeed, 3, 10);
            ReplicationManager.Update(Stats.TotalSize, 3, 11);
            ReplicationManager.Update(Stats.PathfindingRadiusMod, 3, 12);
            ReplicationManager.Update(Stats.Level, 3, 13);
            ReplicationManager.Update(Stats.NumberOfNeutralMinionsKilled, 3, 14);
            ReplicationManager.Update(Stats.IsTargetable, 3, 15);
            ReplicationManager.Update((uint)Stats.IsTargetableToTeamFlags, 3, 16);
        }
        public bool CanMove()
        {
            return !HasCrowdControl(CrowdControlType.Stun) &&
                !IsDashing &&
                !IsCastingSpell &&
                !IsDead &&
                !HasCrowdControl(CrowdControlType.Root);
        }

        public bool CanCast()
        {
            return !HasCrowdControl(CrowdControlType.Stun) &&
                !HasCrowdControl(CrowdControlType.Silence);
        }

        public Vector2 GetSpawnPosition()
        {
            var config = _game.Config;
            var playerIndex = GetPlayerIndex();
            var playerTeam = "";
            var teamSize = GetTeamSize();

            if (teamSize > 6) // ???
            {
                teamSize = 6;
            }

            if (config.Players.ContainsKey(playerIndex))
            {
                var p = config.Players[playerIndex];
                playerTeam = p.Team;
            }

            var spawnsByTeam = new Dictionary<TeamId, Dictionary<int, PlayerSpawns>>
            {
                {TeamId.TEAM_BLUE, config.MapSpawns.Blue},
                {TeamId.TEAM_PURPLE, config.MapSpawns.Purple}
            };

            var spawns = spawnsByTeam[Team];
            return spawns[teamSize - 1].GetCoordsForPlayer((int)_playerTeamSpecialId);
        }

        public Vector2 GetRespawnPosition()
        {
            var config = _game.Config;
            var playerIndex = GetPlayerIndex();

            if (config.Players.ContainsKey(playerIndex))
            {
                var p = config.Players[playerIndex];
            }
            var coords = new Vector2
            {
                X = _game.Map.MapGameScript.GetRespawnLocation(Team).X,
                Y = _game.Map.MapGameScript.GetRespawnLocation(Team).Y
            };
            return new Vector2(coords.X, coords.Y);
        }

        public Spell GetSpell(byte slot)
        {
            return Spells[slot];
        }

        public Spell GetSpellByName(string name)
        {
            foreach(var s in Spells.Values)
            {
                if (s == null)
                    continue;
                if (s.SpellName == name)
                    return s;
            }
            return null;
        }

        public Spell LevelUpSpell(short slot)
        {
            if (_skillPoints == 0)
                return null;

            var s = GetSpell((byte) slot);

            if (s == null)
                return null;

            s.levelUp();
            _skillPoints--;

            return s;
        }

        public override void Update(float diff)
        {
            base.Update(diff);

            if (!IsDead && MoveOrder == MoveOrder.MOVE_ORDER_ATTACKMOVE && TargetUnit != null)
            {
                var objects = _game.ObjectManager.GetObjects();
                var distanceToTarget = 9000000.0f;
                AttackableUnit nextTarget = null;
                var range = Math.Max(Stats.TotalAttackRange, DETECT_RANGE);

                foreach (var it in objects)
                {
                    var u = it.Value as AttackableUnit;

                    if (u == null || u.IsDead || u.Team == Team || GetDistanceTo(u) > range)
                        continue;

                    if (GetDistanceTo(u) < distanceToTarget)
                    {
                        distanceToTarget = GetDistanceTo(u);
                        nextTarget = u;
                    }
                }

                if (nextTarget != null)
                {
                    TargetUnit = nextTarget;
                    _game.PacketNotifier.NotifySetTarget(this, nextTarget);
                }
            }

            if (!IsGeneratingGold && _game.GameTime >= _game.Map.MapGameScript.FirstGoldTime)
            {
                IsGeneratingGold = true;
                _logger.LogCoreInfo("Generating Gold!");
            }

            if (RespawnTimer > 0)
            {
                RespawnTimer -= diff;
                if (RespawnTimer <= 0)
                {
                    Respawn();
                }
            }

            var isLevelup = LevelUp();
            if (isLevelup)
            {
                _game.PacketNotifier.NotifyLevelUp(this);
                _game.PacketNotifier.NotifyUpdatedStats(this, false);
            }

            foreach (var s in Spells.Values)
                s.update(diff);

            if (_championHitFlagTimer > 0)
            {
                _championHitFlagTimer -= diff;
                if (_championHitFlagTimer <= 0)
                {
                    _championHitFlagTimer = 0;
                }
            }
        }

        public void Respawn()
        {
            var spawnPos = GetRespawnPosition();
            SetPosition(spawnPos.X, spawnPos.Y);
            _game.PacketNotifier.NotifyChampionRespawn(this);
            HealthPoints.Current = HealthPoints.Total;
            ManaPoints.Current = ManaPoints.Total;
            IsDead = false;
            RespawnTimer = -1;
        }

	    public void Recall(ObjAIBase owner)
        {
            var spawnPos = GetRespawnPosition();
            _game.PacketNotifier.NotifyTeleport(owner, spawnPos.X, spawnPos.Y);
        }

        public void setSkillPoints(int _skillPoints)
        {
            _skillPoints = (short)_skillPoints;
        }

        public int getChampionHash()
        {
            var szSkin = "";

            if (Skin < 10)
                szSkin = "0" + Skin;
            else
                szSkin = Skin.ToString();

            int hash = 0;
            var gobj = "[Character]";
            for (var i = 0; i < gobj.Length; i++)
            {
                hash = Char.ToLower(gobj[i]) + (0x1003F * hash);
            }
            for (var i = 0; i < Model.Length; i++)
            {
                hash = Char.ToLower(Model[i]) + (0x1003F * hash);
            }
            for (var i = 0; i < szSkin.Length; i++)
            {
                hash = Char.ToLower(szSkin[i]) + (0x1003F * hash);
            }
            return hash;
        }

        public override bool IsInDistress()
        {
            return DistressCause != null;
        }

        public short getSkillPoints()
        {
            return _skillPoints;
        }

        public bool LevelUp()
        {
            var expMap = _game.Map.MapGameScript.ExpToLevelUp;
            if (Stats.Level >= expMap.Count)
            {
                return false;
            }

            if (Stats.Experience < expMap[(int)Stats.Level])
            {
                return false;
            }

            while (Stats.Level < expMap.Count && Stats.Experience >= expMap[(int)Stats.Level])
            {
                Stats.Level++;

                HealthPoints.BaseBonus += Stats.HealthPerLevel;
                ManaPoints.BaseBonus += Stats.ManaPerLevel;
                AttackDamage.BaseBonus += Stats.AttackDamagePerLevel;
                Armor.BaseBonus += Stats.ArmorPerLevel;
                MagicResist.BaseBonus += Stats.MagicResistPerLevel;
                HealthRegeneration.BaseBonus += Stats.HealthRegenerationPerLevel;
                ManaRegeneration.BaseBonus += Stats.ManaRegenerationPerLevel;

                _logger.LogCoreInfo("Champion " + Model + " leveled up to " + Stats.Level);
                _skillPoints++;
            }

            return true;
        }

        public InventoryManager getInventory()
        {
            return Inventory;
        }

        public override void Die(AttackableUnit killer)
        {
            RespawnTimer = 5000 + Stats.Level * 2500;
            _game.ObjectManager.StopTargeting(this);

            _game.PacketNotifier.NotifyUnitAnnounceEvent(UnitAnnounces.Death, this, killer);

            var cKiller = killer as Champion;

            if (cKiller == null && _championHitFlagTimer > 0)
            {
                cKiller = _game.ObjectManager.GetObjectById(_playerHitId) as Champion;
                _logger.LogCoreInfo("Killed by turret, minion or monster, but still  give gold to the enemy.");
            }

            if (cKiller == null)
            {
                _game.PacketNotifier.NotifyChampionDie(this, killer, 0);
                return;
            }

            cKiller.ChampionGoldFromMinions = 0;

            float gold = _game.Map.MapGameScript.GetGoldFor(this);
            _logger.LogCoreInfo(
                "Before: getGoldFromChamp: {0} Killer: {1} Victim {2}",
                gold,
                cKiller.KillDeathCounter,
                KillDeathCounter
            );

            if (cKiller.KillDeathCounter < 0)
                cKiller.KillDeathCounter = 0;

            if (cKiller.KillDeathCounter >= 0)
                cKiller.KillDeathCounter += 1;

            if (KillDeathCounter > 0)
                KillDeathCounter = 0;

            if (KillDeathCounter <= 0)
                KillDeathCounter -= 1;

            if (gold > 0)
            {
                _game.PacketNotifier.NotifyChampionDie(this, cKiller, 0);
                return;
            }

            if (_game.Map.MapGameScript.IsKillGoldRewardReductionActive
                && _game.Map.MapGameScript.HasFirstBloodHappened)
            {
                gold -= gold * 0.25f;
                //CORE_INFO("Still some minutes for full gold reward on champion kills");
            }

            if (!_game.Map.MapGameScript.HasFirstBloodHappened)
            {
                gold += 100;
                _game.Map.MapGameScript.HasFirstBloodHappened = true;
            }

            _game.PacketNotifier.NotifyChampionDie(this, cKiller, (int)gold);

            cKiller.Stats.Gold += gold;
            cKiller.Stats.TotalGold += gold;
            _game.PacketNotifier.NotifyAddGold(cKiller, this, gold);

            //CORE_INFO("After: getGoldFromChamp: %f Killer: %i Victim: %i", gold, cKiller.killDeathCounter,this.killDeathCounter);

            _game.ObjectManager.StopTargeting(this);
        }

        public override void OnCollision(GameObject collider)
        {
            base.OnCollision(collider);
            if (collider == null)
            {
                //CORE_INFO("I bumped into a wall!");
            }
            else
            {
                //CORE_INFO("I bumped into someone else!");
            }
        }

        public override void TakeDamage(ObjAIBase attacker, float damage, DamageType type, DamageSource source,
            bool isCrit)
        {
            base.TakeDamage(attacker, damage, type, source, isCrit);

            _championHitFlagTimer = 15 * 1000; //15 seconds timer, so when you get executed the last enemy champion who hit you gets the gold
            _playerHitId = NetId;
            //CORE_INFO("15 second execution timer on you. Do not get killed by a minion, turret or monster!");
        }
    }
}
