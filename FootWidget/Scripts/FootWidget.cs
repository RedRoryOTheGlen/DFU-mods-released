using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Utility;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FootWidgetMod
{
    public class FootWidget : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<FootWidget>();

            mod.IsReady = true;
        }

        int kickType; //0 = Straight (Up), 1 = Roundhouse (Down), 2 = Side (Left), 3 = random, 4 = Sequential
        KeyCode kickKeyCode = KeyCode.E;
        bool holdToAttack;
        int mirror; //0 = Right, 1 = Left, 2 = Random, 3 = Alternating

        float comboTime = 0.2f;
        float comboTimer;
        int kickCount;

        FPSWeaponStandalone fpsWeapon;

        public event Action OnKickAttack;
        void RaiseKickAttackEvent()
        {
            if (OnKickAttack != null)
                OnKickAttack();
        }
        bool suppressVanillaAttack;

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Controls"))
            {
                kickKeyCode = SetKeyFromText(settings.GetString("Controls", "KickInput"));
                fpsWeapon.keyCode = kickKeyCode;
                holdToAttack = settings.GetValue<bool>("Controls", "HoldToAttack");
            }
            if (change.HasChanged("Sprite"))
            {
                kickType = settings.GetValue<int>("Sprite", "KickType");
                fpsWeapon.swing = settings.GetValue<bool>("Sprite", "ModernSwing");
                mirror = settings.GetValue<int>("Sprite", "Mirror");

                LoadWeaponWidgetSettings();
            }
        }

        Mod ww; //WeaponWidget

        private void ModCompatibilityChecking()
        {
            //do mod compatibility stuff here
            ww = ModManager.Instance.GetModFromGUID("9f301f2b-298b-43d8-8f3f-c54deaa841e0");
            if (ww != null)
            {
                /*ModManager.Instance.SendModMessage(ww.Title, "addPosition", (Action<Vector2>)OnPositionChange);
                ModManager.Instance.SendModMessage(ww.Title, "addScale", (Action<Vector2>)OnScaleChange);*/

                LoadWeaponWidgetSettings();
            }

            //listen to Eye Of The Beholder for changes in POV
            Mod eotb = ModManager.Instance.GetModFromGUID("2942ea8c-dbd4-42af-bdf9-8199d2f4a0aa");
            if (eotb != null)
            {
                ModManager.Instance.SendModMessage(eotb.Title, "onToggleOffset", (Action<bool>)(toggleState => {
                    fpsWeapon.isInThirdPerson = toggleState;
                }));
            }
            //listen to Combat Event Handler for attacks
            Mod ceh = ModManager.Instance.GetModFromGUID("fb086c76-38e7-4d83-91dc-f29e6f1bb17e");
            if (ceh != null)
            {
                ModManager.Instance.SendModMessage(ceh.Title, "onAttackDamageCalculated", (Action<DaggerfallEntity, DaggerfallEntity, DaggerfallUnityItem, int, int>)fpsWeapon.OnAttackDamageCalculated);
            }
        }


        void LoadWeaponWidgetSettings()
        {
            if (ww == null)
                return;

            ModSettings settings = ww.GetSettings();

            fpsWeapon.swing = settings.GetValue<bool>("Modules", "Swings");
            fpsWeapon.swingSpeed = settings.GetValue<float>("Swings", "Speed");
            fpsWeapon.swingWindup = settings.GetValue<int>("Swings", "Windup");
            fpsWeapon.swingRecovery = settings.GetValue<int>("Swings", "Recovery");
            fpsWeapon.swingSpeed = settings.GetValue<float>("Swings", "Speed");
            fpsWeapon.swingAlignmentOverride = settings.GetValue<bool>("Swings", "VanillaAlignmentOverride");
            fpsWeapon.swingRecoveryOverride = settings.GetValue<bool>("Swings", "VanillaRecoveryOverride");
            fpsWeapon.swingNoDaggerRight = settings.GetValue<bool>("Swings", "NoDaggerMirroredStrikes");

            fpsWeapon.offset = settings.GetValue<bool>("Modules", "Offset");
            fpsWeapon.offsetSpeed = settings.GetValue<float>("Offset", "Speed") * 10;

            fpsWeapon.stepTransforms = settings.GetValue<bool>("Modules", "Step");
            fpsWeapon.stepLength = settings.GetValue<int>("Step", "Length");

            fpsWeapon.textureScaleFactor = settings.GetValue<int>("TrueTextureSize", "TextureScaleFactor");
            fpsWeapon.doubleScale = settings.GetValue<bool>("Modules", "DoubleScaleTextures");
            fpsWeapon.trueSize = settings.GetValue<bool>("Modules", "TrueTextureSize");

            fpsWeapon.recoil = settings.GetValue<bool>("Modules", "Recoil");
            fpsWeapon.recoilChance = (float)settings.GetValue<int>("Recoil", "Chance") / 100;
            fpsWeapon.recoilCondition = settings.GetValue<int>("Recoil", "Condition");
            fpsWeapon.recoilEnvironment = settings.GetValue<bool>("Recoil", "DetectEnvironment");
            fpsWeapon.playMissVFXEntity = settings.GetValue<bool>("Recoil", "PlayEntityMissEffects");
            fpsWeapon.playMissVFXEnvironment = settings.GetValue<bool>("Recoil", "PlayEnvironmentMissEffects");
            fpsWeapon.playMissVFXPos = settings.GetValue<int>("Recoil", "MissEffectPlacement");
        }

        void MessageReceiver(string message, object data, DFModMessageCallback callBack)
        {
            switch (message)
            {
                case "AddOnKick":
                    OnKickAttack += data as Action;
                    break;

                case "DisableVanillaAttack":
                    suppressVanillaAttack = true;
                    break;

                case "EnableVanillaAttack":
                    suppressVanillaAttack = false;
                    break;

                default:
                    Debug.LogErrorFormat("{0}: unknown message received ({1}).", this, message);
                    break;
            }
        }

        public void OnPositionChange(Vector2 value)
        {
            fpsWeapon.Position = new Vector2(-value.x, value.y);
        }

        public void OnScaleChange(Vector2 value)
        {
            fpsWeapon.Scale = value;
        }

        private KeyCode SetKeyFromText(string text)
        {
            Debug.Log("Setting Key");
            if (System.Enum.TryParse(text, out KeyCode result))
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
        private void Start()
        {
            fpsWeapon = gameObject.AddComponent<FPSWeaponStandalone>();
            fpsWeapon.OnHitFrame += KickAttack;
            fpsWeapon.drawIdle = false;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            mod.MessageReceiver = MessageReceiver;

            fpsWeapon.SetWeapon(null);

            ModCompatibilityChecking();
        }

        private void LateUpdate()
        {
            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (!fpsWeapon.IsAttacking)
            {
                if ((!holdToAttack && InputManager.Instance.GetKeyDown(kickKeyCode)) ||
                (holdToAttack && InputManager.Instance.GetKey(kickKeyCode)))
                {
                    bool kickCounted = false;

                    if (mirror == 3)
                    {
                        fpsWeapon.FlipHorizontal = kickCount % 2 == 0 ? false : true;

                        kickCounted = true;
                    }
                    else if (mirror == 2)
                        fpsWeapon.FlipHorizontal = UnityEngine.Random.value > 0.5f ? true : false;
                    else if (mirror == 1)
                        fpsWeapon.FlipHorizontal = DaggerfallUnity.Settings.Handedness == 0 ? true : false;
                    else
                        fpsWeapon.FlipHorizontal = DaggerfallUnity.Settings.Handedness == 0 ? false : true;

                    int kickIndex = kickType;
                    if (kickType == 4)
                    {
                        if (kickCount % 3 == 0)
                            kickIndex = 2;
                        else if (kickCount % 2 == 0)
                            kickIndex = 1;
                        else
                            kickIndex = 0;

                        kickCounted = true;
                    }
                    else if (kickType == 3)
                    {
                        kickIndex = UnityEngine.Random.Range(0, 3);
                    }

                    switch (kickIndex)
                    {
                        case 0:
                            fpsWeapon.PlayAttackAnimation(WeaponStates.StrikeUp); break;
                        case 1:
                            fpsWeapon.PlayAttackAnimation(WeaponStates.StrikeDown); break;
                        case 2:
                            fpsWeapon.PlayAttackAnimation(WeaponStates.StrikeLeft); break;
                    }

                    if (kickCounted)
                    {
                        kickCount++;
                        comboTimer = 0;
                    }
                }
                else
                {
                    if (kickCount > 0)
                    {
                        if (comboTimer > comboTime)
                        {
                            kickCount = 0;
                            comboTimer = 0;
                        }
                        else
                            comboTimer += Time.deltaTime;
                    }
                }
            }
        }

        void RandomKick()
        {

        }

        public void DoThud(Transform target)
        {
            DoThud(target.position);
        }

        public void DoThud(Vector3 point)
        {
            //Debug.Log("WEAPON WIDGET - Did THUD");
            //Create oneshot animated billboard for thud effect
            Camera eye = GameManager.Instance.MainCamera;
            Vector3 pos = eye.transform.position + ((point - eye.transform.position) * 0.75f);
            GameObject go = GameObjectHelper.CreateDaggerfallBillboardGameObject(380, 2, null);
            go.name = "THUD";
            Billboard c = go.GetComponent<Billboard>();
            go.transform.position = pos;
            go.transform.localScale *= 2f;
            c.FaceY = true;
            c.OneShot = true;
            c.FramesPerSecond = 20;
        }

        public void KickAttack()
        {
            // Fatigue loss
            GameManager.Instance.PlayerEntity.DecreaseFatigue(11);

            if (OnKickAttack != null)
                RaiseKickAttackEvent();

            if (suppressVanillaAttack)
                return;

            // Chance to play attack voice
            if (DaggerfallUnity.Settings.CombatVoices)
            {
                // Racial override can suppress optional attack voice
                RacialOverrideEffect racialOverride = GameManager.Instance.PlayerEffectManager.GetRacialOverrideEffect();
                bool suppressCombatVoices = racialOverride != null && racialOverride.SuppressOptionalCombatVoices;

                //if (!suppressCombatVoices && currentWeaponType != WeaponTypes.Bow && Dice100.SuccessRoll(20))
                int chance = 20;
                if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                    chance = 10;
                if (Dice100.SuccessRoll(chance))
                    fpsWeapon.PlayAttackSound();
            }

            Transform body = GameManager.Instance.PlayerObject.transform;
            Transform eye = GameManager.Instance.MainCameraObject.transform;
            float reach = GameManager.Instance.WeaponManager.ScreenWeapon.Reach;

            LayerMask layerMask = ~(1 << LayerMask.NameToLayer("Player"));
            layerMask = layerMask & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            // Fire ray along player facing using weapon range
            RaycastHit hit;
            Ray ray = new Ray(body.position + (Vector3.up * (GameManager.Instance.PlayerController.height * 0.5f)), eye.forward);
            //Debug.DrawLine(ray.origin, ray.origin + (ray.direction * reach), Color.green, 1f, false);
            if (Physics.SphereCast(ray, 0.25f, out hit, reach, layerMask))
            {
                if (GameManager.Instance.WeaponManager.WeaponDamage(null, false, false, hit.transform, hit.point, ray.direction))
                    GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.HandToHand, 1);
                else
                {
                    if (WeaponEnvDamage(null, hit))
                        fpsWeapon.hasCurrentAttackHit = true;
                }
            }
        }

        // Returns true if hit the environment
        public bool WeaponEnvDamage(DaggerfallUnityItem strikingWeapon, RaycastHit hit)
        {
            // Check if hit has an DaggerfallAction component
            DaggerfallAction action = hit.transform.gameObject.GetComponent<DaggerfallAction>();
            if (action)
            {
                DoThud(hit.point);
                action.Receive(GameManager.Instance.PlayerObject, DaggerfallAction.TriggerTypes.Attack);
            }

            // Check if hit has an DaggerfallActionDoor component
            DaggerfallActionDoor actionDoor = hit.transform.gameObject.GetComponent<DaggerfallActionDoor>();
            if (actionDoor)
            {
                actionDoor.AttemptBash(true);
                DoThud(hit.point);
                return true;
            }

            // Check if player hit a static door
            if (GameManager.Instance.PlayerActivate.AttemptExteriorDoorBash(hit))
            {
                DoThud(hit.point);
                return true;
            }

            // Make hitting walls do a thud or clinging sound (not in classic)
            if (GameObjectHelper.IsStaticGeometry(hit.transform.gameObject))
            {
                DoThud(hit.point);
                DaggerfallUI.Instance.PlayOneShot(SoundClips.Hit2);
                return true;
            }

            return false;
        }
    }
}
