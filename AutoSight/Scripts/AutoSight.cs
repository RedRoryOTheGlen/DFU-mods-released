using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.UserInterface;

public class AutoSight : MonoBehaviour
{
    const int nativeScreenWidth = 320;
    const int nativeScreenHeight = 200;
    Rect screenRect;
    Rect screenRectLast;
    float HUDHeight;
    float HUDHeightLast;

    Camera eye;
    Transform body;
    PlayerMouseLook mouseLook;
    PlayerHeightChanger heightChanger;

    public LayerMask obstacleLayerMask;
    public LayerMask terrainLayerMask;

    int activationCondition = 0;    //0 = On SwingWeapon Input, 1 = On Input Toggle
    string activationKeyString;
    const KeyCode activationKeyDefault = KeyCode.Tab;
    KeyCode activationKey;
    bool activation;
    float activationInputThresholdUpper = 0.8f;
    float activationInputThresholdLower = 0.2f;
    float activationInputLook;

    public float viewSoftzone = 15;
    public int viewSpeed = 1;
    float viewSpeedFinal;

    public int targetingRange = 100;
    public float targetingRadius = 45;
    float targetingTime = 0.25f;
    float targetingTimer;
    int viewOffset;
    Vector2 viewOffsetInput = Vector2.zero;

    bool quickturn;
    public int quickturnMode;   //0 = Key, 1 = Double-Tap
    string quickturnKeyString;
    KeyCode quickturnKey = KeyCode.LeftShift;
    public InputManager.Actions quickTurnTapLastAction;
    public float quickTurnTapTime = 0.2f;
    float quickTurnTapTimer;

    IEnumerator quickturning;
    public float quickturnDuration = 1;

    public bool feedbackOnTarget;
    public bool feedbackOnQuickturn;
    public int feedbackIndicator;
    public float feedbackWidgetOffsetX;
    public float feedbackWidgetOffsetY;
    public float feedbackWidgetScale;

    public Vector3 vectorCurrent;
    Vector3 offset;

    DaggerfallEntityBehaviour target;
    CharacterController targetController;
    DaggerfallMobileUnit targetMobile;

    Texture2D[] eyeTextures;
    Texture2D eyeTexture;
    Rect eyeRect;
    int eyeFrameCurrent;
    float eyeFrameTime = 0.25f;
    float eyeFrameTimer;

    bool autopitch;
    float autopitchThreshold = 0.5f;
    float autopitchSpeed = 0.5f;
    float autopitchStrength = 0.5f;
    float autopitchTime = 1f;
    float autopitchTimer;
    float autopitchOffset = 0;

