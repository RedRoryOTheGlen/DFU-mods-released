using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace JumpmasterMod
{
    public class Jumpmaster : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<Jumpmaster>();

            mod.IsReady = true;
        }

        GameObject body;
        Camera eye;
        PlayerMotor motor;
        AcrobatMotor motorAcrobat;
        ClimbingMotor motorClimbing;
        PlayerEntity entity;
        CharacterController controller;

        DaggerfallAudioSource dfAudioSource;

        float jumpHoldTime = 0;
        bool jumpPressed;
        IEnumerator jumping;
        KeyCode jumpKeyCode = KeyCode.Space;
        KeyCode cancelKeyCode = KeyCode.Mouse0;
        int jumpDirectionControl = 0;   //0 = forward, 1 = movement, 2 = view
        float jumpHoldSpeed = 1;
        float jumpHoldTimeMin = 0.5f;
        float jumpDistanceBase = 100f;
        float jumpSpeedBase = 20f;
        float jumpSkillEffect = 0.25f;
        float jumpMovementEffect = 0.25f;
        float jumpEncumberanceEffect = 0.25f;
        bool jumpDrawTrajectory = true;
        List<Vector3> positions = new List<Vector3>();
        LineRenderer lineRenderer;
        int layerMask = -1;

        int fallThresholdBase = 5;

        float climbSpeedBase = 1f;
        bool climbContextual;

        bool roll;
        bool releaseJumpOnFall;

        bool grounded;

        //mod compatibility
        Mod ComeSailAway;
        bool isSailing;

        private void Start()
        {
            body = GameManager.Instance.PlayerObject;
            eye = GameManager.Instance.MainCamera;
            motor = GameManager.Instance.PlayerMotor;
            motorAcrobat = motor.GetComponent<AcrobatMotor>();
            motorClimbing = motor.GetComponent<ClimbingMotor>();
            entity = GameManager.Instance.PlayerEntity;
            controller = body.GetComponent<CharacterController>();

            layerMask = Physics.DefaultRaycastLayers;
            layerMask &= ~(1 << LayerMask.NameToLayer("Player"));

            dfAudioSource = gameObject.AddComponent<DaggerfallAudioSource>();

            ModCompatibilityChecking();

            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();

            PlayerEnterExit.OnTransitionInterior += OnTransition;
            PlayerEnterExit.OnTransitionExterior += OnTransition;
            PlayerEnterExit.OnTransitionDungeonInterior += OnTransition;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransition;
            DaggerfallTravelPopUp.OnPostFastTravel += OnPostFastTravel;
            DaggerfallTravelPopUp.OnPreFastTravel += OnPreFastTravel;
            SaveLoadManager.OnLoad += OnLoad;
            FloatingOrigin.OnPositionUpdate += OnPositionUpdate;

            //testing override
            //FormulaHelper.RegisterOverride(mod, "CalculateFallDamage", (Func<DaggerfallEntity, float, float, int>)CalculateFallDamage);
        }

        private void ModCompatibilityChecking()
        {
            ComeSailAway = ModManager.Instance.GetModFromGUID("dbe3e8ff-9059-45f1-a8be-732bb6000df7");
            if (ComeSailAway != null)
            {
                ModManager.Instance.SendModMessage(ComeSailAway.Title, "OnUpdateSailing", (Action<bool>)OnUpdateSailing);
            }
        }
        void OnPositionUpdate(Vector3 offset)
        {
            if (positions.Count > 0)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    positions[i] += offset;
                }
            }
        }

        public void OnUpdateSailing(bool value)
        {
            isSailing = value;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Main"))
            {
                jumpKeyCode = SetKeyFromText(settings.GetString("Main", "JumpInput"));
                cancelKeyCode = SetKeyFromText(settings.GetString("Main", "CancelInput"));
                jumpDirectionControl = settings.GetValue<int>("Main", "JumpDirectionControl");
                jumpDrawTrajectory = settings.GetValue<bool>("Main", "ShowAssist");
                jumpHoldTimeMin = settings.GetValue<int>("Main", "MinimumPower")/100f;
                jumpHoldSpeed = settings.GetValue<float>("Main", "HoldSpeed");
                jumpSkillEffect = settings.GetValue<int>("Main", "JumpingSkillEffect")/100f;
                jumpMovementEffect = settings.GetValue<int>("Main", "MovementSpeedEffect")/100f;
                jumpEncumberanceEffect = settings.GetValue<int>("Main", "EncumberanceEffect")/100f;
                jumpDistanceBase = settings.GetValue<int>("Main", "BaseJumpDistance");
                jumpSpeedBase = settings.GetValue<int>("Main", "BaseJumpSpeed");
                fallThresholdBase = settings.GetValue<int>("Main", "FallDamageThreshold");
                climbSpeedBase = settings.GetValue<int>("Main", "BaseClamberSpeed");
                climbContextual = settings.GetValue<bool>("Main", "ContextualClamber");
                roll = settings.GetValue<bool>("Main", "LandingRoll");
                releaseJumpOnFall = settings.GetValue<bool>("Main", "ReleaseJumpOnFall");
            }
        }

        void OnLoad(SaveData_v1 saveData)
        {
            StopJump();
        }

        void OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
        {
            StopJump();
        }

        void OnPostFastTravel()
        {
            StopJump();
        }

        void OnTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            StopJump();
        }

        private void Update()
        {
            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (!jumpPressed && CanJump() && InputManager.Instance.GetKeyDown(jumpKeyCode))
            {
                jumpHoldTime = jumpHoldTimeMin;
                jumpPressed = true;
            }

            if (jumpPressed)
            {
                if (InputManager.Instance.GetKeyDown(cancelKeyCode))
                {
                    //cancel jump
                    jumpPressed = false;
                    CancelJump();
                }
                else if (InputManager.Instance.GetKeyUp(jumpKeyCode))
                {
                    //release jump
                    jumpPressed = false;
                    if (CanJump())
                        StartJump(jumpHoldTime);
                    else
                        CancelJump();
                }
                else if (!InputManager.Instance.GetKey(jumpKeyCode))
                {
                    //cancel jump if key isn't pressed
                    jumpPressed = false;
                    CancelJump();
                }
            }

            //clamber climb check if moving forward (and running if grounded)
            if (climbContextual && jumping == null && InputManager.Instance.Vertical > 0)
            {
                if (!controller.isGrounded && !motor.IsClimbing)
                    CheckClamber();
                else if (InputManager.Instance.HasAction(InputManager.Actions.Run) || InputManager.Instance.ToggleAutorun)
                    CheckClamber(0.5f);
            }
        }

        private void LateUpdate()
        {
            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (jumpPressed)
            {
                //suppress vanilla jump here?
                DrawJumpTrajectory(jumpHoldTime);
                if (jumpHoldTime < 1)
                    jumpHoldTime += jumpHoldSpeed * Time.deltaTime;
                else
                    jumpHoldTime = 1;
            }

            if (controller.isGrounded)
            {
                if (!grounded)
                {
                    //just grounded
                }
            }
            else
            {
                if (grounded)
                {
                    //just ungrounded
                    if (releaseJumpOnFall && jumpPressed)
                    {
                        jumpPressed = false;
                        StartJump(jumpHoldTime);
                    }
                }
            }

            grounded = controller.isGrounded;
        }

        void DrawJumpTrajectory(float holdTime)
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.widthMultiplier = 0.1f;
                lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
                Color startColor = new Color(1, 1, 1, 0);
                Color endColor = new Color(1, 1, 1, 1);
                lineRenderer.startColor = startColor;
                lineRenderer.endColor = endColor;
                lineRenderer.enabled = false;
            }

            if (!lineRenderer.enabled && jumpDrawTrajectory)
                lineRenderer.enabled = true;

            positions.Clear();

            //get start
            Vector3 start = body.transform.position;

            Vector3 dir = Vector3.forward;

            //jump skill adds/subtracts up to %jumpSkillEffect
            float jumpDistanceBonus = ((entity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Jumping) / 100f) * jumpSkillEffect) - jumpSkillEffect;

            float runningMod = !motor.IsStandingStill && (InputManager.Instance.HasAction(InputManager.Actions.Run) || InputManager.Instance.ToggleAutorun) ? 2f : 1f;

            if (jumpDirectionControl == 2)
            {
                dir = Quaternion.AngleAxis(-45f, eye.transform.right) * eye.transform.forward;
                jumpDistanceBonus += Mathf.Clamp01(InputManager.Instance.Vertical) * (jumpMovementEffect * runningMod);
            }
            else if (jumpDirectionControl == 1 && !motor.IsStandingStill)
            {
                Vector3 inputDir = body.transform.TransformDirection(new Vector3(InputManager.Instance.Horizontal, 0, InputManager.Instance.Vertical).normalized);
                dir = Quaternion.AngleAxis(-GetJumpAngle(), Quaternion.AngleAxis(90, Vector3.up) * inputDir) * inputDir;
                jumpDistanceBonus += Mathf.Clamp01(inputDir.magnitude) * (jumpMovementEffect * runningMod);
            }
            else
            {
                dir = Quaternion.AngleAxis(-GetJumpAngle(), body.transform.right) * body.transform.forward;
                jumpDistanceBonus += Mathf.Clamp01(InputManager.Instance.Vertical) * (jumpMovementEffect * runningMod);
            }

            if (entity.Career.Athleticism)
            {
                jumpDistanceBonus += 0.1f;
                if (entity.ImprovedAthleticism)
                    jumpDistanceBonus += 0.1f;
            }

            jumpDistanceBonus -= Mathf.Clamp01(entity.Items.GetWeight()/entity.MaxEncumbrance) * jumpEncumberanceEffect;

            float jumpDistance = jumpDistanceBase * Mathf.Clamp((1 + jumpDistanceBonus), 0.1f, 10f) * (holdTime / 1);

            positions.Add(start);

            Vector3 startStep = dir * jumpDistance;

            //get all positions
            bool stopped = false;
            Vector3 currentPos = start;
            Vector3 currentGravity = Vector3.zero;
            Vector3 currentStep = startStep;
            Ray ray = new Ray(currentPos, currentStep.normalized);
            RaycastHit hit = new RaycastHit();
            float gravity = GetJumpGravity();
            while (!stopped && positions.Count < 300)
            {
                jumpDistance = 50f * (1f + jumpDistanceBonus) * (holdTime / 1);
                startStep = dir * jumpDistance;

                currentGravity += Vector3.down * gravity;

                currentStep = (startStep * (Time.fixedDeltaTime * 1)) + (currentGravity * (Time.fixedDeltaTime * 1));

                ray = new Ray(currentPos, currentStep.normalized);
                if (Physics.CapsuleCast(ray.origin + (Vector3.up * (controller.height / 2)), ray.origin, controller.radius, currentStep.normalized, out hit, currentStep.magnitude, layerMask, QueryTriggerInteraction.Ignore))
                {
                    stopped = true;
                    positions.Add(currentPos + (currentStep.normalized * hit.distance));
                }
                else
                {
                    currentPos += currentStep;
                    positions.Add(currentPos);
                }
            }

            if (jumpDrawTrajectory)
            {
                lineRenderer.positionCount = positions.Count;
                lineRenderer.SetPositions(positions.ToArray());
            }
        }

        bool CanJump()
        {
            if (jumping != null ||
                (!motor.IsGrounded && !motor.IsClimbing) ||
                motor.IsLevitating ||
                motor.IsSwimming ||
                motor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming ||
                GameManager.Instance.TransportManager.TransportMode == TransportModes.Cart ||
                entity.IsParalyzed ||
                isSailing
                )
                return false;

            return true;
        }

        float GetJumpAngle()
        {
            if (jumpDirectionControl == 1)
            {
                float angle = 75f;
                if (!motor.IsStandingStill)
                {
                    angle -= 30f;

                    if (InputManager.Instance.HasAction(InputManager.Actions.Run) || InputManager.Instance.ToggleAutorun)
                        angle -= 30f;

                    if (motor.IsMovingLessThanHalfSpeed)
                        angle += 15f;
                }
                return angle;
            }
            else
            {
                if (motor.IsCrouching)
                    return 75f;
                else if (InputManager.Instance.HasAction(InputManager.Actions.Run) || InputManager.Instance.ToggleAutorun)
                    return 15f;
                else
                    return 45;
            }
        }

        float GetJumpGravity()
        {
            float gravity = 4.9f;

            if (entity.IsSlowFalling)
                gravity *= 0.5f;

            if (entity.IsEnhancedJumping)
                gravity *= 0.5f;

            return gravity;
        }

        float GetFallThreshold()
        {
            float fallThreshold = fallThresholdBase * (1 + (entity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Jumping) / 100f));
            fallThreshold = Mathf.Round(fallThreshold);

            Debug.Log("JUMPMASTER - FALL THRESHOLD IS " + fallThreshold.ToString());

            return fallThreshold;
        }

        void StartJump(float holdTime)
        {
            if (jumping != null)
                return;

            //set acrobat motor falling threshold
            motorAcrobat.fallingDamageThreshold = GetFallThreshold();

            jumping = JumpCoroutine(holdTime);
            StartCoroutine(jumping);

            jumpHoldTime = jumpHoldTimeMin;
        }

        void StopJump()
        {
            if (jumping == null)
                return;

            StopCoroutine(jumping);
            positions.Clear();
            lineRenderer.enabled = false;
            motor.smoothFollowerLerpSpeed = 25f;
            if (motor.FreezeMotor > 0)
                motor.FreezeMotor = 0.1f;

            jumping = null;
        }

        void CancelJump()
        {
            if (jumping == null)
            {
                jumpHoldTime = jumpHoldTimeMin;
                if (positions.Count > 0)
                    positions.Clear();
                if (lineRenderer != null)
                    lineRenderer.enabled = false;
            }
        }

        IEnumerator JumpCoroutine(float holdTime)
        {
            DrawJumpTrajectory(holdTime);
            lineRenderer.enabled = false;

            Vector3 currentPosition = body.transform.position;
            Vector3 dir = Vector3.zero;

            //jump skill adds/subtracts up to %jumpSkillEffect
            float jumpSpeedBonus = ((entity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Jumping) / 100f) * jumpSkillEffect) - jumpSkillEffect;

            float runningMod = !motor.IsStandingStill && (InputManager.Instance.HasAction(InputManager.Actions.Run) || InputManager.Instance.ToggleAutorun) ? 2f : 1f;

            if (jumpDirectionControl == 1 && !motor.IsStandingStill)
            {
                Vector3 inputDir = body.transform.TransformDirection(new Vector3(InputManager.Instance.Horizontal, 0, InputManager.Instance.Vertical).normalized);
                jumpSpeedBonus += Mathf.Clamp01(inputDir.magnitude) * (jumpMovementEffect * runningMod);
            }
            else
                jumpSpeedBonus += Mathf.Clamp01(InputManager.Instance.Vertical) * (jumpMovementEffect * runningMod);

            if (entity.Career.Athleticism)
            {
                jumpSpeedBonus += 0.1f;
                if (entity.ImprovedAthleticism)
                    jumpSpeedBonus += 0.1f;
            }

            jumpSpeedBonus -= Mathf.Clamp01(entity.Items.GetWeight() / entity.MaxEncumbrance) * jumpEncumberanceEffect;

            float jumpSpeed = jumpSpeedBase * Mathf.Clamp((1 + jumpSpeedBonus), 0.1f, 10f);

            if (entity.IsSlowFalling)
                jumpSpeed *= 0.5f;

            if (entity.IsEnhancedJumping)
                jumpSpeed *= 2f;

            //start
            motor.smoothFollowerLerpSpeed = 250f;
            RaycastHit hit = new RaycastHit();

            Vector3 highestPosition = currentPosition;

            /*Vector3 horizontal = Vector2.zero;
            Vector3 horizontalTarget = Vector2.zero;
            float vertical = 0;
            float verticalTarget = 0;*/

            float halfPositions = (float)positions.Count / 2f;
            int breakout = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                //player started frame inside dungeon water
                if (GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && body.transform.position.y < (float)GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
                    break;

                //capsulecast from current position to next position
                dir = positions[i] - currentPosition;

                if (dir.y > 0)
                    highestPosition = positions[i];

                /*horizontal = new Vector2(body.transform.position.x, body.transform.position.z);
                vertical = body.transform.position.y;*/

                if (Physics.CapsuleCast(currentPosition + (Vector3.up * (controller.height / 2)), currentPosition, controller.radius, dir.normalized, out hit, dir.magnitude, layerMask, QueryTriggerInteraction.Ignore))
                {
                    //if obstructed, lerp player from current position to hit position
                    Vector3 hitPosition = currentPosition + (dir.normalized * hit.distance);
                    breakout = 0;
                    while (body.transform.position != hitPosition && breakout < 30)
                    {
                        if (GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && body.transform.position.y < (float)GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
                            break;
                        motor.FreezeMotor = 1;
                        body.transform.position = Vector3.MoveTowards(body.transform.position, hitPosition, jumpSpeed * Time.deltaTime);
                        Debug.Log("JUMPMASTER - LERPING FROM CURRENT POSITION TO HIT POSITION");
                        breakout++;
                        yield return new WaitForEndOfFrame();
                    }
                    break;
                }
                else if (Physics.Raycast(currentPosition, dir.normalized, out hit, dir.magnitude, layerMask, QueryTriggerInteraction.Ignore))
                {
                    //if obstructed, lerp player from current position to hit position
                    /*Vector3 hitPosition = currentPosition + (dir.normalized * hit.distance);
                    while (body.transform.position != hitPosition)
                    {
                        motor.FreezeMotor = 1;
                        body.transform.position = Vector3.MoveTowards(body.transform.position, hitPosition, jumpSpeed * Time.deltaTime);
                        //body.transform.position = Vector3.MoveTowards(body.transform.position, hitPosition, jumpSpeed * (1+Mathf.Abs(dir.y)) * Time.deltaTime);
                        yield return new WaitForEndOfFrame();
                    }*/
                    break;
                }
                else
                {
                    //if unobstructed, lerp player from current position to next position
                    breakout = 0;
                    while (body.transform.position != positions[i] && breakout < 30)
                    {
                        if (GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && body.transform.position.y < (float)GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
                            break;
                        motor.FreezeMotor = 1;
                        body.transform.position = Vector3.MoveTowards(body.transform.position, positions[i], jumpSpeed * Time.deltaTime);
                        Debug.Log("JUMPMASTER - LERPING FROM CURRENT POSITION TO NEXT POSITION");
                        breakout++;
                        yield return new WaitForEndOfFrame();
                    }
                }

                currentPosition = positions[i];
            }

            //after jump ends, perform unarmed attack in leap direction
            /*Ray ray = new Ray(body.transform.position, eye.transform.forward);
            if (Physics.SphereCast(ray, 0.25f, out hit, 2.25f, layerMask))
            {
                if (Physics.Raycast(ray, out hit, 2.25f, layerMask))
                {
                    GameManager.Instance.WeaponManager.WeaponDamage(null, false, false, hit.transform, hit.point, dir.normalized);
                }
            }*/

            //Decrease fatigue and tally Jumping skill
            //Maybe increase XP and Fatigue cost based on last jump distance or hold time?
            float fatigueLossMultiplier = 1.0f;
            if (entity.Career.Athleticism)
                fatigueLossMultiplier = entity.ImprovedAthleticism ? 0.8f : 0.9f;
            entity.DecreaseFatigue((int)(PlayerEntity.JumpingFatigueLoss * fatigueLossMultiplier));
            entity.TallySkill(DaggerfallConnect.DFCareer.Skills.Jumping, 1);

            bool ground = CheckGround();

            //handle landing
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && body.transform.position.y < (float)GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
            {
                //landed in dungeon water
                motor.GetComponent<PlayerFootsteps>().PlayLargeSplash();
                Debug.Log("JUMPMASTER - LANDED IN DUNGEON WATER");
            }
            else if (GameManager.Instance.StreamingWorld.PlayerTileMapIndex == 0)
            {
                //landed in deep exterior water
                motor.GetComponent<PlayerFootsteps>().PlayLargeSplash();
            }
            else if (!entity.IsSlowFalling)
            {
                //player is not slowfalling
                //fall damage
                float fallDistance = highestPosition.y - body.transform.position.y;
                Vector3 jumpDir = Vector3.ProjectOnPlane(positions[positions.Count - 1] - positions[0], Vector3.up);
                float jumpDistance = jumpDir.magnitude;

                if (entity.IsEnhancedJumping)
                    fallDistance *= 0.5f;

                bool doARoll = false;
                //do a roll if the player landed on the ground, Jumping skill is greater than or equal to 50%, is not holding backwards and fall distance is greater than half the falling damage threshold or total jump distance is greater than the threshold
                int jumpingSkill = entity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Jumping);
                if (roll &&
                    ground &&
                    jumpingSkill >= 50 &&
                    !InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards) &&
                    fallDistance > motorAcrobat.fallingDamageThreshold)
                    //(fallDistance > motorAcrobat.fallingDamageThreshold / 2 || jumpDistance > motorAcrobat.fallingDamageThreshold))
                {
                    //reduce total fall distance
                    //25% reduction at 50% Jumping
                    //75% reduction at 100% Jumping
                    fallDistance *= (1-((float)jumpingSkill/100f))+0.25f;
                    doARoll = true;
                }

                if (fallDistance > motorAcrobat.fallingDamageThreshold)
                    body.SendMessage("ApplyPlayerFallDamage", fallDistance, SendMessageOptions.DontRequireReceiver);
                else
                    body.SendMessage("HardFallAlert", fallDistance, SendMessageOptions.DontRequireReceiver);

                if (roll && doARoll && ground)
                {
                    Vector3 rollAxis = eye.transform.InverseTransformDirection(Quaternion.AngleAxis(75, Vector3.up) * jumpDir);
                    float current = 0;
                    GameManager.Instance.PlayerMouseLook.enabled = false;
                    motor.enabled = false;
                    //yield return new WaitForEndOfFrame();
                    while (current < 360)
                    {
                        current += 720 * Time.deltaTime;
                        motor.FreezeMotor = 1f;
                        controller.SimpleMove(jumpDir);
                        eye.transform.Rotate(rollAxis, 720 * Time.deltaTime);
                        yield return new WaitForEndOfFrame();
                    }
                    GameManager.Instance.PlayerMouseLook.enabled = true;
                    motor.enabled = true;
                }
            }

            if (GameManager.Instance.TransportManager.TransportMode == TransportModes.Horse)
                body.transform.position += Vector3.up * (controller.height * 0.5f);

            motor.smoothFollowerLerpSpeed = 25f;
            motor.FreezeMotor = 0.1f;
            positions.Clear();

            //check vault if the player isn't grounded
            if (!ground)
                CheckClamber();

            jumping = null;
        }

        bool CheckGround()
        {
            //check if the player is standing on a walkable surface
            Vector3 start = body.transform.position;
            Ray ray = new Ray(start, Vector3.down);
            RaycastHit hit = new RaycastHit();

            if (Physics.Raycast(ray, out hit, controller.height, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>() || hit.collider.GetComponent<Terrain>())
                {
                    //we hit something walkable
                    return true;
                }
            }

            return false;
        }

        bool CheckCeiling()
        {
            Ray ray = new Ray();
            RaycastHit hit = new RaycastHit();

            //check if player is facing wall
            ray.origin = body.transform.position + (Vector3.down * controller.height * 0.25f);
            ray.direction = body.transform.forward;
            if (Physics.Raycast(ray, out hit, controller.radius * 3, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>())
                    return true;
            }

            //check if no ceiling above player
            ray.origin = body.transform.position;
            ray.direction = Vector3.up;
            if (Physics.Raycast(ray, out hit, controller.height*0.75f, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>())
                    return true;
            }

            return false;
        }

        void CheckClamber(float range = 1f)
        {
            if (GameManager.Instance.TransportManager.TransportMode == TransportModes.Horse)
                return;

            Ray ray = new Ray();
            RaycastHit hit = new RaycastHit();

            //check if no ceiling above player
            ray.origin = body.transform.position;
            ray.direction = Vector3.up;
            if (Physics.Raycast(ray, out hit, controller.height, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>())
                    return;
            }

            //check if wall in front of player
            ray.origin = body.transform.position + (Vector3.down * controller.height * 0.25f);
            ray.direction = body.transform.forward;
            if (Physics.Raycast(ray, out hit, controller.radius * 4, layerMask, QueryTriggerInteraction.Ignore))
            {
                float dot = Vector3.Dot(Vector3.up, hit.normal);
                if (dot > 0.1 || dot  < -0.1)
                {
                    Debug.DrawLine(ray.origin, hit.point, Color.red, 3);
                    return;
                }
                Debug.DrawLine(ray.origin, hit.point, Color.yellow, 3);
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * controller.radius * 4, Color.green, 3);
                return;
            }

            float crouchMod = motor.IsCrouching ? 2f : 1f;

            float offset = 0;

            //get starting position
            Vector3 start = body.transform.position + (body.transform.forward * (controller.radius * 3f));
            ray.origin = start;
            ray.direction = Vector3.up;
            if (Physics.Raycast(ray, out hit, controller.height * 1.5f * crouchMod * range, layerMask, QueryTriggerInteraction.Ignore))
            {
                start = hit.point + (Vector3.down * controller.radius);
                offset = (controller.height * 1.5f * crouchMod * range) - hit.distance;
                Debug.DrawLine(start, hit.point, Color.blue, 3);
            }
            else
            {
                start += Vector3.up * (controller.height * 1.5f * crouchMod * range);
            }

            ray.origin = start;
            ray.direction = Vector3.down;
            //check if there's a walkable surface in front and above of the player
            if (Physics.SphereCast(ray,controller.radius * 1f, out hit, (controller.height * 1.75f * crouchMod * range) - offset, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<MeshCollider>())
                {
                    Vector3 hitPoint = hit.point;

                    //we hit something climbable
                    Debug.DrawLine(start, hitPoint, Color.green, 3);

                    //TO-DO: check if there is enough space above the object for the player to stand
                    //check if point is underwater
                    if (GameManager.Instance.PlayerEnterExit.blockWaterLevel != 10000 && hitPoint.y < (float)GameManager.Instance.PlayerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)
                        return;

                    //do clamber to hit point
                    StartClamber(hitPoint);
                }
                else
                    Debug.DrawLine(start, hit.point, Color.yellow, 3);
                return;
            }
            Debug.DrawRay(start, Vector3.down * ((controller.height * 2f * crouchMod * range) - offset), Color.red, 3);

            //wall is not clamberable, try climbing
            motorClimbing.ClimbingCheck();
        }

        void StartClamber(Vector3 position)
        {
            if (jumping != null)
                StopJump();

            jumping = ClamberCoroutine(position);
            StartCoroutine(jumping);
        }

        IEnumerator ClamberCoroutine(Vector3 position)
        {
            Vector3 vaultPosition = position + (Vector3.up * (controller.height / 2));

            motor.smoothFollowerLerpSpeed = 250f;

            //climb skill
            float climbSpeedBonus = (entity.Skills.GetLiveSkillValue(DaggerfallConnect.DFCareer.Skills.Climbing) / 100f);

            if (entity.Career.Athleticism)
            {
                climbSpeedBonus += 0.1f;
                if (entity.ImprovedAthleticism)
                    climbSpeedBonus += 0.1f;
            }

            climbSpeedBonus -= Mathf.Clamp01(entity.Items.GetWeight() / entity.MaxEncumbrance) * jumpEncumberanceEffect;

            PlayAttackSound();

            float climbSpeed = climbSpeedBase * Mathf.Clamp((1 + climbSpeedBonus),0.1f,10f);

            float time = Vector3.Distance(body.transform.position, vaultPosition)/climbSpeed;
            float timer = 0;

            Vector3 midPosition = new Vector3(body.transform.position.x, vaultPosition.y, body.transform.position.z);
            while (timer < time)
            {
                motor.FreezeMotor = 1;
                body.transform.position = Vector3.Slerp(body.transform.position, Vector3.Lerp(midPosition,vaultPosition,timer/time), timer / time);
                timer += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            /*while (body.transform.position != vaultPosition)
            {
                motor.FreezeMotor = 1;
                body.transform.position = Vector3.MoveTowards(body.transform.position, vaultPosition, climbSpeed * Time.deltaTime);
                yield return new WaitForEndOfFrame();
            }*/

            //Decrease fatigue and tally Climbing skill
            float fatigueLossMultiplier = 1.0f;
            if (entity.Career.Athleticism)
                fatigueLossMultiplier = entity.ImprovedAthleticism ? 0.8f : 0.9f;
            entity.DecreaseFatigue((int)(PlayerEntity.ClimbingFatigueLoss * fatigueLossMultiplier));
            entity.TallySkill(DaggerfallConnect.DFCareer.Skills.Climbing, 1);

            motor.smoothFollowerLerpSpeed = 25f;
            motor.FreezeMotor = 0.1f;

            jumping = null;
        }
        public void PlayAttackSound(SoundClips customSound = SoundClips.None)
        {
            if (dfAudioSource)
            {
                if (customSound == SoundClips.None)
                {
                    if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                    {
                        LycanthropyTypes lycanthropyType = GameManager.Instance.PlayerEffectManager.LycanthropyType();
                        if (lycanthropyType == LycanthropyTypes.Werewolf)
                        {
                            if (UnityEngine.Random.value > 0.5f)
                                customSound = SoundClips.EnemyWerewolfAttack;
                            else
                                customSound = SoundClips.EnemyWerewolfBark;
                        }
                        else if (lycanthropyType == LycanthropyTypes.Wereboar)
                        {
                            if (UnityEngine.Random.value > 0.5f)
                                customSound = SoundClips.EnemyWereboarAttack;
                            else
                                customSound = SoundClips.EnemyWereboarBark;
                        }
                    }
                    else
                    {
                        PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                        customSound = DaggerfallEntity.GetRaceGenderAttackSound(playerEntity.Race, playerEntity.Gender, true);
                    }

                    float pitch = dfAudioSource.AudioSource.pitch;
                    dfAudioSource.AudioSource.pitch = pitch + UnityEngine.Random.Range(0, 0.3f);
                    dfAudioSource.AudioSource.pitch = pitch;
                    dfAudioSource.PlayOneShot(customSound, 0, 1f);
                }
                else
                {
                    dfAudioSource.PlayOneShot(customSound, 0, 1f);
                }
            }
        }

        public static int CalculateFallDamage(DaggerfallEntity entity, float threshold, float distance)
        {
            Debug.Log("JUMPMASTER - CALCULATE FALL DAMAGE OVERRIDE STARTED!");

            float HPPerMetre = 5;

            if (entity is PlayerEntity)
                Debug.Log("JUMPMASTER - PLAYER IS CALCULATING FALL DAMAGE!");

            int damage = (int)(HPPerMetre * (distance - threshold));

            return damage;
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
                return KeyCode.X;
            }
        }
    }
}
