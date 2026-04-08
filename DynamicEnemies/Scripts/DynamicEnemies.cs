using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace DynamicEnemiesMod
{
    public class DynamicEnemies : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<DynamicEnemies>();

            mod.IsReady = true;
        }

        public static DynamicEnemies Instance;

        //settings
        public int skillCooldownBase = 3;
        public int skillCooldownRange = 1;

        public bool skillSlowEnemies = true;

        public bool maneuvers = true;
        public int maneuversThreshold = 4;

        public bool movement = true;
        public bool movementFlyInside = false;
        public int movementFlyHeight;
        public float movementRetreatSpeed = 1;
        public int movementRetreatDistanceMin = 2;
        public int movementRetreatDistanceMax = 6;

        public bool spellcasting = true;
        public int spellcastingReleaseFrame = 3;
        public bool spellcastingInterruptKnockback = true;
        public float spellcastingInterruptKnockbackMinimum = 1;

        public int chargeSpeedMult = 4;
        public float chargeStartupDuration = 0.25f;
        public int chargeDistanceMin = 2;
        public int chargeDistanceMax = 4;
        public int chargeRangeMax = 2;

        public int jumpSpeedMult = 4;
        public float jumpStartupDuration = 0.25f;
        public int jumpDistanceMin = 2;
        public int jumpDistanceMax = 4;
        public int jumpRangeMax = 2;

        public int dashSpeedMult = 4;
        public int dashTargetDistance = 2;

        public bool cleaveExcludeTarget = true;
        public float cleavePower = 1f;
        public float cleaveReach = 1.5f;
        public float cleaveAngle = 90f;

        public bool vfx = true;
        public float vfxChargeInterval = 0.1f;

        //spells
        public int spellCombatChameleon = 1000;

        //mod compatibility
        bool hasDEX;

        public bool DoesEnemyPreferRanged(int ID)
        {
            if (Instance.EnemiesPreferRanged.Contains(ID))
            {
                //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " PREFERS RANGED!");
                return true;
            }

            //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " DOESN'T PREFER RANGED!");
            return false;
        }
        public bool CanEnemyJumpAttack(int ID)
        {
            if (Instance.EnemiesCanJumpAttack.Contains(ID))
            {
                Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CAN JUMP ATTACK!");
                return true;
            }

            Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CANNOT JUMP ATTACK!");
            return false;
        }
        public bool CanEnemyCleave(int ID)
        {
            if (Instance.EnemiesCanCleave.Contains(ID))
            {
                Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CAN CLEAVE!");
                return true;
            }

            Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CANNOT CLEAVE!");
            return false;
        }
        public bool CanEnemySleep(int ID)
        {
            if (Instance.EnemiesCannotSleep.Contains(ID))
            {
                //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CANNOT SLEEP!");
                return false;
            }

            //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CAN SLEEP!");
            return true;
        }
        public bool CanEnemyDrown(int ID)
        {
            if (Instance.EnemiesCannotDrown.Contains(ID))
            {
                //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CANNOT DROWN!");
                return false;
            }

            //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " CAN DROWN!");
            return true;
        }
        public bool IsEnemySlow(int ID)
        {
            if (!skillSlowEnemies)
                return false;

            if (Instance.EnemiesAreSlow.Contains(ID))
            {
                //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " IS SLOW!");
                return true;
            }

            //Debug.Log("DYNAMIC ENEMIES - ENEMY ID " + ID.ToString() + " IS NOT SLOW!");
            return false;
        }

        List<int> EnemiesPreferRanged = new List<int> {
            (int)MobileTypes.Imp,
            (int)MobileTypes.Lich,
            (int)MobileTypes.AncientLich,
            (int)MobileTypes.Mage,
            (int)MobileTypes.Battlemage,
            (int)MobileTypes.Nightblade,
            (int)MobileTypes.Acrobat,
            (int)MobileTypes.Assassin,
            (int)MobileTypes.Monk,
            (int)MobileTypes.Archer,
            (int)MobileTypes.Ranger,
            394,    //Necromancer Assassin 
        };
        List<int> EnemiesCanJumpAttack = new List<int> {
            (int)MobileTypes.Nightblade,
            (int)MobileTypes.Bard,
            (int)MobileTypes.Burglar,
            (int)MobileTypes.Rogue,
            (int)MobileTypes.Acrobat,
            (int)MobileTypes.Thief,
            (int)MobileTypes.Assassin,
            (int)MobileTypes.Monk,
            (int)MobileTypes.Archer,
            (int)MobileTypes.Ranger,
            (int)MobileTypes.Barbarian,
            (int)MobileTypes.Rat,
            (int)MobileTypes.SabertoothTiger,
            (int)MobileTypes.Spider,
            (int)MobileTypes.Werewolf,
            (int)MobileTypes.Gargoyle,
            (int)MobileTypes.Daedroth,
            (int)MobileTypes.Vampire,
            (int)MobileTypes.VampireAncient,
            (int)MobileTypes.DaedraLord,
            256,    //Goblin
            258,    //Lizard Man
            261,    //Medusa
            262,    //Wolf
            263,    //Snow Wolf
            264,    //Hell Hound
            267,    //Dog
            269,    //Minotaur
            271,    //Blood Spider
            272,    //Troll
            276,    //Fire Daemon
            277,    //Ghoul
            280,    //Mountain Lion
            282,    //Ogre
            287,    //Dire Ghoul
            288,    //Scamp
            390,    //Bounty Hunter
            394,    //Necromancer Assassin 
            395,    //Dark Slayer 
        };
        List<int> EnemiesCanCleave = new List<int> {
            (int)MobileTypes.Barbarian,
            (int)MobileTypes.Knight,
            (int)MobileTypes.GrizzlyBear,
            (int)MobileTypes.Wereboar,
            (int)MobileTypes.Giant,
            (int)MobileTypes.Gargoyle,
            (int)MobileTypes.OrcWarlord,
            (int)MobileTypes.DaedraLord,
            (int)MobileTypes.FireAtronach,
            (int)MobileTypes.IronAtronach,
            (int)MobileTypes.FleshAtronach,
            259,    //Lizard Warrior
            265,    //Grotesque
            269,    //Minotaur
            270,    //Iron Golem
            276,    //Fire Daemon
            282,    //Ogre
            284,    //Ice Golem
            286,    //Stone Golem
            290,    //Steam Centurion
            //385,    //Guard
            391,    //Royal Knight
            393,    //Necromancer Glavier
            393,    //Dark Slayer
        };
        List<int> EnemiesAreSlow = new List<int> {
            (int)MobileTypes.Zombie,
            (int)MobileTypes.Mummy,
        };
        List<int> EnemiesCannotSleep = new List<int> {
            (int)MobileTypes.SkeletalWarrior,
            (int)MobileTypes.Zombie,
            (int)MobileTypes.Ghost,
            (int)MobileTypes.Mummy,
            (int)MobileTypes.Gargoyle,
            (int)MobileTypes.Wraith,
            (int)MobileTypes.Lich,
            (int)MobileTypes.AncientLich,
            (int)MobileTypes.FireAtronach,
            (int)MobileTypes.IronAtronach,
            (int)MobileTypes.FleshAtronach,
            (int)MobileTypes.IceAtronach,
            257,    //Homunculus
            265,    //Grotesque
            266,    //Skeletal Soldier
            270,    //Iron Golem
            284,    //Will-o-wisp
            273,    //Gloom Wraith
            274,    //Faded Ghost
            275,    //Lysandus
            284,    //Ice Golem
            286,    //Stone Golem
            289,    //Centurion Sphere
            290,    //Steam Centurion
        };

        List<int> EnemiesCannotDrown = new List<int> {
            (int)MobileTypes.Slaughterfish,
            (int)MobileTypes.SkeletalWarrior,
            (int)MobileTypes.Zombie,
            (int)MobileTypes.Ghost,
            (int)MobileTypes.Mummy,
            (int)MobileTypes.Gargoyle,
            (int)MobileTypes.Wraith,
            (int)MobileTypes.Lich,
            (int)MobileTypes.AncientLich,
            (int)MobileTypes.IronAtronach,
            (int)MobileTypes.FleshAtronach,
            (int)MobileTypes.IceAtronach,
            (int)MobileTypes.Dreugh,
            (int)MobileTypes.Lamia,
            257,    //Homunculus
            /*258,    //Lizard Man
            259,    //Lizard Warrior*/
            261,    //Medusa
            265,    //Grotesque
            266,    //Skeletal Soldier
            270,    //Iron Golem
            277,    //Ghoul
            281,    //Mudcrab
            284,    //Will-o-wisp
            287,    //Dire Ghoul
            273,    //Gloom Wraith
            274,    //Faded Ghost
            275,    //Lysandus
            279,    //Land Dreugh
            284,    //Ice Golem
            286,    //Stone Golem
            289,    //Centurion Sphere
            290,    //Steam Centurion
        };

        private void Start()
        {
            Instance = this;

            ModCompatibilityChecking();

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            GameManager.OnEnemySpawn += OnEnemySpawn;

            if (spellcasting)
            {
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling].SpellAnimFrames = new int[] { 0, 1, 2, 3, 3, 3 };

                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].SpellAnimFrames = new int[] { 0, 1, 2, 3, 3, 3 };

                /*//changes Quest Dragonling to use DEX unused Dragon sprite
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].MaleTexture = 1637;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].FemaleTexture = 1637;
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].CorpseTexture = EnemyBasics.CorpseTexture(1637, 25);
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].PrimaryAttackAnimFrames = new int[] { 0, 1, 2, -1, 3, 4, 5, 6, 7 };
                EnemyBasics.Enemies[(int)MobileTypes.Dragonling_Alternate].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };*/

                EnemyBasics.Enemies[(int)MobileTypes.Imp].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.OrcShaman].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Wraith].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.FrostDaedra].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.FireDaedra].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Daedroth].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Vampire].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.DaedraSeducer].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.VampireAncient].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.DaedraLord].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Lich].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.AncientLich].CastsMagic = true;

                EnemyBasics.Enemies[(int)MobileTypes.Spriggan].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Spriggan].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Spriggan].SpellAnimFrames = new int[] { 0, 1, 2, 3, 3, 3 };

                EnemyBasics.Enemies[(int)MobileTypes.Nymph].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Nymph].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Nymph].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 5 };

                EnemyBasics.Enemies[(int)MobileTypes.Zombie].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Zombie].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Zombie].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };

                EnemyBasics.Enemies[(int)MobileTypes.Mummy].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.Mummy].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.Mummy].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };

                EnemyBasics.Enemies[(int)MobileTypes.FireAtronach].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.FireAtronach].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.FireAtronach].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };
                EnemyBasics.Enemies[(int)MobileTypes.IronAtronach].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.IronAtronach].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.IronAtronach].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };
                EnemyBasics.Enemies[(int)MobileTypes.FleshAtronach].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.FleshAtronach].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.FleshAtronach].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };
                EnemyBasics.Enemies[(int)MobileTypes.IceAtronach].CombatFlags = MobileCombatFlags.Spells;
                EnemyBasics.Enemies[(int)MobileTypes.IceAtronach].CastsMagic = true;
                EnemyBasics.Enemies[(int)MobileTypes.IceAtronach].SpellAnimFrames = new int[] { 0, 1, 2, 3, 4, 4 };
            }
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Skills"))
            {
                skillCooldownBase = settings.GetValue<int>("Skills", "BaseCooldown");
                skillCooldownRange = settings.GetValue<int>("Skills", "CooldownVariance");
                //skillSlowEnemies = settings.GetValue<bool>("Skills", "SlowEnemies");
            }
            if (change.HasChanged("Charge"))
            {
                chargeSpeedMult = settings.GetValue<int>("Charge", "Speed");
                chargeStartupDuration = settings.GetValue<int>("Charge", "StartupDuration") * 0.01f;
                chargeDistanceMin = settings.GetValue<int>("Charge", "MinimumDistance");
                chargeDistanceMax = settings.GetValue<int>("Charge", "MaximumDistance");
                chargeRangeMax = settings.GetValue<int>("Charge", "MaximumRangeOffset");
            }
            if (change.HasChanged("Jump"))
            {
                jumpSpeedMult = settings.GetValue<int>("Jump", "Speed");
                jumpStartupDuration = settings.GetValue<int>("Jump", "StartupDuration") * 0.01f;
                jumpDistanceMin = settings.GetValue<int>("Jump", "MinimumDistance");
                jumpDistanceMax = settings.GetValue<int>("Jump", "MaximumDistance");
                jumpRangeMax = settings.GetValue<int>("Jump", "MaximumRangeOffset");
            }
            if (change.HasChanged("Dash"))
            {
                dashSpeedMult = settings.GetValue<int>("Dash", "Speed");
                dashTargetDistance = settings.GetValue<int>("Dash", "TargetDistance");
            }
            if (change.HasChanged("Cleave"))
            {
                cleaveExcludeTarget = settings.GetValue<bool>("Cleave", "ExcludeTarget");
                cleavePower = (float)settings.GetValue<int>("Cleave", "Power")*0.01f;
                cleaveReach = settings.GetValue<float>("Cleave", "Reach");
                cleaveAngle = (float)settings.GetValue<int>("Cleave", "Angle");
            }
            if (change.HasChanged("Particles"))
            {
                vfx = settings.GetValue<bool>("Particles", "Enable");
                vfxChargeInterval = settings.GetValue<int>("Particles", "ChargeInterval")*0.01f;
            }
            if (change.HasChanged("Maneuvers"))
            {
                maneuvers = settings.GetValue<bool>("Maneuvers", "Enabled");
                maneuversThreshold = settings.GetValue<int>("Maneuvers", "MinimumDistance");
            }
            if (change.HasChanged("MovementOverrides"))
            {
                movement = settings.GetValue<bool>("MovementOverrides", "Enabled");
                movementFlyInside = settings.GetValue<bool>("MovementOverrides", "AllowInteriorFlying");
                movementFlyHeight = settings.GetValue<int>("MovementOverrides", "FlyingHeight");
                movementRetreatSpeed = settings.GetValue<float>("MovementOverrides", "RetreatSpeed");
                movementRetreatDistanceMin = settings.GetValue<int>("MovementOverrides", "RetreatMinimumDistance");
                movementRetreatDistanceMax = settings.GetValue<int>("MovementOverrides", "RetreatMaximumDistance");
            }
            if (change.HasChanged("ImprovedSpellcasting"))
            {
                spellcasting = settings.GetValue<bool>("ImprovedSpellcasting", "Enabled");
                spellcastingReleaseFrame = settings.GetValue<int>("ImprovedSpellcasting", "ReleaseFrame");
                spellcastingInterruptKnockback = settings.GetValue<bool>("ImprovedSpellcasting", "KnockbackInterruptsSpellcasting");
                spellcastingInterruptKnockbackMinimum = (float)settings.GetValue<int>("ImprovedSpellcasting", "InterruptMinimumKnockback");
            }
        }

        private void ModCompatibilityChecking()
        {
            //Check if DEX is active
            Mod dex = ModManager.Instance.GetModFromGUID("76557441-7025-402e-a145-e3e1a28a093d");
            if (dex != null)
                hasDEX = true;

            //listen to Combat Event Handler for attacks
            Mod ceh = ModManager.Instance.GetModFromGUID("fb086c76-38e7-4d83-91dc-f29e6f1bb17e");
            if (ceh != null)
            {
                ModManager.Instance.SendModMessage(ceh.Title, "onAttackDamageCalculated", (Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int>)OnAttackDamageCalculated);
            }
        }

        void OnEnemySpawn(GameObject enemy)
        {
            enemy.AddComponent<DynamicEnemyMotor>();
        }

        public void OnAttackDamageCalculated(DaggerfallEntity attacker, DaggerfallEntity target, DaggerfallUnityItem weapon, int bodyPart, int damage)
        {
            //Cleaving
            //if attacker is the player or there is no target or recoil is disabled, do nothing
            if (attacker == GameManager.Instance.PlayerEntity)
                return;

            DynamicEnemyMotor dynamicMotor = attacker.EntityBehaviour.GetComponent<DynamicEnemyMotor>();

            if (dynamicMotor != null)
            {
                if (dynamicMotor.canCleave)
                {
                    if (cleaveExcludeTarget)
                        dynamicMotor.TryCleave(target.EntityBehaviour);
                    else
                        dynamicMotor.TryCleave();
                }
            }
        }
    }
}
