using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using UnityEngine;
using static DaggerfallWorkshop.DaggerfallLocation;

namespace WorldwalkingMod
{
    public class MapWindowPanel : Panel
    {
        public static MapWindowPanel Instance;

        public int nativeScreenWidth = 320;
        public int nativeScreenHeight = 200;
        public Rect screenRect;
        public float screenScaleX;
        public float screenScaleY;

        Panel mainPanel = new Panel();
        Panel vitalsPanel = new Panel();
        MapVitals mapVitals = new MapVitals();

        Panel speedPanel = new Panel();
        Panel speedCampPanel = new Panel();
        Panel speedCampButton = new Button();
        Panel speedCautiousPanel = new Panel();
        Panel speedCautiousButton = new Button();
        Panel speedRecklessPanel = new Panel();
        Panel speedRecklessButton = new Button();

        Panel timePanel = new Panel();
        TextLabel timeLabel = new TextLabel();
        Panel timeScale0Panel = new Panel();
        Panel timeScale0Button = new Button();
        Panel timeScale1Panel = new Panel();
        Panel timeScale1Button = new Button();
        Panel timeScale2Panel = new Panel();
        Panel timeScale2Button = new Button();
        Panel timeScale3Panel = new Panel();
        Panel timeScale3Button = new Button();
        Panel timeScale4Panel = new Panel();
        Panel timeScale4Button = new Button();
        Panel timeScale5Panel = new Panel();
        Panel timeScale5Button = new Button();

        Panel actionsPanel = new Panel();
        Panel actionsFindButton = new Button();
        Panel actionsImAtButton = new Button();
        Panel actionsShipButton = new Button();

        Panel pinPanel = new Panel();
        Panel pinSetButton = new Button();
        Panel pinGoToButton = new Button();
        Panel pinTravelButton = new Button();
        Panel pinClearButton = new Button();

        Panel exitButton = new Panel();

        Panel navigationPanel = new Panel();
        Panel navigationMoveForwardButton = new Button();
        Panel navigationMoveBackwardButton = new Button();
        Panel navigationMoveLeftButton = new Button();
        Panel navigationMoveRightButton = new Button();
        Panel navigationOrbitLeftButton = new Button();
        Panel navigationOrbitRightButton = new Button();
        Panel navigationZoomInButton = new Button();
        Panel navigationZoomOutButton = new Button();
        Panel navigationTargetButton = new Button();

        Panel playerMarker = new Panel();

        TextLabel toolTip = new TextLabel();

        public MapPopupText logLabel = new MapPopupText();

        //GUI Textures
        Texture2D texturePanelVitals;
        Texture2D textureBarVitalsHealth;
        Texture2D textureBarVitalsFatigue;
        Texture2D textureBarVitalsMagicka;

        Texture2D texturePanelSpeed;
        Texture2D textureButtonSpeedCamp;
        Texture2D textureButtonSpeedCautious;
        Texture2D textureButtonSpeedReckless;

        Texture2D texturePanelTime;
        Texture2D textureButtonTimeScale0;
        Texture2D textureButtonTimeScale1;
        Texture2D textureButtonTimeScale2;
        Texture2D textureButtonTimeScale3;
        Texture2D textureButtonTimeScale4;
        Texture2D textureButtonTimeScale5;

        Texture2D texturePanelButtons;

        Texture2D texturePanelPins;

        Texture2D textureButtonExit;

        Texture2D texturePanelNavigation;

        public Texture2D texturePlayerHead;
        public bool showPlayerMarker;

        public bool Started = false; //will be false until first game load or character creation is complete

        public MapWindowPanel() : base()
        {
            Instance = this;
            Enabled = false; //The overlay will be disabled until game finishes starting/loading.
        }

