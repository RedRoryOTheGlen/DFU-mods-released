using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.UserInterface;

public class GyreAndGimble : MonoBehaviour
{
    Camera eye;
    Transform body;
    PlayerMotor playerMotor;
    PlayerMouseLook mouseLook;
    HeadBobber headBobber;
    Camera skyCamera;

    Vector2 cursorPosition;

    bool cursorMovement;
    KeyCode cursorMovementKeyCode = KeyCode.Mouse0;
    bool cursorMovementPressed;

    Rect cursorMovementRect;
    Texture2D cursorMovementTexture;
    Texture2D[] cursorMovementTextures;
    int frameCurrent;

    bool cursorSoftware;
    Rect cursorSoftwareRect;
    Texture2D cursorSoftwareTexture;
    Vector2 cursorSoftwareSize;

    Vector2 viewVector;
    Vector2 moveVector;
    float factorX = 1;
    float factorY = 1;

    bool freelook;
    bool freelookActive;
    bool freelooked;
    bool freelookAutoReset;
    Transform freelookPivot;
    Vector2 lookCurrent;
    Vector2 lookTarget;
    float Pitch;
    float Yaw;
    KeyCode freelookKeyCode = KeyCode.X;
    KeyCode freelookResetKeyCode = KeyCode.Mouse0;

    bool cameraMovement;

    bool cameraUnlocked;

    const int nativeScreenWidth = 320;
    const int nativeScreenHeight = 200;
    Rect screenRect;
    float weaponScaleX;
    float weaponScaleY;
    float weaponOffsetHeight;

