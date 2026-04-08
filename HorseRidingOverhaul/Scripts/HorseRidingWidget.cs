using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;

public class HorseRidingWidget : MonoBehaviour
{
    bool initialized;

    bool paused;
    public bool Paused
    {
        get
        {
            return paused;
        }
        set
        {
            paused = value;
        }
    }

    public bool isEnabled {
        get
        {
            if ((speed || heading || sprint) && initialized && !paused)
                return true;
            else
                return false;
        }
    }

    public int scaleToScreen;   //0 = Do not scale, 1 = Scale to screen height, 2 = Scale to screen dimensions
    public float screenScaleX;
    public float screenScaleY;

    //speed indicator
    public bool speed;
    public Vector2 speedOffset;
    public float speedScale;
    public Color speedColor = Color.white;
    Texture2D[] speedTextures;
    Texture2D speedTextureCurrent;
    Rect speedRect;

    //heading indicator
    public bool heading;
    public Vector2 headingOffset;
    public float headingScale;
    public Color headingColor = Color.white;
    public int headingIntervalIndex;
    Texture2D[] headingTextures;
    Texture2D headingTextureCurrent;
    Rect headingRect;
    int[] headingIntervals = new int[] { 1, 2, 3, 5, 6, 9, 10, 15, 18, 30, 45, 90 };    //factors of 90
    int headingInterval
    {
        get { return headingIntervals[headingIntervalIndex]; }
    }
    int headingFrameCount
    {
        get
        {
            return (int)(360/headingInterval);
        }
    }

    //charge indicator
    public bool sprint;
    public Vector2 sprintOffset;
    public float sprintScale;
    public Color sprintColor = Color.white;
    Texture2D[] sprintTextures;
    Texture2D sprintTextureCurrent;
    Rect sprintRect;
    public int sprintFrameCount;