        public bool MouseOverGUI(int clicked = 0)
        {
            if (navigationPanel.Enabled)
            {
                if (clicked == 2)
                {
                    //reset press stuff here
                    Worldwalking.Instance.GUIMoveVerticalTarget = 0;
                    Worldwalking.Instance.GUIMoveHorizontalTarget = 0;
                    Worldwalking.Instance.GUIOrbitTarget = 0;
                    Worldwalking.Instance.GUIZoomTarget = 0;
                }

                if (navigationPanel.MouseOverComponent)
                {
                    //check for button rects here on mouse button down
                    if (clicked == 1)
                    {
                        if (navigationTargetButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.SetTargetPosition(Mathf.RoundToInt(Worldwalking.Instance.CameraBaseObject.transform.localPosition.x), Mathf.RoundToInt(-Worldwalking.Instance.CameraBaseObject.transform.localPosition.z));
                            //always reset to default timescale
                            Worldwalking.Instance.SetTimeScale(Worldwalking.Instance.defaultTimeScaleTravel);
                            if (Worldwalking.Instance.travelSpeed == 0)
                            {
                                Worldwalking.Instance.SetTravelSpeed(1);
                            }
                            Worldwalking.Instance.automatic = true;
                        }
                        if (navigationMoveForwardButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIMoveVerticalTarget = 1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationMoveBackwardButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIMoveVerticalTarget = -1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationMoveRightButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIMoveHorizontalTarget = 1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationMoveLeftButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIMoveHorizontalTarget = -1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationOrbitRightButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIOrbitTarget = 1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationOrbitLeftButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIOrbitTarget = -1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationZoomInButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIZoomTarget = 1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                        if (navigationZoomOutButton.MouseOverComponent)
                        {
                            Worldwalking.Instance.GUIZoomTarget = -1;
                            Worldwalking.Instance.PlayButtonClick();
                        }
                    }

                    //check for button rects here on mouse button up
                    if (clicked == 2)
                    {

                    }
                    return true;
                }
                else
                {
                }
            }

            if (vitalsPanel.MouseOverComponent)
                return true;

            if (speedPanel.MouseOverComponent)
            {
                if (clicked == 2)
                {
                    if (speedCampButton.MouseOverComponent)
                        ClickButton_SetTravelSpeed(0);
                    else if (speedCautiousButton.MouseOverComponent)
                        ClickButton_SetTravelSpeed(1);
                    else if (speedRecklessButton.MouseOverComponent)
                        ClickButton_SetTravelSpeed(2);
                }
                return true;
            }

            if (timePanel.MouseOverComponent)
            {
                if (clicked == 2)
                {
                    if (timeScale0Button.MouseOverComponent)
                        ClickButton_SetTimeScale(0);
                    else if (timeScale1Button.MouseOverComponent)
                        ClickButton_SetTimeScale(1);
                    else if (timeScale2Button.MouseOverComponent)
                        ClickButton_SetTimeScale(2);
                    else if (timeScale3Button.MouseOverComponent)
                        ClickButton_SetTimeScale(3);
                    else if (timeScale4Button.MouseOverComponent)
                        ClickButton_SetTimeScale(4);
                    else if (timeScale5Button.MouseOverComponent)
                        ClickButton_SetTimeScale(5);
                }
                return true;
            }

            if (actionsPanel.MouseOverComponent)
            {
                if (clicked == 2)
                {
                    if (actionsFindButton.MouseOverComponent)
                        ClickButton_FindLocation();
                    else if (actionsImAtButton.MouseOverComponent)
                        ClickButton_ImAt();
                    else if (actionsShipButton.MouseOverComponent)
                        ClickButton_Ship();
                }
                return true;
            }

            if (pinPanel.MouseOverComponent)
            {
                if (clicked == 2)
                {
                    if (pinSetButton.MouseOverComponent)
                        ClickButton_SetPin();
                    else if (pinGoToButton.MouseOverComponent)
                        ClickButton_GoToPin();
                    else if (pinTravelButton.MouseOverComponent)
                        ClickButton_TravelToPin();
                    else if (pinClearButton.MouseOverComponent)
                        ClickButton_ClearPin();
                }
                return true;
            }

            if (exitButton.MouseOverComponent)
            {
                if (clicked == 2)
                {
                    ClickButton_Exit();
                }
                return true;
            }

            return false;
        }

        void ClickButton_SetTravelSpeed(int index)
        {
            //if index == 0
            if (index == 0)
            {
                if (Worldwalking.Instance.TryCamp())
                {
                    Worldwalking.Instance.SetTravelSpeed(index);
                    Worldwalking.Instance.automatic = true;
                    Worldwalking.Instance.PlayButtonClick();
                }
            }
            else
            {
                if (Worldwalking.Instance.travelSpeed == 0)
                    Worldwalking.Instance.automatic = true;
                Worldwalking.Instance.SetTravelSpeed(index);
                Worldwalking.Instance.PlayButtonClick();
            }
        }

        void ClickButton_SetTimeScale(int index)
        {
            Worldwalking.Instance.SetTimeScale(index);
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_FindLocation()
        {
            ClickButtonFindLocation();
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_ImAt()
        {
            Worldwalking.Instance.SetCameraTargetPlayer();
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_Ship()
        {
            Worldwalking.Instance.TryShip();
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_SetPin()
        {
            if (Worldwalking.Instance.TargetObject.activeSelf)
                Worldwalking.Instance.SetPinPosition((int)Worldwalking.Instance.TargetObject.transform.localPosition.x, (int)-Worldwalking.Instance.TargetObject.transform.localPosition.z);
            else
                Worldwalking.Instance.SetPinPosition((int)Worldwalking.Instance.PlayerObject.transform.localPosition.x, (int)-Worldwalking.Instance.PlayerObject.transform.localPosition.z);
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_GoToPin()
        {
            if (Worldwalking.Instance.PinPosition.sqrMagnitude > 0)
            {
                Worldwalking.Instance.SetCameraPosition((int)Worldwalking.Instance.PinPosition.x, (int)-Worldwalking.Instance.PinPosition.z);
            }
            else
                logLabel.AddText("No pinned position");
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_TravelToPin()
        {
            if (Worldwalking.Instance.PinPosition.sqrMagnitude > 0)
            {
                logLabel.AddText("Travelling to pinned position");
                Worldwalking.Instance.SetTargetPosition((int)Worldwalking.Instance.PinPosition.x, (int)-Worldwalking.Instance.PinPosition.z);
                if (Worldwalking.Instance.timeScaleIndex == 0)
                    Worldwalking.Instance.SetTimeScale(Worldwalking.Instance.defaultTimeScaleTravel);
                if (Worldwalking.Instance.travelSpeed == 0)
                    Worldwalking.Instance.SetTravelSpeed(1);
                Worldwalking.Instance.automatic = true;
                Worldwalking.Instance.locked = true;
            }
            else
                logLabel.AddText("No pinned position");
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_ClearPin()
        {
            if (Worldwalking.Instance.PinPosition.sqrMagnitude > 0)
            {
                Worldwalking.Instance.ClearPinPosition();
                Worldwalking.Instance.SetTimeScale(0);
            }
            else
                logLabel.AddText("No pinned position");
            Worldwalking.Instance.PlayButtonClick();
        }

        void ClickButton_Exit()
        {
            Debug.Log("WORLDWALKING - Clicked the exit button");
            Worldwalking.Instance.StopShowingMap();
            Worldwalking.Instance.PlayButtonClick();
        }

        public void Setup()
        {
            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (Worldwalking.Instance.scaleToScreen == 2) //Scale to whole screen
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = (float)screenRect.width / nativeScreenWidth;
            }
            else if (Worldwalking.Instance.scaleToScreen == 1)    //Scale only to screen height, maintaining aspect ratio
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = screenScaleY;
            }
            else //Do not scale to screen
            {
                screenScaleY = 1;
                screenScaleX = 1;
            }

            screenScaleY *= Worldwalking.Instance.interfaceScale;
            screenScaleX *= Worldwalking.Instance.interfaceScale;

            LoadAssets();

            Tag = "MapWindowPanel";

            //add panels here
            Components.Add(mainPanel);
            mainPanel.Tag = "MainPanel";
            mainPanel.Size = new Vector2(Screen.width, Screen.height);
            mainPanel.AutoSize = AutoSizeModes.ScaleFreely;

            Components.Add(playerMarker);
            playerMarker.Tag = "PlayerMarker";
            playerMarker.Size = new Vector2(Screen.width, Screen.height);

            Components.Add(toolTip);
            toolTip.Tag = "Label";
            toolTip.Size = new Vector2(Screen.width, Screen.height);
            toolTip.TextScale = DaggerfallUnity.Settings.SDFFontRendering ? Worldwalking.Instance.textScaleSDF : Worldwalking.Instance.textScaleDefault;
            toolTip.ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor;
            toolTip.ShadowPosition = new Vector2(DaggerfallUI.DaggerfallDefaultShadowPos.x * screenScaleX, DaggerfallUI.DaggerfallDefaultShadowPos.y * screenScaleY);

            //Log panel
            mainPanel.Components.Add(logLabel);
            logLabel.Tag = "Log";
            logLabel.VerticalAlignment = VerticalAlignment.Middle;
            logLabel.HorizontalAlignment = HorizontalAlignment.Left;
            logLabel.Size = new Vector2(0, 0);
            logLabel.screenScale = screenScaleY;
            logLabel.Offset = new Vector2(4,texturePanelTime.height * screenScaleY + 4);

            //vitals panel
            mainPanel.Components.Add(vitalsPanel);
            vitalsPanel.Tag = "Vitals";
            vitalsPanel.VerticalAlignment = VerticalAlignment.Bottom;
            vitalsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            vitalsPanel.Size = new Vector2(texturePanelVitals.width * screenScaleX, texturePanelVitals.height * screenScaleY);
            vitalsPanel.BackgroundTexture = texturePanelVitals;
            //vitalsPanel.ToolTip = defaultToolTip;

            //vitals bars
            vitalsPanel.Components.Add(mapVitals);
            mapVitals.Tag = "VitalsBars";
            mapVitals.HorizontalAlignment = HorizontalAlignment.None;
            mapVitals.VerticalAlignment = VerticalAlignment.None;
            mapVitals.AutoSize = AutoSizeModes.None;
            mapVitals.SetMargins(Margins.All, 0);
            mapVitals.SetAllAutoSize(AutoSizeModes.None);
            mapVitals.SetAllHorizontalAlignment(HorizontalAlignment.None);
            mapVitals.SetAllVerticalAlignment(VerticalAlignment.None);
            Vector2 barSize = new Vector2(4 * screenScaleX, 32 * screenScaleY);
            float barInterval = 4 * screenScaleX;
            mapVitals.CustomHealthBarPosition = new Vector2(7 * screenScaleX, 7 * screenScaleY);
            mapVitals.CustomHealthBarSize = barSize;
            mapVitals.CustomFatigueBarPosition = new Vector2(mapVitals.CustomHealthBarPosition.Value.x + barSize.x + barInterval, mapVitals.CustomHealthBarPosition.Value.y);
            mapVitals.CustomFatigueBarSize = barSize;
            mapVitals.CustomMagickaBarPosition = new Vector2(mapVitals.CustomFatigueBarPosition.Value.x + barSize.x + barInterval, mapVitals.CustomHealthBarPosition.Value.y);
            mapVitals.CustomMagickaBarSize = barSize;

            //Speed panel
            mainPanel.Components.Add(speedPanel);
            speedPanel.Tag = "Speed";
            speedPanel.VerticalAlignment = VerticalAlignment.Bottom;
            speedPanel.HorizontalAlignment = HorizontalAlignment.Center;
            speedPanel.Size = new Vector2(texturePanelSpeed.width * screenScaleX, texturePanelSpeed.height * screenScaleY);
            speedPanel.BackgroundTexture = texturePanelSpeed;
            //vitalsPanel.ToolTip = defaultToolTip;

            speedPanel.Components.Add(speedRecklessPanel);
            speedRecklessPanel.Tag = "SpeedReckless";
            speedRecklessPanel.VerticalAlignment = VerticalAlignment.None;
            speedRecklessPanel.HorizontalAlignment = HorizontalAlignment.Center;
            speedRecklessPanel.Position = new Vector2(0, textureButtonSpeedReckless.height * screenScaleY);
            speedRecklessPanel.Size = new Vector2(textureButtonSpeedReckless.width * screenScaleX, textureButtonSpeedReckless.height * screenScaleY);
            speedRecklessPanel.BackgroundTexture = textureButtonSpeedReckless;

            speedPanel.Components.Add(speedCautiousPanel);
            speedCautiousPanel.Tag = "SpeedCautious";
            speedCautiousPanel.VerticalAlignment = VerticalAlignment.None;
            speedCautiousPanel.HorizontalAlignment = HorizontalAlignment.Center;
            speedCautiousPanel.Position = new Vector2(0, 2 * textureButtonSpeedCautious.height * screenScaleY);
            speedCautiousPanel.Size = new Vector2(textureButtonSpeedCautious.width * screenScaleX, textureButtonSpeedCautious.height * screenScaleY);
            speedCautiousPanel.BackgroundTexture = textureButtonSpeedCautious;

            speedPanel.Components.Add(speedCampPanel);
            speedCampPanel.Tag = "SpeedCamp";
            speedCampPanel.VerticalAlignment = VerticalAlignment.None;
            speedCampPanel.HorizontalAlignment = HorizontalAlignment.Center;
            speedCampPanel.Position = new Vector2(0, 3 * textureButtonSpeedCamp.height * screenScaleY);
            speedCampPanel.Size = new Vector2(textureButtonSpeedCamp.width * screenScaleX, textureButtonSpeedCamp.height * screenScaleY);
            speedCampPanel.BackgroundTexture = textureButtonSpeedCamp;

            speedPanel.Components.Add(speedRecklessButton);
            speedRecklessButton.Tag = "SpeedRecklessButton";
            speedRecklessButton.VerticalAlignment = VerticalAlignment.None;
            speedRecklessButton.HorizontalAlignment = HorizontalAlignment.Center;
            speedRecklessButton.Position = speedRecklessPanel.Position;
            speedRecklessButton.Size = speedRecklessPanel.Size;

            speedPanel.Components.Add(speedCautiousButton);
            speedCautiousButton.Tag = "SpeedCautiousButton";
            speedCautiousButton.VerticalAlignment = VerticalAlignment.None;
            speedCautiousButton.HorizontalAlignment = HorizontalAlignment.Center;
            speedCautiousButton.Position = speedCautiousPanel.Position;
            speedCautiousButton.Size = speedCautiousPanel.Size;

            speedPanel.Components.Add(speedCampButton);
            speedCampButton.Tag = "SpeedCampButton";
            speedCampButton.VerticalAlignment = VerticalAlignment.None;
            speedCampButton.HorizontalAlignment = HorizontalAlignment.Center;
            speedCampButton.Position = speedCampPanel.Position;
            speedCampButton.Size = speedCampPanel.Size;

            //Time panel
            mainPanel.Components.Add(timePanel);
            timePanel.Tag = "Time";
            timePanel.VerticalAlignment = VerticalAlignment.Top;
            timePanel.HorizontalAlignment = HorizontalAlignment.Left;
            timePanel.Size = new Vector2(texturePanelTime.width * screenScaleX, texturePanelTime.height * screenScaleY);
            timePanel.BackgroundTexture = texturePanelTime;
            //vitalsPanel.ToolTip = defaultToolTip;

            timePanel.Components.Add(timeLabel);
            timeLabel.Tag = "TimeLabel";
            timeLabel.VerticalAlignment = VerticalAlignment.None;
            timeLabel.HorizontalAlignment = HorizontalAlignment.Center;
            timeLabel.Position = new Vector2(0, 5 * screenScaleY);
            timeLabel.Text = "NOT WORKING YET";
            timeLabel.TextScale = DaggerfallUnity.Settings.SDFFontRendering ? Worldwalking.Instance.textScaleSDF : Worldwalking.Instance.textScaleDefault;
            timeLabel.HorizontalTextAlignment = TextLabel.HorizontalTextAlignmentSetting.Center;
            timeLabel.Size = new Vector2(texturePanelTime.width * screenScaleX, texturePanelTime.height - textureButtonTimeScale0.height * screenScaleY);
            timeLabel.ShadowColor = DaggerfallUI.DaggerfallDefaultShadowColor;
            timeLabel.ShadowPosition = toolTip.ShadowPosition;

            timePanel.Components.Add(timeScale0Panel);
            timeScale0Panel.Tag = "Time0";
            timeScale0Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale0Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale0Panel.Position = new Vector2(0, 0);
            timeScale0Panel.Size = new Vector2(textureButtonTimeScale0.width * screenScaleX, textureButtonTimeScale0.height * screenScaleY);
            timeScale0Panel.BackgroundTexture = textureButtonTimeScale0;

            timePanel.Components.Add(timeScale1Panel);
            timeScale1Panel.Tag = "Time1";
            timeScale1Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale1Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale1Panel.Position = new Vector2(textureButtonTimeScale0.width * screenScaleX, 0);
            timeScale1Panel.Size = new Vector2(textureButtonTimeScale1.width * screenScaleX, textureButtonTimeScale1.height * screenScaleY);
            timeScale1Panel.BackgroundTexture = textureButtonTimeScale1;

            timePanel.Components.Add(timeScale2Panel);
            timeScale2Panel.Tag = "Time2";
            timeScale2Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale2Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale2Panel.Position = new Vector2(timeScale1Panel.Position.x + textureButtonTimeScale1.width * screenScaleX, 0);
            timeScale2Panel.Size = new Vector2(textureButtonTimeScale2.width * screenScaleX, textureButtonTimeScale2.height * screenScaleY);
            timeScale2Panel.BackgroundTexture = textureButtonTimeScale2;

            timePanel.Components.Add(timeScale3Panel);
            timeScale3Panel.Tag = "Time3";
            timeScale3Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale3Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale3Panel.Position = new Vector2(timeScale2Panel.Position.x + textureButtonTimeScale2.width * screenScaleX, 0);
            timeScale3Panel.Size = new Vector2(textureButtonTimeScale3.width * screenScaleX, textureButtonTimeScale3.height * screenScaleY);
            timeScale3Panel.BackgroundTexture = textureButtonTimeScale3;

            timePanel.Components.Add(timeScale4Panel);
            timeScale4Panel.Tag = "Time4";
            timeScale4Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale4Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale4Panel.Position = new Vector2(timeScale3Panel.Position.x + textureButtonTimeScale3.width * screenScaleX, 0);
            timeScale4Panel.Size = new Vector2(textureButtonTimeScale4.width * screenScaleX, textureButtonTimeScale4.height * screenScaleY);
            timeScale4Panel.BackgroundTexture = textureButtonTimeScale4;

            timePanel.Components.Add(timeScale5Panel);
            timeScale5Panel.Tag = "Time5";
            timeScale5Panel.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale5Panel.HorizontalAlignment = HorizontalAlignment.None;
            timeScale5Panel.Position = new Vector2(timeScale4Panel.Position.x + textureButtonTimeScale4.width * screenScaleX, 0);
            timeScale5Panel.Size = new Vector2(textureButtonTimeScale5.width * screenScaleX, textureButtonTimeScale5.height * screenScaleY);
            timeScale5Panel.BackgroundTexture = textureButtonTimeScale5;

            timePanel.Components.Add(timeScale0Button);
            timeScale0Button.Tag = "Time0Button";
            timeScale0Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale0Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale0Button.Position = timeScale0Panel.Position;
            timeScale0Button.Size = timeScale0Panel.Size;

            timePanel.Components.Add(timeScale1Button);
            timeScale1Button.Tag = "Time1Button";
            timeScale1Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale1Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale1Button.Position = timeScale1Panel.Position;
            timeScale1Button.Size = timeScale1Panel.Size;

            timePanel.Components.Add(timeScale2Button);
            timeScale2Button.Tag = "Time2Button";
            timeScale2Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale2Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale2Button.Position = timeScale2Panel.Position;
            timeScale2Button.Size = timeScale2Panel.Size;

            timePanel.Components.Add(timeScale3Button);
            timeScale3Button.Tag = "Time3Button";
            timeScale3Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale3Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale3Button.Position = timeScale3Panel.Position;
            timeScale3Button.Size = timeScale3Panel.Size;

            timePanel.Components.Add(timeScale4Button);
            timeScale4Button.Tag = "Time4Button";
            timeScale4Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale4Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale4Button.Position = timeScale4Panel.Position;
            timeScale4Button.Size = timeScale4Panel.Size;

            timePanel.Components.Add(timeScale5Button);
            timeScale5Button.Tag = "Time5Button";
            timeScale5Button.VerticalAlignment = VerticalAlignment.Bottom;
            timeScale5Button.HorizontalAlignment = HorizontalAlignment.None;
            timeScale5Button.Position = timeScale5Panel.Position;
            timeScale5Button.Size = timeScale5Panel.Size;

            //Actions panel
            mainPanel.Components.Add(actionsPanel);
            actionsPanel.Tag = "Actions";
            actionsPanel.VerticalAlignment = VerticalAlignment.Top;
            actionsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            actionsPanel.Size = new Vector2(texturePanelButtons.width * screenScaleX, texturePanelButtons.height * screenScaleY);
            actionsPanel.BackgroundTexture = texturePanelButtons;

            float buttonHeight = actionsPanel.Size.y / 3;

            actionsPanel.Components.Add(actionsFindButton);
            actionsFindButton.Tag = "FindLocationButton";
            actionsFindButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsFindButton.Position = new Vector2(0, 0);

            actionsPanel.Components.Add(actionsImAtButton);
            actionsImAtButton.Tag = "ImAtButton";;
            actionsImAtButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsImAtButton.Position = actionsFindButton.Position + (Vector2.up * buttonHeight);

            actionsPanel.Components.Add(actionsShipButton);
            actionsShipButton.Tag = "ShipButton";
            actionsShipButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsShipButton.Position = actionsImAtButton.Position + (Vector2.up * buttonHeight);

            //Pin panel
            mainPanel.Components.Add(pinPanel);
            pinPanel.Tag = "Pins";
            pinPanel.VerticalAlignment = VerticalAlignment.Middle;
            pinPanel.HorizontalAlignment = HorizontalAlignment.Right;
            pinPanel.Size = new Vector2(texturePanelPins.width * screenScaleX, texturePanelPins.height * screenScaleY);
            pinPanel.BackgroundTexture = texturePanelPins;

            buttonHeight = pinPanel.Size.y / 4;

            pinPanel.Components.Add(pinSetButton);
            pinSetButton.Tag = "SetPinButton";
            pinSetButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinSetButton.Position = new Vector2(0, 0);

            pinPanel.Components.Add(pinGoToButton);
            pinGoToButton.Tag = "GoToPinButton"; ;
            pinGoToButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinGoToButton.Position = pinSetButton.Position + (Vector2.up * buttonHeight);

            pinPanel.Components.Add(pinTravelButton);
            pinTravelButton.Tag = "TravelToPinButton";
            pinTravelButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinTravelButton.Position = pinGoToButton.Position + (Vector2.up * buttonHeight);

            pinPanel.Components.Add(pinClearButton);
            pinClearButton.Tag = "ClearPinButton";
            pinClearButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinClearButton.Position = pinTravelButton.Position + (Vector2.up * buttonHeight);

            //Exit Button
            mainPanel.Components.Add(exitButton);
            exitButton.Tag = "Exit";
            exitButton.VerticalAlignment = VerticalAlignment.Bottom;
            exitButton.HorizontalAlignment = HorizontalAlignment.Right;
            exitButton.Size = new Vector2(textureButtonExit.width * screenScaleX, textureButtonExit.height * screenScaleY);
            exitButton.BackgroundTexture = textureButtonExit;

            //virtual sticks
            mainPanel.Components.Add(navigationPanel);
            navigationPanel.Tag = "Navigation";
            navigationPanel.VerticalAlignment = VerticalAlignment.Middle;
            navigationPanel.HorizontalAlignment = HorizontalAlignment.Left;
            navigationPanel.Size = new Vector2(texturePanelNavigation.width * screenScaleX, texturePanelNavigation.height * screenScaleY) * Worldwalking.Instance.navigationPanelScale;
            navigationPanel.BackgroundTexture = texturePanelNavigation;

            navigationPanel.Components.Add(navigationOrbitLeftButton);
            navigationOrbitLeftButton.Tag = "OrbitLeft";
            navigationOrbitLeftButton.VerticalAlignment = VerticalAlignment.Top;
            navigationOrbitLeftButton.HorizontalAlignment = HorizontalAlignment.Left;
            navigationOrbitLeftButton.Size = navigationPanel.Size / 3;

            navigationPanel.Components.Add(navigationMoveForwardButton);
            navigationMoveForwardButton.Tag = "MoveForward";
            navigationMoveForwardButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x);
            navigationMoveForwardButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationOrbitRightButton);
            navigationOrbitRightButton.Tag = "OrbitRight";
            navigationOrbitRightButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x);
            navigationOrbitRightButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationMoveLeftButton);
            navigationMoveLeftButton.Tag = "MoveLeft";
            navigationMoveLeftButton.Position = navigationOrbitLeftButton.Position + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationMoveLeftButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationTargetButton);
            navigationTargetButton.Tag = "Target";
            navigationTargetButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x) + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationTargetButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationMoveRightButton);
            navigationMoveRightButton.Tag = "MoveRight";
            navigationMoveRightButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x) + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationMoveRightButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationZoomOutButton);
            navigationZoomOutButton.Tag = "ZoomOut";
            navigationZoomOutButton.Position = navigationOrbitLeftButton.Position + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationZoomOutButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationMoveBackwardButton);
            navigationMoveBackwardButton.Tag = "MoveBackward";
            navigationMoveBackwardButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x) + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationMoveBackwardButton.Size = navigationOrbitLeftButton.Size;

            navigationPanel.Components.Add(navigationZoomInButton);
            navigationZoomInButton.Tag = "ZoomOut";
            navigationZoomInButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x) + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationZoomInButton.Size = navigationOrbitLeftButton.Size;
        }

        public void UpdatePlayerHeadTexture(Texture2D texture, float scale = 1f)
        {
            texturePlayerHead = texture;

            playerMarker.BackgroundTexture = texturePlayerHead;
            playerMarker.Size = new Vector2(texturePlayerHead.width * screenScaleX * Worldwalking.Instance.interfaceScale * scale, texturePlayerHead.height * screenScaleY * Worldwalking.Instance.interfaceScale * scale);
        }

        void LoadAssets()
        {
            TextureReplacement.TryImportImage("panelVitals", true, out texturePanelVitals);
            textureBarVitalsHealth = DaggerfallUI.GetTextureFromImg("MAIN03I0.IMG");
            textureBarVitalsFatigue = DaggerfallUI.GetTextureFromImg("MAIN04I0.IMG");
            textureBarVitalsMagicka = DaggerfallUI.GetTextureFromImg("MAIN05I0.IMG");

            TextureReplacement.TryImportImage("panelSpeed", true, out texturePanelSpeed);
            TextureReplacement.TryImportImage("buttonSpeed0", true, out textureButtonSpeedCamp);
            TextureReplacement.TryImportImage("buttonSpeed1", true, out textureButtonSpeedCautious);
            TextureReplacement.TryImportImage("buttonSpeed2", true, out textureButtonSpeedReckless);

            TextureReplacement.TryImportImage("panelTime", true, out texturePanelTime);
            TextureReplacement.TryImportImage("buttonTimeScale0", true, out textureButtonTimeScale0);
            TextureReplacement.TryImportImage("buttonTimeScale1", true, out textureButtonTimeScale1);
            TextureReplacement.TryImportImage("buttonTimeScale2", true, out textureButtonTimeScale2);
            TextureReplacement.TryImportImage("buttonTimeScale3", true, out textureButtonTimeScale3);
            TextureReplacement.TryImportImage("buttonTimeScale4", true, out textureButtonTimeScale4);
            TextureReplacement.TryImportImage("buttonTimeScale5", true, out textureButtonTimeScale5);

            TextureReplacement.TryImportImage("panelButtons", true, out texturePanelButtons);

            TextureReplacement.TryImportImage("panelPins", true, out texturePanelPins);

            TextureReplacement.TryImportImage("buttonExit", true, out textureButtonExit);
            TextureReplacement.TryImportImage("panelNavigation", true, out texturePanelNavigation);
        }

        public void UpdateInterface()
        {
            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (Worldwalking.Instance.scaleToScreen == 2) //Scale to whole screen
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = (float)screenRect.width / nativeScreenWidth;
            }
            else if (Worldwalking.Instance.scaleToScreen == 1)    //Scale only to screen height, maintaining aspect ratio
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = screenScaleY;
            }
            else //Do not scale to screen
            {
                screenScaleY = 1;
                screenScaleX = 1;
            }

            screenScaleY *= Worldwalking.Instance.interfaceScale;
            screenScaleX *= Worldwalking.Instance.interfaceScale;

            //add panels here
            toolTip.TextScale = DaggerfallUnity.Settings.SDFFontRendering ? Worldwalking.Instance.textScaleSDF : Worldwalking.Instance.textScaleDefault;
            toolTip.ShadowPosition = new Vector2(DaggerfallUI.DaggerfallDefaultShadowPos.x * screenScaleX, DaggerfallUI.DaggerfallDefaultShadowPos.y * screenScaleY);

            if (texturePlayerHead != null)
                UpdatePlayerHeadTexture(texturePlayerHead);

            //Log panel
            logLabel.screenScale = screenScaleY;
            logLabel.Offset = new Vector2(2 * screenScaleX, texturePanelTime.height + 2 * screenScaleY);

            //vitals panel
            vitalsPanel.Size = new Vector2(texturePanelVitals.width * screenScaleX, texturePanelVitals.height * screenScaleY);

            //vitals bars
            Vector2 barSize = new Vector2(4 * screenScaleX, 32 * screenScaleY);
            float barInterval = 4 * screenScaleX;
            mapVitals.CustomHealthBarPosition = new Vector2(7 * screenScaleX, 7 * screenScaleY);
            mapVitals.CustomHealthBarSize = barSize;
            mapVitals.CustomFatigueBarPosition = new Vector2(mapVitals.CustomHealthBarPosition.Value.x + barSize.x + barInterval, mapVitals.CustomHealthBarPosition.Value.y);
            mapVitals.CustomFatigueBarSize = barSize;
            mapVitals.CustomMagickaBarPosition = new Vector2(mapVitals.CustomFatigueBarPosition.Value.x + barSize.x + barInterval, mapVitals.CustomHealthBarPosition.Value.y);
            mapVitals.CustomMagickaBarSize = barSize;

            //Speed panel
            speedPanel.Size = new Vector2(texturePanelSpeed.width * screenScaleX, texturePanelSpeed.height * screenScaleY);

            speedRecklessPanel.Position = new Vector2(0, textureButtonSpeedReckless.height * screenScaleY);
            speedRecklessPanel.Size = new Vector2(textureButtonSpeedReckless.width * screenScaleX, textureButtonSpeedReckless.height * screenScaleY);

            speedCautiousPanel.Position = new Vector2(0, 2 * textureButtonSpeedCautious.height * screenScaleY);
            speedCautiousPanel.Size = new Vector2(textureButtonSpeedCautious.width * screenScaleX, textureButtonSpeedCautious.height * screenScaleY);

            speedCampPanel.Position = new Vector2(0, 3 * textureButtonSpeedCamp.height * screenScaleY);
            speedCampPanel.Size = new Vector2(textureButtonSpeedCamp.width * screenScaleX, textureButtonSpeedCamp.height * screenScaleY);

            speedRecklessButton.Position = speedRecklessPanel.Position;
            speedRecklessButton.Size = speedRecklessPanel.Size;

            speedCautiousButton.Position = speedCautiousPanel.Position;
            speedCautiousButton.Size = speedCautiousPanel.Size;

            speedCampButton.Position = speedCampPanel.Position;
            speedCampButton.Size = speedCampPanel.Size;

            //Time panel
            timePanel.Size = new Vector2(texturePanelTime.width * screenScaleX, texturePanelTime.height * screenScaleY);

            timeLabel.Position = new Vector2(0, 5 * screenScaleY);
            timeLabel.TextScale = DaggerfallUnity.Settings.SDFFontRendering ? Worldwalking.Instance.textScaleSDF : Worldwalking.Instance.textScaleDefault;
            timeLabel.Size = new Vector2(texturePanelTime.width * screenScaleX, texturePanelTime.height - textureButtonTimeScale0.height * screenScaleY);
            timeLabel.ShadowPosition = toolTip.ShadowPosition;

            timeScale0Panel.Size = new Vector2(textureButtonTimeScale0.width * screenScaleX, textureButtonTimeScale0.height * screenScaleY);

            timeScale1Panel.Position = new Vector2(textureButtonTimeScale0.width * screenScaleX, 0);
            timeScale1Panel.Size = new Vector2(textureButtonTimeScale1.width * screenScaleX, textureButtonTimeScale1.height * screenScaleY);

            timeScale2Panel.Position = new Vector2(timeScale1Panel.Position.x + textureButtonTimeScale1.width * screenScaleX, 0);
            timeScale2Panel.Size = new Vector2(textureButtonTimeScale2.width * screenScaleX, textureButtonTimeScale2.height * screenScaleY);

            timeScale3Panel.Position = new Vector2(timeScale2Panel.Position.x + textureButtonTimeScale2.width * screenScaleX, 0);
            timeScale3Panel.Size = new Vector2(textureButtonTimeScale3.width * screenScaleX, textureButtonTimeScale3.height * screenScaleY);

            timeScale4Panel.Position = new Vector2(timeScale3Panel.Position.x + textureButtonTimeScale3.width * screenScaleX, 0);
            timeScale4Panel.Size = new Vector2(textureButtonTimeScale4.width * screenScaleX, textureButtonTimeScale4.height * screenScaleY);

            timeScale5Panel.Position = new Vector2(timeScale4Panel.Position.x + textureButtonTimeScale4.width * screenScaleX, 0);
            timeScale5Panel.Size = new Vector2(textureButtonTimeScale5.width * screenScaleX, textureButtonTimeScale5.height * screenScaleY);

            timeScale0Button.Position = timeScale0Panel.Position;
            timeScale0Button.Size = timeScale0Panel.Size;

            timeScale1Button.Position = timeScale1Panel.Position;
            timeScale1Button.Size = timeScale1Panel.Size;

            timeScale2Button.Position = timeScale2Panel.Position;
            timeScale2Button.Size = timeScale2Panel.Size;

            timeScale3Button.Position = timeScale3Panel.Position;
            timeScale3Button.Size = timeScale3Panel.Size;

            timeScale4Button.Position = timeScale4Panel.Position;
            timeScale4Button.Size = timeScale4Panel.Size;

            timeScale5Button.Position = timeScale5Panel.Position;
            timeScale5Button.Size = timeScale5Panel.Size;

            //Actions panel
            actionsPanel.Size = new Vector2(texturePanelButtons.width * screenScaleX, texturePanelButtons.height * screenScaleY);

            float buttonHeight = actionsPanel.Size.y / 3;

            actionsFindButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsFindButton.Position = new Vector2(0, 0);

            actionsImAtButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsImAtButton.Position = actionsFindButton.Position + (Vector2.up * buttonHeight);

            actionsShipButton.Size = new Vector2(actionsPanel.Size.x, buttonHeight);
            actionsShipButton.Position = actionsImAtButton.Position + (Vector2.up * buttonHeight);

            //Pin panel
            pinPanel.Size = new Vector2(texturePanelPins.width * screenScaleX, texturePanelPins.height * screenScaleY);

            buttonHeight = pinPanel.Size.y / 4;

            pinSetButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinSetButton.Position = new Vector2(0, 0);

            pinGoToButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinGoToButton.Position = pinSetButton.Position + (Vector2.up * buttonHeight);

            pinTravelButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinTravelButton.Position = pinGoToButton.Position + (Vector2.up * buttonHeight);

            pinClearButton.Size = new Vector2(pinPanel.Size.x, buttonHeight);
            pinClearButton.Position = pinTravelButton.Position + (Vector2.up * buttonHeight);

            //Exit Button
            exitButton.Size = new Vector2(textureButtonExit.width * screenScaleX, textureButtonExit.height * screenScaleY);

            //navig


            //virtual sticks
            navigationPanel.Size = new Vector2(texturePanelNavigation.width * screenScaleX, texturePanelNavigation.height * screenScaleY) * Worldwalking.Instance.navigationPanelScale;
            navigationOrbitLeftButton.Size = navigationPanel.Size / 3;
            navigationMoveForwardButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x);
            navigationMoveForwardButton.Size = navigationOrbitLeftButton.Size;
            navigationOrbitRightButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x);
            navigationOrbitRightButton.Size = navigationOrbitLeftButton.Size;
            navigationMoveLeftButton.Position = navigationOrbitLeftButton.Position + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationMoveLeftButton.Size = navigationOrbitLeftButton.Size;
            navigationTargetButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x) + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationTargetButton.Size = navigationOrbitLeftButton.Size;
            navigationMoveRightButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x) + (Vector2.up * navigationOrbitLeftButton.Size.y);
            navigationMoveRightButton.Size = navigationOrbitLeftButton.Size;
            navigationZoomOutButton.Position = navigationOrbitLeftButton.Position + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationZoomOutButton.Size = navigationOrbitLeftButton.Size;
            navigationMoveBackwardButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * navigationOrbitLeftButton.Size.x) + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationMoveBackwardButton.Size = navigationOrbitLeftButton.Size;
            navigationZoomInButton.Position = navigationOrbitLeftButton.Position + (Vector2.right * 2 * navigationOrbitLeftButton.Size.x) + (Vector2.up * 2 * navigationOrbitLeftButton.Size.y);
            navigationZoomInButton.Size = navigationOrbitLeftButton.Size;
        }

        public override void Update()
        {
            if (!Enabled)
                return;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            if (Worldwalking.Instance.scaleToScreen == 2) //Scale to whole screen
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = (float)screenRect.width / nativeScreenWidth;
            }
            else if (Worldwalking.Instance.scaleToScreen == 1)    //Scale only to screen height, maintaining aspect ratio
            {
                screenScaleY = (float)screenRect.height / nativeScreenHeight;
                screenScaleX = screenScaleY;
            }
            else //Do not scale to screen
            {
                screenScaleY = 1;
                screenScaleX = 1;
            }

            screenScaleY *= Worldwalking.Instance.interfaceScale;
            screenScaleX *= Worldwalking.Instance.interfaceScale;

            base.Update();

            speedRecklessPanel.Enabled = false;
            speedCautiousPanel.Enabled = false;
            speedCampPanel.Enabled = false;

            if (Worldwalking.Instance.travelSpeed == 2)
                speedRecklessPanel.Enabled = true;
            else if (Worldwalking.Instance.travelSpeed == 1)
                speedCautiousPanel.Enabled = true;
            else if (Worldwalking.Instance.travelSpeed == 0)
                speedCampPanel.Enabled = true;

            timeScale5Panel.Enabled = false;
            timeScale4Panel.Enabled = false;
            timeScale3Panel.Enabled = false;
            timeScale2Panel.Enabled = false;
            timeScale1Panel.Enabled = false;
            timeScale0Panel.Enabled = false;

            if (Worldwalking.Instance.timeScaleIndex == 5)
                timeScale5Panel.Enabled = true;
            else if (Worldwalking.Instance.timeScaleIndex == 4)
                timeScale4Panel.Enabled = true;
            else if (Worldwalking.Instance.timeScaleIndex == 3)
                timeScale3Panel.Enabled = true;
            else if (Worldwalking.Instance.timeScaleIndex == 2)
                timeScale2Panel.Enabled = true;
            else if (Worldwalking.Instance.timeScaleIndex == 1)
                timeScale1Panel.Enabled = true;
            else if (Worldwalking.Instance.timeScaleIndex == 0)
                timeScale0Panel.Enabled = true;

            timeLabel.Text = DaggerfallUnity.Instance.WorldTime.Now.DateTimeString();

            if (Worldwalking.Instance.tooltipString != string.Empty)
            {
                if (!toolTip.Enabled)
                    toolTip.Enabled = true;
                toolTip.Text = Worldwalking.Instance.tooltipString;
                if (Worldwalking.Instance.lastInputDeviceIsGamepad != 0)
                    toolTip.Position = new Vector2((Screen.width/ 2) - screenRect.x, (Screen.height/2) - screenRect.y);
                else
                    toolTip.Position = new Vector2(MousePosition.x - screenRect.x, MousePosition.y - screenRect.y);
                toolTip.Position += new Vector2(3 * screenScaleX, -6 * screenScaleY);
            }
            else
            {
                if (toolTip.Enabled)
                    toolTip.Enabled = false;
            }

            //if touch controls are enabled draw the sticks
            if (Worldwalking.Instance.showNavigationPanel)
            {
                if (!navigationPanel.Enabled)
                    navigationPanel.Enabled = true;
            }
            else
            {
                if (navigationPanel.Enabled)
                    navigationPanel.Enabled = false;
            }

            if (showPlayerMarker && texturePlayerHead != null)
            {
                if (!playerMarker.Enabled)
                    playerMarker.Enabled = true;

                //place
                Vector3 posRaw = Worldwalking.Instance.Camera.WorldToScreenPoint(Worldwalking.Instance.PlayerObject.transform.position);
                playerMarker.Position = new Vector2(posRaw.x - screenRect.x - (playerMarker.Size.x / 2),screenRect.height - posRaw.y - playerMarker.Size.y);
                //playerMarker.Position = new Vector2(posRaw.x - screenRect.x,screenRect.height - posRaw.y);
            }
            else
            {
                if (playerMarker.Enabled)
                    playerMarker.Enabled = false;
            }
        }

        public override void Draw()
        {
            if (!Enabled)
                return;

            base.Draw();
        }

        //find location stuff
        protected readonly int maxMatchingResults = 1000;
        protected string distanceRegionName = null;
        protected IDistance distance;

        DFRegion currentDFRegion;

        // Populated with localized names whenever player searches or lists inside this region
        // Used to complete search and list on localized names over canonical names
        protected Dictionary<string, int> localizedMapNameLookup = new Dictionary<string, int>();

        protected ContentReader.MapSummary locationSummary;

        DaggerfallListPickerWindow locationPicker;

        class MatchesCutOff
        {
            private readonly float threshold;

            public MatchesCutOff(float bestRelevance)
            {
                // If perfect match exists, return all perfect matches only
                // Normally there should be only one perfect match, but if string canonization generates collisions that's no longer guaranteed
                threshold = bestRelevance == 1f ? 1f : bestRelevance * 0.5f;
            }

            public bool Keep(float relevance)
            {
                return relevance >= threshold;
            }
        }

        // Subclass DaggerfallListPickerWindow to delay calls to listBox.AddItem() after its setup
        // Otherwise, added items do not inherit the restricted area from the ListBox
        public class DaggerfallLocationsListPickerWindow : DaggerfallListPickerWindow
        {
            private List<string> locations;

            public DaggerfallLocationsListPickerWindow(IUserInterfaceManager uiManager, IUserInterfaceWindow previous, List<string> locations) : base(uiManager, previous)
            {
                this.locations = locations;
            }

            protected override void Setup()
            {
                base.Setup();
                listBox.RectRestrictedRenderArea = new Rect(listBox.Position, listBox.Size);
                listBox.RestrictedRenderAreaCoordinateType = BaseScreenComponent.RestrictedRenderArea_CoordinateType.ParentCoordinates;
                listBox.AddItems(locations);
            }
        }

        void ClickButtonFindLocation()
        {
            //lock map controls
            int region = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetPoliticIndex(Mathf.RoundToInt(Worldwalking.Instance.CameraBaseObject.transform.localPosition.x), -Mathf.RoundToInt(Worldwalking.Instance.CameraBaseObject.transform.localPosition.z)) - 128;
            currentDFRegion = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(region);

            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            DaggerfallInputMessageBox findPopUp = new DaggerfallInputMessageBox(DaggerfallUI.UIManager, null, 31, "Search " + currentDFRegion.Name + " for:");
            findPopUp.TextPanelDistanceY = 5;
            findPopUp.TextBox.WidthOverride = 308;
            findPopUp.TextBox.MaxCharacters = 32;
            findPopUp.OnGotUserInput += HandleLocationFindEvent;
            findPopUp.Show();
        }

        // Handles events from Find Location pop-up.
        protected virtual void HandleLocationFindEvent(DaggerfallInputMessageBox inputMessageBox, string locationName)
        {
            List<DistanceMatch> matching;
            if (FindLocation(locationName, out matching))
            {
                if (matching.Count == 1)
                {
                    //center camera on location
                    DFLocation location = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetLocation(currentDFRegion.Name, matching[0].text);
                    DFPosition pos = MapsFile.LongitudeLatitudeToMapPixel(location.MapTableData.Longitude, location.MapTableData.Latitude);
                    Worldwalking.Instance.SetCameraTargetPosition(pos.X, pos.Y);
                    Worldwalking.Instance.SetPinPosition(pos.X, pos.Y);
                }
                else
                {
                    ShowLocationPicker(matching.ConvertAll(match => match.text).ToArray(), false);
                }
            }
            else
            {
                TextFile.Token[] textTokens = DaggerfallUnity.Instance.TextProvider.GetRSCTokens(13);
                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                messageBox.SetTextTokens(textTokens);
                messageBox.ClickAnywhereToClose = true;
                DaggerfallUI.UIManager.PushWindow(messageBox);
                return;
            }
        }

        // Find location by name
        protected virtual bool FindLocation(string name, out List<DistanceMatch> matching)
        {
            matching = new List<DistanceMatch>();
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (distanceRegionName != currentDFRegion.Name)
            {
                distanceRegionName = currentDFRegion.Name;
                distance = DaggerfallDistance.GetDistance();
                distance.SetDictionary(GetCurrentRegionLocalizedMapNames());
            }

            DistanceMatch[] bestMatches = distance.FindBestMatches(name, maxMatchingResults);

            // Check if selected locations actually exist/are visible
            MatchesCutOff cutoff = null;
            ContentReader.MapSummary findLocationSummary;

            foreach (DistanceMatch match in bestMatches)
            {
                // Must have called GetCurrentRegionLocalizedMapNames() prior to this point
                if (!localizedMapNameLookup.ContainsKey(match.text))
                {
                    Debug.LogWarningFormat("Error: location name '{0}' key not found in localizedMapNameLookup dictionary for this region.", match.text);
                    continue;
                }
                int index = localizedMapNameLookup[match.text];
                DFRegion.RegionMapTable locationInfo = currentDFRegion.MapTable[index];
                DFPosition pos = MapsFile.LongitudeLatitudeToMapPixel((int)locationInfo.Longitude, (int)locationInfo.Latitude);
                if (DaggerfallUnity.Instance.ContentReader.HasLocation(pos.X, pos.Y, out findLocationSummary))
                {
                    // only make location searchable if it is already discovered
                    if (!checkLocationDiscovered(findLocationSummary))
                        continue;

                    if (cutoff == null)
                    {
                        cutoff = new MatchesCutOff(match.relevance);

                        // Set locationSummary to first result's MapSummary in case we skip the location list picker step
                        locationSummary = findLocationSummary;
                    }
                    else
                    {
                        if (!cutoff.Keep(match.relevance))
                            break;
                    }
                    matching.Add(match);
                }
            }

            return matching.Count > 0;
        }

        // Check if location with MapSummary summary is already discovered
        protected virtual bool checkLocationDiscovered(ContentReader.MapSummary summary)
        {
            // Check location MapTableData.Discovered flag in world replacement data then cached MAPS.BSA data
            bool discovered = false;
            DFLocation location;
            if (WorldDataReplacement.GetDFLocationReplacementData(summary.RegionIndex, summary.MapIndex, out location))
                discovered = location.MapTableData.Discovered;
            else
                discovered = summary.Discovered;

            return GameManager.Instance.PlayerGPS.HasDiscoveredLocation(summary.ID) || discovered;
        }

        // Get localized names of all locations in current region with fallback to canonical name
        // Builds a new name lookup dictionary for this region on every call used to complete search
        protected string[] GetCurrentRegionLocalizedMapNames()
        {
            localizedMapNameLookup.Clear();
            List<string> localizedNames = new List<string>(currentDFRegion.MapNames.Length);
            for (int l = 0; l < currentDFRegion.MapNames.Length; l++)
            {
                // Handle duplicate names in same way as Region.MapNameLookup
                string name = TextManager.Instance.GetLocalizedLocationName(currentDFRegion.MapTable[l].MapId, currentDFRegion.MapNames[l]);
                if (!localizedNames.Contains(name))
                {
                    localizedNames.Add(name);
                    localizedMapNameLookup.Add(name, l);
                }
            }
            return localizedNames.ToArray();
        }

        // Creates a ListPickerWindow with a list of locations from current region
        // Locations displayed will be filtered out depending on the dungeon / town / temple / home button settings
        private void ShowLocationPicker(string[] locations, bool applyFilters)
        {
            List<string> filteredLocations = new List<string>();
            for (int i = 0; i < locations.Length; i++)
            {
                if (applyFilters)
                {
                    // Must have called GetCurrentRegionLocalizedMapNames() prior to this point
                    int index = localizedMapNameLookup[locations[i]];
                    if (Worldwalking.Instance.GetPixelColorIndex(currentDFRegion.MapTable[index].LocationType) == -1)
                        continue;
                }
                filteredLocations.Add(locations[i]);
            }

            locationPicker = new DaggerfallLocationsListPickerWindow(DaggerfallUI.UIManager, null, filteredLocations);
            locationPicker.OnItemPicked += HandleLocationPickEvent;
            DaggerfallUI.UIManager.PushWindow(locationPicker);
        }
        public void HandleLocationPickEvent(int index, string locationName)
        {
            Worldwalking.Instance.inputDelayTimer = 0;
            locationPicker.CloseWindow();
            HandleLocationFindEvent(null, locationName);
        }
    }
}
