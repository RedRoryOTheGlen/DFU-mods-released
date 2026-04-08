using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace UnmobilizerMod
{
    public class Unmobilizer : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<Unmobilizer>();

            mod.IsReady = true;
        }

        PlayerEntity entity;
        PlayerMotor motor;
        AcrobatMotor acrobat;
        PlayerSpeedChanger speedChanger;
        WeaponManager weaponManager;

        bool lastSneakToggle = false;

        bool slowedAttack = true;
        bool slowedSpellCast = true;
        bool slowedSpellReadied = true;
        bool isSlowed
        {
            get
            {
                if ((slowedAttack && GameManager.Instance.RightHandWeapon.IsAttacking()) ||  //if is attacking
                (slowedSpellCast && GameManager.Instance.PlayerSpellCasting.IsPlayingAnim) ||    //if casting a spll
                (slowedSpellReadied && GameManager.Instance.PlayerEffectManager.HasReadySpell))  //if has a spell readied
                    return true;

                return false;
            }
        }

        bool sprintBlockStrafeReverse = true;
        bool sprintBlockDiagonals = true;
        bool sprintBlockCrouch = true;
        bool sprintBlockUnsheathed = true;
        bool canSprint
        {
            get
            {
                if (isSlowed ||
                    (sprintBlockStrafeReverse && !InputManager.Instance.HasAction(InputManager.Actions.MoveForwards) && !InputManager.Instance.ToggleAutorun) ||  //if is not moving forward
                    (sprintBlockDiagonals && (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft) || InputManager.Instance.HasAction(InputManager.Actions.MoveRight))) ||    //if is moving diagonally
                    (sprintBlockUnsheathed && !weaponManager.Sheathed) ||    //if is moving diagonally
                    (sprintBlockCrouch && motor.IsCrouching))    //if is crouching
                    return false;

                return true;
            }
        }

        float moveStrafeMod = 0.75f;
        float moveReverseMod = 0.5f;
        bool moveWeaponOnly;

        bool ride;
        float rideStrafeModHorse = 0.5f;
        float rideReverseModHorse = 0.25f;
        float rideStrafeModCart = 0.25f;
        float rideReverseModCart = 0f;

        float slowedMod = 0.5f;

        TransportModes lastTransportMode = TransportModes.Foot;

        bool lastSlowed = false;
        bool lastRiding = false;
        bool lastLevitating = false;
        bool lastWaterwalking = false;
        bool lastSheathed = false;

        private void Start()
        {
            entity = GameManager.Instance.PlayerEntity;
            motor = GameManager.Instance.PlayerMotor;
            acrobat = GameManager.Instance.AcrobatMotor;
            speedChanger = GameManager.Instance.SpeedChanger;
            weaponManager = GameManager.Instance.WeaponManager;

            SaveLoadManager.OnLoad += OnLoad;

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }
        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Movement"))
            {
                moveStrafeMod = settings.GetValue<int>("Movement", "StrafeMultiplier") / 100f;
                moveReverseMod = settings.GetValue<int>("Movement", "ReverseMultiplier") / 100f;
                moveWeaponOnly = settings.GetValue<bool>("Movement", "OnlyWhenUnsheathed");
            }
            if (change.HasChanged("Slowing"))
            {
                slowedMod = settings.GetValue<int>("Slowing", "SlowMultiplier")/100f;
                slowedAttack = settings.GetValue<bool>("Slowing", "SlowWhileAttacking");
                slowedSpellCast = settings.GetValue<bool>("Slowing", "SlowWhileSpellcasting");
                slowedSpellReadied = settings.GetValue<bool>("Slowing", "SlowWhileSpellReadied");
            }
            if (change.HasChanged("Running"))
            {
                sprintBlockStrafeReverse = settings.GetValue<bool>("Running", "BlockStrafeReverseRunning");
                sprintBlockDiagonals = settings.GetValue<bool>("Running", "BlockDiagonalRunning");
                sprintBlockCrouch = settings.GetValue<bool>("Running", "BlockCrouchedRunning");
                sprintBlockUnsheathed = settings.GetValue<bool>("Running", "BlockUnsheathedRunning");
            }
            if (change.HasChanged("Riding"))
            {
                ride = settings.GetValue<bool>("Riding", "Enable");
                rideStrafeModHorse = settings.GetValue<int>("Riding", "HorseStrafeMultiplier") / 100f;
                rideReverseModHorse = settings.GetValue<int>("Riding", "HorseReverseMultiplier") / 100f;
                rideStrafeModCart = settings.GetValue<int>("Riding", "CartStrafeMultiplier") / 100f;
                rideReverseModCart = settings.GetValue<int>("Riding", "CartReverseMultiplier") / 100f;
            }

            ForceUpdateVariable();
        }
        void OnLoad(SaveData_v1 saveData)
        {
            ForceUpdateVariable();
        }

        void ForceUpdateVariable()
        {
            lastRiding = !motor.IsRiding;
            lastLevitating = !motor.IsLevitating;
            lastSlowed = !isSlowed;
            lastWaterwalking = !(motor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.WaterWalking || (motor.IsSwimming && entity.IsWaterWalking) ? true : false);
            lastSheathed = !weaponManager.Sheathed;
        }

        private void LateUpdate()
        {
            if (GameManager.IsGamePaused)
                return;

            //do not apply if on horse/cart or levitating
            bool riding = motor.IsRiding;
            bool levitating = motor.IsLevitating;
            bool waterwalking = entity.IsWaterWalking && (motor.IsSwimming || motor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.WaterWalking) ? true : false;
            bool sheathed = weaponManager.Sheathed;

            if (!riding && !levitating && !waterwalking)
            {
                //player stopped riding/levitating/waterwalking
                if (riding != lastRiding || levitating != lastLevitating || waterwalking != lastWaterwalking)
                {
                    InputManager.Instance.PosVerticalLimit = 1;
                    InputManager.Instance.NegHorizontalLimit = 1 * moveStrafeMod;
                    InputManager.Instance.PosHorizontalLimit = 1 * moveStrafeMod;
                    InputManager.Instance.NegVerticalLimit = 1 * moveReverseMod;

                    lastRiding = riding;
                    lastLevitating = levitating;
                    lastWaterwalking = waterwalking;
                    lastSheathed = sheathed;
                }

                //when to prevent run
                if (speedChanger.runningMode && !waterwalking && !canSprint)
                    speedChanger.runningMode = false;

                //when to slow
                bool slowed = isSlowed;
                if (slowed != lastSlowed)
                {
                    if (isSlowed)
                    {
                        InputManager.Instance.PosVerticalLimit = 1 * slowedMod;
                        InputManager.Instance.NegHorizontalLimit = 1 * moveStrafeMod * slowedMod;
                        InputManager.Instance.PosHorizontalLimit = 1 * moveStrafeMod * slowedMod;
                        InputManager.Instance.NegVerticalLimit = 1 * moveReverseMod * slowedMod;
                    }
                    else
                    {
                        InputManager.Instance.PosVerticalLimit = 1;
                        InputManager.Instance.NegHorizontalLimit = 1 * moveStrafeMod;
                        InputManager.Instance.PosHorizontalLimit = 1 * moveStrafeMod;
                        InputManager.Instance.NegVerticalLimit = 1 * moveReverseMod;
                    }
                    lastSlowed = slowed;
                }
            }
            else
            {
                //if riding, check if we changed transport modes
                TransportModes transportMode = lastTransportMode;
                if (riding && ride)
                {
                    transportMode = GameManager.Instance.TransportManager.TransportMode;

                    //when to prevent run
                    if (speedChanger.runningMode && !waterwalking && !canSprint)
                        speedChanger.runningMode = false;
                }

                //player started riding, levitating or waterwalking
                if (riding != lastRiding || levitating != lastLevitating || waterwalking != lastWaterwalking || transportMode != lastTransportMode)
                {
                    if (!levitating && !waterwalking && riding && ride)
                    {
                        InputManager.Instance.PosHorizontalLimit = (transportMode == TransportModes.Cart) ? rideStrafeModCart : rideStrafeModHorse;
                        InputManager.Instance.NegHorizontalLimit = InputManager.Instance.PosHorizontalLimit;
                        InputManager.Instance.NegVerticalLimit = (transportMode == TransportModes.Cart) ? rideReverseModCart : rideReverseModHorse;
                    }
                    else
                    {
                        InputManager.Instance.NegHorizontalLimit = 1;
                        InputManager.Instance.PosHorizontalLimit = 1;
                        InputManager.Instance.NegVerticalLimit = 1;
                    }

                    lastRiding = riding;
                    lastLevitating = levitating;
                    lastWaterwalking = waterwalking;
                    lastSheathed = sheathed;
                    lastTransportMode = transportMode;
                }
            }
        }
    }
}
