using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldwalkingMod
{
    public class BillboardPlayer : BillboardMobile
    {
        public static BillboardPlayer Instance;

        public override float GetSizeMod()
        {
            if (isRiding)
                return 0.028f * scaleOffset;
            else
                return 0.019f * scaleOffset;
        }

        public override float GetFrameTime()
        {
            if (isRiding)
                return 0.0625f * (2 - walkAnimSpeedMod);
            else
                return 0.25f * (2 - walkAnimSpeedMod);
        }

        MobileBillboardState[] StatesIdle;
        MobileBillboardState[] StatesMove;
        MobileBillboardState[] StatesAttack;

        MobileBillboardState[] StatesIdleHorse;
        MobileBillboardState[] StatesMoveHorse;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            SaveLoadManager.OnLoad += OnLoad;
        }

        public static void OnLoad(SaveData_v1 saveData)
        {
            Instance.InitializeStates();
        }

        public override void InitializeStates()
        {
            int genderOffset = GameManager.Instance.PlayerEntity.Gender == DaggerfallWorkshop.Game.Entity.Genders.Female ? 0 : 1;
            int archiveFoot = 487 + genderOffset;
            int archiveHorse = 493 + genderOffset;

            StatesIdle = new MobileBillboardState[numberOrientations];
            StatesIdle[0] = new MobileBillboardState("IdleForward", archiveFoot, 15, 0, 1, false);
            StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archiveFoot, 16, 0, 1, true);
            StatesIdle[2] = new MobileBillboardState("IdleLeft", archiveFoot, 17, 0, 1, true);
            StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archiveFoot, 18, 0, 1, true);
            StatesIdle[4] = new MobileBillboardState("IdleBackward", archiveFoot, 19, 0, 1, false);
            StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archiveFoot, 18, 0, 1, false);
            StatesIdle[6] = new MobileBillboardState("IdleRight", archiveFoot, 17, 0, 1, false);
            StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archiveFoot, 16, 0, 1, false);

            StatesMove = new MobileBillboardState[numberOrientations];
            StatesMove[0] = new MobileBillboardState("MoveForward", archiveFoot, 0, 0, 4, false);
            StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archiveFoot, 1, 0, 4, true);
            StatesMove[2] = new MobileBillboardState("MoveLeft", archiveFoot, 2, 0, 4, true);
            StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archiveFoot, 3, 0, 4, true);
            StatesMove[4] = new MobileBillboardState("MoveBackward", archiveFoot, 4, 0, 4, false);
            StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archiveFoot, 3, 0, 4, false);
            StatesMove[6] = new MobileBillboardState("MoveRight", archiveFoot, 2, 0, 4, false);
            StatesMove[7] = new MobileBillboardState("MoveForwardRight", archiveFoot, 1, 0, 4, false);

            StatesAttack = new MobileBillboardState[numberOrientations];
            StatesAttack[0] = new MobileBillboardState("AttackMeleeForward", archiveFoot, 5, 0, 6, false);
            StatesAttack[1] = new MobileBillboardState("AttackMeleeForwardLeft", archiveFoot, 6, 0, 6, true);
            StatesAttack[2] = new MobileBillboardState("AttackMeleeLeft", archiveFoot, 7, 0, 6, true);
            StatesAttack[3] = new MobileBillboardState("AttackMeleeBackwardLeft", archiveFoot, 8, 0, 6, true);
            StatesAttack[4] = new MobileBillboardState("AttackMeleeBackward", archiveFoot, 9, 0, 6, false);
            StatesAttack[5] = new MobileBillboardState("AttackMeleeBackwardRight", archiveFoot, 8, 0, 6, false);
            StatesAttack[6] = new MobileBillboardState("AttackMeleeRight", archiveFoot, 7, 0, 6, false);
            StatesAttack[7] = new MobileBillboardState("AttackMeleeForwardRight", archiveFoot, 6, 0, 6, false);

            StatesIdleHorse = new MobileBillboardState[numberOrientations];
            StatesIdleHorse[0] = new MobileBillboardState("IdleForward", archiveHorse, 0, 0, 1, false);
            StatesIdleHorse[1] = new MobileBillboardState("IdleForwardLeft", archiveHorse, 1, 0, 1, true);
            StatesIdleHorse[2] = new MobileBillboardState("IdleLeft", archiveHorse, 2, 0, 1, true);
            StatesIdleHorse[3] = new MobileBillboardState("IdleBackwardLeft", archiveHorse, 3, 0, 1, true);
            StatesIdleHorse[4] = new MobileBillboardState("IdleBackward", archiveHorse, 4, 0, 1, false);
            StatesIdleHorse[5] = new MobileBillboardState("IdleBackwardRight", archiveHorse, 3, 0, 1, false);
            StatesIdleHorse[6] = new MobileBillboardState("IdleRight", archiveHorse, 2, 0, 1, false);
            StatesIdleHorse[7] = new MobileBillboardState("IdleForwardRight", archiveHorse, 1, 0, 1, false);

            StatesMoveHorse = new MobileBillboardState[numberOrientations];
            StatesMoveHorse[0] = new MobileBillboardState("MoveForward", archiveHorse, 0, 0, 8, false);
            StatesMoveHorse[1] = new MobileBillboardState("MoveForwardLeft", archiveHorse, 1, 0, 8, true);
            StatesMoveHorse[2] = new MobileBillboardState("MoveLeft", archiveHorse, 2, 0, 8, true);
            StatesMoveHorse[3] = new MobileBillboardState("MoveBackwardLeft", archiveHorse, 3, 0, 8, true);
            StatesMoveHorse[4] = new MobileBillboardState("MoveBackward", archiveHorse, 4, 0, 8, false);
            StatesMoveHorse[5] = new MobileBillboardState("MoveBackwardRight", archiveHorse, 3, 0, 8, false);
            StatesMoveHorse[6] = new MobileBillboardState("MoveRight", archiveHorse, 2, 0, 8, false);
            StatesMoveHorse[7] = new MobileBillboardState("MoveForwardRight", archiveHorse, 1, 0, 8, false);
        }

        // Start is called before the first frame update
        public override void Initialize(int newArchive, float size, float sizeOffset)
        {
            if (!IsReady)
                return;

            orientationTime = 0;

            scale = size;
            scaleOffsetMod = sizeOffset;

            // Get component references
            mainCamera = Worldwalking.Instance.Camera;
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            if (newArchive != archive)
            {
                archive = newArchive;
                InitializeStates();
            }

            if (meshRenderer.sharedMaterial == null)
                AssignMeshAndMaterial();

            if (stateLast != null)
                stateCurrent = stateLast;
            else
                stateCurrent = StatesIdle;

            UpdateOrientation(true);
        }

        private void Update()
        {
            /*if (GameManager.IsGamePaused)
                return;*/

            if (orientationTime > 0)
            {
                if (orientationTimer <= orientationTime)
                    orientationTimer += Time.unscaledDeltaTime;
            }
        }

        public override void LateUpdate()
        {
            /*if (GameManager.IsGamePaused)
                return;*/

            // Rotate to face camera in game
            if (mainCamera && Application.isPlaying)
            {
                Vector3 viewDirection = -new Vector3(mainCamera.transform.forward.x, 0, mainCamera.transform.forward.z);

                // Rotate billboard based on camera facing
                cameraPosition = mainCamera.transform.position;

                transform.LookAt(transform.position + viewDirection);

                // Orient enemy based on camera position
                UpdateOrientation();

                LoopIdleBillboard();
            }
        }

        public override void LoopIdleBillboard()
        {
            bool forceUpdate = false;

            if (stateCurrent[lastOrientation].frames.Count > 1)
            {
                float speed = 1f;

                if (frameTimer > GetFrameTime() * speed)
                {
                    if (frameCurrent < stateCurrent[lastOrientation].frames.Count - 1)
                        frameCurrent++;
                    else
                        frameCurrent = 0;

                    // Assign imported texture
                    if (frameCurrent > stateCurrent[lastOrientation].frames.Count - 1)
                        frameCurrent = 0;

                    UpdateBillboardDelayed(frameCurrent, lastOrientation, stateCurrent);
                    frameTimer = 0;
                }
                else
                {
                    frameTimer += Time.unscaledDeltaTime * Worldwalking.Instance.timeScaleIndex;
                }
            }

            if (isMoving)
            {
                if (isRiding)
                {
                    stateCurrent = StatesMoveHorse;
                }
                else
                {
                    stateCurrent = StatesMove;
                }
            }
            else
            {
                if (isRiding)
                    stateCurrent = StatesIdleHorse;
                else
                {
                    stateCurrent = StatesIdle;
                }
            }

            if (forceUpdate || stateLast != stateCurrent)
                UpdateBillboardDelayed(frameCurrent, lastOrientation, stateCurrent);

            stateLast = stateCurrent;
        }
    }
}

