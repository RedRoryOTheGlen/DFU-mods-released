using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallConnect;

public class WeaponFeatures : MonoBehaviour
{
    static Mod mod;

    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        WeaponFeatures wf = go.AddComponent<WeaponFeatures>();
    }

    //settings
    KeyCode attackKeyCode = KeyCode.Mouse1;
    int meleeMode;    //0 = gesture, 1 = hold, 2 = movement, 3 = speed
    int bowMode; //0 = vanilla, 1 = hold and release, 2 = typed
    float cleaveInterval;
    bool cleaveRequireLOS;
    bool cleaveParries;
    float cleaveValueMultiplier = 1;
    float cleaveWeightMultiplier = 1;
    float reachMultiplier = 1;
    bool reachRadial;

    //configuration
    Vector2 statMelee;
    Vector2 statDagger;
    Vector2 statTanto;
    Vector2 statShortsword;
    Vector2 statWakazashi;
    Vector2 statBroadsword;
    Vector2 statSaber;
    Vector2 statLongsword;
    Vector2 statKatana;
    Vector2 statClaymore;
    Vector2 statDaikatana;
    Vector2 statStaff;
    Vector2 statMace;
    Vector2 statFlail;
    Vector2 statWarhammer;
    Vector2 statBattleAxe;
    Vector2 statWarAxe;
    Vector2 statArchersAxe;
    Vector2 statLightFlail;

    Camera playerCamera;
    CharacterController playerController;
    PlayerEntity playerEntity;
    PlayerMotor playerMotor;
    PlayerSpeedChanger speedChanger;
    PlayerHeightChanger heightChanger;
    WeaponManager weaponManager;
    FPSWeapon ScreenWeapon;

    bool isAttacking;
    bool hasAttacked;

    DaggerfallEntity attackTarget;
    int attackDamage;

    LayerMask layerMask;

    int swingCount = 0;
    float swingTime = 0.2f;
    float swingTimer = 0;

    bool reloaded = true;
    float reloadTime = 10;
    float reloadTimer = 0;
    float reloadSizeMod
    {
        get
        {
            if ((Weapons)ScreenWeapon.SpecificWeapon.TemplateIndex == Weapons.Short_Bow)
                return 0.8f;
            else
                return 1.0f;
        }
    }

    float drawTime = 10;
    float drawTimer;

    //Vanilla attack control stuff
    private Gesture _gesture;
    private int _longestDim;
    private const float MaxGestureSeconds = 1.0f;
    bool joystickSwungOnce = false;
    private const float resetJoystickSwingRadius = 0.4f;

    private class Gesture
    {
        // The cursor is auto-centered every frame so the x/y becomes delta x/y
        private readonly List<TimestampedMotion> _points;
        // The result of the sum of all points in the gesture trail
        private Vector2 _sum;
        // The total travel distance of the gesture trail
        // This isn't equal to the magnitude of the sum because the trail may bend
        public float TravelDist { get; private set; }

        public Gesture()
        {
            _points = new List<TimestampedMotion>();
            _sum = new Vector2();
            TravelDist = 0f;
        }

        // Trims old gesture points & keeps the sum and travel variables up to date
        private void TrimOld()
        {
            var old = 0;
            foreach (var point in _points)
            {
                if (Time.time - point.Time <= MaxGestureSeconds)
                    continue;
                old++;
                _sum -= point.Delta;
                TravelDist -= point.Delta.magnitude;
            }
            _points.RemoveRange(0, old);
        }

        /// <summary>
        /// Adds the given delta mouse x/ys top the gesture trail
        /// </summary>
        /// <param name="dx">Mouse delta x</param>
        /// <param name="dy">Mouse delta y</param>
        /// <returns>The summed vector of the gesture (not the trail itself)</returns>
        public Vector2 Add(float dx, float dy)
        {
            TrimOld();

            _points.Add(new TimestampedMotion
            {
                Time = Time.time,
                Delta = new Vector2 { x = dx, y = dy }
            });
            _sum += _points.Last().Delta;
            TravelDist += _points.Last().Delta.magnitude;

            return new Vector2 { x = _sum.x, y = _sum.y };
        }

        /// <summary>
        /// Clears the gesture
        /// </summary>
        public void Clear()
        {
            _points.Clear();
            _sum *= 0;
            TravelDist = 0f;
        }
    }

    private struct TimestampedMotion
    {
        public float Time;
        public Vector2 Delta;

        public override string ToString()
        {
            return string.Format("t={0}s, dx={1}, dy={2}", Time, Delta.x, Delta.y);
        }
    }

    public Vector3 GetEyePos
    {
        get
        {
            return playerController.transform.position + playerController.center + (Vector3.up * (playerController.height * 0.5f));
        }
    }

    void Awake()
    {
        layerMask = ~(1 << LayerMask.NameToLayer("Player"));

        playerCamera = GameManager.Instance.MainCamera;
        playerController = GameManager.Instance.PlayerController;
        playerEntity = GameManager.Instance.PlayerEntity;
        playerMotor = GameManager.Instance.PlayerMotor;
        speedChanger = GameManager.Instance.SpeedChanger;
        heightChanger = GameManager.Instance.PlayerObject.GetComponent<PlayerHeightChanger>();
        weaponManager = GameManager.Instance.WeaponManager;
        ScreenWeapon = GameManager.Instance.WeaponManager.ScreenWeapon;

        _gesture = new Gesture();
        _longestDim = Math.Max(Screen.width, Screen.height);

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        ModCompatibilityChecking();

        mod.MessageReceiver = MessageReceiver;
        mod.IsReady = true;
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Main"))
        {
            attackKeyCode = SetKeyFromText(settings.GetString("Main", "AttackInput"));
            meleeMode = settings.GetValue<int>("Main", "MeleeMode");
            bowMode = settings.GetValue<int>("Main", "BowMode");
            if (bowMode == 0)
                DaggerfallUnity.Settings.BowDrawback = false;
            else
                DaggerfallUnity.Settings.BowDrawback = true;
        }
        if (change.HasChanged("Cleave"))
        {
            cleaveInterval = settings.GetValue<float>("Cleave", "Interval");
            cleaveRequireLOS = settings.GetValue<bool>("Cleave", "RequireLineOfSight");
            cleaveValueMultiplier = settings.GetValue<float>("Cleave", "StartingCleaveMultiplier");
            cleaveWeightMultiplier = settings.GetValue<float>("Cleave", "TargetWeightMultiplier");
        }
        if (change.HasChanged("Reach"))
        {
            reachRadial = settings.GetValue<bool>("Reach", "Radial");
            reachMultiplier = settings.GetValue<float>("Reach", "Multiplier");
        }
        if (change.HasChanged("Variables"))
        {
            statMelee = new Vector2(settings.GetTupleFloat("Variables", "HandToHand").First,settings.GetTupleFloat("Variables", "HandToHand").Second);
            statDagger = new Vector2(settings.GetTupleFloat("Variables", "Dagger").First,settings.GetTupleFloat("Variables", "Dagger").Second);
            statTanto = new Vector2(settings.GetTupleFloat("Variables", "Tanto").First,settings.GetTupleFloat("Variables", "Tanto").Second);
            statShortsword = new Vector2(settings.GetTupleFloat("Variables", "Shortsword").First,settings.GetTupleFloat("Variables", "Shortsword").Second);
            statWakazashi = new Vector2(settings.GetTupleFloat("Variables", "Wakazashi").First,settings.GetTupleFloat("Variables", "Wakazashi").Second);
            statBroadsword = new Vector2(settings.GetTupleFloat("Variables", "Broadsword").First,settings.GetTupleFloat("Variables", "Broadsword").Second);
            statSaber = new Vector2(settings.GetTupleFloat("Variables", "Saber").First,settings.GetTupleFloat("Variables", "Saber").Second);
            statLongsword = new Vector2(settings.GetTupleFloat("Variables", "Longsword").First,settings.GetTupleFloat("Variables", "Longsword").Second);
            statKatana = new Vector2(settings.GetTupleFloat("Variables", "Katana").First,settings.GetTupleFloat("Variables", "Katana").Second);
            statClaymore = new Vector2(settings.GetTupleFloat("Variables", "Claymore").First,settings.GetTupleFloat("Variables", "Claymore").Second);
            statDaikatana = new Vector2(settings.GetTupleFloat("Variables", "Daikatana").First,settings.GetTupleFloat("Variables", "Daikatana").Second);
            statStaff = new Vector2(settings.GetTupleFloat("Variables", "Staff").First,settings.GetTupleFloat("Variables", "Staff").Second);
            statMace = new Vector2(settings.GetTupleFloat("Variables", "Mace").First,settings.GetTupleFloat("Variables", "Mace").Second);
            statFlail = new Vector2(settings.GetTupleFloat("Variables", "Flail").First,settings.GetTupleFloat("Variables", "Flail").Second);
            statWarhammer = new Vector2(settings.GetTupleFloat("Variables", "Warhammer").First,settings.GetTupleFloat("Variables", "Warhammer").Second);
            statBattleAxe = new Vector2(settings.GetTupleFloat("Variables", "BattleAxe").First,settings.GetTupleFloat("Variables", "BattleAxe").Second);
            statWarAxe = new Vector2(settings.GetTupleFloat("Variables", "WarAxe").First,settings.GetTupleFloat("Variables", "WarAxe").Second);
            statArchersAxe = new Vector2(settings.GetTupleFloat("Variables", "Archer'sAxe").First,settings.GetTupleFloat("Variables", "Archer'sAxe").Second);
            statLightFlail = new Vector2(settings.GetTupleFloat("Variables", "LightFlail").First,settings.GetTupleFloat("Variables", "LightFlail").Second);
        }
    }

    void MessageReceiver(string message, object data, DFModMessageCallback callBack)
    {
        switch (message)
        {
            case "getSwingKeyCode":
                callBack?.Invoke("getSwingKeyCode", attackKeyCode);
                break;

            default:
                Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                break;
        }
    }

    private void LateUpdate()
    {
        //Typed Bow mode
        if (bowMode == 2)
        {
            if (ScreenWeapon.WeaponType == WeaponTypes.Bow)
            {
                if ((Weapons)ScreenWeapon.SpecificWeapon.TemplateIndex == Weapons.Long_Bow && !DaggerfallUnity.Settings.BowDrawback)
                    DaggerfallUnity.Settings.BowDrawback = true;

                if ((Weapons)ScreenWeapon.SpecificWeapon.TemplateIndex == Weapons.Short_Bow && DaggerfallUnity.Settings.BowDrawback)
                    DaggerfallUnity.Settings.BowDrawback = false;
            }
        }

        if (!reloaded)
        {
            if (reloadTimer < reloadTime)
                reloadTimer += Time.deltaTime;
            else
                reloaded = true;
        }

        if (ScreenWeapon.IsAttacking())
        {
            //started an attack
            if (!isAttacking)
            {
                hasAttacked = false;
                isAttacking = true;
            }

            if (!hasAttacked)
            {
                if (ScreenWeapon.GetCurrentFrame() >= ScreenWeapon.GetHitFrame())
                {
                    if (ScreenWeapon.WeaponType != WeaponTypes.Bow)
                        StartCoroutine(DoCleaveOnNextFrame());
                    else
                        SpawnMissile();

                    // Fatigue loss
                    playerEntity.DecreaseFatigue(11);

                    hasAttacked = true;
                }
            }

            if (ScreenWeapon.WeaponType == WeaponTypes.Bow)
            {
                //do bow stuff here
                if (DaggerfallUnity.Settings.BowDrawback)
                {
                    if (drawTimer > drawTime || InputManager.Instance.ActionStarted(InputManager.Actions.ActivateCenterObject))
                    {
                        ScreenWeapon.ChangeWeaponState(WeaponStates.Idle);
                        reloaded = false;
                        reloadTime = FormulaHelper.GetBowCooldownTime(playerEntity) * reloadSizeMod *0.5f;
                        reloadTimer = 0;
                    }
                    else if (!InputManager.Instance.GetKey(attackKeyCode))
                    {
                        ScreenWeapon.OnAttackDirection(WeaponManager.MouseDirections.Down);
                    }
                }
            }

            swingTimer = 0;
        }
        else
        {
            if (!reloaded)
            {
                if (ScreenWeapon.WeaponType == WeaponTypes.Bow)
                {
                    ScreenWeapon.ShowWeapon = false;
                }
            }

            //attack has finished
            if (isAttacking)
            {
                attackTarget = null;
                attackDamage = 0;
                isAttacking = false;
            }
            else
            {
                //reset swing count after interval
                if (swingTimer < swingTime)
                    swingTimer += Time.deltaTime;
                else
                    swingCount = 0;

                if (InputManager.Instance.GetKey(attackKeyCode))
                {
                    WeaponManager.MouseDirections swingDirection = WeaponManager.MouseDirections.None;

                    if (ScreenWeapon.WeaponType != WeaponTypes.Bow)
                    {
                        //method to pick attack type here
                        if (meleeMode == 3)
                        {
                            //Speed-based
                            if (!playerMotor.IsGrounded && (new Vector2(playerMotor.MoveDirection.x, playerMotor.MoveDirection.z).magnitude <= (GameManager.Instance.SpeedChanger.GetBaseSpeed() * 0.6f)))
                            {
                                //do a chop if in the air while moving at half-speed or less
                                swingDirection = WeaponManager.MouseDirections.Down;
                            }
                            else if (!playerMotor.IsGrounded)
                            {
                                //do a thrust if in the air while moving normally
                                swingDirection = WeaponManager.MouseDirections.Up;
                            }
                            else if (playerMotor.IsGrounded && playerMotor.IsMovingLessThanHalfSpeed)
                            {
                                //do alternating horizontal swings while moving at half-speed or less
                                swingDirection = WeaponManager.MouseDirections.Left;

                                if (swingCount % 2 != 0)
                                    swingDirection = WeaponManager.MouseDirections.Right;
                            }
                            else if (playerMotor.IsGrounded)
                            {
                                //do alternating diagonal swings if moving normally
                                swingDirection = WeaponManager.MouseDirections.DownLeft;

                                if (swingCount % 2 != 0)
                                    swingDirection = WeaponManager.MouseDirections.DownRight;
                            }
                        }
                        else if (meleeMode == 2)
                        {
                            /*//Movement-based
                            if (Mathf.Abs(InputManager.Instance.Horizontal) > 0)
                            {
                                //do alternating horizontal swings
                                swingDirection = WeaponManager.MouseDirections.Left;

                                if (swingCount % 2 != 0)
                                    swingDirection = WeaponManager.MouseDirections.Right;
                            }
                            else if (InputManager.Instance.Vertical > 0)
                            {
                                swingDirection = WeaponManager.MouseDirections.Up;
                            }
                            else if (InputManager.Instance.Vertical < 0)
                            {
                                swingDirection = WeaponManager.MouseDirections.Down;
                            }
                            else
                            {
                                //do alternating diagonal swings
                                swingDirection = WeaponManager.MouseDirections.DownLeft;

                                if (swingCount % 2 != 0)
                                    swingDirection = WeaponManager.MouseDirections.DownRight;
                            }*/

                            //Morrowind
                            if (Mathf.Abs(InputManager.Instance.Horizontal) > 0 && Mathf.Abs(InputManager.Instance.Vertical) == 0)
                            {
                                //do alternating horizontal swings if strafing
                                swingDirection = WeaponManager.MouseDirections.Left;

                                if (InputManager.Instance.Horizontal < 0)
                                    swingDirection = WeaponManager.MouseDirections.Right;

                                if (swingCount % 2 != 0)
                                {
                                    if (swingDirection == WeaponManager.MouseDirections.Left)
                                        swingDirection = WeaponManager.MouseDirections.Right;
                                    else
                                        swingDirection = WeaponManager.MouseDirections.Left;
                                }
                            }
                            else if (InputManager.Instance.Vertical > 0 && Mathf.Abs(InputManager.Instance.Horizontal) == 0)
                            {
                                //do a thrust if moving forward
                                swingDirection = WeaponManager.MouseDirections.Up;
                            }
                            else
                            {
                                //do a chop
                                swingDirection = WeaponManager.MouseDirections.Down;
                            }
                        }
                        else if (meleeMode == 1)
                        {
                            //Hold to attack
                            if (InputManager.Instance.GetKey(attackKeyCode))
                            {
                                //random swing direction
                                //swingDirection = (WeaponManager.MouseDirections)UnityEngine.Random.Range((int)WeaponManager.MouseDirections.UpRight, (int)WeaponManager.MouseDirections.DownRight + 1);

                                //iterate through swing directions
                                //start at the top because there's some weirdness with Weapon Widget startup animations when doing a StrikeDown after a StrikeDownLeft
                                int value = 8 - swingCount;
                                if (value < 3)
                                {
                                    value = 8;
                                    swingCount = 0;
                                }
                                swingDirection = (WeaponManager.MouseDirections)value;
                            }
                        }
                        else
                        {
                            //Drag to attack
                            if (InputManager.Instance.GetKey(attackKeyCode))
                                swingDirection = TrackMouseAttack();
                            else
                                _gesture.Clear();
                        }

                        swingCount++;
                    }
                    else
                    {
                        //do bow stuff here
                        if (reloaded)
                        {
                            if (DaggerfallUnity.Settings.BowDrawback)
                            {
                                swingDirection = WeaponManager.MouseDirections.Up;
                                drawTimer = 0;
                            }
                            else
                            {
                                swingDirection = WeaponManager.MouseDirections.Down;
                            }
                        }
                    }

                    if (swingDirection != WeaponManager.MouseDirections.None)
                        ScreenWeapon.OnAttackDirection(swingDirection);
                }
            }
        }
    }

    void SpawnMissile()
    {
        DaggerfallMissile missile = Instantiate(weaponManager.ArrowMissilePrefab);
        if (missile)
        {
            // Remove arrow
            ItemCollection playerItems = playerEntity.Items;
            DaggerfallUnityItem arrow = playerItems.GetItem(ItemGroups.Weapons, (int)Weapons.Arrow, allowQuestItem: false, priorityToConjured: true);
            bool isArrowSummoned = arrow.IsSummoned;
            playerItems.RemoveOne(arrow);

            missile.Caster = GameManager.Instance.PlayerEntityBehaviour;
            missile.TargetType = TargetTypes.SingleTargetAtRange;
            missile.ElementType = ElementTypes.None;
            missile.IsArrow = true;
            missile.IsArrowSummoned = isArrowSummoned;
            missile.CustomAimPosition = GetEyePos;

            ScreenWeapon.PlaySwingSound();
            reloadTime = FormulaHelper.GetBowCooldownTime(playerEntity) * reloadSizeMod;
            reloadTimer = 0;
            reloaded = false;
        }
    }

    private void ModCompatibilityChecking()
    {
        /*//listen to Combat Event Handler for attacks
        Mod ceh = ModManager.Instance.GetModFromGUID("fb086c76-38e7-4d83-91dc-f29e6f1bb17e");
        if (ceh != null)
            ModManager.Instance.SendModMessage(ceh.Title, "onAttackDamageCalculated", (Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int>)OnAttackDamageCalculated);*/
    }

    public void OnAttackDamageCalculated(DaggerfallEntity attacker, DaggerfallEntity target, DaggerfallUnityItem weapon, int bodyPart, int damage)
    {
        if (attacker == playerEntity)
        {
            if (GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType != WeaponTypes.Bow)
            {
                if (attackTarget == null)
                {
                    attackTarget = target;
                    attackDamage = damage;
                }
            }
        }
    }

    IEnumerator DoCleaveOnNextFrame()
    {
        ScreenWeapon.PlaySwingSound();

        yield return null;

        DoCleave();
    }

    float GetWeaponReach()
    {
        float reach = 0f;

        if (ScreenWeapon.WeaponType == WeaponTypes.Melee)
        {
            reach = statMelee.x;

            //increase the reach by 50% if kicking
            if (ScreenWeapon.WeaponState == WeaponStates.StrikeLeft || ScreenWeapon.WeaponState == WeaponStates.StrikeDown || ScreenWeapon.WeaponState == WeaponStates.StrikeUp)
                reach *= 1.5f;

            return reach * reachMultiplier;
        }

        switch (ScreenWeapon.SpecificWeapon.TemplateIndex)
        {
            case (int)Weapons.Broadsword:
                reach = statBroadsword.x; break;
            case (int)Weapons.Claymore:
                reach = statClaymore.x; break;
            case (int)Weapons.Dai_Katana:
                reach = statDaikatana.x; break;
            case (int)Weapons.Katana:
                reach = statKatana.x; break;
            case (int)Weapons.Longsword:
                reach = statLongsword.x; break;
            case (int)Weapons.Saber:
                reach = statSaber.x; break;
            case (int)Weapons.Dagger:
                reach = statDagger.x; break;
            case (int)Weapons.Shortsword:
                reach = statShortsword.x; break;
            case (int)Weapons.Tanto:
                reach = statTanto.x; break;
            case (int)Weapons.Wakazashi:
                reach = statWakazashi.x; break;
            case (int)Weapons.Battle_Axe:
                reach = statBattleAxe.x; break;
            case (int)Weapons.War_Axe:
                reach = statWarAxe.x; break;
            case (int)Weapons.Flail:
                reach = statFlail.x; break;
            case (int)Weapons.Mace:
                reach = statMace.x; break;
            case (int)Weapons.Staff:
                reach = statStaff.x; break;
            case (int)Weapons.Warhammer:
                reach = statWarhammer.x; break;
            case 513:   //Archer's Axe from Roleplay & Realism - Items
                reach = statArchersAxe.x; break;
            case 514:   //Light Flail from Roleplay & Realism - Items
                reach = statLightFlail.x; break;
        }

        return reach * reachMultiplier;
    }

    int GetWeaponCleave()
    {
        int cleave = 200;

        if (ScreenWeapon.WeaponType == WeaponTypes.Melee)
        {
            cleave = (int)statMelee.y;

            //increase the cleave by 100% if kicking
            if (ScreenWeapon.WeaponState == WeaponStates.StrikeLeft || ScreenWeapon.WeaponState == WeaponStates.StrikeDown || ScreenWeapon.WeaponState == WeaponStates.StrikeUp)
                cleave = Mathf.RoundToInt(cleave * 2f);

            return Mathf.RoundToInt(cleave * cleaveValueMultiplier);
        }

        switch (ScreenWeapon.SpecificWeapon.TemplateIndex)
        {
            case (int)Weapons.Broadsword:
                cleave = (int)statBroadsword.y; break;
            case (int)Weapons.Claymore:
                cleave = (int)statClaymore.y; break;
            case (int)Weapons.Dai_Katana:
                cleave = (int)statDaikatana.y; break;
            case (int)Weapons.Katana:
                cleave = (int)statKatana.y; break;
            case (int)Weapons.Longsword:
                cleave = (int)statLongsword.y; break;
            case (int)Weapons.Saber:
                cleave = (int)statSaber.y; break;
            case (int)Weapons.Dagger:
                cleave = (int)statDagger.y; break;
            case (int)Weapons.Shortsword:
                cleave = (int)statShortsword.y; break;
            case (int)Weapons.Tanto:
                cleave = (int)statTanto.y; break;
            case (int)Weapons.Wakazashi:
                cleave = (int)statWakazashi.y; break;
            case (int)Weapons.Battle_Axe:
                cleave = (int)statBattleAxe.y; break;
            case (int)Weapons.War_Axe:
                cleave = (int)statWarAxe.y; break;
            case (int)Weapons.Flail:
                cleave = (int)statFlail.y; break;
            case (int)Weapons.Mace:
                cleave = (int)statMace.y; break;
            case (int)Weapons.Staff:
                cleave = (int)statStaff.y; break;
            case (int)Weapons.Warhammer:
                cleave = (int)statWarhammer.y; break;
            case 513:   //Archer's Axe from Roleplay & Realism - Items
                cleave = (int)statArchersAxe.y; break;
            case 514:   //Light Flail from Roleplay & Realism - Items
                cleave = (int)statLightFlail.y; break;
        }

        return Mathf.RoundToInt(cleave * cleaveValueMultiplier);
    }

    void DoCleave()
    {
        WeaponStates swingDirection = GameManager.Instance.WeaponManager.ScreenWeapon.WeaponState;

        //create variables
        Transform body = GameManager.Instance.PlayerObject.transform;
        Transform eye = playerCamera.transform;

        //weapon determines base cleave
        int weaponCleave = GetWeaponCleave();

        //subtract weight of targeted enemy if any from the starting cleave
        int startWeight = 0;
        if (attackTarget != null && attackDamage > 0)
        {
            EnemyEntity targetEnemy = attackTarget as EnemyEntity;
            startWeight = Mathf.RoundToInt(targetEnemy.GetWeightInClassicUnits() * cleaveWeightMultiplier) ;
        }

        //scale base cleave with player strength
        int cleaveValue = Mathf.RoundToInt(weaponCleave * (playerEntity.Stats.LiveStrength/100f)) - startWeight;
        //Debug.Log("Beginning cleave with a cleave value of " + cleaveValue.ToString() + " with first enemy weighing " + startWeight.ToString());

        //weapon determines reach
        float weaponReach = GetWeaponReach();

        //construct the bounds
        float x = weaponReach;
        float y = 0.25f;
        float z = weaponReach * 0.5f;

        //if thrusting, increase the weapon reach but halve the cleave value
        if (swingDirection == WeaponStates.StrikeUp)
        {
            cleaveValue = Mathf.RoundToInt(cleaveValue * 0.5f);
            x = 0.25f;
            y = 0.25f;
            z = weaponReach * 0.625f;
        }

        Vector3 scale = new Vector3(x, y, z);
        Vector3 pos = GetEyePos + (eye.forward * ((z * 0.5f) + playerController.radius));
        Quaternion rot = eye.rotation;

        int angle = 0;
        if (swingDirection == WeaponStates.StrikeDown)
            angle = 90;
        else if (swingDirection == WeaponStates.StrikeDownLeft)
            angle = 45;
        else if (swingDirection == WeaponStates.StrikeDownRight)
            angle = -45;

        //rotate the box
        if (angle != 0)
            rot = eye.rotation * Quaternion.AngleAxis(angle,Vector3.forward);

        List<Transform> transforms = new List<Transform>();

        //overlap box using bounds + player position + camera rotation
        //DrawBox(pos,rot,scale,Color.red, 3);
        Collider[] colliders = Physics.OverlapBox(pos, scale, rot, layerMask);

        //get entities inside box, exclude original target, if any
        if (colliders.Length > 0)
        {
            Vector3 eyeOrigin = GetEyePos + (eye.forward * playerController.radius);
            foreach (Collider collider in colliders)
            {

                DaggerfallEntityBehaviour behaviour = collider.GetComponent<DaggerfallEntityBehaviour>();
                MobilePersonNPC mobileNpc = collider.GetComponent<MobilePersonNPC>();
                if ((behaviour != null && behaviour.Entity != attackTarget) || mobileNpc != null)
                {
                    //add a radial check
                    if (reachRadial && Vector3.Distance(eyeOrigin, collider.ClosestPoint(eyeOrigin)) > z)
                        continue;

                    transforms.Add(collider.transform);
                }
            }
        }

        if (transforms.Count < 1)
        {
            //no entities to hit, do environment check
            // Fire ray along player facing using weapon range
            RaycastHit hit;
            Ray ray = new Ray(GetEyePos, eye.forward);
            if (Physics.SphereCast(ray, 0.25f, out hit, weaponReach, layerMask))
            {
                if (weaponManager.WeaponEnvDamage(ScreenWeapon.SpecificWeapon, hit))
                {

                }
            }
            return;
        }

        Vector3 origin = GetEyePos;
        if (swingDirection == WeaponStates.StrikeDown)
            origin += (eye.forward * playerController.radius) + (eye.up * (x * 0.5f));
        if (swingDirection == WeaponStates.StrikeLeft)
            origin += (eye.forward * playerController.radius) + (eye.right * (x * 0.5f));
        if (swingDirection == WeaponStates.StrikeDownLeft)
            origin += (eye.forward * playerController.radius) + ((eye.right + eye.up).normalized * (x * 0.5f));
        if (swingDirection == WeaponStates.StrikeRight)
            origin += (eye.forward * playerController.radius) - (eye.right * (x * 0.5f));
        if (swingDirection == WeaponStates.StrikeDownRight)
            origin += (eye.forward * playerController.radius) + ((-eye.right + eye.up).normalized * (x * 0.5f));

        //Debug.DrawLine(GetEyePos + (eye.forward * playerController.radius), origin, Color.green, 3);

        //sort entities by their distance to the swing origin
        transforms = SortByDistanceToPoint(transforms, origin);

        int cleaves = 0;
        int hits = 0;

        if (cleaveInterval > 0)
        {
            StartCoroutine(ApplyCleaveInterval(transforms, cleaveValue, z, GameManager.classicUpdateInterval*cleaveInterval));
        }
        else
        {
            foreach (Transform target in transforms)
            {
                //if cleave is out, stop cleaving
                if (cleaveValue < 1)
                    break;

                MobilePersonNPC mobileNpc = target.GetComponent<MobilePersonNPC>();
                if (mobileNpc == null)
                {
                    if (cleaveRequireLOS)
                    {
                        //check if LOS is obstructed
                        Vector3 rayPos = GetEyePos;
                        Vector3 rayDir = target.position - rayPos;
                        Ray ray = new Ray(rayPos, rayDir);
                        RaycastHit hit = new RaycastHit();
                        if (Physics.Raycast(ray, out hit, 10, layerMask, QueryTriggerInteraction.Ignore))
                        {
                            if (hit.collider.transform != target)
                            {
                                //Target is obstructed by something
                                continue;
                            }
                            else
                            {
                                cleaves++;
                                DaggerfallEntityBehaviour behaviour = target.GetComponent<DaggerfallEntityBehaviour>();
                                if (weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, hit.transform, hit.point, ray.direction))
                                {
                                    hits++;
                                    if (behaviour != null)
                                    {
                                        EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                        if (enemy != null)
                                        {
                                            int weight = Mathf.RoundToInt(enemy.GetWeightInClassicUnits() * cleaveWeightMultiplier);
                                            cleaveValue -= weight;
                                        }
                                    }
                                }
                                else
                                {
                                    if (cleaveParries)
                                    {
                                        if (behaviour != null)
                                        {
                                            EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                            if (enemy != null)
                                            {
                                                if (enemy.MobileEnemy.ParrySounds)
                                                    cleaveValue = 0;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        cleaves++;
                        DaggerfallEntityBehaviour behaviour = target.GetComponent<DaggerfallEntityBehaviour>();
                        if (weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, target, target.position, target.position - body.position))
                        {
                            hits++;
                            if (behaviour != null)
                            {
                                EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                if (enemy != null)
                                {
                                    int weight = Mathf.RoundToInt(enemy.GetWeightInClassicUnits() * cleaveWeightMultiplier);
                                    cleaveValue -= weight;
                                }
                            }
                        }
                        else
                        {
                            if (cleaveParries)
                            {
                                if (behaviour != null)
                                {
                                    EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                    if (enemy != null)
                                    {
                                        if (enemy.MobileEnemy.ParrySounds)
                                            cleaveValue = 0;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    //Don't LOS check MobileNPCs
                    weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, target, target.position, target.position - body.position);
                    SoundClips soundClip = DaggerfallEntity.GetRaceGenderPainSound(mobileNpc.Race, mobileNpc.Gender, false);
                    GameManager.Instance.PlayerObject.GetComponent<DaggerfallAudioSource>().PlayClipAtPoint(soundClip, target.position, 1);
                }
            }

            // Tally skills if attack targeted at least one enemy
            if (cleaves > 0)
            {
                if (ScreenWeapon.WeaponType == WeaponTypes.Melee || ScreenWeapon.WeaponType == WeaponTypes.Werecreature)
                    playerEntity.TallySkill(DFCareer.Skills.HandToHand, 1);
                else
                    playerEntity.TallySkill(ScreenWeapon.SpecificWeapon.GetWeaponSkillID(), 1);

                playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
            }
        }

        attackTarget = null;
        attackDamage = 0;
    }

    IEnumerator ApplyCleaveInterval(List<Transform> targets, int cleaveValue, float reach, float interval = GameManager.classicUpdateInterval)
    {
        int cleaves = 0;
        int hits = 0;

        foreach (Transform target in targets)
        {
            //if cleave is out, stop cleaving
            if (cleaveValue < 1)
                break;

            MobilePersonNPC mobileNpc = target.GetComponent<MobilePersonNPC>();
            if (mobileNpc == null)
            {
                //check if target is still within reach
                Vector3 eyeOrigin = GetEyePos + (playerCamera.transform.forward * playerController.radius);
                if (Vector2.Distance(eyeOrigin, target.GetComponent<Collider>().ClosestPoint(eyeOrigin)) > reach)
                    continue;

                if (cleaveRequireLOS)
                {
                    //check if LOS is obstructed
                    Vector3 rayPos = GetEyePos;
                    Vector3 rayDir = target.position - rayPos;
                    Ray ray = new Ray(rayPos, rayDir);
                    RaycastHit hit = new RaycastHit();
                    if (Physics.Raycast(ray, out hit, 10, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform != target)
                        {
                            //Target is obstructed by something
                            continue;
                        }
                        else
                        {
                            cleaves++;
                            DaggerfallEntityBehaviour behaviour = target.GetComponent<DaggerfallEntityBehaviour>();
                            if (weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, hit.transform, hit.point, ray.direction))
                            {
                                hits++;
                                if (behaviour != null)
                                {
                                    EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                    if (enemy != null)
                                    {
                                        int weight = Mathf.RoundToInt(enemy.GetWeightInClassicUnits() * cleaveWeightMultiplier);
                                        cleaveValue -= weight;
                                    }
                                }
                            }
                            else
                            {
                                if (cleaveParries)
                                {
                                    if (behaviour != null)
                                    {
                                        EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                        if (enemy != null)
                                        {
                                            if (enemy.MobileEnemy.ParrySounds)
                                                cleaveValue = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    cleaves++;
                    DaggerfallEntityBehaviour behaviour = target.GetComponent<DaggerfallEntityBehaviour>();
                    if (weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, target, target.position, target.position - playerController.transform.position))
                    {
                        hits++;
                        if (behaviour != null)
                        {
                            EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                            if (enemy != null)
                            {
                                int weight = Mathf.RoundToInt(enemy.GetWeightInClassicUnits() * cleaveWeightMultiplier);
                                cleaveValue -= weight;
                            }
                        }
                    }
                    else
                    {
                        if (cleaveParries)
                        {
                            if (behaviour != null)
                            {
                                EnemyEntity enemy = behaviour.Entity as EnemyEntity;
                                if (enemy != null)
                                {
                                    if (enemy.MobileEnemy.ParrySounds)
                                        cleaveValue = 0;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                //Don't LOS check MobileNPCs
                weaponManager.WeaponDamage(ScreenWeapon.SpecificWeapon, false, false, target, target.position, target.position - playerController.transform.position);
                SoundClips soundClip = DaggerfallEntity.GetRaceGenderPainSound(mobileNpc.Race, mobileNpc.Gender, false);
                GameManager.Instance.PlayerObject.GetComponent<DaggerfallAudioSource>().PlayClipAtPoint(soundClip, target.position, 1);
            }

            yield return new WaitForSeconds(interval);
        }

        // Tally skills if attack targeted at least one enemy
        if (cleaves > 0)
        {
            if (ScreenWeapon.WeaponType == WeaponTypes.Melee || ScreenWeapon.WeaponType == WeaponTypes.Werecreature)
                playerEntity.TallySkill(DFCareer.Skills.HandToHand, 1);
            else
                playerEntity.TallySkill(ScreenWeapon.SpecificWeapon.GetWeaponSkillID(), 1);

            playerEntity.TallySkill(DFCareer.Skills.CriticalStrike, 1);
        }

    }

    List<Transform> SortByDistanceToPoint(List<Transform> transforms, Vector3 origin)
    {
        transforms.Sort(delegate (Transform a, Transform b)
        {
            return (origin - a.position).sqrMagnitude.CompareTo((origin - b.position).sqrMagnitude);
        }
        );

        return transforms;
    }

    // https://forum.unity.com/threads/debug-drawbox-function-is-direly-needed.1038499/
    public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c, float duration)
    {
        // create matrix
        Matrix4x4 m = new Matrix4x4();
        m.SetTRS(pos, rot, scale);

        var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
        var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
        var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
        var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

        var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
        var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
        var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
        var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

        Debug.DrawLine(point1, point2, c, duration);
        Debug.DrawLine(point2, point3, c, duration);
        Debug.DrawLine(point3, point4, c, duration);
        Debug.DrawLine(point4, point1, c, duration);

        Debug.DrawLine(point5, point6, c, duration);
        Debug.DrawLine(point6, point7, c, duration);
        Debug.DrawLine(point7, point8, c, duration);
        Debug.DrawLine(point8, point5, c, duration);

        Debug.DrawLine(point1, point5, c, duration);
        Debug.DrawLine(point2, point6, c, duration);
        Debug.DrawLine(point3, point7, c, duration);
        Debug.DrawLine(point4, point8, c, duration);

        /*// optional axis display
        Debug.DrawRay(m.GetPosition(), m.GetForward(), Color.magenta);
        Debug.DrawRay(m.GetPosition(), m.GetUp(), Color.yellow);
        Debug.DrawRay(m.GetPosition(), m.GetRight(), Color.red);*/
    }

    WeaponManager.MouseDirections TrackMouseAttack()
    {
        // Track action for idle plus all eight mouse directions
        var sum = _gesture.Add(InputManager.Instance.MouseX, InputManager.Instance.MouseY);

        if (InputManager.Instance.UsingController)
        {
            float x = InputManager.Instance.MouseX;
            float y = InputManager.Instance.MouseY;

            bool inResetJoystickSwingRadius = (x >= -resetJoystickSwingRadius && x <= resetJoystickSwingRadius && y >= -resetJoystickSwingRadius && y <= resetJoystickSwingRadius);

            if (joystickSwungOnce || inResetJoystickSwingRadius)
            {
                if (inResetJoystickSwingRadius)
                    joystickSwungOnce = false;

                return WeaponManager.MouseDirections.None;
            }
        }
        else if (_gesture.TravelDist / _longestDim < weaponManager.AttackThreshold)
        {
            return WeaponManager.MouseDirections.None;
        }

        joystickSwungOnce = true;

        // Treat mouse movement as a vector from the origin
        // The angle of the vector will be used to determine the angle of attack/swing
        var angle = Mathf.Atan2(sum.y, sum.x) * Mathf.Rad2Deg;
        // Put angle into 0 - 360 deg range
        if (angle < 0f) angle += 360f;
        // The swing gestures are divided into radial segments
        // Up-down and left-right attacks are in a 30 deg cone about the x/y axes
        // Up-right and up-left aren't valid so the up range is expanded to fill the range
        // The remaining 60 deg quadrants trigger the diagonal attacks
        var radialSection = Mathf.CeilToInt(angle / 15f);
        WeaponManager.MouseDirections direction;
        switch (radialSection)
        {
            case 0: // 0 - 15 deg
            case 1:
            case 24: // 345 - 365 deg
                direction = WeaponManager.MouseDirections.Right;
                break;
            case 2: // 15 - 75 deg
            case 3:
            case 4:
            case 5:
            case 6: // 75 - 105 deg
            case 7:
            case 8: // 105 - 165 deg
            case 9:
            case 10:
            case 11:
                direction = WeaponManager.MouseDirections.Up;
                break;
            case 12: // 165 - 195 deg
            case 13:
                direction = WeaponManager.MouseDirections.Left;
                break;
            case 14: // 195 - 255 deg
            case 15:
            case 16:
            case 17:
                direction = WeaponManager.MouseDirections.DownLeft;
                break;
            case 18: // 255 - 285 deg
            case 19:
                direction = WeaponManager.MouseDirections.Down;
                break;
            case 20: // 285 - 345 deg
            case 21:
            case 22:
            case 23:
                direction = WeaponManager.MouseDirections.DownRight;
                break;
            default: // Won't happen
                direction = WeaponManager.MouseDirections.None;
                break;
        }
        _gesture.Clear();
        return direction;
    }

    private KeyCode SetKeyFromText(string text)
    {
        Debug.Log("Setting Key");
        if (System.Enum.TryParse(text, true, out KeyCode result))
        {
            Debug.Log("Key set to " + result.ToString());
            return result;
        }
        else
        {
            Debug.Log("Detected an invalid key code. Setting to default.");
            return KeyCode.None;
        }
    }

}