    // Start is called before the first frame update
    public void Initialize()
    {
        //initialize speed textures
        speedTextures = new Texture2D[6];
        int archive = 112389;
        int record = 0;
        int frame = 0;
        for (int i = 0; i < 6; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            speedTextures[i] = texture;
            frame++;
        }
        speedTextureCurrent = speedTextures[0];

        //initialize heading textures
        headingTextures = new Texture2D[headingFrameCount];
        archive = 112389;
        record = 1;
        frame = 0;
        for (int i = 0; i < headingFrameCount; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            headingTextures[i] = texture;
            frame++;
        }
        headingTextureCurrent = headingTextures[0];

        //initialize charge textures
        sprintTextures = new Texture2D[sprintFrameCount + 1];
        archive = 112389;
        if (HorseRidingOverhaul.Instance.galloping == 3)
            record = 2;
        else
            record = 3;
        frame = 0;
        for (int i = 0; i < sprintFrameCount + 1; i++)
        {
            Texture2D texture;
            DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.TryImportTexture(archive, record, frame, out texture);
            sprintTextures[i] = texture;
            frame++;
        }
        sprintTextureCurrent = sprintTextures[0];

        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (isEnabled)
        {
            if (speed)
            {
                if (HorseRidingOverhaul.Instance.customAudio)
                {
                    //update speed texture
                    //set if moving at speed, turning or strafing
                    //if (HorseRidingOverhaul.Instance.speedIndex != 1 || HorseRidingOverhaul.Instance.currentYaw != 0 || HorseRidingOverhaul.Instance.strafeVector.sqrMagnitude > 0)
                    if ((HorseRidingOverhaul.Instance.throttle == 1 && HorseRidingOverhaul.Instance.currentThrottle != 0)  || (HorseRidingOverhaul.Instance.throttle == 0 && HorseRidingOverhaul.Instance.speedIndex != 1))
                    {
                        if (HorseRidingOverhaul.Instance.currentGait == HorseRidingOverhaul.Instance.gait5)
                            speedTextureCurrent = speedTextures[5];
                        else if (HorseRidingOverhaul.Instance.currentGait == HorseRidingOverhaul.Instance.gait4)
                            speedTextureCurrent = speedTextures[4];
                        else if (HorseRidingOverhaul.Instance.currentGait == HorseRidingOverhaul.Instance.gait3)
                            speedTextureCurrent = speedTextures[3];
                        else if (HorseRidingOverhaul.Instance.currentGait == HorseRidingOverhaul.Instance.gait2)
                            speedTextureCurrent = speedTextures[2];
                        else if (HorseRidingOverhaul.Instance.currentGait == HorseRidingOverhaul.Instance.gait1)
                            speedTextureCurrent = speedTextures[1];
                    }
                    else
                        speedTextureCurrent = speedTextures[0];
                }
                else
                {
                    bool sneaking = GameManager.Instance.SpeedChanger.isSneaking;
                    bool running = GameManager.Instance.SpeedChanger.isRunning;
                    TransportModes mode = GameManager.Instance.TransportManager.TransportMode;

                    if (HorseRidingOverhaul.Instance.throttle == 1)
                    {
                        if (mode == TransportModes.Cart)
                        {
                            //Carts can't gallop or reverse
                            if (HorseRidingOverhaul.Instance.currentThrottle >= 1 && !sneaking)  //fast
                                speedTextureCurrent = speedTextures[3];
                            else if ((HorseRidingOverhaul.Instance.currentThrottle >= 1 && sneaking) || (HorseRidingOverhaul.Instance.currentThrottle >= 0.5f && !sneaking)) //fast+sneak or slow
                                speedTextureCurrent = speedTextures[2];
                            else if (HorseRidingOverhaul.Instance.currentThrottle >= 0.5f && sneaking)  //slow+sneak
                                speedTextureCurrent = speedTextures[1];
                            else if (HorseRidingOverhaul.Instance.currentThrottle == 0f)  //stopped
                                speedTextureCurrent = speedTextures[0];
                        }
                        else
                        {
                            if (HorseRidingOverhaul.Instance.currentThrottle >= 1 && running)    //fast+run
                                speedTextureCurrent = speedTextures[5];
                            else if ((HorseRidingOverhaul.Instance.currentThrottle >= 1 && !sneaking && !running) || (HorseRidingOverhaul.Instance.currentThrottle >= 0.5f && running)) //fast or slow+run
                                speedTextureCurrent = speedTextures[4];
                            else if ((HorseRidingOverhaul.Instance.currentThrottle >= 1 && sneaking) || (HorseRidingOverhaul.Instance.currentThrottle >= 0.5f && !sneaking && !running) || (HorseRidingOverhaul.Instance.currentThrottle < 0 && running))   //fast+sneak or slow or reverse+run
                                speedTextureCurrent = speedTextures[3];
                            else if ((HorseRidingOverhaul.Instance.currentThrottle >= 0.5f && sneaking) || (HorseRidingOverhaul.Instance.currentThrottle < 0 && !sneaking && !running))  //slow+sneak or reverse
                                speedTextureCurrent = speedTextures[2];
                            else if (HorseRidingOverhaul.Instance.currentThrottle < 0 && sneaking)  //reverse+sneak
                                speedTextureCurrent = speedTextures[1];
                            else
                                speedTextureCurrent = speedTextures[0];
                        }
                    }
                    else
                    {
                        if (mode == TransportModes.Cart)
                        {
                            //Carts can't gallop or reverse
                            if (HorseRidingOverhaul.Instance.speedIndex == 3 && !sneaking)  //fast
                                speedTextureCurrent = speedTextures[3];
                            else if ((HorseRidingOverhaul.Instance.speedIndex == 3 && sneaking) || (HorseRidingOverhaul.Instance.speedIndex == 2 && !sneaking)) //fast+sneak or slow
                                speedTextureCurrent = speedTextures[2];
                            else if (HorseRidingOverhaul.Instance.speedIndex == 2 && sneaking)  //slow+sneak
                                speedTextureCurrent = speedTextures[1];
                            else if (HorseRidingOverhaul.Instance.speedIndex == 1)  //stopped
                                speedTextureCurrent = speedTextures[0];
                        }
                        else
                        {
                            if (HorseRidingOverhaul.Instance.speedIndex == 3 && running)    //fast+run
                                speedTextureCurrent = speedTextures[5];
                            else if ((HorseRidingOverhaul.Instance.speedIndex == 3 && !sneaking && !running) || (HorseRidingOverhaul.Instance.speedIndex == 2 && running)) //slow+run or fast
                                speedTextureCurrent = speedTextures[4];
                            else if ((HorseRidingOverhaul.Instance.speedIndex == 3 && sneaking) || (HorseRidingOverhaul.Instance.speedIndex == 2 && !sneaking && !running) || (HorseRidingOverhaul.Instance.speedIndex == 0 && running)) //slow or fast+sneak or reverse+run
                                speedTextureCurrent = speedTextures[3];
                            else if (HorseRidingOverhaul.Instance.speedIndex == 2 && sneaking || (HorseRidingOverhaul.Instance.speedIndex == 0 && !sneaking)) //slow+sneak or reverse
                                speedTextureCurrent = speedTextures[2];
                            else if (HorseRidingOverhaul.Instance.speedIndex == 0 && sneaking)  //reverse+sneak
                                speedTextureCurrent = speedTextures[1];
                            else if (HorseRidingOverhaul.Instance.speedIndex == 1)  //stopped or strafing
                                speedTextureCurrent = speedTextures[0];
                        }
                    }
                }
            }

            if (heading)
            {
                //update heading texture
                int headingTextureIndex = 0;
                headingTextureIndex = (int)((360 - ((HorseRidingOverhaul.Instance.angleYaw / headingInterval) * headingInterval)) / headingInterval);
                if (HorseRidingOverhaul.Instance.angleYaw < 0)
                    headingTextureIndex = (int)(((-HorseRidingOverhaul.Instance.angleYaw / headingInterval) * headingInterval) / headingInterval);
                headingTextureIndex = Mathf.Clamp(headingTextureIndex, 0, headingFrameCount);
                if (headingTextureIndex == 0)
                    headingTextureIndex = headingFrameCount;
                headingTextureCurrent = headingTextures[headingFrameCount - headingTextureIndex];
            }

            if (sprint)
            {
                //update charge texture
                if (HorseRidingOverhaul.Instance.galloping == 3)
                    sprintTextureCurrent = sprintTextures[HorseRidingOverhaul.Instance.tokenCurrent];
                else if (HorseRidingOverhaul.Instance.galloping == 2)
                {
                    int chargeTextureIndex = Mathf.RoundToInt((sprintTextures.Length-1)*(HorseRidingOverhaul.Instance.staminaCurrent / HorseRidingOverhaul.Instance.staminaMax));
                    chargeTextureIndex = Mathf.Clamp(chargeTextureIndex, 0, sprintTextures.Length-1);
                    sprintTextureCurrent = sprintTextures[chargeTextureIndex];
                }
            }
        }
    }
    private void OnGUI()
    {
        if (!isEnabled || GameManager.IsGamePaused || InputManager.Instance.IsPaused || SaveLoadManager.Instance.LoadInProgress || !GameManager.Instance.PlayerMotor.IsRiding || HorseRidingOverhaul.Instance.isTravelling)
            return;

        GUI.depth = -1;

        if (scaleToScreen == 2) //Scale to whole screen
        {
            screenScaleY = (float)HorseRidingOverhaul.Instance.screenRect.height / HorseRidingOverhaul.Instance.nativeScreenHeight;
            screenScaleX = (float)HorseRidingOverhaul.Instance.screenRect.width / HorseRidingOverhaul.Instance.nativeScreenWidth;
        }
        else if (scaleToScreen == 1)    //Scale only to screen height, maintaining aspect ratio
        {
            screenScaleY = (float)HorseRidingOverhaul.Instance.screenRect.height / HorseRidingOverhaul.Instance.nativeScreenHeight;
            screenScaleX = screenScaleY;
        }
        else //Do not scale to screen
        {
            screenScaleY = 1;
            screenScaleX = 1;
        }

        // Allow texture to be offset when large HUD enabled
        // This is enabled by default to match classic but can be toggled for either docked/undocked large HUD
        float LargeHudOffset = 0;
        if (DaggerfallUI.Instance.DaggerfallHUD != null && DaggerfallUnity.Settings.LargeHUD && DaggerfallUnity.Settings.LargeHUDOffsetHorse)
            LargeHudOffset = (int)DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;


        //draw the indicator at a point away from the center to indicate the heading
        /*headingOffset = yawAngle * (screenRect.height * 0.1f);
        Vector2 yawAngle = RotateVector(Vector2.up, angleYaw);
        //draw heading indicator
        headingRect = new Rect(widgetPos - headingOffset, headingScale);
        DaggerfallUI.DrawTexture(headingRect, headingTexture);*/

        //rotate the heading indicator around the center
        /*float yawAngle = angleYaw;
        Matrix4x4 matrixBackup = GUI.matrix;
        Vector2 headingScale = new Vector2(headingTexture.width, headingTexture.height) * widgetScale;
        Vector2 headingOffset = new Vector2(headingScale.x, headingScale.y) * 0.5f;
        GUIUtility.RotateAroundPivot(yawAngle, widgetPos);
        //draw heading indicator
        headingRect = new Rect(widgetPos - headingOffset, headingScale);
        DaggerfallUI.DrawTexture(headingRect, headingTexture);
        GUI.matrix = matrixBackup;*/

        //animate the heading indicator to show the angle
        if (heading)
        {
            Vector2 headingPos = new Vector2(HorseRidingOverhaul.Instance.screenRect.x + (HorseRidingOverhaul.Instance.screenRect.width * headingOffset.x), HorseRidingOverhaul.Instance.screenRect.y + (HorseRidingOverhaul.Instance.screenRect.height * headingOffset.y) - LargeHudOffset);
            Vector2 headingTextureScale = new Vector2(headingTextureCurrent.width * screenScaleX, headingTextureCurrent.height * screenScaleY) * headingScale;
            Vector2 headingTextureOffset = new Vector2(headingTextureScale.x, headingTextureScale.y) * 0.5f;
            headingRect = new Rect(headingPos - headingTextureOffset, headingTextureScale);
            DaggerfallUI.DrawTexture(headingRect, headingTextureCurrent, ScaleMode.StretchToFill, false, headingColor);
        }

        //draw speed indicator
        if (speed)
        {
            Vector2 speedPos = new Vector2(HorseRidingOverhaul.Instance.screenRect.x + (HorseRidingOverhaul.Instance.screenRect.width * speedOffset.x), HorseRidingOverhaul.Instance.screenRect.y + (HorseRidingOverhaul.Instance.screenRect.height * speedOffset.y) - LargeHudOffset);
            Vector2 speedTextureScale = new Vector2(speedTextureCurrent.width * screenScaleX, speedTextureCurrent.height * screenScaleY) * speedScale;
            Vector2 speedTextureOffset = new Vector2(speedTextureScale.x, speedTextureScale.y) * 0.5f;
            speedRect = new Rect(speedPos - speedTextureOffset, speedTextureScale);
            DaggerfallUI.DrawTexture(speedRect, speedTextureCurrent,ScaleMode.StretchToFill,false,speedColor);
        }

        if (sprint && GameManager.Instance.TransportManager.TransportMode == TransportModes.Horse && HorseRidingOverhaul.Instance.ShowCharges)
        {
            Vector2 chargePos = new Vector2(HorseRidingOverhaul.Instance.screenRect.x + (HorseRidingOverhaul.Instance.screenRect.width * sprintOffset.x), HorseRidingOverhaul.Instance.screenRect.y + (HorseRidingOverhaul.Instance.screenRect.height * sprintOffset.y) - LargeHudOffset);
            Vector2 chargeTextureScale = new Vector2(sprintTextureCurrent.width * screenScaleX, sprintTextureCurrent.height * screenScaleY) * sprintScale;
            Vector2 chargeTextureOffset = new Vector2(chargeTextureScale.x, chargeTextureScale.y) * 0.5f;
            sprintRect = new Rect(chargePos - chargeTextureOffset, chargeTextureScale);
            DaggerfallUI.DrawTexture(sprintRect, sprintTextureCurrent, ScaleMode.StretchToFill, false, sprintColor);
        }
    }
}