    bool autopitch;
    float autopitchSpeed = 0.5f;
    float autopitchStrength = 0.5f;
    float autopitchOffset = 0;
    public LayerMask terrainLayerMask;

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<GyreAndGimble>();
    }

    void Awake()
    {

        eye = GameManager.Instance.MainCamera;

        body = GameManager.Instance.PlayerObject.transform;
        playerMotor = GameManager.Instance.PlayerMotor;
        mouseLook = GameManager.Instance.PlayerMouseLook;
        headBobber = GameManager.Instance.PlayerObject.GetComponent<HeadBobber>();
        skyCamera = GameManager.Instance.SkyRig.SkyCamera;
        terrainLayerMask |= (1 << LayerMask.NameToLayer("Default"));

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        InitializeTextures();

        InitializeFreelook();

        mod.IsReady = true;
    }

    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("Modules"))
        {
            cursorMovement = settings.GetValue<bool>("Modules", "CursorMovement");
            cursorSoftware = settings.GetValue<bool>("Modules", "SoftwareCursor");
            freelook = settings.GetValue<bool>("Modules", "FreeLook");
            cameraMovement = settings.GetValue<bool>("Modules", "CursorModeCameraControls");
            cameraUnlocked = settings.GetValue<bool>("Modules", "UnlockedVanillaAttackCamera");
            autopitch = settings.GetValue<bool>("Modules", "Auto-pitch");
        }
        if (change.HasChanged("FreeLook"))
        {
            freelookKeyCode = SetKeyFromText(settings.GetString("FreeLook", "ActivationInput"));
            freelookResetKeyCode = SetKeyFromText(settings.GetString("FreeLook", "ResetInput"));
            freelookAutoReset = settings.GetValue<bool>("FreeLook", "AutoReset");
        }
        if (change.HasChanged("CursorMovement"))
        {
            cursorMovementKeyCode = SetKeyFromText(settings.GetString("CursorMovement", "MovementInput"));
        }
        if (change.HasChanged("Autopitch"))
        {
            autopitchSpeed = settings.GetValue<float>("Autopitch", "Speed") * 100;
            autopitchStrength = settings.GetValue<float>("Autopitch", "Strength");
            autopitchOffset = settings.GetValue<float>("Autopitch", "Offset");
        }
    }

    void InitializeTextures()
    {
        cursorMovementTextures = new Texture2D[9];
        int archive = 112394;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 9; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            cursorMovementTextures[i] = texture;
            frame++;
        }

        frameCurrent = 4;
        cursorMovementTexture = cursorMovementTextures[frameCurrent];

        cursorSoftwareTexture = DaggerfallUI.GetTextureFromResources("Cursor2");
        cursorSoftwareSize = new Vector2(32,32);

        UpdateCursor();
    }
    void InitializeFreelook()
    {
        freelookPivot = Instantiate(new GameObject("FreelookPivot")).transform;
        freelookPivot.SetParent(eye.transform.parent);
        freelookPivot.localPosition = Vector3.zero;
        eye.transform.SetParent(freelookPivot);
    }
    private void OnGUI()
    {
        if (cursorMovement && !(!GameManager.Instance.PlayerMouseLook.cursorActive || GameManager.IsGamePaused || freelookActive || !IsCursorInView()))
        {
            //Draw movement cursor
            // Do nothing if player has cursor active over large HUD (player is clicking on HUD not clicking to attack)

            GUI.depth = 0;

            DaggerfallUI.DrawTexture(cursorMovementRect, cursorMovementTexture);
        }
        else if (cursorSoftware && (GameManager.Instance.PlayerMouseLook.cursorActive || GameManager.IsGamePaused) || !IsCursorInView())
        {
            //Draw clicky cursor
            GUI.depth = -1;

            cursorSoftwareRect = new Rect(new Vector2(cursorPosition.x, Screen.height - cursorPosition.y), cursorSoftwareSize);
            DaggerfallUI.DrawTexture(cursorSoftwareRect, cursorSoftwareTexture);
        }
    }

    private void Update()
    {
        UpdateCursor();

        if (!GameManager.Instance.PlayerMouseLook.cursorActive)
        {
            viewVector = new Vector2(mouseLook.Yaw, 0);
            return;
        }

        //let me use turn and pitch inputs while cursor is active
        if (cameraMovement && !freelookActive)
        {
            if (InputManager.Instance.HasAction(InputManager.Actions.TurnLeft))
                viewVector.x -= mouseLook.sensitivityScale;

            if (InputManager.Instance.HasAction(InputManager.Actions.TurnRight))
                viewVector.x += mouseLook.sensitivityScale;

            if (InputManager.Instance.HasAction(InputManager.Actions.LookUp))
                viewVector.y -= mouseLook.sensitivityScale;

            if (InputManager.Instance.HasAction(InputManager.Actions.LookDown))
                viewVector.y += mouseLook.sensitivityScale;
        }

        if (cursorMovement && !freelookActive)
        {
            // Do nothing if player has cursor active over large HUD (player is clicking on HUD not clicking to attack)
            if (!GameManager.IsGamePaused)
            {
                UpdateMovementCursor();

                if (InputManager.Instance.GetKeyDown(cursorMovementKeyCode) && IsCursorInView())
                    cursorMovementPressed = true;

                if (InputManager.Instance.GetKeyUp(cursorMovementKeyCode))
                    cursorMovementPressed = false;

                if (cursorMovementPressed)
                    CheckCursor();
            }

            //suppress PlayerActivate if LMB is held down and cursor is on movement
            if (InputManager.Instance.GetKey(cursorMovementKeyCode) && frameCurrent != 4)
            {
                if (GameManager.Instance.PlayerActivate.enabled)
                    GameManager.Instance.PlayerActivate.enabled = false;
            }
            else if (InputManager.Instance.GetKeyUp(cursorMovementKeyCode))
            {
                if (!GameManager.Instance.PlayerActivate.enabled)
                    GameManager.Instance.PlayerActivate.enabled = true;
            }
        }

        if (autopitch && !freelooked)
        {
            AdjustPitch();
        }

        mouseLook.SetFacing(viewVector.x, viewVector.y);
        if (!freelookActive)
        {
            body.transform.localEulerAngles = new Vector3(0, viewVector.x, 0);
            eye.transform.localEulerAngles = new Vector3(viewVector.y, 0, 0);
        }
    }

    private void LateUpdate()
    {
        if (cursorSoftware && (GameManager.Instance.PlayerMouseLook.cursorActive || GameManager.IsGamePaused) || !IsCursorInView())
        {
            //hide regular cursor
            Cursor.visible = false;
        }

        if (GameManager.Instance.PlayerMouseLook.cursorActive)
        {
            if (cursorMovement && !freelookActive)
            {
                bool cursorInView = IsCursorInView();
                // Do nothing if player has cursor active over large HUD (player is clicking on HUD not clicking to attack)
                if (GameManager.IsGamePaused || !cursorInView)
                {
                    if (!InputManager.Instance.CursorVisible)
                        InputManager.Instance.CursorVisible = true;
                }
                else
                {
                    if (InputManager.Instance.CursorVisible)
                        InputManager.Instance.CursorVisible = false;
                }
            }
        }
        else
        {
            if (cameraUnlocked)
            {
                if (InputManager.Instance.ActionStarted(InputManager.Actions.SwingWeapon))
                {
                    lookCurrent.y = -mouseLook.Pitch;
                    lookCurrent.x = mouseLook.Yaw;

                    lookTarget = lookCurrent;

                    Pitch = -lookCurrent.y;
                    Yaw = lookCurrent.x;
                }

                if (InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon))
                {
                    ApplyLook();

                    // If there's a character body that acts as a parent to the camera
                    if (body)
                    {
                        eye.transform.localEulerAngles = new Vector3(Pitch, 0, 0);
                        body.transform.localEulerAngles = new Vector3(0, Yaw, 0);
                    }
                    else
                    {
                        eye.transform.localEulerAngles = new Vector3(Pitch, Yaw, 0);
                    }

                    mouseLook.SetFacing(lookTarget);
                }
            }
        }

        if (freelook)
        {
            //Debug.Log(eye.transform.localEulerAngles.ToString());
            if (!freelookActive)
            {
                if (InputManager.Instance.GetKeyDown(freelookKeyCode))
                {
                    lookCurrent.x = freelookPivot.eulerAngles.y;
                    lookCurrent.y = -eye.transform.localEulerAngles.x;
                    if (eye.transform.localEulerAngles.x > 90)
                        lookCurrent.y = 360 - eye.transform.localEulerAngles.x;

                    lookTarget = lookCurrent;

                    //Activate Freelook
                    freelookActive = true;
                    freelooked = true;
                    mouseLook.enabled = false;
                    headBobber.enabled = false;
                }
            }
            else
            {
                if (InputManager.Instance.CursorVisible)
                    InputManager.Instance.CursorVisible = false;

                if (Cursor.lockState != CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.Locked;

                ApplyLook();

                mouseLook.SetFacing(mouseLook.Yaw, Pitch);

                eye.transform.localEulerAngles = new Vector3(Pitch, 0, 0);
                freelookPivot.transform.eulerAngles = body.TransformVector(new Vector3(0, Yaw, 0));

                if (!freelookAutoReset && InputManager.Instance.GetKeyUp(freelookResetKeyCode))
                {
                    //reset angle
                    lookCurrent.y = -mouseLook.Pitch;
                    lookCurrent.x = mouseLook.Yaw;
                    lookTarget = lookCurrent;
                    Pitch = -lookCurrent.y;
                    Yaw = lookCurrent.x;
                    freelookPivot.localEulerAngles = Vector3.zero;
                    viewVector.y = 0;
                    mouseLook.SetFacing(mouseLook.Yaw, 0);
                    mouseLook.enabled = true;
                    headBobber.enabled = DaggerfallUnity.Settings.HeadBobbing;
                    freelookActive = false;
                    freelooked = false;
                    cursorPosition = InputManager.Instance.MousePosition;
                    Cursor.lockState = CursorLockMode.None;
                }

                if (InputManager.Instance.GetKeyUp(freelookKeyCode))
                {
                    if (freelookAutoReset)
                    {
                        //reset angle
                        lookCurrent.y = 0;
                        lookCurrent.x = mouseLook.Yaw;
                        lookTarget = lookCurrent;
                        Pitch = -lookCurrent.y;
                        Yaw = lookCurrent.x;
                        freelookPivot.localEulerAngles = Vector3.zero;
                        viewVector.y = 0;
                        mouseLook.SetFacing(mouseLook.Yaw, 0);
                        mouseLook.enabled = true;
                        headBobber.enabled = DaggerfallUnity.Settings.HeadBobbing;
                        freelookActive = false;
                        freelooked = false;
                        cursorPosition = InputManager.Instance.MousePosition;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        //disable freelook
                        viewVector.y = Pitch;
                        mouseLook.SetFacing(mouseLook.Yaw, Pitch);
                        mouseLook.enabled = true;
                        freelookActive = false;
                    }
                }
            }
        }

    }

    void AdjustPitch()
    {
        if (!playerMotor.IsGrounded)
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
        float angle = (Vector3.Angle(body.transform.forward, pointDir) - autopitchOffset) * autopitchStrength;
        float pitchTarget = angle;
        if (pointDir.y > 0)
            pitchTarget = -angle;

        float buffer = 1;

        if (viewVector.y < pitchTarget-buffer)
            viewVector.y += autopitchSpeed * Time.deltaTime;
        else if (viewVector.y > pitchTarget+buffer)
            viewVector.y -= autopitchSpeed * Time.deltaTime;

        /*if (!mouseLook.cursorActive)
            mouseLook.SetFacing(mouseLook.Yaw + InputManager.Instance.LookX, pitchCurrent);
        else
            eye.transform.localEulerAngles = new Vector3(pitchCurrent, 0, 0);*/
    }

    // Applies scaled raw mouse deltas to lookTarget, then calls ApplySmoothing method to update lookCurrent
    void ApplyLook()
    {
        // Scale sensitivity
        float sensitivityX = 1.0f;
        float sensitivityY = 1.0f;

        if (InputManager.Instance.UsingController)
        {
            // Make sure it keeps consistent speed regardless of framerate
            // Speed = speed * 60 frames / (1 / unscaledDeltaTime) or speed * 60 * unscaledDeltaTime
            // 60 frames -> speed * 60 / 60 = speed * 1.0
            // 30 frames -> speed * 60 / 30 = speed * 2.0
            // 120 frames -> speed * 60 / 120 = speed * 0.5
            sensitivityX = mouseLook.sensitivity.x * mouseLook.joystickSensitivityScale * 60f * Time.unscaledDeltaTime;
            sensitivityY = mouseLook.sensitivity.y * mouseLook.joystickSensitivityScale * 60f * Time.unscaledDeltaTime;
        }
        else
        {
            sensitivityX = mouseLook.sensitivity.x * mouseLook.sensitivityScale;
            sensitivityY = mouseLook.sensitivity.y * mouseLook.sensitivityScale;
        }

        Vector2 rawMouseDelta = new Vector2(InputManager.Instance.LookX, InputManager.Instance.LookY);

        lookTarget += Vector2.Scale(rawMouseDelta, new Vector2(sensitivityX, sensitivityY * (mouseLook.invertMouseY ? -1 : 1)));

        float range = 360.0f;

        if (lookTarget.x < 0.0f || lookTarget.x >= range) // Wrap look yaws to range 0..<360
        {
            float delta = Mathf.Floor(lookTarget.x / range) * range;
            lookTarget.x -= delta;
            lookCurrent.x -= delta;
        }

        // Clamp target look pitch to range of straight down to straight up
        lookTarget.y = Mathf.Clamp(lookTarget.y, mouseLook.PitchMinLimit, mouseLook.PitchMaxLimit);

        ApplySmoothing();

        Yaw = lookCurrent.x;
        Pitch = -lookCurrent.y;
    }

    // Updates lookCurrent by moving it a fraction towards lookTarget
    // If smoothing is 0.0 (off) then lookCurrent will be set to lookTarget with no intermediates
    void ApplySmoothing()
    {
        float smoothing = mouseLook.Smoothing;

        // Enforce some minimum smoothing for controllers (if you like)
        if (InputManager.Instance.UsingController && smoothing < 0.5f)
            smoothing = 0.5f;

        // Scale for FPS
        smoothing = 1.0f - GetFrameRateScaledFractionOfProgression(1.0f - smoothing);

        // Move lookCurrent a fraction towards lookTarget (weighted average formula)
        lookCurrent = lookCurrent * smoothing + lookTarget * (1.0f - smoothing);
    }

    // Scales fractional progression (non-linear) to frame rate
    private float GetFrameRateScaledFractionOfProgression(float fractionAt60FPS)
    {
        float frames = Time.unscaledDeltaTime * 60f; // Number of frames to handle this tick, can be partial
        float c = (1.0f - fractionAt60FPS) / fractionAt60FPS;
        return 1.0f - c / (frames + c);
    }

    bool IsCursorInView()
    {
        if (DaggerfallUI.Instance.DaggerfallHUD != null && DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ActiveMouseOverLargeHUD)
            return false;

        if (InputManager.Instance.MousePosition.x < screenRect.x || InputManager.Instance.MousePosition.x > screenRect.x + screenRect.width)
            return false;

        if (InputManager.Instance.MousePosition.y < screenRect.y || InputManager.Instance.MousePosition.y > screenRect.y + screenRect.height)
            return false;

        return true;
    }

    void UpdateCursor()
    {
        cursorPosition = InputManager.Instance.MousePosition;

        if (DaggerfallUI.Instance.CustomScreenRect != null)
            screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
        else
            screenRect = new Rect(0, 0, Screen.width, Screen.height);

        weaponOffsetHeight = 0;
        if (DaggerfallUI.Instance.DaggerfallHUD != null &&
            DaggerfallUnity.Settings.LargeHUD &&
            (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
        {
            weaponOffsetHeight = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
        }

        weaponScaleX = (float)screenRect.width / (float)nativeScreenWidth;
        weaponScaleY = (float)screenRect.height / (float)nativeScreenHeight;
    }

    void UpdateMovementCursor()
    {
        if (cursorPosition.x < Screen.width * 0.33f)
        {
            if (cursorPosition.y < (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.33f))          //BOTTOM LEFT (STRAFE LEFT)
                frameCurrent = 6;
            else if (cursorPosition.y > (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.67f))    //TOP LEFT (MOVE FORWARDS LEFT)
                frameCurrent = 0;
            else                                                                   //MID LEFT (TURN LEFT)
                frameCurrent = 3;
        }
        else if (cursorPosition.x > Screen.width * 0.67f)
        {
            if (cursorPosition.y < (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.33f))        //BOTTOM RIGHT (STRAFE RIGHT)
                frameCurrent = 8;
            else if (cursorPosition.y > (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.67f))    //TOP RIGHT (MOVE FORWARDS RIGHT)
                frameCurrent = 2;
            else                                                                    //MID RIGHT (TURN RIGHT)
                frameCurrent = 5;
        }
        else
        {
            if (cursorPosition.y < (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.33f))        //BOTTOM CENTER (MOVE BACKWARDS)
                frameCurrent = 7;
            else if (cursorPosition.y > (weaponOffsetHeight * 0.5f) + (screenRect.height * 0.67f))    //TOP CENTER (MOVE FORWARDS)
                frameCurrent = 1;
            else
                frameCurrent = 4;
        }

        cursorMovementTexture = cursorMovementTextures[frameCurrent];

        float width = cursorMovementTexture.width * weaponScaleX;
        float height = cursorMovementTexture.height * weaponScaleY;

        cursorMovementRect = new Rect(cursorPosition.x - (width * 0.5f), Screen.height - cursorPosition.y - (height * 0.5f), width, height);
    }

    void CheckCursor()
    {
        float viewSpeed = mouseLook.sensitivityScale;
        float moveSpeed = 1f;

        moveVector = Vector2.zero;
        factorX = 1;
        factorY = 1;

        if (frameCurrent == 6)  //BOTTOM LEFT (STRAFE LEFT)
        {
            factorX = 1 - Mathf.Clamp01((cursorPosition.x - screenRect.x) / (screenRect.width * 0.33f));
            moveVector.x = -moveSpeed;

            //Get cursor distance to bottem left corner
        }
        else if (frameCurrent == 0) //TOP LEFT (MOVE FORWARDS LEFT)
        {
            factorX = 1 - Mathf.Clamp01((cursorPosition.x - screenRect.x) / (screenRect.width * 0.33f));
            factorY = Mathf.Clamp01((cursorPosition.y - (weaponOffsetHeight * 0.5f) - (screenRect.height * 0.67f)) / ((screenRect.height * 0.33f) - (weaponOffsetHeight * 0.5f)));
            viewVector.x -= viewSpeed * factorX;
            moveVector.y = moveSpeed;
        }
        else if (frameCurrent == 3) //MID LEFT (TURN LEFT)
        {
            factorX = 1 - Mathf.Clamp01((cursorPosition.x - screenRect.x) / (screenRect.width * 0.33f));
            viewVector.x -= viewSpeed * factorX;
        }
        else if (frameCurrent == 8) //BOTTOM RIGHT (STRAFE RIGHT)
        {
            factorX = Mathf.Clamp01((cursorPosition.x - screenRect.x - (screenRect.width * 0.67f)) / (screenRect.width * 0.33f));
            moveVector.x = moveSpeed;
        }
        else if (frameCurrent == 2) //TOP RIGHT (MOVE FORWARDS RIGHT)
        {
            factorX = Mathf.Clamp01((cursorPosition.x - screenRect.x - (screenRect.width * 0.67f)) / (screenRect.width * 0.33f));
            factorY = Mathf.Clamp01((cursorPosition.y - (weaponOffsetHeight * 0.5f) - (screenRect.height * 0.67f)) / ((screenRect.height * 0.33f) - (weaponOffsetHeight * 0.5f)));
            viewVector.x += viewSpeed * factorX;
            moveVector.y = moveSpeed;
        }
        else if (frameCurrent == 5) //MID RIGHT (TURN RIGHT)
        {
            factorX = Mathf.Clamp01((cursorPosition.x - screenRect.x - (screenRect.width * 0.67f)) / (screenRect.width * 0.33f));
            viewVector.x += viewSpeed * factorX;
        }
        else if (frameCurrent == 7) //BOTTOM CENTER (MOVE BACKWARDS)
        {
            factorY = 1 - Mathf.Clamp01((cursorPosition.y - weaponOffsetHeight) / (screenRect.height * 0.33f));
            moveVector.y = -moveSpeed;
        }
        else if (frameCurrent == 1)    //TOP CENTER (MOVE FORWARDS)
        {
            factorY = Mathf.Clamp01((cursorPosition.y - (weaponOffsetHeight * 0.5f) - (screenRect.height * 0.67f)) / ((screenRect.height * 0.33f) - (weaponOffsetHeight * 0.5f)));
            moveVector.y = moveSpeed;
        }

        if (moveVector != Vector2.zero)
        {
            InputManager.Instance.ApplyHorizontalForce(moveVector.x * Mathf.Sin(factorX * Mathf.PI * 0.5f));
            InputManager.Instance.ApplyVerticalForce(moveVector.y * Mathf.Sin(factorY * Mathf.PI * 0.5f));
        }
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