    Mod tomeOfBattle;
    KeyCode tomeOfBattleSwingKeyCode = KeyCode.None;

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<AutoSight>();
    }

    void Awake()
    {
        eye = GameManager.Instance.MainCamera;
        body = GameManager.Instance.PlayerObject.transform;
        mouseLook = GameManager.Instance.PlayerMouseLook;
        heightChanger = GameManager.Instance.PlayerController.GetComponent<PlayerHeightChanger>();
        obstacleLayerMask |= (1 << LayerMask.NameToLayer("Default"));
        obstacleLayerMask |= (1 << LayerMask.NameToLayer("Enemies"));
        terrainLayerMask |= (1 << LayerMask.NameToLayer("Default"));

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        InitializeTextures();

        mod.IsReady = true;
    }

    private void Start()
    {
        ModCompatibilityChecking();
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Activation"))
        {
            activationCondition = settings.GetValue<int>("Activation", "Condition");
            activationKeyString = settings.GetValue<string>("Activation", "Input");
            activationKey = SetKeyFromText(activationKeyString);
            activationInputThresholdLower = settings.GetTupleFloat("Activation", "InputThreshold").First;
            activationInputThresholdUpper = settings.GetTupleFloat("Activation", "InputThreshold").Second;
        }
        if (change.HasChanged("View"))
        {
            viewSoftzone = settings.GetValue<int>("View", "Softzone") * 0.01f * eye.fieldOfView;
            viewSpeed = settings.GetValue<int>("View", "Speed");
            viewOffset = settings.GetValue<int>("View", "OffsetAimpoint");
        }
        if (change.HasChanged("Targeting"))
        {
            targetingRange = settings.GetValue<int>("Targeting", "Range");
            targetingRadius = settings.GetValue<int>("Targeting", "Radius") * 0.01f * eye.fieldOfView;
            targetingTime = settings.GetValue<int>("Targeting", "Interval")*0.0625f;
        }
        if (change.HasChanged("Quickturn"))
        {
            quickturn = settings.GetValue<bool>("Quickturn", "Enable");
            quickturnDuration = settings.GetValue<float>("Quickturn", "Duration");
            quickturnMode = settings.GetValue<int>("Quickturn", "InputMode");
            quickturnKeyString = settings.GetValue<string>("Quickturn", "KeyInput");
            quickturnKey = SetKeyFromText(quickturnKeyString);
            quickTurnTapTime = settings.GetValue<float>("Quickturn", "TapTime");
        }
        if (change.HasChanged("Autopitch"))
        {
            autopitch = settings.GetValue<bool>("Autopitch", "Enable");
            autopitchThreshold = settings.GetValue<float>("Autopitch", "Threshold");
            autopitchTime = settings.GetValue<float>("Autopitch", "Time");
            autopitchSpeed = settings.GetValue<float>("Autopitch", "Speed")*100;
            autopitchStrength = settings.GetValue<float>("Autopitch", "Strength");
            autopitchOffset = settings.GetValue<float>("Autopitch", "Offset");
        }
        if (change.HasChanged("Feedback"))
        {
            feedbackOnTarget = settings.GetValue<bool>("Feedback", "OnTargetAcquired");
            feedbackOnQuickturn = settings.GetValue<bool>("Feedback", "OnQuickturn");
            feedbackIndicator = settings.GetValue<int>("Feedback", "Indicator");
            feedbackWidgetOffsetX = settings.GetTupleFloat("Feedback", "WidgetOffset").First;
            feedbackWidgetOffsetY = 1 - settings.GetTupleFloat("Feedback", "WidgetOffset").Second;
            feedbackWidgetScale = settings.GetValue<float>("Feedback", "WidgetScale");
            GetEyeRect();
        }
    }

    private void ModCompatibilityChecking()
    {
        //check if Tome of Battle is installed
        tomeOfBattle = ModManager.Instance.GetModFromGUID("a166c215-0a5a-4582-8bf3-8be8df80d5e5");
        if (tomeOfBattle != null)
        {
            ModManager.Instance.SendModMessage(tomeOfBattle.Title, "getSwingKeyCode", null, (string message, object data) =>
            {
                tomeOfBattleSwingKeyCode = (KeyCode)data;
            });
        }
    }

    void InitializeTextures()
    {
        eyeTextures = new Texture2D[5];
        int archive = 112387;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 5; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            eyeTextures[i] = texture;
            frame++;
        }

        eyeFrameCurrent = 0;
    }

    void RefreshWidget()
    {
        if (feedbackIndicator == 2)
        {
            if (activation && DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair)
                DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = false;
            else if (!activation && !DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair)
                DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = true;
        }
        else
        {
            if (!DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair)
                DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = true;
        }
    }

    void GetEyeRect()
    {
        if (eyeTexture == null)
            return;

        Vector2 size = (new Vector2(eyeTexture.width * ((float)screenRect.width / (float)nativeScreenWidth), eyeTexture.height * ((float)screenRect.height / (float)nativeScreenHeight))*0.5f)*feedbackWidgetScale;
        Vector2 position = Vector2.zero;

        switch (feedbackIndicator)
        {
            case 1: //Widget
                position = new Vector2(screenRect.x + (screenRect.width * feedbackWidgetOffsetX) - (size.x * 0.5f), screenRect.y + (screenRect.height * feedbackWidgetOffsetY) - (size.y * 0.5f) - (HUDHeight * 0.5f));
                break;
            case 2: //Crosshair
                position = new Vector2(screenRect.x + screenRect.width * 0.5f - (size.x * 0.5f), screenRect.y + screenRect.height * 0.5f - (size.y * 0.5f) - (HUDHeight * 0.5f));
                break;
        }

        eyeRect = new Rect(position, size);
    }

    private void OnGUI()
    {
        GUI.depth = 0;

        if (eyeTexture == null || GameManager.Instance.PlayerEntity == null || feedbackIndicator == 0)
            return;

        if (activationCondition > 0)
        {
            if (!activation)
                return;
        }
        else
        {
            if ((tomeOfBattleSwingKeyCode == KeyCode.None && !InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon)) ||
                (tomeOfBattleSwingKeyCode != KeyCode.None && !InputManager.Instance.GetKey(tomeOfBattleSwingKeyCode)))
                return;
        }

        //get eye texture rect

        DaggerfallUI.DrawTexture(eyeRect, eyeTexture);
    }

    private void LateUpdate()
    {
        if (autopitch)
        {
            if (InputManager.Instance.LookY > autopitchThreshold || InputManager.Instance.LookY < -autopitchThreshold)
                autopitchTimer = 0;
            else
            {
                if (autopitchTimer > autopitchTime)
                    AdjustPitch();
                else if (InputManager.Instance.LookY != 0)
                    autopitchTimer = 0;
                else
                    autopitchTimer += Time.deltaTime;
            }
        }

        if (quickturn)
        {
            if (quickturning == null)
            {
                if (quickturnMode == 0)
                {
                    if (InputManager.Instance.GetKey(quickturnKey))
                    {
                        if (!InputManager.Instance.HasAction(InputManager.Actions.MoveForwards))
                        {
                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveBackwards))
                                QuickTurn(180);

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveRight))
                                QuickTurn(90);

                            if (InputManager.Instance.HasAction(InputManager.Actions.MoveLeft))
                                QuickTurn(-90);
                        }
                    }
                }
                else if (quickturnMode == 1)
                {
                    if (quickTurnTapLastAction != InputManager.Actions.Unknown)
                    {
                        if (InputManager.Instance.ActionStarted(quickTurnTapLastAction))
                        {
                            if (quickTurnTapLastAction == InputManager.Actions.MoveBackwards)
                                QuickTurn(180);
                            else if(quickTurnTapLastAction == InputManager.Actions.MoveRight)
                                QuickTurn(90);
                            else if (quickTurnTapLastAction == InputManager.Actions.MoveLeft)
                                QuickTurn(-90);

                            quickTurnTapLastAction = InputManager.Actions.Unknown;
                        }
                        else
                        {
                            if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveBackwards))
                            {
                                quickTurnTapLastAction = InputManager.Actions.MoveBackwards;
                                quickTurnTapTimer = 0;
                            }

                            if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveRight))
                            {
                                quickTurnTapLastAction = InputManager.Actions.MoveRight;
                                quickTurnTapTimer = 0;
                            }

                            if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveLeft))
                            {
                                quickTurnTapLastAction = InputManager.Actions.MoveLeft;
                                quickTurnTapTimer = 0;
                            }
                        }

                        //if timer is past time, reset last action, else increment timer
                        if (quickTurnTapTimer > quickTurnTapTime)
                            quickTurnTapLastAction = InputManager.Actions.Unknown;
                        else
                            quickTurnTapTimer += Time.deltaTime;
                    }
                    else
                    {
                        if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveBackwards))
                        {
                            quickTurnTapLastAction = InputManager.Actions.MoveBackwards;
                            quickTurnTapTimer = 0;
                        }

                        if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveRight))
                        {
                            quickTurnTapLastAction = InputManager.Actions.MoveRight;
                            quickTurnTapTimer = 0;
                        }

                        if (InputManager.Instance.ActionStarted(InputManager.Actions.MoveLeft))
                        {
                            quickTurnTapLastAction = InputManager.Actions.MoveLeft;
                            quickTurnTapTimer = 0;
                        }
                    }
                }
            }
        }

        activationInputLook = new Vector2(InputManager.Instance.LookX, InputManager.Instance.LookY).magnitude;

        if (feedbackIndicator > 0)
        { 
            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            HUDHeight = 0;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                HUDHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
            }

            if (screenRect != screenRectLast || HUDHeight != HUDHeightLast)
                GetEyeRect();

            screenRectLast = screenRect;
            HUDHeightLast = HUDHeight;

            RefreshWidget();
        }

        if (activationCondition > 0)
        {
            if (!activation && target != null)
                ClearTarget();
        }
        else
        {
            if ((tomeOfBattleSwingKeyCode == KeyCode.None && InputManager.Instance.ActionComplete(InputManager.Actions.SwingWeapon)) ||
                (tomeOfBattleSwingKeyCode != KeyCode.None && InputManager.Instance.GetKeyUp(tomeOfBattleSwingKeyCode)))
            {
                ClearTarget();
                DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = true;
            }
        }

        if (activationCondition > 0)
        {
            if (InputManager.Instance.GetKeyUp(activationKey))
            {
                activation = !activation;

                if (activation)
                    DaggerfallUI.Instance.PopupMessage("Auto-sight enabled");
                else
                    DaggerfallUI.Instance.PopupMessage("Auto-sight disabled");
            }

            if (activation)
            {
                if (target == null)
                    TrySeekTarget();
            }
        }
        else
        {
            if ((tomeOfBattleSwingKeyCode == null && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon)) ||
                (tomeOfBattleSwingKeyCode != null && InputManager.Instance.GetKey(tomeOfBattleSwingKeyCode)))
            {
                if (target == null)
                    TrySeekTarget();
                if (feedbackIndicator == 2)
                    DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = false;
            }
        }

        if (target == null)
        {
            if (eyeFrameTimer > eyeFrameTime)
            {
                if (eyeFrameCurrent >= 3)
                    eyeFrameCurrent = 0;
                else
                    eyeFrameCurrent++;
                eyeFrameTimer = 0;
            }
            else
                eyeFrameTimer += Time.deltaTime;
            eyeTexture = eyeTextures[eyeFrameCurrent];
            return;
        }

        if (activationCondition > 0)
        {
            if (activationInputLook > activationInputThresholdUpper)
            {
                ClearTarget();
                return;
            }
        }

        eyeFrameCurrent = 4;
        eyeTexture = eyeTextures[eyeFrameCurrent];

        if (!target.gameObject.activeSelf || target.Entity.IsMagicallyConcealed)
        {
            ClearTarget();
            return;
        }

        UpdateView();
    }

    void AdjustPitch()
    {
        if (!GameManager.Instance.PlayerMotor.IsGrounded)
            return;

        float height = GameManager.Instance.PlayerController.height;

        Vector3 originPlayer = body.transform.position + (Vector3.up * height * 0.5f);

        Vector3 point = Vector3.zero;

        Ray ray = new Ray(originPlayer + body.transform.forward, Vector3.down);
        RaycastHit hit = new RaycastHit();
        if (Physics.Raycast(ray, out hit, height * 3, terrainLayerMask))
            point = hit.point + (Vector3.up * height);
        else
            point = body.transform.position + body.transform.forward;

        if (point == Vector3.zero)
            return;

        Vector3 pointDir = (point - originPlayer).normalized;
        float angle = Vector3.Angle(body.transform.forward, pointDir)-autopitchOffset;

        float pitchCurrent = mouseLook.Pitch + InputManager.Instance.LookY;
        float pitchTarget = angle;
        if (pointDir.y > 0)
            pitchTarget = -angle;

        pitchCurrent = Mathf.MoveTowards(pitchCurrent, pitchTarget * autopitchStrength, autopitchSpeed*Time.deltaTime);

        if (!mouseLook.cursorActive)
            mouseLook.SetFacing(mouseLook.Yaw + InputManager.Instance.LookX, pitchCurrent);
        else
            eye.transform.localEulerAngles = new Vector3(pitchCurrent,0,0);
    }

    void UpdateView()
    {
        /*float offsetY = 0.25f;
        switch (targetMobile.Summary.Enemy.ID)
        {
            case (int)MobileTypes.Spider:
            case (int)MobileTypes.Slaughterfish:
            case (int)MobileTypes.Lamia:
            case (int)MobileTypes.Wraith:
            case (int)MobileTypes.OrcShaman:
            case (int)MobileTypes.Lich:
            case (int)MobileTypes.AncientLich:
            case (int)MobileTypes.Mage:
            case (int)MobileTypes.Spellsword:
            case (int)MobileTypes.Healer:
                offsetY = 0.125f;
                break;
            case (int)MobileTypes.Imp:
            case (int)MobileTypes.GiantScorpion:
                offsetY = 0f;
                break;
            case (int)MobileTypes.GiantBat:
            case (int)MobileTypes.Dragonling:
            case (int)MobileTypes.Dragonling_Alternate:
                offsetY = -0.125f;
                break;
            case (int)MobileTypes.Harpy:
                offsetY = -0.5f;
                break;
        }
        Vector3 vectorTarget = (((target.transform.position - targetController.center) + (Vector3.up * (targetController.height * offsetY))) - eye.transform.position).normalized;*/

        if (activationCondition > 0 && viewOffset > 0)
        {
            viewOffsetInput.x += InputManager.Instance.LookX * 0.2f;
            viewOffsetInput.y += InputManager.Instance.LookY * 0.2f;
            float radius = targetController.height / 2;
            if (viewOffset == 1)
            {
                if (viewOffsetInput.magnitude > radius)
                {
                    ClearTarget();
                    return;
                }
            }
            else if (viewOffset == 2)
                viewOffsetInput = Vector3.ClampMagnitude(viewOffsetInput, radius);
        }

        Vector3 vectorTarget = (((target.transform.position-targetController.center) + (Vector3.up * viewOffsetInput.y) + (eye.transform.right * viewOffsetInput.x)) - eye.transform.position).normalized;
        viewSpeedFinal = viewSpeed * Mathf.Clamp01(Vector3.Angle(vectorCurrent, vectorTarget) / viewSoftzone);
        vectorCurrent = Vector3.MoveTowards(vectorCurrent, vectorTarget, Time.deltaTime * viewSpeedFinal);
        offset = Quaternion.LookRotation(vectorCurrent, Vector3.up).eulerAngles;

        if (!mouseLook.cursorActive)
        {
            if (offset.x > 180)
                offset.x -= 360;
            mouseLook.SetFacing(offset.y, offset.x);
        }
    }

    void TrySeekTarget()
    {
        if (activationCondition > 0)
        {
            if (activationInputLook > activationInputThresholdLower)
                return;
        }

        if (targetingTimer > targetingTime)
        {
            SeekTarget();
            targetingTimer = 0;
        }
        else
            targetingTimer += Time.deltaTime;
    }

    void SeekTarget()
    {
        if (target != null)
            return;

        DaggerfallEntityBehaviour closestValidTarget = null;
        float closestViewAngle = Mathf.Infinity;

        foreach (DaggerfallEntityBehaviour entityBehaviour in ActiveGameObjectDatabase.GetActiveEnemyBehaviours())
        {
            if (entityBehaviour.EntityType == EntityTypes.EnemyMonster || entityBehaviour.EntityType == EntityTypes.EnemyClass)
            {
                if (IsValidTarget(entityBehaviour))
                {
                    Vector3 targetDir = (entityBehaviour.transform.position - eye.transform.position).normalized;
                    float viewAngle = Vector3.Angle(eye.transform.forward, targetDir);
                    if (viewAngle < closestViewAngle)
                    {
                        closestValidTarget = entityBehaviour;
                        closestViewAngle = viewAngle;
                    }
                }
            }
        }

        if (closestValidTarget != null)
        {
            if (feedbackOnTarget)
                DaggerfallUI.Instance.PopupMessage("Auto-sighting on " + TextManager.Instance.GetLocalizedEnemyName(closestValidTarget.GetComponent<DaggerfallEnemy>().MobileUnit.Enemy.ID));
            SetTarget(closestValidTarget);
            vectorCurrent = eye.transform.forward;
        }
    }

    void SetTarget(DaggerfallEntityBehaviour newTarget)
    {
        target = newTarget;
        targetController = target.GetComponent<CharacterController>();
        targetMobile = target.GetComponentInChildren<DaggerfallMobileUnit>(true);

        viewOffsetInput = Vector2.zero;
        float offsetY = 0.5f;
        switch (targetMobile.Summary.Enemy.ID)
        {
            case (int)MobileTypes.OrcShaman:
            case (int)MobileTypes.Lich:
            case (int)MobileTypes.AncientLich:
            case (int)MobileTypes.Mage:
            case (int)MobileTypes.Spellsword:
            case (int)MobileTypes.Healer:
                offsetY = 0.25f;
                break;
            case (int)MobileTypes.Spider:
            case (int)MobileTypes.Slaughterfish:
            case (int)MobileTypes.Lamia:
            case (int)MobileTypes.Wraith:
                offsetY = 0.125f;
                break;
            case (int)MobileTypes.Imp:
            case (int)MobileTypes.GiantScorpion:
                offsetY = 0f;
                break;
            case (int)MobileTypes.GiantBat:
            case (int)MobileTypes.Dragonling:
            case (int)MobileTypes.Dragonling_Alternate:
                offsetY = -0.125f;
                break;
            case (int)MobileTypes.Harpy:
                offsetY = -0.5f;
                break;
        }
        viewOffsetInput.y = offsetY;
    }
    void ClearTarget()
    {
        target = null;
        targetController = null;
        targetMobile = null;

        viewOffsetInput = Vector2.zero;
    }

    bool IsValidTarget(DaggerfallEntityBehaviour target)
    {
        if (target.Entity.IsMagicallyConcealed)
            return false;

        Vector3 targetVector = target.transform.position - eye.transform.position;

        float distance = targetVector.magnitude;
        if (distance > targetingRange)
            return false;

        float angle = Vector3.Angle(eye.transform.forward,targetVector.normalized);
        if (angle > targetingRadius)
            return false;

        RaycastHit hit;
        Ray ray = new Ray(eye.transform.position + ((-targetVector).normalized*2), targetVector);
        //if (Physics.SphereCast(ray, 2f, out hit, distance, obstacleLayerMask))
        if (Physics.Raycast(ray, out hit, distance, obstacleLayerMask))
        {
            if (hit.transform != target.transform)
                return false;
        }

            return true;
    }

    private KeyCode SetKeyFromText(string text)
    {
        Debug.Log("Setting Key");
        if (System.Enum.TryParse(text, out KeyCode result))
        {
            Debug.Log("Key set to " + activationKey.ToString());
            return result;
        }
        else
        {
            Debug.Log("Detected an invalid key code. Setting to default.");
            return activationKeyDefault;
        }
    }

    void QuickTurn(float yawTarget)
    {
        if (quickturning != null)
            return;

        ClearTarget();

        if (feedbackOnQuickturn)
            DaggerfallUI.Instance.PopupMessage("Quickturning");

        float duration = quickturnDuration * (Mathf.Abs(yawTarget) / 180);

        quickturning = QuickTurnCoroutine(yawTarget, duration);

        StartCoroutine(quickturning);
    }

    IEnumerator QuickTurnCoroutine(float target, float duration = 1)
    {
        float yawCurrent = mouseLook.Yaw;
        float yawTarget = mouseLook.Yaw + target;

        Debug.Log("Gyring from " + yawCurrent.ToString() + " to " + yawTarget.ToString());

        float time = 0;

        while (time < duration)
        {
            yawCurrent = Mathf.Lerp(yawCurrent, yawTarget, time / duration);

            mouseLook.SetFacing(yawCurrent, mouseLook.Pitch);

            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        quickturning = null;
    }

}
