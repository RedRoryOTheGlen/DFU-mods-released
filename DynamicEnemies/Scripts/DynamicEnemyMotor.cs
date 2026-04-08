using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace DynamicEnemiesMod
{
    public class DynamicEnemyMotor : MonoBehaviour
    {
        DaggerfallEntityBehaviour behaviour;
        EnemyEntity entity;
        EnemyMotor motor;
        EnemySenses senses;
        EnemySounds sounds;
        EnemyAttack attack;
        MobileUnit mobile;
        DaggerfallMobileUnit dfMobile;
        EntityEffectManager entityEffectManager;

        QuestResourceBehaviour behaviourQuest;

        CharacterController controller;
        DaggerfallAudioSource dfAudioSource;

        EnemyMotor.TakeActionCallback defaultTakeAction;

        IEnumerator attacking;

        int skillCooldown = 3;
        float lastSkillUse;

        int damage = 0;

        LayerMask layerMask;

        public bool prefersRanged;
        public bool canJumpAttack;
        public bool canCleave;
        public bool isSlow;
        public bool canSleep;
        public bool canDrown;

        float lastAttack;
        float lastCleave;
        List<DaggerfallEntityBehaviour> attacked = new List<DaggerfallEntityBehaviour>();

        float spotTime = 0;

        //state events
        bool sleeping;
        DaggerfallDateTime sleepTime;

        bool swimming = false;
        int tileMapIndex = -1;

        bool submerged = false;
        int currentBreath = -1;
        int intervalBreath = 0;

        bool CanRangedAttack
        {
            get
            {
                if (!prefersRanged)
                    return false;

                if (mobile.Enemy.CastsMagic)
                {
                    if (entity.CurrentMagicka < 1 && !mobile.Enemy.HasRangedAttack2)
                        return false;
                }

                return true;
            }
        }

        bool CanDrown
        {
            get
            {
                if (behaviourQuest != null)
                    return false;

                if (mobile.Enemy.Behaviour == MobileBehaviour.Aquatic)
                    return false;

                if (!canDrown)
                    return false;

                if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeonCastle)
                    return false;

                return true;
            }
        }

        private void Start()
        {
            behaviour = gameObject.GetComponent<DaggerfallEntityBehaviour>();
            behaviourQuest = gameObject.GetComponent<QuestResourceBehaviour>();
            entity = behaviour.Entity as EnemyEntity;

            motor = gameObject.GetComponent<EnemyMotor>();
            defaultTakeAction = motor.TakeActionHandler;
            motor.TakeActionHandler = TakeAction;

            senses = gameObject.GetComponent<EnemySenses>();
            sounds = gameObject.GetComponent<EnemySounds>();
            attack = gameObject.GetComponent<EnemyAttack>();

            mobile = gameObject.GetComponentInChildren<MobileUnit>();
            dfMobile = mobile as DaggerfallMobileUnit;

            entityEffectManager = gameObject.GetComponent<EntityEffectManager>();

            controller = gameObject.GetComponent<CharacterController>();
            dfAudioSource = gameObject.GetComponent<DaggerfallAudioSource>();

            prefersRanged = DynamicEnemies.Instance.DoesEnemyPreferRanged(mobile.Enemy.ID);
            canJumpAttack = DynamicEnemies.Instance.CanEnemyJumpAttack(mobile.Enemy.ID);
            canCleave = DynamicEnemies.Instance.CanEnemyCleave(mobile.Enemy.ID);
            isSlow = DynamicEnemies.Instance.IsEnemySlow(mobile.Enemy.ID);
            canSleep = DynamicEnemies.Instance.CanEnemySleep(mobile.Enemy.ID);
            canDrown = DynamicEnemies.Instance.CanEnemyDrown(mobile.Enemy.ID);

            layerMask = new LayerMask();
            layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            if (DynamicEnemies.Instance.spellcasting)
                ExtraEnemySpellcasting();
        }

        //Give Monsters some spellcasting
        void ExtraEnemySpellcasting()
        {
            if (!mobile.Enemy.CastsMagic)
                return;

            entityEffectManager.OnNewReadySpell += OnNewReadySpell;

            if (mobile.Enemy.ID == (int)MobileTypes.Dragonling || mobile.Enemy.ID == (int)MobileTypes.Dragonling_Alternate)
            {
                //Fireball
                //int[] spellList = new int[] { 7, 14 };
                int[] spellList = new int[] { 14 };

                /*//50% chance of using Frost element instead
                if (UnityEngine.Random.value > 0.5f)
                {
                    //Ice Storm
                    //spellList = new int[] { 16, 20 };
                    spellList = new int[] { 20 };
                }*/

                SetupSpells(spellList);

                //entityEffectManager.OnNewReadySpell -= OnNewReadySpell;
                //entityEffectManager.OnNewReadySpell += OnNewReadySpell_Dragonling;
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.Spriggan)
            {
                //Combat Chameleon
                int[] spellList = new int[] { 1000 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.Nymph || mobile.Enemy.ID == 268)
            {
                //Sleep
                int[] spellList = new int[] { 51 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.Mummy)
            {
                //Toxic Cloud, Medusa's Gaze
                int[] spellList = new int[] { 29, 35 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.Zombie)
            {
                //Troll's Blood
                int[] spellList = new int[] { 24 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.Vampire || mobile.Enemy.ID == (int)MobileTypes.VampireAncient)
            {
                //Levitate
                int[] spellList = new int[] { 4 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.FireAtronach)
            {
                //Wizard's Fire
                int[] spellList = new int[] { 7 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.IronAtronach)
            {
                //Lightning
                int[] spellList = new int[] { 31 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.FleshAtronach)
            {
                //Toxic Cloud
                int[] spellList = new int[] { 29 };

                SetupSpells(spellList);
            }
            else if (mobile.Enemy.ID == (int)MobileTypes.IceAtronach)
            {
                //Ice Bolt
                int[] spellList = new int[] { 16 };

                SetupSpells(spellList);
            }
        }

        void SetupSpells(int[] spellList)
        {
            entity.MaxMagicka = 10 * entity.Level + 100;
            entity.CurrentMagicka = entity.MaxMagicka;
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Destruction, 80);
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Restoration, 80);
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Illusion, 80);
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Alteration, 80);
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Thaumaturgy, 80);
            entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Mysticism, 80);

            foreach (int spellID in spellList)
            {
                DaggerfallConnect.Save.SpellRecord.SpellRecordData spellData;
                GameManager.Instance.EntityEffectBroker.GetClassicSpellRecord(spellID, out spellData);
                if (spellData.index == -1)
                {
                    Debug.LogError("Failed to locate enemy spell in standard spells list.");
                    continue;
                }

                EffectBundleSettings bundle;
                if (!GameManager.Instance.EntityEffectBroker.ClassicSpellRecordDataToEffectBundleSettings(spellData, BundleTypes.Spell, out bundle))
                {
                    Debug.LogError("Failed to create effect bundle for enemy spell: " + spellData.spellName);
                    continue;
                }
                entity.AddSpell(bundle);
            }
        }

        void OnNewReadySpell(EntityEffectBundle spell)
        {
            if (attacking == null)
            {
                //can fire a volley
                //Debug.Log("DYNAMIC ENEMIES - NPC IS CASTING A SPELL");
                DoSpell(senses.Target != null ? senses.Target.transform : null, spell);
            }
        }

        void OnNewReadySpell_Dragonling(EntityEffectBundle spell)
        {
            if (attacking == null)
            {
                //can fire a volley
                //Debug.Log("DYNAMIC ENEMIES - DRAGONLING IS CASTING A SPELL");
                DoSpellVolley(senses.Target.transform, spell);
            }
        }

        private void FixedUpdate()
        {
            if (entity.CurrentHealth < 1)
                return;

            UpdateTileMapIndex();

            //exhaustion
            if (entity.CurrentFatigue < 1 && canSleep)
            {
                if (!sleeping)
                {
                    sleeping = true;
                    DaggerfallUI.AddHUDText(entity.Name + " has collapsed from exhaustion");
                    sleepTime = DaggerfallUnity.Instance.WorldTime.Now.Clone();
                }
                else
                {
                    //one in-game hour after falling asleep, restore half maximum fatigue and wake up
                    if (DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() - sleepTime.ToSeconds() > 3600)
                        entity.CurrentFatigue = Mathf.RoundToInt(entity.MaxFatigue * 0.5f);
                }
            }
            else
            {
                if (sleeping)
                {
                    sleeping = false;
                    DaggerfallUI.AddHUDText(entity.Name + " has woken up");
                }
            }

            //submerged
            if (IsEntitySubmerged())
            {
                if (!submerged)
                {
                    submerged = true;
                    currentBreath = entity.MaxBreath;
                    intervalBreath = 0;
                    //motor.IsLevitating = true;
                }
            }
            else
            {
                if (submerged)
                {
                    submerged = false;
                    //motor.IsLevitating = false;
                }
            }

            //swimming
            if (IsEntityOnExteriorWater())
            {
                if (!swimming)
                {
                    swimming = true;
                    currentBreath = entity.MaxBreath;
                    intervalBreath = 0;
                }
            }
            else
            {
                if (swimming)
                    swimming = false;
            }

            //drowning
            if (submerged || swimming)
            {
                if (CanDrown)
                {
                    //breath countdown, same as the player's
                    if (currentBreath < 1)
                    {
                        DaggerfallUI.AddHUDText(entity.Name + " has drowned");
                        entity.SetHealth(0);
                    }
                    else
                    {
                        if (intervalBreath > 18)
                        {
                            currentBreath--;
                            intervalBreath = 0;
                        }
                        else
                            intervalBreath++;
                    }
                }
            }
        }

        private void Update()
        {
            if (entity.CurrentHealth < 1)
                return;

            if (sleeping)
            {
                entity.IsImmuneToParalysis = false;
                entity.IsParalyzed = true;
                mobile.FreezeAnims = true;
            }
        }

        private void LateUpdate()
        {
            if (entity.CurrentHealth < 1)
                return;

            if (attacked.Count > 0 && Time.time - lastAttack > 0.1f)
                attacked.Clear();

            if (senses.Target != null && senses.Target.Entity.CurrentHealth > 0)
            {
                if (DynamicEnemies.Instance.movementFlyInside)
                {
                    //track how long the target has been in sight
                    if (senses.TargetInSight)
                        spotTime += Time.deltaTime;
                    else
                        spotTime = 0;
                }
            }

            if (swimming)
            {
                //offset billboard halfway down
                float offset = (controller.height / 2);
                if (CanDrown)
                {
                    float breath = 1 + (1 - ((float)currentBreath / (float)entity.MaxBreath));
                    offset *= breath;
                }
                mobile.transform.localPosition = Vector3.down * offset;
            }
        }

        void UpdateTileMapIndex()
        {
            tileMapIndex = -1;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
                return;

            // Player must be above a known terrain object
            DaggerfallTerrain terrain = GetTerrain();
            if (!terrain)
                return;

            // The terrain must have a valid tilemap array
            if (terrain.TileMap == null || terrain.TileMap.Length == 0)
                return;

            // Get player relative position from terrain origin
            Vector3 relativePos = transform.position - terrain.transform.position;

            // Convert X, Z position into 0-1 domain
            float dim = MapsFile.WorldMapTerrainDim * MeshReader.GlobalScale;
            float u = relativePos.x / dim;
            float v = relativePos.z / dim;

            // Get clamped offset into tilemap array
            int x = Mathf.Clamp((int)(MapsFile.WorldMapTileDim * u), 0, MapsFile.WorldMapTileDim - 1);
            int y = Mathf.Clamp((int)(MapsFile.WorldMapTileDim * v), 0, MapsFile.WorldMapTileDim - 1);

            // Update index - divide by 4 to find actual tile base as each tile has 4x variants (flip, rotate, etc.)
            tileMapIndex = terrain.TileMap[y * MapsFile.WorldMapTileDim + x].r / 4;

            //Debug.LogFormat("X={0}, Z={1}, Index={2}", x, y, playerTilemapIndex);
        }

        DaggerfallTerrain GetTerrain()
        {
            RaycastHit hit;
            Ray ray = new Ray(transform.position, Vector3.down);
            if (Physics.Raycast(ray, out hit, 128, layerMask))
            {
                DaggerfallTerrain terrain = hit.collider.gameObject.GetComponent<DaggerfallTerrain>();
                if (terrain != null)
                    return terrain;
            }

            return null;
        }

        bool IsEntitySubmerged()
        {
            if (!GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)    //is not inside dungeon
                return false;
            else if (GameManager.Instance.PlayerEnterExit.blockWaterLevel == 10000)     //dungeon does not have a water level
                return false;
            else if ((transform.position.y + (76 * MeshReader.GlobalScale) - 0.95f) >= (GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale))    //entity is not fully below the water level
                return false;

            return true;
        }

        bool IsEntityOnExteriorWater()
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)    //is inside dungeon
                return false;

            return tileMapIndex == 0 && IsEntityGrounded();
        }

        bool IsEntityGrounded()
        {
            if (((mobile.Enemy.Behaviour == MobileBehaviour.Flying || motor.IsLevitating) && !entity.IsParalyzed) || mobile.Enemy.Behaviour == MobileBehaviour.Spectral)
                return false;

            RaycastHit hit;
            Ray ray = new Ray(transform.position + Vector3.up, Vector3.down);
            if (Physics.Raycast(ray, out hit, 2, layerMask))
            {
                Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                if (terrain != null ||
                    hit.collider is MeshCollider ||
                    !hit.collider.isTrigger
                    )
                    return true;
            }

            return false;
        }

        float YawCheck(Vector3 targetPosition, bool signed = false)
        {
            float angle = Vector3.SignedAngle(transform.forward, transform.position - targetPosition, Vector3.up);

            if (signed)
                return angle;
            else
                return Mathf.Abs(angle);
        }

        float PitchCheck(Vector3 targetPosition, bool signed = false)
        {
            Vector3 direction = targetPosition - transform.position;
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(direction,Vector3.up), direction, transform.right);

            if (!signed)
                angle = Mathf.Abs(angle);

            //Debug.Log("DYNAMIC ENEMIES - TARGET PITCH ANGLE IS " + angle.ToString());
            return angle;
        }

        void TakeAction()
        {
            if (entity.CurrentHealth < 1)
                return;

            if (entity.CurrentFatigue < 1 && canSleep)
                return;

            if (attacking != null)
                return;

            // Get isPlayingOneShot for use below
            bool isPlayingOneShot = mobile.IsPlayingOneShot();
            bool flying = (mobile.Enemy.Behaviour == MobileBehaviour.Flying || mobile.Enemy.Behaviour == MobileBehaviour.Spectral || mobile.Enemy.Behaviour == MobileBehaviour.Aquatic || motor.IsLevitating) ? true : false;
            float distance = Mathf.Infinity;
            float height = 0;
            float pitch = 0;
            if (senses.Target != null && senses.TargetInSight)
            {
                distance = senses.DistanceToTarget;
                height = Mathf.Abs((senses.Target.transform.position - transform.position).y);
                pitch = PitchCheck(senses.Target.transform.position);
            }

            //check if target is in sight
            if (!isPlayingOneShot &&
                (senses.Target != null && (senses.Target.Entity.CurrentHealth > 0 && senses.Target != GameManager.Instance.PlayerEntityBehaviour || (senses.Target == GameManager.Instance.PlayerEntityBehaviour && motor.IsHostile))) &&
                senses.TargetInSight &&
                attack.MeleeTimer == 0 && Time.time-lastSkillUse > skillCooldown &&
                //Aquatic enemies can only perform maneuvers if their target is in water
                (mobile.Enemy.Behaviour != MobileBehaviour.Aquatic || (mobile.Enemy.Behaviour == MobileBehaviour.Aquatic && GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && senses.Target.transform.position.y < (GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale))))
            {
                bool usedSkill = false;

                if (!usedSkill && !isSlow && (prefersRanged || (canJumpAttack && UnityEngine.Random.value > 0.8f)) && distance < (attack.MeleeDistance * DynamicEnemies.Instance.dashTargetDistance))
                {
                    //can backdash
                    usedSkill = true;
                    DoBackDash(senses.Target.transform);
                }

                if (DynamicEnemies.Instance.maneuvers && !usedSkill && !isSlow && !prefersRanged && distance > attack.MeleeDistance * DynamicEnemies.Instance.maneuversThreshold)
                {
                    //target is far away
                    usedSkill = true;
                    if (pitch > 15 && !flying)
                        DoLeapAttack(senses.Target.transform);
                    else
                        DoChargeAttack(senses.Target.transform);
                }

                if (!usedSkill && !isSlow && !prefersRanged)
                    {
                    //can charge
                    if (!flying && (pitch > 15 || canJumpAttack) && (distance < (attack.MeleeDistance * DynamicEnemies.Instance.jumpDistanceMax) && distance > attack.MeleeDistance * DynamicEnemies.Instance.jumpDistanceMin))
                    {
                        usedSkill = true;
                        DoLeapAttack(senses.Target.transform);
                    }
                    else if ((flying || pitch <= 15) && distance < (attack.MeleeDistance * DynamicEnemies.Instance.chargeDistanceMax) && distance > attack.MeleeDistance * DynamicEnemies.Instance.chargeDistanceMin)
                    {
                        usedSkill = true;
                        DoChargeAttack(senses.Target.transform);
                    }
                }

                if (usedSkill)
                {
                    /*ResetSkillTimer();
                    attack.ResetMeleeTimer();*/
                    return;
                }
            }

            defaultTakeAction();
            //TakeActionOriginal();

            //do movement overrides here
            if (DynamicEnemies.Instance.movement)
            {
                if (!isPlayingOneShot &&
                    senses.Target != null &&
                    senses.Target.Entity.CurrentHealth > 0 &&
                    senses.TargetInSight)
                {
                    Vector3 motion = Vector3.zero;

                    //ranged enemies should attempt to keep their distance
                    if (prefersRanged)
                    {
                        //scale up movement speed to counteract base DFU trying to make them move towards their target
                        //also enemy gives up retreating if player gets inside melee distance
                        if (distance >= attack.MeleeDistance * DynamicEnemies.Instance.movementRetreatDistanceMin && CanRangedAttack)
                        {
                            if (distance < attack.MeleeDistance * DynamicEnemies.Instance.movementRetreatDistanceMax)
                            {
                                Vector3 retreatDir = transform.position - senses.Target.transform.position;
                                retreatDir.y = 0;
                                motion += retreatDir.normalized * 2 * DynamicEnemies.Instance.movementRetreatSpeed;
                            }

                            /*if (distance < attack.MeleeDistance * 4)
                                motion += (transform.position - senses.Target.transform.position).normalized * 2;
                            else if (distance < attack.MeleeDistance * 8)
                                motion += (transform.position - senses.Target.transform.position).normalized * 1;*/
                        }
                    }

                    //flying creatures should attempt to keep their height advantage
                    if (flying && mobile.Enemy.Behaviour != MobileBehaviour.Aquatic &&
                        //fly inside only if target has been spotted for some time
                        ((DynamicEnemies.Instance.movementFlyInside && (!GameManager.Instance.PlayerEnterExit.IsPlayerInside || (GameManager.Instance.PlayerEnterExit.IsPlayerInside && spotTime > 3))) ||
                        //do not fly inside
                        (!DynamicEnemies.Instance.movementFlyInside && !GameManager.Instance.PlayerEnterExit.IsPlayerInside)) &&
                        height < attack.MeleeDistance * DynamicEnemies.Instance.movementFlyHeight)
                    {
                        //can fly up
                        //check if target is flying
                        bool fly = true;
                        if (senses.Target == GameManager.Instance.PlayerEntityBehaviour)
                            fly = !GameManager.Instance.PlayerMotor.IsLevitating;
                        else
                        {
                            MobileUnit targetMobile = senses.Target.GetComponentInChildren<MobileUnit>();
                            if (targetMobile != null)
                            {
                                EnemyMotor targetMotor = senses.Target.GetComponent<EnemyMotor>();
                                DaggerfallEntityBehaviour targetBehaviour = senses.Target.GetComponent<DaggerfallEntityBehaviour>();
                                //if target is flying/levitating and not paralyzed, or is a specter, do not fly over them
                                if (((targetMobile.Enemy.Behaviour == MobileBehaviour.Flying || motor.IsLevitating) && !targetBehaviour.Entity.IsParalyzed) || targetMobile.Enemy.Behaviour == MobileBehaviour.Spectral)
                                    fly = false;
                            }
                        }

                        if (fly)
                            motion += Vector3.up;
                    }

                    if (motion.sqrMagnitude > 0)
                    {
                        float speed = (entity.Stats.LiveSpeed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale;
                        controller.Move((motion * speed) * Time.deltaTime);
                    }
                }
            }

        }

        void ResetSkillTimer(bool fast = false)
        {
            lastSkillUse = Time.time;

            skillCooldown = DynamicEnemies.Instance.skillCooldownBase + (int)GameManager.Instance.PlayerEntity.Reflexes;
            skillCooldown += UnityEngine.Random.Range(-DynamicEnemies.Instance.skillCooldownRange, DynamicEnemies.Instance.skillCooldownRange);

            if (fast)
                skillCooldown = Mathf.FloorToInt(skillCooldown*0.5f);

            if (skillCooldown < 1)
                skillCooldown = 1;
        }

        void PlayDustVFX(Vector3 point, float scale = 1, int fps = 10)
        {
            if (!DynamicEnemies.Instance.vfx)
                return;

            GameObject go = GameObjectHelper.CreateDaggerfallBillboardGameObject(112398, 0, null);
            go.name = "DustVFX";
            Billboard c = go.GetComponent<Billboard>();
            go.transform.position = point;
            go.transform.localScale *= scale;
            c.FaceY = true;
            c.OneShot = true;
            c.FramesPerSecond = fps;
        }
        public Vector3 FindGroundPosition(Vector3 pos)
        {
            float distance = 2;
            RaycastHit hit;
            Ray ray = new Ray(pos + Vector3.up, Vector3.down);
            if (Physics.Raycast(ray, out hit, distance + 1, layerMask))
            {
                Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                if (terrain != null ||
                    hit.collider is MeshCollider ||
                    !hit.collider.isTrigger
                    )
                    return hit.point;
            }

            /*if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
            {
                distance = 2;
                ray = new Ray(pos + Vector3.up, Vector3.down);
                if (Physics.Raycast(ray, out hit, distance+1, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        return hit.point;
                }
            }
            else
            {
                ray = new Ray(pos + (Vector3.up * distance), Vector3.down);
                if (Physics.Raycast(ray, out hit, distance, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        return hit.point;
                }
            }*/

            return pos;
        }
        public void DoChargeAttack(Transform target)
        {
            //Debug.Log("DYNAMIC ENEMIES - PERFORMING CHARGE!");
            attacking = ChargeAttack(target, DynamicEnemies.Instance.chargeDistanceMax + DynamicEnemies.Instance.chargeRangeMax);
            StartCoroutine(attacking);
        }
        public void DoLeapAttack(Transform target)
        {
            //Debug.Log("DYNAMIC ENEMIES - PERFORMING LEAP!");
            attacking = LeapAttack(target, DynamicEnemies.Instance.jumpDistanceMax+ DynamicEnemies.Instance.jumpRangeMax);
            StartCoroutine(attacking);
        }
        public void DoBackDash(Transform target)
        {
            //Debug.Log("DYNAMIC ENEMIES - PERFORMING DASH!");
            attacking = BackDash(target);
            StartCoroutine(attacking);
        }
        IEnumerator ChargeAttack(Transform target, float maxRange = 10)
        {
            motor.enabled = false;
            Vector3 direction = Vector3.up;

            // Monster speed of movement follows the same formula as for when the player walks
            float moveSpeed = (entity.Stats.LiveSpeed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale;
            float chargeSpeed = moveSpeed * DynamicEnemies.Instance.chargeSpeedMult;
            bool flying = (mobile.Enemy.Behaviour == MobileBehaviour.Flying || mobile.Enemy.Behaviour == MobileBehaviour.Spectral || mobile.Enemy.Behaviour == MobileBehaviour.Aquatic || motor.IsLevitating) ? true : false;

            //force play the attack animation
            mobile.ChangeEnemyState(MobileStates.PrimaryAttack);
            yield return new WaitForSeconds(0.2f);

            //freeze for startup
            float startupTimer = 0;
            while (startupTimer < DynamicEnemies.Instance.chargeStartupDuration)
            {
                mobile.FreezeAnims = true;

                //turn to face target
                direction = (target.position - transform.position).normalized;
                transform.forward = direction;

                startupTimer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (behaviour.EntityType == EntityTypes.EnemyClass)
            {
                Genders gender;
                if (mobile.Enemy.Gender == MobileGender.Male || entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                    gender = Genders.Male;
                else
                    gender = Genders.Female;
                sounds.PlayCombatVoice(gender, true);
            }
            else
            {
                dfAudioSource.PlayOneShot(sounds.AttackSound);
            }

            Vector3 startPoint = transform.position;
            float weight = 1;
            if (DaggerfallUnity.Settings.EnhancedCombatAI)
                weight = 0.75f;
            Vector3 predictedPoint = Vector3.Lerp(senses.PredictNextTargetPos(chargeSpeed), target.position, weight);
            Vector3 targetPoint = predictedPoint + ((predictedPoint - transform.position).normalized * attack.MeleeDistance);

            direction = (targetPoint - startPoint).normalized;

            //apply maximum range
            if (flying)
                maxRange += DynamicEnemies.Instance.movementFlyHeight * 1.5f;

            if ((targetPoint - startPoint).magnitude > attack.MeleeDistance * maxRange)
                targetPoint = startPoint + (direction * attack.MeleeDistance * maxRange);

            if (!flying)
                targetPoint = FindGroundPosition(targetPoint);

            //turn to face target before charging
            transform.forward = direction;

            if (flying)
                targetPoint += Vector3.up * (controller.height * 0.75f);
            else
                targetPoint += Vector3.up * (controller.height * 0.5f);

            float distancePoint = Vector3.Distance(transform.position, targetPoint);
            float distanceTarget = Vector3.Distance(transform.position, target.position);
            bool hasAttacked = false;

            float time = distancePoint / chargeSpeed;
            float timer = 0;
            float i = 0;
            while (timer <= time)
            {
                if (entity.CurrentHealth < 1)
                    break;

                if (motor.KnockbackSpeed > 0)
                    motor.KnockbackSpeed = 0;

                distanceTarget = Vector3.Distance(transform.position, target.position);

                if (distanceTarget <= attack.MeleeDistance && !hasAttacked)
                {
                    MeleeDamage();
                    hasAttacked = true;
                    //exclude the target from the follow-up attack if possible
                    break;
                }

                float index = timer / time;
                index = index * index;

                float end = time - timer;
                if (end > 0.25f)
                    mobile.FreezeAnims = true;
                else
                    mobile.FreezeAnims = false;

                //lerping?
                //index = Mathf.Sin(index * Mathf.PI * 0.5f);
                //index = index * index;
                Vector3 newPoint = Vector3.Lerp(startPoint, targetPoint, index);

                //check collision here
                Vector3 origin = transform.position;
                Vector3 dir = (newPoint - transform.position).normalized;
                Ray ray = new Ray(origin, dir);
                RaycastHit hit = new RaycastHit();
                /*if (Physics.Raycast(ray, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        break;
                }*/
                Vector3 point1 = origin + (Vector3.up * (controller.height / 4));
                Vector3 point2 = origin - (Vector3.up * (controller.height / 4));
                if (Physics.CapsuleCast(point1, point2, controller.radius/4, dir, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        break;
                }

                transform.position = newPoint;

                //play VFX every interval
                if (i > DynamicEnemies.Instance.vfxChargeInterval)
                {
                    if (!flying)
                        PlayDustVFX(transform.position - (Vector3.up * (controller.height / 2)), 1f, 15);
                    i = 0;
                }
                else
                    i += Time.deltaTime;

                timer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (!flying && entity.CurrentHealth > 0)
                transform.position = motor.FindGroundPosition() + (Vector3.up * (controller.height / 2));

            ResetSkillTimer();
            attack.ResetMeleeTimer();
            motor.enabled = true;
            attacking = null;
        }
        IEnumerator LeapAttack(Transform target, float maxRange = 5)
        {
            motor.enabled = false;
            Vector3 direction = Vector3.up;

            // Monster speed of movement follows the same formula as for when the player walks
            float moveSpeed = (entity.Stats.LiveSpeed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale;
            float leapSpeed = moveSpeed * DynamicEnemies.Instance.jumpSpeedMult;
            bool flying = (mobile.Enemy.Behaviour == MobileBehaviour.Flying || mobile.Enemy.Behaviour == MobileBehaviour.Spectral || mobile.Enemy.Behaviour == MobileBehaviour.Aquatic || motor.IsLevitating) ? true : false;

            //force play the attack animation
            mobile.ChangeEnemyState(MobileStates.PrimaryAttack);
            yield return new WaitForSeconds(0.2f);

            //freeze for startup
            float startupTimer = 0;
            while (startupTimer < DynamicEnemies.Instance.jumpStartupDuration)
            {
                mobile.FreezeAnims = true;

                //turn to face target
                direction = (target.position - transform.position).normalized;
                transform.forward = direction;

                startupTimer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (behaviour.EntityType == EntityTypes.EnemyClass)
            {
                Genders gender;
                if (mobile.Enemy.Gender == MobileGender.Male || entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                    gender = Genders.Male;
                else
                    gender = Genders.Female;
                sounds.PlayCombatVoice(gender, true);
            }
            else
            {
                dfAudioSource.PlayOneShot(sounds.AttackSound);
            }

            Vector3 startPoint = transform.position;
            float weight = 1;
            if (DaggerfallUnity.Settings.EnhancedCombatAI)
                weight = 0.75f;
            Vector3 predictedPoint = Vector3.Lerp(senses.PredictNextTargetPos(leapSpeed), target.position, weight);
            //Vector3 targetPoint = predictedPoint + ((predictedPoint - transform.position).normalized * attack.MeleeDistance);
            Vector3 targetPoint = predictedPoint - ((predictedPoint - transform.position).normalized * (attack.MeleeDistance/2));
            direction = (targetPoint - startPoint).normalized;

            //apply maximum range and ground the target point
            if ((targetPoint - startPoint).magnitude > attack.MeleeDistance * maxRange)
                targetPoint = FindGroundPosition(startPoint + (direction * attack.MeleeDistance * maxRange));
            else
                targetPoint = FindGroundPosition(targetPoint);

            //turn to face target before charging
            transform.forward = direction;

            float height = Mathf.Abs((targetPoint - startPoint).y)+1;
            if (flying)
            {
                targetPoint += Vector3.up * (controller.height);
            }
            else
            {
                targetPoint += Vector3.up * (controller.height / 2);
            }

            //play VFX on start
            PlayDustVFX(transform.position - (Vector3.up * (controller.height / 2)), 2, 10);

            float distancePoint = Vector3.Distance(transform.position, targetPoint);
            float distanceTarget = Vector3.Distance(transform.position, target.position);
            bool hasAttacked = false;

            float time = distancePoint/ leapSpeed;
            float timer = 0;
            while (timer <= time)
            {
                if (entity.CurrentHealth < 1)
                    break;

                if (motor.KnockbackSpeed > 0)
                    motor.KnockbackSpeed = 0;

                distanceTarget = Vector3.Distance(transform.position, target.position);

                if (distanceTarget <= attack.MeleeDistance && !hasAttacked)
                {
                    MeleeDamage();
                    hasAttacked = true;
                    //exclude the target from the follow-up attack if possible
                    break;
                }

                float index = timer / time;

                float end = time - timer;
                if (end > 0.25f)
                    mobile.FreezeAnims = true;
                else
                    mobile.FreezeAnims = false;

                //lerping?
                //index = Mathf.Sin(index * Mathf.PI * 0.5f);
                //index = index * index;
                Vector3 newPoint = Vector3.Lerp(startPoint, targetPoint, index);
                float verticalIndex = index;
                if (index > 0.5)
                    verticalIndex = 1 - index;
                newPoint += Vector3.up * height * verticalIndex;

                //check collision here
                Vector3 origin = transform.position;
                Vector3 dir = (newPoint - transform.position).normalized;
                Ray ray = new Ray(origin, dir);
                RaycastHit hit = new RaycastHit();
                /*if (Physics.Raycast(ray, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        break;
                }*/
                if (Physics.SphereCast(origin, controller.radius/4, dir, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        break;
                }

                transform.position = newPoint;

                timer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            if (!flying && entity.CurrentHealth > 0)
            {
                transform.position = motor.FindGroundPosition() + (Vector3.up * (controller.height / 2));

                //play VFX on land
                PlayDustVFX(transform.position - (Vector3.up * (controller.height / 2)), 2, 10);
            }

            ResetSkillTimer();
            attack.ResetMeleeTimer();
            motor.enabled = true;
            attacking = null;
        }
        IEnumerator BackDash(Transform target)
        {
            motor.enabled = false;

            // Monster speed of movement follows the same formula as for when the player walks
            float moveSpeed = (entity.Stats.LiveSpeed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale;
            float dashSpeed = moveSpeed * DynamicEnemies.Instance.dashSpeedMult;

            //force play the attack animation
            if (mobile.Enemy.HasRangedAttack1)
            {
                if (mobile.Enemy.HasRangedAttack2)
                    mobile.ChangeEnemyState(MobileStates.RangedAttack2);
                else if (!mobile.Enemy.CastsMagic)
                    mobile.ChangeEnemyState(MobileStates.RangedAttack1);
            }

            if (behaviour.EntityType == EntityTypes.EnemyClass)
            {
                Genders gender;
                if (mobile.Enemy.Gender == MobileGender.Male || entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                    gender = Genders.Male;
                else
                    gender = Genders.Female;
                sounds.PlayCombatVoice(gender, true);
            }
            else
            {
                dfAudioSource.PlayOneShot(sounds.AttackSound);
            }

            Vector3 startPoint = transform.position;
            Vector3 targetPoint = FindGroundPosition(transform.position + ((transform.position - target.position).normalized * (attack.MeleeDistance * 2))) + (Vector3.up * (controller.height / 2));

            float distancePoint = Vector3.Distance(startPoint, targetPoint);
            //Debug.DrawLine(startPoint, targetPoint, Color.blue, 3, false);

            float height = Mathf.Abs((targetPoint - startPoint).y) + 1;

            float time = distancePoint / dashSpeed;
            float timer = 0;
            while (timer <= time)
            {
                if (entity.CurrentHealth < 1)
                    break;

                if (motor.KnockbackSpeed > 0)
                    motor.KnockbackSpeed = 0;

                float index = timer / time;
                Vector3 newPoint = Vector3.Lerp(startPoint, targetPoint, index);
                float verticalIndex = index;
                if (index > 0.5)
                    verticalIndex = 1 - index;
                newPoint += Vector3.up * height * verticalIndex;

                //check collision here
                Vector3 origin = transform.position;
                Vector3 dir = (newPoint - transform.position).normalized;
                float step = (newPoint - transform.position).magnitude;
                Ray ray = new Ray(origin, dir);
                RaycastHit hit = new RaycastHit();
                if (Physics.Raycast(ray, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                    {
                        //Debug.DrawLine(origin, hit.point, Color.red, 3, false);
                        break;
                    }
                    else
                    {
                        //Debug.DrawLine(origin, hit.point, Color.yellow, 3, false);
                    }
                } else
                {
                    //Debug.DrawLine(origin, origin + dir * step, Color.green, 3, false);
                }
                /*if (Physics.SphereCast(origin, controller.radius/4, dir, out hit, (newPoint - transform.position).magnitude, layerMask))
                {
                    Terrain terrain = hit.collider.gameObject.GetComponent<Terrain>();
                    if (terrain != null ||
                        hit.collider is MeshCollider ||
                        !hit.collider.isTrigger
                        )
                        break;
                }*/

                transform.position = newPoint;

                timer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            ResetSkillTimer();
            attack.ResetMeleeTimer();

            motor.enabled = true;

            if (entity.CurrentHealth > 0)
                transform.position = motor.FindGroundPosition() + (Vector3.up * (controller.height/2));

            attacking = null;
        }
        public void TryCleave(DaggerfallEntityBehaviour excludeTarget = null)
        {
            if (Time.time - lastCleave > 0.1f)
            {
                //Debug.Log("DYNAMIC ENEMIES - PERFORMING CLEAVE!");
                lastCleave = Time.time;
                DoCleave(excludeTarget);
            }
        }
        void DoCleave(DaggerfallEntityBehaviour excludeTarget = null)
        {
            if (behaviour)
            {
                List<DaggerfallEntityBehaviour> enemies = new List<DaggerfallEntityBehaviour>();
                //is player a valid target
                if ((excludeTarget == null || (excludeTarget != null && excludeTarget != GameManager.Instance.PlayerEntityBehaviour)) &&
                    !attacked.Contains(GameManager.Instance.PlayerEntityBehaviour) &&
                    Vector3.Distance(transform.position, GameManager.Instance.PlayerEntityBehaviour.transform.position) <= attack.MeleeDistance * DynamicEnemies.Instance.cleaveReach &&
                    motor.IsHostile)
                    enemies.Add(GameManager.Instance.PlayerEntityBehaviour);
                foreach (DaggerfallEntityBehaviour entity in ActiveGameObjectDatabase.GetActiveEnemyBehaviours())
                {
                    EnemyMotor targetMotor = GetComponent<EnemyMotor>();
                    if (entity != behaviour &&
                        (excludeTarget == null || (excludeTarget != null && excludeTarget != entity)) &&
                        //hostile enemies can cleave other hostile enemies, non-hostile enemies can only cleave hostile enemies
                        (motor.IsHostile || (!motor.IsHostile && targetMotor.IsHostile)) &&
                        //enemies in the same team won't cleave each other
                        mobile.Enemy.Team != entity.Entity.Team &&
                        !attacked.Contains(entity) &&
                        Vector3.Distance(transform.position, entity.transform.position) <= attack.MeleeDistance * DynamicEnemies.Instance.cleaveReach)
                        enemies.Add(entity);
                }

                if (enemies.Count > 0)
                {
                    foreach (DaggerfallEntityBehaviour enemy in enemies)
                    {
                        EnemyEntity targetEntity = null;
                        if (enemy != GameManager.Instance.PlayerEntityBehaviour)
                            targetEntity = enemy.Entity as EnemyEntity;

                        // Switch to hand-to-hand if enemy is immune to weapon
                        DaggerfallUnityItem weapon = entity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                        if (weapon != null && targetEntity != null && targetEntity.MobileEnemy.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                            weapon = null;

                        damage = 0;

                        // Melee hit detection, matched to classic
                        if (Vector3.Distance(transform.position, enemy.transform.position) <= attack.MeleeDistance * DynamicEnemies.Instance.cleaveReach && senses.TargetIsWithinYawAngle(DynamicEnemies.Instance.cleaveAngle, enemy.transform.position))
                        {
                            attacked.Add(enemy);
                            if (enemy == GameManager.Instance.PlayerEntityBehaviour)
                                damage = ApplyCleaveDamageToPlayer(weapon);
                            else
                                damage = ApplyCleaveDamageToNonPlayer(enemy, weapon, transform.forward);
                        }

                        sounds.PlayMissSound(weapon);
                    }

                    //Debug.Log("DYNAMIC ENEMIES - PERFORMED CLEAVE ON " + enemies.Count.ToString() + " TARGETS!");
                }
            }
        }

        public void DoSpell(Transform target, EntityEffectBundle spell)
        {
            //Debug.Log("DYNAMIC ENEMIES - PERFORMING SPELL!");
            attacking = Spell(target, spell);
            StartCoroutine(attacking);
        }
        IEnumerator Spell(Transform target, EntityEffectBundle spell)
        {
            entityEffectManager.enabled = false;
            motor.enabled = false;
            Vector3 direction = Vector3.up;

            //force play the attack animation
            mobile.ChangeEnemyState(MobileStates.Spell);

            int clip = (int)SoundClips.CastSpell1 + UnityEngine.Random.Range(0, 5);
            dfAudioSource.PlayOneShot((SoundClips)clip);

            //wait for active frame
            int frame = 0;
            float interval = 0;
            bool interrupted = false;
            while (frame < DynamicEnemies.Instance.spellcastingReleaseFrame)
            {
                if (entity.CurrentHealth < 1)
                    break;

                if (DynamicEnemies.Instance.spellcastingInterruptKnockback)
                {
                    if (motor.KnockbackSpeed > DynamicEnemies.Instance.spellcastingInterruptKnockbackMinimum)
                    {
                        interrupted = true;
                        break;
                    }
                }
                else
                {
                    if (motor.KnockbackSpeed > 0)
                        motor.KnockbackSpeed = 0;
                }

                if (interval > 0.1)
                {
                    frame++;
                    interval = 0;
                }
                else
                    interval += Time.deltaTime;

                if (target != null)
                {
                    direction = (target.position - transform.position).normalized;
                    transform.forward = direction;
                }

                yield return new WaitForEndOfFrame();
            }

            if (entity.CurrentHealth > 0 && !interrupted)
            {
                //shoot spell projectile here
                entityEffectManager.SetReadySpell(spell);
                entityEffectManager.CastReadySpell();
            }

            ResetSkillTimer();
            attack.ResetMeleeTimer();
            entityEffectManager.enabled = true;
            motor.enabled = true;
            attacking = null;
        }


        public void DoSpellVolley(Transform target, EntityEffectBundle spell)
        {
            //Debug.Log("DYNAMIC ENEMIES - PERFORMING SPELL VOLLEY!");
            attacking = SpellVolley(target, spell, 4);
            StartCoroutine(attacking);
        }

        IEnumerator SpellVolley(Transform target, EntityEffectBundle spell, int count = 8)
        {
            entityEffectManager.enabled = false;
            motor.enabled = false;
            Vector3 direction = Vector3.up;

            // Monster speed of movement follows the same formula as for when the player walks

            //force play the attack animation
            if (mobile.Enemy.HasRangedAttack1)
                mobile.ChangeEnemyState(MobileStates.RangedAttack1);
            else
                mobile.ChangeEnemyState(MobileStates.PrimaryAttack);

            yield return new WaitForSeconds(0.2f);

            //freeze for startup
            float startupTimer = 0;
            while (startupTimer < DynamicEnemies.Instance.jumpStartupDuration)
            {
                mobile.FreezeAnims = true;

                //turn to face target
                direction = (target.position - transform.position).normalized;
                transform.forward = direction;

                startupTimer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            int clip = (int)SoundClips.CastSpell1 + UnityEngine.Random.Range(0, 5);
            dfAudioSource.PlayOneShot((SoundClips)clip);

            float missileSpeed = 25.0f;

            /*float weight = 1;
            if (DaggerfallUnity.Settings.EnhancedCombatAI)
                weight = 0f;
            Vector3 targetPoint = Vector3.Lerp(senses.PredictNextTargetPos(missileSpeed), target.position, weight);*/
            Vector3 targetPoint = target.position;
            direction = (targetPoint - transform.position).normalized;

            //turn to face target before charging

            targetPoint += Vector3.up * (controller.height / 2);

            int currentCount = 0;
            float interval = 0;
            while (currentCount < count)
            {
                if (entity.CurrentHealth < 1)
                    break;

                if (motor.KnockbackSpeed > 0)
                    motor.KnockbackSpeed = 0;

                mobile.FreezeAnims = true;

                if (interval > 0.0625f)
                {
                    targetPoint = target.position;
                    direction = (targetPoint - transform.position).normalized;
                    transform.forward = direction;
                    //shoot spell projectile here
                    //only first cast costs magicka
                    bool noCost = currentCount == 0 ? false : true;
                    entityEffectManager.SetReadySpell(spell, noCost);
                    entityEffectManager.CastReadySpell();
                    currentCount++;
                    interval = 0;
                }
                else
                    interval += Time.deltaTime;

                yield return new WaitForEndOfFrame();
            }

            ResetSkillTimer();
            attack.ResetMeleeTimer();
            entityEffectManager.enabled = true;
            motor.enabled = true;
            attacking = null;
        }

        //EnemyAttack
        void MeleeDamage()
        {
            if (attacked.Contains(senses.Target))
                return;

            if (behaviour)
            {
                EnemyEntity targetEntity = null;

                if (senses.Target != null && senses.Target != GameManager.Instance.PlayerEntityBehaviour)
                    targetEntity = senses.Target.Entity as EnemyEntity;

                // Switch to hand-to-hand if enemy is immune to weapon
                DaggerfallUnityItem weapon = entity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                if (weapon != null && targetEntity != null && targetEntity.MobileEnemy.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                    weapon = null;

                damage = 0;

                // Melee hit detection, matched to classic
                //removed yaw angle check and sight check
                //if (senses.Target != null && senses.TargetInSight && (senses.DistanceToTarget <= 0.25f
                if (senses.Target != null && senses.DistanceToTarget <= attack.MeleeDistance * 2)
                {
                    attacked.Add(senses.Target);
                    if (senses.Target == GameManager.Instance.PlayerEntityBehaviour)
                        damage = ApplyDamageToPlayer(weapon);
                    else
                        damage = ApplyDamageToNonPlayer(senses.Target, weapon, transform.forward);
                }

                sounds.PlayMissSound(weapon);

                if (canCleave)
                    DoCleave();
            }

            lastAttack = Time.time;
        }
        int ApplyDamageToPlayer(DaggerfallUnityItem weapon)
        {
            const int doYouSurrenderToGuardsTextID = 15;

            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

            // Calculate damage
            damage = FormulaHelper.CalculateAttackDamage(entity, playerEntity, false, 0, weapon);

            // Break any "normal power" concealment effects on enemy
            if (entity.IsMagicallyConcealedNormalPower && damage > 0)
                EntityEffectManager.BreakNormalPowerConcealmentEffects(behaviour);

            // Tally player's dodging skill
            playerEntity.TallySkill(DFCareer.Skills.Dodging, 1);

            // Handle Strikes payload from enemy to player target - this could change damage amount
            if (damage > 0 && weapon != null && weapon.IsEnchanted)
            {
                EntityEffectManager effectManager = GetComponent<EntityEffectManager>();
                if (effectManager)
                    damage = effectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, weapon, entity.Items, playerEntity.EntityBehaviour, damage);
            }

            if (damage > 0)
            {
                if (entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                {
                    // If hit by a guard, lower reputation and show the surrender dialogue
                    if (!playerEntity.HaveShownSurrenderToGuardsDialogue && playerEntity.CrimeCommitted != PlayerEntity.Crimes.None)
                    {
                        playerEntity.LowerRepForCrime();

                        DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                        messageBox.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRSCTokens(doYouSurrenderToGuardsTextID));
                        messageBox.ParentPanel.BackgroundColor = Color.clear;
                        messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
                        messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
                        messageBox.OnButtonClick += SurrenderToGuardsDialogue_OnButtonClick;
                        messageBox.Show();

                        playerEntity.HaveShownSurrenderToGuardsDialogue = true;
                    }
                    // Surrender dialogue has been shown and player refused to surrender
                    // Guard damages player if player can survive hit, or if hit is fatal but guard rejects player's forced surrender
                    else if (playerEntity.CurrentHealth > damage || !playerEntity.SurrenderToCityGuards(false))
                        SendDamageToPlayer();
                }
                else
                    SendDamageToPlayer();
            }
            else
                sounds.PlayMissSound(weapon);

            return damage;
        }
        int ApplyDamageToNonPlayer(DaggerfallEntityBehaviour target, DaggerfallUnityItem weapon, Vector3 direction, bool bowAttack = false)
        {
            if (target == null)
                return 0;
            // TODO: Merge with hit code in WeaponManager to eliminate duplicate code
            EnemyEntity entity = behaviour.Entity as EnemyEntity;
            EnemyEntity targetEntity = target.Entity as EnemyEntity;
            EnemySounds targetSounds = target.GetComponent<EnemySounds>();
            EnemyMotor targetMotor = target.transform.GetComponent<EnemyMotor>();

            // Calculate damage
            damage = FormulaHelper.CalculateAttackDamage(entity, targetEntity, false, 0, weapon);

            // Break any "normal power" concealment effects on enemy
            if (entity.IsMagicallyConcealedNormalPower && damage > 0)
                EntityEffectManager.BreakNormalPowerConcealmentEffects(behaviour);

            // Play hit sound and trigger blood splash at hit point
            if (damage > 0)
            {
                targetSounds.PlayHitSound(weapon);

                EnemyBlood blood = target.transform.GetComponent<EnemyBlood>();
                CharacterController targetController = target.transform.GetComponent<CharacterController>();
                Vector3 bloodPos = target.transform.position + targetController.center;
                bloodPos.y += targetController.height / 8;

                if (blood)
                {
                    blood.ShowBloodSplash(targetEntity.MobileEnemy.BloodIndex, bloodPos);
                }

                // Knock back enemy based on damage and enemy weight
                if (targetMotor && (targetMotor.KnockbackSpeed <= (5 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10))
                        && (behaviour.EntityType == EntityTypes.EnemyClass || targetEntity.MobileEnemy.Weight > 0)))
                {
                    float enemyWeight = targetEntity.GetWeightInClassicUnits();
                    float tenTimesDamage = damage * 10;
                    float twoTimesDamage = damage * 2;

                    float knockBackAmount = ((tenTimesDamage - enemyWeight) * 256) / (enemyWeight + tenTimesDamage) * twoTimesDamage;
                    float KnockbackSpeed = (tenTimesDamage / enemyWeight) * (twoTimesDamage - (knockBackAmount / 256));
                    KnockbackSpeed /= (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10);

                    if (KnockbackSpeed < (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)))
                        KnockbackSpeed = (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10));
                    targetMotor.KnockbackSpeed = KnockbackSpeed;
                    targetMotor.KnockbackDirection = direction;
                }

                if (DaggerfallUnity.Settings.CombatVoices && target.EntityType == EntityTypes.EnemyClass && Dice100.SuccessRoll(40))
                {
                    var targetMobileUnit = target.GetComponentInChildren<MobileUnit>();
                    Genders gender;
                    if (targetMobileUnit.Enemy.Gender == MobileGender.Male || targetEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                        gender = Genders.Male;
                    else
                        gender = Genders.Female;

                    targetSounds.PlayCombatVoice(gender, false, damage >= targetEntity.MaxHealth / 4);
                }
            }
            else
            {
                WeaponTypes weaponType = WeaponTypes.Melee;
                if (weapon != null)
                    weaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(weapon);

                if ((!bowAttack && !targetEntity.MobileEnemy.ParrySounds) || weaponType == WeaponTypes.Melee)
                    sounds.PlayMissSound(weapon);
                else if (targetEntity.MobileEnemy.ParrySounds)
                    targetSounds.PlayParrySound();
            }

            // Handle Strikes payload from enemy to non-player target - this could change damage amount
            if (weapon != null && weapon.IsEnchanted)
            {
                EntityEffectManager effectManager = GetComponent<EntityEffectManager>();
                if (effectManager)
                    damage = effectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, weapon, entity.Items, targetEntity.EntityBehaviour, damage);
            }

            targetEntity.DecreaseHealth(damage);

            if (targetMotor)
            {
                targetMotor.MakeEnemyHostileToAttacker(behaviour);
            }

            return damage;
        }
        int ApplyCleaveDamageToPlayer(DaggerfallUnityItem weapon)
        {
            const int doYouSurrenderToGuardsTextID = 15;

            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;

            // Calculate damage
            damage = FormulaHelper.CalculateAttackDamage(entity, playerEntity, false, 0, weapon);

            // Apply Cleave Power
            damage = Mathf.CeilToInt((float)damage * DynamicEnemies.Instance.cleavePower);

            // Break any "normal power" concealment effects on enemy
            if (entity.IsMagicallyConcealedNormalPower && damage > 0)
                EntityEffectManager.BreakNormalPowerConcealmentEffects(behaviour);

            // Tally player's dodging skill
            playerEntity.TallySkill(DFCareer.Skills.Dodging, 1);

            // Handle Strikes payload from enemy to player target - this could change damage amount
            if (damage > 0 && weapon != null && weapon.IsEnchanted)
            {
                EntityEffectManager effectManager = GetComponent<EntityEffectManager>();
                if (effectManager)
                    damage = effectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, weapon, entity.Items, playerEntity.EntityBehaviour, damage);
            }

            if (damage > 0)
            {
                if (entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                {
                    // If hit by a guard, lower reputation and show the surrender dialogue
                    if (!playerEntity.HaveShownSurrenderToGuardsDialogue && playerEntity.CrimeCommitted != PlayerEntity.Crimes.None)
                    {
                        playerEntity.LowerRepForCrime();

                        DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                        messageBox.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRSCTokens(doYouSurrenderToGuardsTextID));
                        messageBox.ParentPanel.BackgroundColor = Color.clear;
                        messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
                        messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
                        messageBox.OnButtonClick += SurrenderToGuardsDialogue_OnButtonClick;
                        messageBox.Show();

                        playerEntity.HaveShownSurrenderToGuardsDialogue = true;
                    }
                    // Surrender dialogue has been shown and player refused to surrender
                    // Guard damages player if player can survive hit, or if hit is fatal but guard rejects player's forced surrender
                    else if (playerEntity.CurrentHealth > damage || !playerEntity.SurrenderToCityGuards(false))
                        SendDamageToPlayer();
                }
                else
                    SendDamageToPlayer();
            }
            else
                sounds.PlayMissSound(weapon);

            return damage;
        }
        int ApplyCleaveDamageToNonPlayer(DaggerfallEntityBehaviour target, DaggerfallUnityItem weapon, Vector3 direction, bool bowAttack = false)
        {
            if (target == null)
                return 0;
            // TODO: Merge with hit code in WeaponManager to eliminate duplicate code
            EnemyEntity entity = behaviour.Entity as EnemyEntity;
            EnemyEntity targetEntity = target.Entity as EnemyEntity;
            EnemySounds targetSounds = target.GetComponent<EnemySounds>();
            EnemyMotor targetMotor = target.transform.GetComponent<EnemyMotor>();

            // Calculate damage
            damage = FormulaHelper.CalculateAttackDamage(entity, targetEntity, false, 0, weapon);

            // Apply Cleave Power
            damage = Mathf.CeilToInt((float)damage * DynamicEnemies.Instance.cleavePower);

            // Break any "normal power" concealment effects on enemy
            if (entity.IsMagicallyConcealedNormalPower && damage > 0)
                EntityEffectManager.BreakNormalPowerConcealmentEffects(behaviour);

            // Play hit sound and trigger blood splash at hit point
            if (damage > 0)
            {
                targetSounds.PlayHitSound(weapon);

                EnemyBlood blood = target.transform.GetComponent<EnemyBlood>();
                CharacterController targetController = target.transform.GetComponent<CharacterController>();
                Vector3 bloodPos = target.transform.position + targetController.center;
                bloodPos.y += targetController.height / 8;

                if (blood)
                {
                    blood.ShowBloodSplash(targetEntity.MobileEnemy.BloodIndex, bloodPos);
                }

                // Knock back enemy based on damage and enemy weight
                if (targetMotor && (targetMotor.KnockbackSpeed <= (5 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10))
                        && (behaviour.EntityType == EntityTypes.EnemyClass || targetEntity.MobileEnemy.Weight > 0)))
                {
                    float enemyWeight = targetEntity.GetWeightInClassicUnits();
                    float tenTimesDamage = damage * 10;
                    float twoTimesDamage = damage * 2;

                    float knockBackAmount = ((tenTimesDamage - enemyWeight) * 256) / (enemyWeight + tenTimesDamage) * twoTimesDamage;
                    float KnockbackSpeed = (tenTimesDamage / enemyWeight) * (twoTimesDamage - (knockBackAmount / 256));
                    KnockbackSpeed /= (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10);

                    if (KnockbackSpeed < (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10)))
                        KnockbackSpeed = (15 / (PlayerSpeedChanger.classicToUnitySpeedUnitRatio / 10));
                    targetMotor.KnockbackSpeed = KnockbackSpeed;
                    targetMotor.KnockbackDirection = direction;
                }

                if (DaggerfallUnity.Settings.CombatVoices && target.EntityType == EntityTypes.EnemyClass && Dice100.SuccessRoll(40))
                {
                    var targetMobileUnit = target.GetComponentInChildren<MobileUnit>();
                    Genders gender;
                    if (targetMobileUnit.Enemy.Gender == MobileGender.Male || targetEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                        gender = Genders.Male;
                    else
                        gender = Genders.Female;

                    targetSounds.PlayCombatVoice(gender, false, damage >= targetEntity.MaxHealth / 4);
                }
            }
            else
            {
                WeaponTypes weaponType = WeaponTypes.Melee;
                if (weapon != null)
                    weaponType = DaggerfallUnity.Instance.ItemHelper.ConvertItemToAPIWeaponType(weapon);

                if ((!bowAttack && !targetEntity.MobileEnemy.ParrySounds) || weaponType == WeaponTypes.Melee)
                    sounds.PlayMissSound(weapon);
                else if (targetEntity.MobileEnemy.ParrySounds)
                    targetSounds.PlayParrySound();
            }

            // Handle Strikes payload from enemy to non-player target - this could change damage amount
            if (weapon != null && weapon.IsEnchanted)
            {
                EntityEffectManager effectManager = GetComponent<EntityEffectManager>();
                if (effectManager)
                    damage = effectManager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, weapon, entity.Items, targetEntity.EntityBehaviour, damage);
            }

            targetEntity.DecreaseHealth(damage);

            if (targetMotor)
            {
                targetMotor.MakeEnemyHostileToAttacker(behaviour);
            }

            return damage;
        }
        private void SendDamageToPlayer()
        {
            GameManager.Instance.PlayerObject.SendMessage("RemoveHealth", damage);

            EnemyEntity entity = behaviour.Entity as EnemyEntity;
            DaggerfallUnityItem weapon = entity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            if (weapon == null)
                weapon = entity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            if (weapon != null)
                GameManager.Instance.PlayerObject.SendMessage("PlayWeaponHitSound");
            else
                GameManager.Instance.PlayerObject.SendMessage("PlayWeaponlessHitSound");
        }
        private void SurrenderToGuardsDialogue_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                GameManager.Instance.PlayerEntity.SurrenderToCityGuards(true);
            else
                SendDamageToPlayer();
        }

        /*//EnemyMotor
        /// <summary>
        /// Make decision about what action to take.
        /// </summary>
        void TakeActionOriginal()
        {
            // Monster speed of movement follows the same formula as for when the player walks
            float moveSpeed = (entity.Stats.LiveSpeed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale;

            // Get isPlayingOneShot for use below
            bool isPlayingOneShot = mobile.IsPlayingOneShot();

            // Reduced speed if playing a one-shot animation with enhanced AI
            if (isPlayingOneShot && DaggerfallUnity.Settings.EnhancedCombatAI)
                moveSpeed /= 2f;

            // Classic AI moves only as close as melee range. It uses a different range for the player and for other AI.
            if (!DaggerfallUnity.Settings.EnhancedCombatAI)
            {
                if (senses.Target == GameManager.Instance.PlayerEntityBehaviour)
                    stopDistance = attack.MeleeDistance;
                else
                    stopDistance = attack.ClassicMeleeDistanceVsAI;
            }

            // Get location to move towards.
            GetDestination();

            // Get direction & distance to destination.
            Vector3 direction = (destination - transform.position).normalized;

            float distance;
            // If enemy sees the target, use the distance value from EnemySenses, as this is also used for the melee attack decision and we need to be consistent with that.
            if (avoidObstaclesTimer <= 0 && senses.TargetInSight)
                distance = senses.DistanceToTarget;
            else
                distance = (destination - transform.position).magnitude;

            // Do not change action if currently playing oneshot wants to stop actions
            if (isPlayingOneShot && mobile.OneShotPauseActionsWhilePlaying())
                return;

            // Ranged attacks
            if (DoRangedAttack(direction, moveSpeed, distance, isPlayingOneShot))
                return;

            // Touch spells
            if (DoTouchSpell())
                return;

            // Update advance/retreat decision
            if (moveInForAttackTimer <= 0 && avoidObstaclesTimer <= 0)
                EvaluateMoveInForAttack();

            // If detouring, always attempt to move
            if (avoidObstaclesTimer > 0)
            {
                AttemptMove(direction, moveSpeed);
            }
            // Otherwise, if not still executing a retreat, approach target until close enough to be on-guard.
            // If decided to move in for attack, continue until within melee range. Classic always moves in for attack.
            else if ((!retreating && distance >= (stopDistance * 2.75)) || (distance > stopDistance && moveInForAttack))
            {
                // If state change timer is done, or we are continuing an already started pursuit, we can move immediately
                if (changeStateTimer <= 0 || pursuing)
                    AttemptMove(direction, moveSpeed);
                // Otherwise, look at target until timer finishes
                else if (!senses.TargetIsWithinYawAngle(22.5f, destination))
                    TurnToTarget(direction);
            }
            else if (DaggerfallUnity.Settings.EnhancedCombatAI && strafeTimer <= 0)
            {
                StrafeDecision();
            }
            else if (doStrafe && strafeTimer > 0 && (distance >= stopDistance * .8f))
            {
                AttemptMove(direction, moveSpeed / 4, false, true, distance);
            }
            // Back away from combat target if right next to it, or if decided to retreat and enemy is too close.
            // Classic AI never backs away.
            else if (DaggerfallUnity.Settings.EnhancedCombatAI && senses.TargetInSight && (distance < stopDistance * .8f ||
                !moveInForAttack && distance < stopDistance * retreatDistanceMultiplier && (changeStateTimer <= 0 || retreating)))
            {
                // If state change timer is done, or we are already executing a retreat, we can move immediately
                if (changeStateTimer <= 0 || retreating)
                    AttemptMove(direction, moveSpeed / 2, true);
            }
            // Not moving, just look at target
            else if (!senses.TargetIsWithinYawAngle(22.5f, destination))
            {
                TurnToTarget(direction);
            }
            else // Not moving, and no need to turn
            {
                SetChangeStateTimer();
                pursuing = false;
                retreating = false;
            }

        }

        /// <summary>
        /// Get the destination to move towards.
        /// </summary>
        void GetDestination()
        {
            CharacterController targetController = senses.Target.GetComponent<CharacterController>();
            // If detouring around an obstacle or fall, use the detour position
            if (avoidObstaclesTimer > 0)
            {
                destination = detourDestination;
            }
            // Otherwise, try to get to the combat target if there is a clear path to it
            else if (ClearPathToPosition(senses.PredictedTargetPos, (destination - transform.position).magnitude) || (senses.TargetInSight && (hasBowAttack || entity.CurrentMagicka > 0)))
            {
                destination = senses.PredictedTargetPos;
                // Flying enemies and slaughterfish aim for target face
                if (flies || IsLevitating || (swims && mobile.Enemy.ID == (int)MonsterCareers.Slaughterfish))
                    destination.y += targetController.height * 0.5f;

                searchMult = 0;
            }
            // Otherwise, search for target based on its last known position and direction
            else
            {
                Vector3 searchPosition = senses.LastKnownTargetPos + (senses.LastPositionDiff.normalized * searchMult);
                if (searchMult <= 10 && (searchPosition - transform.position).magnitude <= stopDistance)
                    searchMult++;

                destination = searchPosition;
            }

            if (avoidObstaclesTimer <= 0 && !flies && !IsLevitating && !swims && senses.Target)
            {
                // Ground enemies target at their own height
                // Otherwise, short enemies' vector can aim up towards the target, which could interfere with distance-to-target calculations.
                float deltaHeight = (targetController.height - originalHeight) / 2;
                destination.y -= deltaHeight;
            }
        }

        /// <summary>
        /// Handles ranged attacks with bows and spells.
        /// </summary>
        bool DoRangedAttack(Vector3 direction, float moveSpeed, float distance, bool isPlayingOneShot)
        {
            bool inRange = senses.DistanceToTarget > EnemyAttack.minRangedDistance && senses.DistanceToTarget < EnemyAttack.maxRangedDistance;
            if (inRange && senses.TargetInSight && senses.DetectedTarget && (CanShootBow() || CanCastRangedSpellHandler()))
            {
                if (DaggerfallUnity.Settings.EnhancedCombatAI && senses.TargetIsWithinYawAngle(22.5f, destination) && strafeTimer <= 0)
                {
                    StrafeDecision();
                }

                if (doStrafe && strafeTimer > 0)
                {
                    AttemptMove(direction, moveSpeed / 4, false, true, distance);
                }

                if (GameManager.ClassicUpdate && senses.TargetIsWithinYawAngle(22.5f, destination))
                {
                    if (!isPlayingOneShot)
                    {
                        if (hasBowAttack)
                        {
                            // Random chance to shoot bow
                            if (Random.value < 1 / 32f)
                            {
                                if (mobile.Enemy.HasRangedAttack1 && !mobile.Enemy.HasRangedAttack2)
                                    mobile.ChangeEnemyState(MobileStates.RangedAttack1);
                                else if (mobile.Enemy.HasRangedAttack2)
                                    mobile.ChangeEnemyState(MobileStates.RangedAttack2);
                            }
                        }
                        // Random chance to shoot spell
                        else if (Random.value < 1 / 40f && entityEffectManager.SetReadySpell(SelectedSpell))
                        {
                            mobile.ChangeEnemyState(MobileStates.Spell);
                        }
                    }
                }
                else
                    TurnToTarget(direction);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles touch-range spells.
        /// </summary>
        bool DoTouchSpell()
        {
            if (senses.TargetInSight && senses.DetectedTarget && attack.MeleeTimer == 0
                && senses.DistanceToTarget <= attack.MeleeDistance + senses.TargetRateOfApproach
                && CanCastTouchSpellHandler() && entityEffectManager.SetReadySpell(SelectedSpell))
            {
                if (mobile.EnemyState != MobileStates.Spell)
                    mobile.ChangeEnemyState(MobileStates.Spell);

                attack.ResetMeleeTimer();
                return true;
            }

            return false;
        }*/
    }
}
