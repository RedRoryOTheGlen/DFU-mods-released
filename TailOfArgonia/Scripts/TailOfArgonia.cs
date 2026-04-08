using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Weather;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

public class TailOfArgonia : MonoBehaviour
{
    public static TailOfArgonia Instance;

    WeatherManager weatherManager;
    WorldTime worldTime;
    PlayerAmbientLight playerAmbientLight;
    PlayerEnterExit playerEnterExit;
    DaggerfallSky sky;

    int[] frameRates = new int[] { 12, 24, 30, 60, -1};
    int frameRateIndex = 4;
    bool vsyncDefault;

    bool view;
    int viewDistance = 64;
    int viewDistanceInterior = 64;
    int viewDistanceDungeon = 64;
    int[] viewDistances = new int[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 };

    bool fog;
    float fogStartDistanceMultiplier = 0.2f;
    float fogEndDistanceMultiplier = 0.8f;
    float fogDensityModifier = 1;
    float[] fogDensities = new float[] { 0.1f, 0.05f, 0.025f, 0.0125f, 0.00625f, 0.003125f, 0.0015625f, 0.00078125f };
    bool fogInteriorColored = false;
    float fogInteriorColorScale = 0.1f;
    float fogInteriorBrightness = 0.1f;
    bool fogDungeonColored = false;
    float fogDungeonColorScale = 0.1f;
    float fogDungeonBrightness = 0.1f;

    int[] terrainDistances = new int[] { 1, 1, 2, 2, 3, 3, 4, 4 };
    int terrainDistanceOverride = 0;

    public static WeatherManager.FogSettings SunnyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 2560, excludeSkybox = true };
    public static WeatherManager.FogSettings SunnyFogSettingsDefault;
    public static WeatherManager.FogSettings OvercastFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 2048, excludeSkybox = true };
    public static WeatherManager.FogSettings OvercastFogSettingsDefault;
    public static WeatherManager.FogSettings RainyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 1536, excludeSkybox = true };
    public static WeatherManager.FogSettings RainyFogSettingsDefault;
    public static WeatherManager.FogSettings SnowyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 1280, excludeSkybox = true };
    public static WeatherManager.FogSettings SnowyFogSettingsDefault;
    public static WeatherManager.FogSettings HeavyFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 1024, excludeSkybox = true };
    public static WeatherManager.FogSettings HeavyFogSettingsDefault;
    public static WeatherManager.FogSettings InteriorFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 1024, excludeSkybox = true };
    public static WeatherManager.FogSettings InteriorFogSettingsDefault;
    public static WeatherManager.FogSettings DungeonFogSettings = new WeatherManager.FogSettings { fogMode = FogMode.Linear, density = 0.0f, startDistance = 0, endDistance = 1024, excludeSkybox = true };
    public static WeatherManager.FogSettings DungeonFogSettingsDefault;

    FogMode currentFogMode;

    bool debugShowMessages;

    bool smoothClip;
    float smoothClipStartDistance = 0.75f;
    float smoothClipLastUpdate;
    int startCount = 0;

    public Texture[] ditherSizeTextures;
    int ditherSize;

    public float textureBrightness = 1f;

    public Shader shaderDefault;
    public Shader shaderBillboard;
    public Shader shaderBillboardBatch;
    public Shader shaderBillboardBatchNoShadows;
    public Shader shaderTilemap;
    public Shader shaderTilemapTextureArray;

    public Shader shaderParticleLit;
    public Shader shaderParticleEmissive;

    bool shearing;
    Camera shearingEye;
    PostProcessLayer shearingPostprocessingLayer;
    bool isShearing;
    bool shearingCrosshairAlwaysVisible;
    KeyCode shearingKeyCode;

    const string defaultCrosshairFilename = "Crosshair";
    public Vector2 crosshairSize;
    public Texture2D CrosshairTexture;
    public float CrosshairScale = 1.0f;

    public int nativeScreenWidth = 320;
    public int nativeScreenHeight = 200;
    Rect screenRect;
    Vector3 crosshairOffset;

    bool ambientLighting;
    Color lastAmbientColor;
    Color lastCameraClearColor;

    Light sunLight;
    float sunLightScale = 1;
    float moonLightScale = 1;
    float MoonLightScale
    {
        get
        {
            return moonLightScale * moonPhaseScale;
        }
    }
    int lightColor;             //0 = sky, 1 = fog
    float LightColorScale
    {
        get
        {
            if (playerEnterExit.IsPlayerInsideDungeon)
                return lightDungeonColorScale;
            else if (playerEnterExit.IsPlayerInside)
                return lightInteriorColorScale;
            else
                return lightColorScale;
        }
    }
    float lightColorScale = 1;
    float lightInteriorColorScale = 1;
    float lightDungeonColorScale = 1;

    float moonPhaseScale = 1;
    LunarPhases moonPhaseMassar = LunarPhases.None;
    LunarPhases moonPhaseSecunda = LunarPhases.None;

    float ambientLightExteriorDayScale = 1;
    float ambientLightExteriorNightScale = 1;
    float ambientLightInteriorDayScale = 1;
    float ambientLightInteriorNightScale = 1;
    float ambientLightCastleScale = 1;
    float ambientLightDungeonScale = 1;

    bool ambientLightingInitialized = false;
    Color ExteriorNoonAmbientLightDefault = new Color(0.9f, 0.9f, 0.9f);
    Color ExteriorNightAmbientLightDefault = new Color(0.25f, 0.25f, 0.25f);
    Color InteriorAmbientLightDefault = new Color(0.18f, 0.18f, 0.18f);
    Color InteriorNightAmbientLightDefault = new Color(0.20f, 0.18f, 0.20f);
    Color InteriorAmbientLight_AmbientOnlyDefault = new Color(0.8f, 0.8f, 0.8f);
    Color InteriorNightAmbientLight_AmbientOnlyDefault = new Color(0.5f, 0.5f, 0.5f);
    Color DungeonAmbientLightDefault = new Color(0.12f, 0.12f, 0.12f);
    Color CastleAmbientLightDefault = new Color(0.58f, 0.58f, 0.58f);

    float OvercastSunlightScaleDefault = 0.65f;
    float RainSunlightScaleDefault = 0.45f;
    float StormSunlightScaleDefault = 0.25f;
    float SnowSunlightScaleDefault = 0.45f;
    float WinterSunlightScaleDefault = 0.65f;

    float SeasonalSunlightScale = 1;

    bool sunShadowsHard = true;

    Mod DynamicSkies;

    IEnumerator worldUpdateMessage;

    FieldInfo DaggerfallSkyMainCamera;

    //weather stuff
    bool weather = false;
    bool weatherPixel = false;
    bool weatherShader = false;
    float weatherEmissionRain = 0.5f;
    float weatherEmissionSnow = 0.5f;
    bool weatherSplashSnow = true;
    bool weatherSplashRain = true;

    ParticleSystem rainParticle;
    ParticleSystemRenderer rainParticleRenderer;
    ParticleSystem rainParticleSplash;
    ParticleSystemRenderer rainParticleSplashRenderer;
    ParticleSystem snowParticle;
    ParticleSystemRenderer snowParticleRenderer;
    ParticleSystem snowParticleSplash;
    ParticleSystemRenderer snowParticleSplashRenderer;
    //materials
    Material rainDefaultMaterial;
    Material snowDefaultMaterial;
    Material rainPixelMaterial;
    Material snowPixelMaterial;

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;

        var go = new GameObject(mod.Title);
        TailOfArgonia toa = go.AddComponent<TailOfArgonia>();

        toa.ditherSizeTextures = new Texture[4];
        toa.ditherSizeTextures[0] = mod.GetAsset<Texture>("Textures/BayerDither2x2.png");
        toa.ditherSizeTextures[1] = mod.GetAsset<Texture>("Textures/BayerDither4x4.png");
        toa.ditherSizeTextures[2] = mod.GetAsset<Texture>("Textures/BayerDither8x8.png");
        toa.ditherSizeTextures[3] = mod.GetAsset<Texture>("Textures/BayerDither16x16.png");


        toa.shaderDefault = mod.GetAsset<Shader>("Shaders/DaggerfallDefaultDither.shader");
        toa.shaderBillboard = mod.GetAsset<Shader>("Shaders/DaggerfallBillboardDither.shader");
        toa.shaderBillboardBatch = mod.GetAsset<Shader>("Shaders/DaggerfallBillboardBatchDither.shader");
        toa.shaderBillboardBatchNoShadows = mod.GetAsset<Shader>("Shaders/DaggerfallBillboardBatchNoShadowsDither.shader");
        toa.shaderTilemap = mod.GetAsset<Shader>("Shaders/DaggerfallTilemapDither.shader");
        toa.shaderTilemapTextureArray = mod.GetAsset<Shader>("Shaders/DaggerfallTilemapTextureArrayDither.shader");

        toa.shaderParticleLit = mod.GetAsset<Shader>("Shaders/Alpha-VertexLit.shader");
        toa.shaderParticleEmissive = mod.GetAsset<Shader>("Shaders/Particle VertexLit Blended.shader");

        ModSettings settings = mod.GetSettings();
        FilterMode filterMode = (FilterMode)settings.GetValue<int>("Other", "InterfaceFilterMode");
        DaggerfallUI.Instance.GlobalFilterMode = filterMode;
        DaggerfallUnity.Instance.MaterialReader.MainFilterMode = filterMode;
        DaggerfallUnity.Instance.MaterialReader.SkyFilterMode = filterMode;
    }
    private void LoadSettings(ModSettings settings, ModSettingsChange change)
    {
        if (change.HasChanged("FrameRate"))
        {
            frameRateIndex = settings.GetValue<int>("FrameRate", "Target");
        }
        if (change.HasChanged("Modules"))
        {
            view = settings.GetValue<bool>("Modules", "ViewDistance");
            fog = settings.GetValue<bool>("Modules", "Fog");
            smoothClip = settings.GetValue<bool>("Modules", "SmoothClipping");
            shearing = settings.GetValue<bool>("Modules", "YShearing");
            ambientLighting = settings.GetValue<bool>("Modules", "ImprovedAmbientLight");
        }
        if (change.HasChanged("ViewDistance"))
        {
            viewDistance = settings.GetValue<int>("ViewDistance", "Maximum");
            viewDistanceInterior = settings.GetValue<int>("ViewDistance", "InteriorMaximum");
            viewDistanceDungeon = settings.GetValue<int>("ViewDistance", "DungeonMaximum");
            terrainDistanceOverride = settings.GetValue<int>("ViewDistance", "TerrainDistanceOverride");
        }
        if (change.HasChanged("Fog"))
        {
            fogStartDistanceMultiplier = settings.GetValue<int>("Fog", "LinearStartDistance") * 0.01f;
            fogEndDistanceMultiplier = settings.GetValue<int>("Fog", "LinearEndDistance") * 0.01f;
            fogDensityModifier = settings.GetValue<float>("Fog", "ExponentialDensityOffset");
            fogInteriorColored = settings.GetValue<bool>("Fog", "InteriorFogColor");
            fogInteriorColorScale = settings.GetValue<float>("Fog", "InteriorFogColorScale");
            fogInteriorBrightness = settings.GetValue<float>("Fog", "InteriorFogBrightness");
            fogDungeonColored = settings.GetValue<bool>("Fog", "DungeonFogColor");
            fogDungeonColorScale = settings.GetValue<float>("Fog", "DungeonFogColorScale");
            fogDungeonBrightness = settings.GetValue<float>("Fog", "DungeonFogBrightness");
        }

        if (change.HasChanged("SmoothClipping"))
        {
            smoothClipStartDistance = settings.GetValue<int>("SmoothClipping", "StartDistance")*0.01f;
            ditherSize = settings.GetValue<int>("SmoothClipping", "DitherSize");
        }

        if (change.HasChanged("ImprovedAmbientLight"))
        {
            sunShadowsHard = settings.GetValue<bool>("ImprovedAmbientLight", "SunHardShadows");
            sunLightScale = settings.GetValue<float>("ImprovedAmbientLight", "SunLightScale");
            moonLightScale = settings.GetValue<float>("ImprovedAmbientLight", "MoonLightScale");
            lightColor = settings.GetValue<int>("ImprovedAmbientLight", "LightColor");
            lightColorScale = settings.GetValue<float>("ImprovedAmbientLight", "LightColorScale");
            lightInteriorColorScale = settings.GetValue<float>("ImprovedAmbientLight", "InteriorLightColorScale");
            lightDungeonColorScale = settings.GetValue<float>("ImprovedAmbientLight", "DungeonLightColorScale");
            ambientLightExteriorDayScale = settings.GetValue<float>("ImprovedAmbientLight", "ExteriorDayLightScale")*2;
            ambientLightExteriorNightScale = settings.GetValue<float>("ImprovedAmbientLight", "ExteriorNightLightScale");
            ambientLightInteriorDayScale = settings.GetValue<float>("ImprovedAmbientLight", "InteriorDayLightScale");
            ambientLightInteriorNightScale = settings.GetValue<float>("ImprovedAmbientLight", "InteriorNightLightScale");
            ambientLightCastleScale = settings.GetValue<float>("ImprovedAmbientLight", "CastleLightScale");
            ambientLightDungeonScale = settings.GetValue<float>("ImprovedAmbientLight", "DungeonLightScale");
        }

        if (change.HasChanged("YShearing"))
        {
            shearingCrosshairAlwaysVisible = settings.GetValue<bool>("YShearing", "CrosshairAlwaysVisible");
            shearingKeyCode = SetKeyFromText(settings.GetString("YShearing", "ToggleInput"));
        }

        if (change.HasChanged("WeatherParticles"))
        {
            weather = settings.GetValue<bool>("WeatherParticles", "Enable");
            weatherPixel = settings.GetValue<bool>("WeatherParticles", "PixelTexture");
            weatherShader = settings.GetValue<bool>("WeatherParticles", "LitShader");
            weatherEmissionRain = (float)settings.GetValue<int>("WeatherParticles", "LitRainEmission") * 0.01f;
            weatherEmissionSnow = (float)settings.GetValue<int>("WeatherParticles", "LitSnowEmission") * 0.01f;
            weatherSplashRain = settings.GetValue<bool>("WeatherParticles", "RainSplashEffect");
            weatherSplashSnow = settings.GetValue<bool>("WeatherParticles", "SnowSplashEffect");
        }

        if (change.HasChanged("Debug"))
        {
            debugShowMessages = settings.GetValue<bool>("Debug", "ShowMessages");
        }

        if (change.HasChanged("Other"))
        {
            FilterMode filterMode = (FilterMode)settings.GetValue<int>("Other", "InterfaceFilterMode");
            DaggerfallUI.Instance.GlobalFilterMode = filterMode;
            DaggerfallUnity.Instance.MaterialReader.MainFilterMode = filterMode;
            DaggerfallUnity.Instance.MaterialReader.SkyFilterMode = filterMode;
        }

        if (change.HasChanged("FrameRate") || change.HasChanged("Modules") || change.HasChanged("ViewDistance") || change.HasChanged("ImprovedAmbientLight") || change.HasChanged("Fog") || change.HasChanged("SmoothClipping") || change.HasChanged("WeatherParticles"))
        {
            if (frameRateIndex != 4)
            {
                //disable vsync
                DaggerfallUnity.Settings.VSync = false;
                if (DaggerfallUnity.Settings.VSync)
                    QualitySettings.vSyncCount = 1;
                else
                    QualitySettings.vSyncCount = 0;

                DaggerfallUnity.Settings.TargetFrameRate = frameRates[frameRateIndex];
                Application.targetFrameRate = DaggerfallUnity.Settings.TargetFrameRate;
            }
            else
            {
                DaggerfallUnity.Settings.VSync = vsyncDefault;
                if (DaggerfallUnity.Settings.VSync)
                    QualitySettings.vSyncCount = 1;
                else
                    QualitySettings.vSyncCount = 0;

                DaggerfallUnity.Settings.TargetFrameRate = -1;
                Application.targetFrameRate = -1;
            }

            if (view)
                ApplyViewDistance();
            else
                ResetViewDistance();

            if (fog)
                ApplyFog((FogMode)settings.GetValue<int>("Fog", "Type") + 1);
            else
                ResetFog();

            if (startCount > 0)
            {
                if (smoothClip)
                    ApplyFadeShader();
                else
                    RemoveFadeShader();
            }
            else
                startCount++;

            ToggleImprovedExteriorLighting(ambientLighting);

            isShearing = shearing;

            SetUpWeatherParticles();
        }
    }

    private void ModCompatibilityChecking()
    {
        DynamicSkies = ModManager.Instance.GetModFromGUID("53a9b8f5-6271-4f74-9b8b-9220dd105a04");
    }

    void Start()
    {
        if (Instance == null)
            Instance = this;

        vsyncDefault = DaggerfallUnity.Settings.VSync;

        weatherManager = GameManager.Instance.WeatherManager;
        worldTime = DaggerfallUnity.Instance.WorldTime;
        playerEnterExit = GameManager.Instance.PlayerEnterExit;
        playerAmbientLight = GameManager.Instance.PlayerObject.GetComponent<PlayerAmbientLight>();

        sky = GameManager.Instance.SkyRig;
        DaggerfallSkyMainCamera = sky.GetType().GetField("mainCamera", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
        if (DaggerfallSkyMainCamera != null)
            Debug.Log("TAIL OF ARGONIA - FOUND DAGGERFALL SKY MAIN CAMERA FIELD!");

        SunnyFogSettingsDefault = weatherManager.SunnyFogSettings;
        OvercastFogSettingsDefault = weatherManager.OvercastFogSettings;
        RainyFogSettingsDefault = weatherManager.RainyFogSettings;
        SnowyFogSettingsDefault = weatherManager.SnowyFogSettings;
        HeavyFogSettingsDefault = weatherManager.HeavyFogSettings;
        InteriorFogSettingsDefault = weatherManager.InteriorFogSettings;
        DungeonFogSettingsDefault = weatherManager.DungeonFogSettings;

        SpawnShearingCamera();

        WorldTime.OnCityLightsOn += ApplyFadeShader_OnCityLights;
        WorldTime.OnCityLightsOff += ApplyFadeShader_OnCityLights;

        PlayerEnterExit.OnTransitionInterior += ApplyViewDistance_OnTransitionInterior;
        PlayerEnterExit.OnTransitionDungeonInterior += ApplyViewDistance_OnTransitionInterior;
        PlayerEnterExit.OnTransitionExterior += ApplyViewDistance_OnTransitionExterior;
        PlayerEnterExit.OnTransitionDungeonExterior += ApplyViewDistance_OnTransitionExterior;
        SaveLoadManager.OnLoad += ApplyViewDistance_OnLoad;

        StreamingWorld.OnUpdateTerrainsEnd += ApplyFadeShader_OnUpdateTerrainsEnd;
        PlayerEnterExit.OnTransitionExterior += ApplyFadeShader_OnTransitionExterior;
        PlayerEnterExit.OnTransitionDungeonExterior += ApplyFadeShader_OnTransitionExterior;
        SaveLoadManager.OnLoad += ApplyFadeShader_OnLoad;
        DaggerfallTravelPopUp.OnPostFastTravel += ApplyFadeShader_OnPostFastTravel;

        /*GameManager.OnEnemySpawn += ApplyFadeShader_OnEnemySpawn;
        PopulationManager.OnMobileNPCCreate += ApplyFadeShader_OnMobileNPCCreate;*/

        PlayerEnterExit.OnTransitionInterior += ToggleShearingCamera_OnTransition;
        PlayerEnterExit.OnTransitionDungeonInterior += ToggleShearingCamera_OnTransition;
        PlayerEnterExit.OnTransitionExterior += ToggleShearingCamera_OnTransition;
        PlayerEnterExit.OnTransitionDungeonExterior += ToggleShearingCamera_OnTransition;
        SaveLoadManager.OnLoad += ToggleShearingCamera_OnLoad;

        sunLight = GameManager.Instance.SunlightManager.GetComponent<Light>();

        CrosshairTexture = DaggerfallUI.GetTextureFromResources(defaultCrosshairFilename, out crosshairSize);

        mod.LoadSettingsCallback = LoadSettings;
        mod.LoadSettings();

        //reinitialize terrain array to allow for more terrains
        FieldInfo terrainArray = GameManager.Instance.StreamingWorld.GetType().GetField("terrainArray", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance);
        if (terrainArray != null)
        {
            Debug.Log("TAIL OF ARGONIA - FOUND TERRAIN ARRAY FIELD");
            terrainArray.SetValue(GameManager.Instance.StreamingWorld, new DaggerfallWorkshop.StreamingWorld.TerrainDesc[1089]);
        }

        ModCompatibilityChecking();

        mod.IsReady = true;
    }

    void SetUpWeatherParticles()
    {
        //on first launch, get particle emitters
        if (snowParticle == null)
        {
            rainParticle = GameManager.Instance.WeatherManager.PlayerWeather.RainParticles.GetComponent<ParticleSystem>();
            rainParticleRenderer = rainParticle.GetComponent<ParticleSystemRenderer>();
            rainDefaultMaterial = rainParticleRenderer.sharedMaterial;
            rainParticleSplash = rainParticle.transform.GetChild(0).GetComponent<ParticleSystem>();
            rainParticleSplashRenderer = rainParticleSplash.GetComponent<ParticleSystemRenderer>();

            snowParticle = GameManager.Instance.WeatherManager.PlayerWeather.SnowParticles.GetComponent<ParticleSystem>();
            snowParticleRenderer = snowParticle.GetComponent<ParticleSystemRenderer>();
            snowDefaultMaterial = snowParticleRenderer.sharedMaterial;
            snowParticleSplash = snowParticle.transform.GetChild(0).GetComponent<ParticleSystem>();
            snowParticleSplashRenderer = snowParticleSplash.GetComponent<ParticleSystemRenderer>();

            rainPixelMaterial = new Material(shaderParticleEmissive);
            snowPixelMaterial = new Material(shaderParticleEmissive);
        }

        ParticleSystem.MainModule rainParticleMain = rainParticle.main;
        ParticleSystem.MainModule rainParticleSplashMain = rainParticleSplash.main;
        ParticleSystem.ColorOverLifetimeModule rainParticleColorOverLifetime = rainParticle.colorOverLifetime;
        ParticleSystem.MainModule snowParticleMain = snowParticle.main;
        ParticleSystem.MainModule snowParticleSplashMain = snowParticleSplash.main;
        ParticleSystem.ColorOverLifetimeModule snowParticleColorOverLifetime = snowParticle.colorOverLifetime;
        ParticleSystem.RotationOverLifetimeModule snowParticleRotationOverLifetime = snowParticle.rotationOverLifetime;
        ParticleSystem.RotationOverLifetimeModule snowParticleSplashRotationOverLifetime = snowParticleSplash.rotationOverLifetime;

        if (weather)
        {
            rainParticleSplash.gameObject.SetActive(weatherSplashRain);
            snowParticleSplash.gameObject.SetActive(weatherSplashSnow);

            if (weatherPixel)
            {
                if (weatherShader)
                {
                    rainPixelMaterial.shader = shaderParticleLit;
                    snowPixelMaterial.shader = shaderParticleLit;
                    Color x = new Color(weatherEmissionRain, weatherEmissionRain, weatherEmissionRain, 1);
                    Color y = new Color(weatherEmissionSnow, weatherEmissionSnow, weatherEmissionSnow, 1);
                    rainPixelMaterial.SetColor("_Emission", x);
                    snowPixelMaterial.SetColor("_Emission", y);
                }
                else
                {
                    rainPixelMaterial.shader = rainDefaultMaterial.shader;
                    snowPixelMaterial.shader = rainDefaultMaterial.shader;
                }

                rainPixelMaterial.SetTexture("_MainTex",null);
                snowPixelMaterial.SetTexture("_MainTex",null);

                Color a = new Color(1, 1, 1, 0.627f);
                Color b = new Color(1, 1, 1, 0.549f);

                rainParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 1f);
                rainParticleMain.startColor = new ParticleSystem.MinMaxGradient(a, b);

                snowParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                snowParticleMain.startRotation = new ParticleSystem.MinMaxCurve(0);
                snowParticleSplashMain.startSize = new ParticleSystem.MinMaxCurve(0.075f);
                snowParticleMain.startColor = new ParticleSystem.MinMaxGradient(Color.white);
                snowParticleColorOverLifetime.enabled = false;
                snowParticleRotationOverLifetime.enabled = false;
                snowParticleSplashRotationOverLifetime.enabled = false;
            }
            else
            {

                Texture defaultParticleTexture = snowDefaultMaterial.GetTexture("_MainTex");

                snowPixelMaterial.SetTexture("_MainTex", defaultParticleTexture);
                rainPixelMaterial.SetTexture("_MainTex", defaultParticleTexture);

                if (weatherShader)
                {
                    rainPixelMaterial.shader = shaderParticleLit;
                    snowPixelMaterial.shader = shaderParticleLit;
                    Color x = new Color(weatherEmissionRain, weatherEmissionRain, weatherEmissionRain, 1);
                    Color y = new Color(weatherEmissionSnow, weatherEmissionSnow, weatherEmissionSnow, 1);
                    rainPixelMaterial.SetColor("_Emission", x);
                    snowPixelMaterial.SetColor("_Emission", y);
                }
                else
                {
                    rainPixelMaterial.shader = rainDefaultMaterial.shader;
                    snowPixelMaterial.shader = rainDefaultMaterial.shader;
                }

                Color a = new Color(1, 1, 1, 0.627f);
                Color b = new Color(1, 1, 1, 0.549f);

                rainParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.32f, 2f);
                rainParticleMain.startColor = new ParticleSystem.MinMaxGradient(a, b);

                snowParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 1.0f);
                snowParticleMain.startRotation = new ParticleSystem.MinMaxCurve(0, 360);
                snowParticleSplashMain.startSize = new ParticleSystem.MinMaxCurve(0.08f);
                snowParticleMain.startColor = new ParticleSystem.MinMaxGradient(a, b);
                snowParticleColorOverLifetime.enabled = true;
                snowParticleRotationOverLifetime.enabled = true;
                snowParticleSplashRotationOverLifetime.enabled = true;
            }

            rainParticleRenderer.material = rainPixelMaterial;
            rainParticleSplashRenderer.material = rainPixelMaterial;
            snowParticleRenderer.material = snowPixelMaterial;
            snowParticleSplashRenderer.material = snowPixelMaterial;

            if (weatherShader)
                rainParticleRenderer.material.SetTexture("_MainTex", null);
            else
                rainParticleRenderer.material.SetTexture("_MainTex", rainDefaultMaterial.GetTexture("_MainTex"));
        }
        else
        {
            rainParticleSplash.gameObject.SetActive(true);
            snowParticleSplash.gameObject.SetActive(true);

            Color a = new Color(1, 1, 1, 0.627f);
            Color b = new Color(1, 1, 1, 0.549f);

            rainParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 1f);
            rainParticleMain.startColor = new ParticleSystem.MinMaxGradient(a, b);

            snowParticleMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 1.0f);
            snowParticleMain.startRotation = new ParticleSystem.MinMaxCurve(0, 360);
            snowParticleSplashMain.startSize = new ParticleSystem.MinMaxCurve(0.08f);
            snowParticleMain.startColor = new ParticleSystem.MinMaxGradient(a, b);
            snowParticleColorOverLifetime.enabled = true;
            snowParticleRotationOverLifetime.enabled = true;
            snowParticleSplashRotationOverLifetime.enabled = true;

            rainParticleRenderer.material = rainDefaultMaterial;
            rainParticleSplashRenderer.material = rainDefaultMaterial;
            snowParticleRenderer.material = snowDefaultMaterial;
            snowParticleSplashRenderer.material = snowDefaultMaterial;
        }
    }

    void ToggleImprovedExteriorLighting(bool setting)
    {
        if (playerAmbientLight == null)
            return;

        if (!ambientLightingInitialized)
        {
            ambientLightingInitialized = true;

            ExteriorNoonAmbientLightDefault = playerAmbientLight.ExteriorNoonAmbientLight;
            ExteriorNightAmbientLightDefault = playerAmbientLight.ExteriorNightAmbientLight;
            InteriorAmbientLightDefault = playerAmbientLight.InteriorAmbientLight;
            InteriorNightAmbientLightDefault = playerAmbientLight.InteriorNightAmbientLight;
            InteriorAmbientLight_AmbientOnlyDefault = playerAmbientLight.InteriorAmbientLight_AmbientOnly;
            InteriorNightAmbientLight_AmbientOnlyDefault = playerAmbientLight.InteriorNightAmbientLight_AmbientOnly;
            DungeonAmbientLightDefault = playerAmbientLight.DungeonAmbientLight;
            CastleAmbientLightDefault = playerAmbientLight.CastleAmbientLight;
        }

        if (setting)
        {
            playerAmbientLight.enabled = false;
            //GameManager.Instance.SunlightManager.IndirectLight.enabled = false;

            if (sunLight != null)
            {
                if (DaggerfallUnity.Settings.ExteriorLightShadows)
                {
                    if (sunShadowsHard)
                        sunLight.shadows = LightShadows.Hard;
                    else
                        sunLight.shadows = LightShadows.Soft;
                }
                else
                    sunLight.shadows = LightShadows.None;
            }

            //set colors to scaled
            playerAmbientLight.ExteriorNoonAmbientLight = ExteriorNoonAmbientLightDefault * ambientLightExteriorDayScale;
            playerAmbientLight.ExteriorNightAmbientLight = ExteriorNightAmbientLightDefault * ambientLightExteriorNightScale;
            playerAmbientLight.InteriorAmbientLight = InteriorAmbientLightDefault * ambientLightInteriorDayScale;
            playerAmbientLight.InteriorNightAmbientLight = InteriorNightAmbientLightDefault * ambientLightInteriorNightScale;
            playerAmbientLight.InteriorAmbientLight_AmbientOnly = InteriorAmbientLight_AmbientOnlyDefault * ambientLightInteriorDayScale;
            playerAmbientLight.InteriorNightAmbientLight_AmbientOnly = InteriorNightAmbientLight_AmbientOnlyDefault * ambientLightInteriorNightScale;
            playerAmbientLight.DungeonAmbientLight = DungeonAmbientLightDefault * ambientLightDungeonScale;
            playerAmbientLight.CastleAmbientLight = CastleAmbientLightDefault * ambientLightCastleScale;
        }
        else
        {
            playerAmbientLight.enabled = true;
            //GameManager.Instance.SunlightManager.IndirectLight.enabled = true;

            if (sunLight != null)
            {
                if (DaggerfallUnity.Settings.ExteriorLightShadows)
                    sunLight.shadows = LightShadows.Soft;
                else
                    sunLight.shadows = LightShadows.None;
            }

            //set colors to default
            playerAmbientLight.ExteriorNoonAmbientLight = ExteriorNoonAmbientLightDefault;
            playerAmbientLight.ExteriorNightAmbientLight = ExteriorNightAmbientLightDefault;
            playerAmbientLight.InteriorAmbientLight = InteriorAmbientLightDefault;
            playerAmbientLight.InteriorNightAmbientLight = InteriorNightAmbientLightDefault;
            playerAmbientLight.InteriorAmbientLight_AmbientOnly = InteriorAmbientLight_AmbientOnlyDefault;
            playerAmbientLight.InteriorNightAmbientLight_AmbientOnly = InteriorNightAmbientLight_AmbientOnlyDefault;
            playerAmbientLight.DungeonAmbientLight = DungeonAmbientLightDefault;
            playerAmbientLight.CastleAmbientLight = CastleAmbientLightDefault;
        }
    }

    void SpawnShearingCamera()
    {
        Camera mainCamera = GameManager.Instance.MainCamera;
        PostProcessLayer mainPostprocessingLayer = mainCamera.gameObject.GetComponent<PostProcessLayer>();

        var go = new GameObject(mod.Title + " - Shearing Camera");

        //disable the gameobject to prevent post-processing layer from shitting itself immediately after being copied
        go.SetActive(false);

        shearingEye = go.AddComponent<Camera>();
        shearingEye.transform.SetParent(mainCamera.transform.parent);
        shearingEye.enabled = false;

        shearingEye.CopyFrom(mainCamera);
        shearingEye.depth = 0;

        shearingPostprocessingLayer = CopyComponent<PostProcessLayer>(mainPostprocessingLayer,go);
        shearingPostprocessingLayer.volumeTrigger = shearingEye.transform;

        //enable the gameobject after adding in the post-processing layer
        go.SetActive(true);
    }

    void ToggleShearingCamera(bool state)
    {
        Camera mainCamera = GameManager.Instance.MainCamera;
        PostProcessLayer mainPostprocessingLayer = mainCamera.gameObject.GetComponent<PostProcessLayer>();

        shearingEye.enabled = state;

        if (state)
        {
            shearingEye.gameObject.tag = "MainCamera";
            mainCamera.gameObject.tag = "Untagged";

            //put the mainCamera behind the sky camera
            mainCamera.depth = -4;
            sky.SkyCamera.depth = -3;

            //set variables
            shearingEye.CopyFrom(mainCamera);
            shearingEye.depth = 0;

            //update anti-aliasing mode?
            //shearingPostprocessingLayer = CopyComponent<PostProcessLayer>(mainPostprocessingLayer, shearingEye.gameObject);
            shearingPostprocessingLayer.antialiasingMode = mainPostprocessingLayer.antialiasingMode;
            shearingPostprocessingLayer.fastApproximateAntialiasing = mainPostprocessingLayer.fastApproximateAntialiasing;
            shearingPostprocessingLayer.subpixelMorphologicalAntialiasing = mainPostprocessingLayer.subpixelMorphologicalAntialiasing;
            shearingPostprocessingLayer.temporalAntialiasing = mainPostprocessingLayer.temporalAntialiasing;

            if (DynamicSkies != null)
            {
                shearingEye.clearFlags = CameraClearFlags.Skybox;
            }

            //DaggerfallSkyMainCamera.SetValue(sky, shearingEye);

            DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = false;
        }
        else
        {
            mainCamera.gameObject.tag = "MainCamera";
            shearingEye.gameObject.tag = "Untagged";

            //main camera depth is 3 in case Distant Terrain is installed
            mainCamera.depth = 0;
            sky.SkyCamera.depth = -3;

            //DaggerfallSkyMainCamera.SetValue(sky, mainCamera);

            DaggerfallUI.Instance.DaggerfallHUD.ShowCrosshair = DaggerfallUnity.Settings.Crosshair;
        }
    }

    void UpdateShearingCamera()
    {
        //copy main camera viewport rect and such
        Camera mainCamera = GameManager.Instance.MainCamera;
        shearingEye.rect = mainCamera.rect;
        shearingEye.nearClipPlane = mainCamera.nearClipPlane;
        shearingEye.farClipPlane = mainCamera.farClipPlane;
        //shearingEye.fieldOfView = mainCamera.fieldOfView;

        //copy world position
        shearingEye.transform.position = mainCamera.transform.position;

        //get Y world rotation
        shearingEye.transform.eulerAngles = new Vector3(0,mainCamera.transform.eulerAngles.y, mainCamera.transform.eulerAngles.z);

        //derive pitch angle
        float angle = Vector3.SignedAngle(shearingEye.transform.forward, mainCamera.transform.forward, shearingEye.transform.right);

        Matrix4x4 mat = shearingEye.projectionMatrix;
        mat[1, 2] = -angle / (shearingEye.fieldOfView/2f);
        shearingEye.projectionMatrix = mat;
    }

    void UpdateShearingCrosshair()
    {
        if (isShearing)
        {
            Camera mainCamera = GameManager.Instance.MainCamera;
            Vector3 crosshairPoint = mainCamera.transform.position + mainCamera.transform.forward * 5;
            crosshairOffset = shearingEye.WorldToViewportPoint(crosshairPoint);
            crosshairOffset.y -= 0.5f;
        }
        else
            crosshairOffset = new Vector3(0,0,0);
    }

    private void OnGUI()
    {
        // Do not draw crosshair when cursor is active - i.e. player is now using mouse to point and click not crosshair target
        if (GameManager.Instance.PlayerMouseLook.cursorActive || GameManager.IsGamePaused || !DaggerfallUI.Instance.DaggerfallHUD.Enabled)
            return;

        if (shearing)
        {
            GUI.depth = 0;

            if (DaggerfallUI.Instance.CustomScreenRect != null)
                screenRect = DaggerfallUI.Instance.CustomScreenRect.Value;
            else
                screenRect = new Rect(0, 0, Screen.width, Screen.height);

            float screenScaleY = (float)screenRect.height / nativeScreenHeight;
            float screenScaleX = (float)screenRect.width / nativeScreenWidth;

            //Vector2 crosshairTextureScale = new Vector2(CrosshairTexture.width * screenScaleX, CrosshairTexture.height * screenScaleY);
            //Vector2 crosshairTextureScale = new Vector2(CrosshairTexture.width, CrosshairTexture.height) * crosshairSize;
            Vector2 crosshairTextureScale = crosshairSize * DaggerfallUI.Instance.DaggerfallHUD.CrosshairScale;

            float HUDHeight = 0;
            float LargeHUDMult = 1;
            if (DaggerfallUI.Instance.DaggerfallHUD != null &&
                DaggerfallUnity.Settings.LargeHUD &&
                (DaggerfallUnity.Settings.LargeHUDUndockedOffsetWeapon || DaggerfallUnity.Settings.LargeHUDDocked))
            {
                HUDHeight = DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight;
                LargeHUDMult = DaggerfallUI.Instance.DaggerfallHUD.LargeHUD.ScreenHeight / screenRect.height;
            }

            Rect crosshairRect = new Rect(
                screenRect.x + (screenRect.width * 0.5f) - (crosshairTextureScale.x * 0.5f),
                screenRect.y + (screenRect.height * 0.5f) - (HUDHeight * 0.5f) - (crosshairTextureScale.y * 0.5f) - (screenRect.height * crosshairOffset.y),
                crosshairTextureScale.x,
                crosshairTextureScale.y
                );

            if (!shearingCrosshairAlwaysVisible && !DaggerfallUnity.Settings.Crosshair)
                return;

            DaggerfallUI.DrawTexture(crosshairRect, CrosshairTexture, ScaleMode.StretchToFill, false, Color.white);
        }
    }

    T CopyComponent<T>(T original, GameObject destination) where T : Component
    {
        System.Type type = original.GetType();
        Component copy = destination.AddComponent(type);
        System.Reflection.FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (System.Reflection.FieldInfo field in fields)
        {
            field.SetValue(copy, field.GetValue(original));
        }
        return copy as T;
    }

    public void StartToggleShearingCameraDelayed(bool state)
    {
        StartCoroutine(ToggleShearingCameraDelayed(state));
    }

    IEnumerator ToggleShearingCameraDelayed(bool state, float delay = 0.1f)
    {
        yield return new WaitForSeconds(delay);

        ToggleShearingCamera(state);
    }

    public static void ToggleShearingCamera_OnLoad(SaveData_v1 saveData)
    {
        Instance.StartToggleShearingCameraDelayed(Instance.shearing);
    }

    public static void ToggleShearingCamera_OnTransition(PlayerEnterExit.TransitionEventArgs args)
    {
        Instance.StartToggleShearingCameraDelayed(Instance.shearing);
    }

    private void LateUpdate()
    {
        if (shearing)
        {
            if (GameManager.Instance.IsPlayingGame())
            {
                if (InputManager.Instance.GetKeyUp(shearingKeyCode))
                    isShearing = !isShearing;

                if (isShearing)
                {
                    if (!Instance.shearingEye.enabled)
                        ToggleShearingCamera(shearing);

                    UpdateShearingCamera();
                }
                else
                {
                    if (Instance.shearingEye.enabled)
                        ToggleShearingCamera(shearing);
                }

                UpdateShearingCrosshair();
            }
        }
        else
        {
            if (GameManager.Instance.IsPlayingGame())
            {
                if (Instance.shearingEye.enabled)
                    ToggleShearingCamera(shearing);
            }
        }

        if (ambientLighting && sunLight != null)
        {
            if (GameManager.Instance.IsPlayingGame())
            {
                Color skyColor = Color.white;

                if (lightColor == 1 || DynamicSkies != null)
                    skyColor = RenderSettings.fogColor;
                else
                    skyColor = sky.skyColors.west[Mathf.RoundToInt(sky.skyColors.west.Length / 2)];

                lastCameraClearColor = Scale(skyColor, 0.5f * LightColorScale, 2 * sunLightScale);
                if (sunLight.color != lastCameraClearColor)
                    sunLight.color = lastCameraClearColor;

                //mess with ambient light
                if (playerAmbientLight != null)
                {
                    UpdateAmbientLight();

                    if (!playerEnterExit.IsPlayerInside)
                    {
                        if (worldTime.Now.IsNight)
                        {
                            //always use sky color at night
                            if (DynamicSkies == null)
                                skyColor = sky.skyColors.west[Mathf.RoundToInt(sky.skyColors.west.Length / 2)];
                            else
                                skyColor = Color.blue * 0.25f;

                            //check for moon phase
                            if (moonPhaseMassar == LunarPhases.None || moonPhaseSecunda == LunarPhases.None)
                                UpdateMoonPhases();

                            lastAmbientColor *= Scale(skyColor, 0.5f * LightColorScale, 10f * MoonLightScale);
                        }
                        else
                        {
                            moonPhaseMassar = LunarPhases.None;
                            moonPhaseSecunda = LunarPhases.None;

                            lastAmbientColor *= Scale(skyColor, 0.5f * LightColorScale, 0.5f);
                        }
                    }
                    else
                    {
                        /*if (playerEnterExit.IsPlayerInsideDungeon || worldTime.Now.IsNight)
                            skyColor = Color.blue * 0.25f;
                        else
                            skyColor = Color.blue * 0.25f;*/
                        if (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle)
                            skyColor = GetDungeonFogColor() * 0.25f;
                        else
                            skyColor = GetBuildingFogColor() * 0.25f;
                        lastAmbientColor *= Scale(skyColor, 0.5f * LightColorScale, 5f);
                    }

                    if (RenderSettings.ambientLight != lastAmbientColor)
                    {
                        RenderSettings.ambientLight = Vector4.MoveTowards(RenderSettings.ambientLight, lastAmbientColor, 1f * Time.deltaTime);
                        //GameManager.Instance.SunlightManager.IndirectLight.color = RenderSettings.ambientLight;
                    }
                }
            }
        }
    }

    public void ResetMoonPhases()
    {
        moonPhaseMassar = LunarPhases.None;
        moonPhaseSecunda = LunarPhases.None;
    }

    public void UpdateMoonPhases()
    {
        moonPhaseMassar = DaggerfallUnity.Instance.WorldTime.Now.MassarLunarPhase;
        moonPhaseSecunda = DaggerfallUnity.Instance.WorldTime.Now.SecundaLunarPhase;

        float scaleMassar = 1;
        if (moonPhaseMassar == LunarPhases.New)
            scaleMassar = 0.2f;
        else if (moonPhaseMassar == LunarPhases.OneWax || moonPhaseMassar == LunarPhases.OneWane)
            scaleMassar = 0.4f;
        else if (moonPhaseMassar == LunarPhases.ThreeWax || moonPhaseMassar == LunarPhases.ThreeWane)
            scaleMassar = 0.6f;
        else if (moonPhaseMassar == LunarPhases.HalfWax || moonPhaseMassar == LunarPhases.HalfWane)
            scaleMassar = 0.8f;

        float scaleSecunda = 1;
        if (moonPhaseSecunda == LunarPhases.New)
            scaleSecunda = 0.2f;
        else if (moonPhaseSecunda == LunarPhases.OneWax || moonPhaseSecunda == LunarPhases.OneWane)
            scaleSecunda = 0.4f;
        else if (moonPhaseSecunda == LunarPhases.ThreeWax || moonPhaseSecunda == LunarPhases.ThreeWane)
            scaleSecunda = 0.6f;
        else if (moonPhaseSecunda == LunarPhases.HalfWax || moonPhaseSecunda == LunarPhases.HalfWane)
            scaleSecunda = 0.8f;

        moonPhaseScale = (scaleMassar + scaleSecunda)/2;
    }

    public void UpdateAmbientLight()
    {
        if (!playerEnterExit)
            return;

        if (!playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon)
        {
            lastAmbientColor = CalcDaytimeAmbientLight();
        }
        else if (playerEnterExit.IsPlayerInside && !playerEnterExit.IsPlayerInsideDungeon)
        {
            if (worldTime.Now.IsNight)
                lastAmbientColor = (DaggerfallUnity.Settings.AmbientLitInteriors) ? playerAmbientLight.InteriorNightAmbientLight_AmbientOnly : playerAmbientLight.InteriorNightAmbientLight;
            else
                lastAmbientColor = (DaggerfallUnity.Settings.AmbientLitInteriors) ? playerAmbientLight.InteriorAmbientLight_AmbientOnly : playerAmbientLight.InteriorAmbientLight;
        }
        else if (playerEnterExit.IsPlayerInside && playerEnterExit.IsPlayerInsideDungeon)
        {
            if (playerEnterExit.IsPlayerInsideDungeonCastle)
                lastAmbientColor = playerAmbientLight.CastleAmbientLight;
            else if (playerEnterExit.IsPlayerInsideSpecialArea)
                lastAmbientColor = playerAmbientLight.SpecialAreaLight;
            else
                lastAmbientColor = playerAmbientLight.DungeonAmbientLight;
        }
    }

    Color Scale(Color color, float saturation, float brightness)
    {
        float h;
        float s;
        float v;

        Color.RGBToHSV(color, out h, out s, out v);

        s *= saturation;
        v *= brightness;

        return Color.HSVToRGB(h,s,v);
    }

    Color CalcDaytimeAmbientLight()
    {
        float scale = GameManager.Instance.SunlightManager.DaylightScale * GameManager.Instance.SunlightManager.ScaleFactor;

        float weather = 1;

        /*// Apply rain, storm, snow light scale
        if (weatherManager.IsRaining && !weatherManager.IsStorming)
        {
            weather = RainSunlightScaleDefault;
        }
        else if (weatherManager.IsRaining && weatherManager.IsStorming)
        {
            weather = StormSunlightScaleDefault;
        }
        else if (weatherManager.IsSnowing)
        {
            weather = SnowSunlightScaleDefault;
        }
        else if (weatherManager.IsOvercast)
        {
            weather = OvercastSunlightScaleDefault;
        }*/

        Color startColor = playerAmbientLight.ExteriorNightAmbientLight * weather;

        return Color.Lerp(startColor, playerAmbientLight.ExteriorNoonAmbientLight * weather, scale);
    }

    void ResetViewDistance()
    {
        GameManager.Instance.MainCamera.farClipPlane = 2600;

        bool updateWorld = false;
        if (GameManager.Instance.StreamingWorld.TerrainDistance != DaggerfallUnity.Settings.TerrainDistance)
            updateWorld = true;

        GameManager.Instance.StreamingWorld.TerrainDistance = DaggerfallUnity.Settings.TerrainDistance;

        if (updateWorld)
            ForceUpdateWorld();
    }

    void ResetFog()
    {
        weatherManager.SunnyFogSettings = SunnyFogSettingsDefault;
        weatherManager.OvercastFogSettings = OvercastFogSettingsDefault;
        weatherManager.RainyFogSettings = RainyFogSettingsDefault;
        weatherManager.SnowyFogSettings = SnowyFogSettingsDefault;
        weatherManager.HeavyFogSettings = HeavyFogSettingsDefault;
        weatherManager.InteriorFogSettings = InteriorFogSettingsDefault;
        weatherManager.DungeonFogSettings = DungeonFogSettingsDefault;

        //Reset the current weather for the fog settings to take effect
        weatherManager.SetWeather(weatherManager.PlayerWeather.WeatherType);
    }

    Color GetDungeonFogColor()
    {
        DFRegion.DungeonTypes dungeonType = playerEnterExit.Dungeon.Summary.DungeonType;

        Color fogColor = Color.black;

        switch (dungeonType)
        {
            case DFRegion.DungeonTypes.VolcanicCaves:
            case DFRegion.DungeonTypes.DragonsDen:
            case DFRegion.DungeonTypes.Coven:
            case DFRegion.DungeonTypes.DesecratedTemple:
            case DFRegion.DungeonTypes.ScorpionNest:
                fogColor = Color.red;
                break;
            case DFRegion.DungeonTypes.SpiderNest:
            case DFRegion.DungeonTypes.Laboratory:
            case DFRegion.DungeonTypes.Prison:
                fogColor = Color.green;
                break;
            case DFRegion.DungeonTypes.BarbarianStronghold:
            case DFRegion.DungeonTypes.GiantStronghold:
            case DFRegion.DungeonTypes.HumanStronghold:
            case DFRegion.DungeonTypes.OrcStronghold:
                fogColor = new Color(1,0.5f,0); //orange
                break;
            case DFRegion.DungeonTypes.Cemetery:
            case DFRegion.DungeonTypes.Crypt:
            case DFRegion.DungeonTypes.VampireHaunt:
            case DFRegion.DungeonTypes.RuinedCastle:
                fogColor = Color.blue;
                break;
            case DFRegion.DungeonTypes.Mine:
            case DFRegion.DungeonTypes.NaturalCave:
            case DFRegion.DungeonTypes.HarpyNest:
                fogColor = Color.white;
                break;
        }

        return Scale(fogColor, fogDungeonColorScale, 1f);
    }

    Color GetBuildingFogColor()
    {
        DFLocation.BuildingTypes buildingType = playerEnterExit.BuildingType;

        Color fogColor = Color.white;

        if (worldTime.Now.IsNight)
            fogColor = Color.blue;
        else
        {
            /*switch (buildingType)
            {
                case DFLocation.BuildingTypes.WeaponSmith:
                    fogColor = Color.red;
                    break;
                case DFLocation.BuildingTypes.Alchemist:
                    fogColor = Color.green;
                    break;
                case DFLocation.BuildingTypes.Tavern:
                    fogColor = new Color(1, 0.5f, 0); //orange
                    break;
                case DFLocation.BuildingTypes.Temple:
                    fogColor = Color.blue;
                    break;
                case DFLocation.BuildingTypes.GeneralStore:
                    fogColor = Color.white;
                    break;
            }*/
        }

        return Scale(fogColor, fogInteriorColorScale, 1f);
    }

    void ApplyFog(FogMode fogMode)
    {
        if (!fog)
            return;

        if (currentFogMode != fogMode)
        {
            currentFogMode = fogMode;
            SunnyFogSettings.fogMode = currentFogMode;
            OvercastFogSettings.fogMode = currentFogMode;
            RainyFogSettings.fogMode = currentFogMode;
            SnowyFogSettings.fogMode = currentFogMode;
            HeavyFogSettings.fogMode = currentFogMode;
            InteriorFogSettings.fogMode = currentFogMode;
            DungeonFogSettings.fogMode = currentFogMode;
        }

        float distance = GameManager.Instance.MainCamera.farClipPlane;

        if (fogMode == FogMode.Linear)
        {
            float fogStartDistance = distance * fogStartDistanceMultiplier;
            float fogEndDistance = distance * fogEndDistanceMultiplier;

            float multiplier = distance / 64;

            SunnyFogSettings.startDistance = fogStartDistance - (2 * multiplier);
            SunnyFogSettings.endDistance = fogEndDistance - (2 * multiplier);

            OvercastFogSettings.startDistance = fogStartDistance - (4 * multiplier);
            OvercastFogSettings.endDistance = fogEndDistance - (4 * multiplier);

            RainyFogSettings.startDistance = fogStartDistance - (8 * multiplier);
            RainyFogSettings.endDistance = fogEndDistance - (8 * multiplier);

            SnowyFogSettings.startDistance = fogStartDistance - (16 * multiplier);
            SnowyFogSettings.endDistance = fogEndDistance - (16 * multiplier);

            HeavyFogSettings.startDistance = fogStartDistance - (32 * multiplier);
            HeavyFogSettings.endDistance = fogEndDistance - (32 * multiplier);

            InteriorFogSettings.startDistance = fogStartDistance - (4 * multiplier);
            InteriorFogSettings.endDistance = fogEndDistance - (4 * multiplier);

            DungeonFogSettings.startDistance = fogStartDistance - (8 * multiplier);
            DungeonFogSettings.endDistance = fogEndDistance - (8 * multiplier);
        }

        if (fogMode == FogMode.Exponential || fogMode == FogMode.ExponentialSquared)
        {
            //default fog density is 0.0005, too thin for my taste
            float density = 0.001f * fogDensityModifier;
            if (view)
            {
                if (terrainDistanceOverride == 0)
                {
                    SunnyFogSettings.density = fogDensities[viewDistance] * fogDensityModifier * 1;
                    OvercastFogSettings.density = fogDensities[viewDistance] * fogDensityModifier * 2f;
                    RainyFogSettings.density = fogDensities[viewDistance] * fogDensityModifier * 3;
                    SnowyFogSettings.density = fogDensities[viewDistance] * fogDensityModifier * 3;
                    HeavyFogSettings.density = fogDensities[viewDistance] * fogDensityModifier * 4;
                }
                else
                {
                    SunnyFogSettings.density = fogDensities[7] * fogDensityModifier * 1;
                    OvercastFogSettings.density = fogDensities[7] * fogDensityModifier * 2f;
                    RainyFogSettings.density = fogDensities[7] * fogDensityModifier * 3;
                    SnowyFogSettings.density = fogDensities[7] * fogDensityModifier * 3;
                    HeavyFogSettings.density = fogDensities[7] * fogDensityModifier * 4;
                }
                InteriorFogSettings.density = fogDensities[viewDistanceInterior] * fogDensityModifier * 1;
                DungeonFogSettings.density = fogDensities[viewDistanceDungeon] * fogDensityModifier * 2;
            }
            else
            {
                SunnyFogSettings.density = density * 1;
                OvercastFogSettings.density = density * 2f;
                RainyFogSettings.density = density * 3;
                SnowyFogSettings.density = density * 3;
                HeavyFogSettings.density = density * 4;
                InteriorFogSettings.density = density * 2;
                DungeonFogSettings.density = density * 4;
            }
        }

        weatherManager.SunnyFogSettings = SunnyFogSettings;
        weatherManager.OvercastFogSettings = OvercastFogSettings;
        weatherManager.RainyFogSettings = RainyFogSettings;
        weatherManager.SnowyFogSettings = SnowyFogSettings;
        weatherManager.HeavyFogSettings = HeavyFogSettings;
        weatherManager.DungeonFogSettings = DungeonFogSettings;
        weatherManager.InteriorFogSettings = InteriorFogSettings;

        //Reset the current weather for the fog settings to take effect
        weatherManager.SetWeather(weatherManager.PlayerWeather.WeatherType);

        // then check if player is inside and set fog accordingly
        if (playerEnterExit.IsPlayerInsideBuilding)
        {
            weatherManager.SetFog(InteriorFogSettings, true);
            if (fogInteriorColored)
                GameManager.Instance.MainCamera.backgroundColor = GetBuildingFogColor() * fogInteriorBrightness;
            else
                GameManager.Instance.MainCamera.backgroundColor = Color.black;
            RenderSettings.fogColor = GameManager.Instance.MainCamera.backgroundColor;
        }
        else if (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle)
        {
            weatherManager.SetFog(DungeonFogSettings, true);
            if (fogDungeonColored)
                GameManager.Instance.MainCamera.backgroundColor = GetDungeonFogColor() * fogDungeonBrightness;
            else
                GameManager.Instance.MainCamera.backgroundColor = Color.black;
            RenderSettings.fogColor = GameManager.Instance.MainCamera.backgroundColor;
        }
    }

    void ApplyViewDistance()
    {
        if (!view)
            return;

        bool updateWorld = false;

        if (terrainDistanceOverride == 0)
        {
            //Terrain Distance is handled automatically
            if (GameManager.Instance.StreamingWorld.TerrainDistance != terrainDistances[viewDistance])
                updateWorld = true;

            GameManager.Instance.StreamingWorld.TerrainDistance = terrainDistances[viewDistance];
        }
        else
        {
            int terrainDistance = 4 + terrainDistanceOverride;
            if (GameManager.Instance.StreamingWorld.TerrainDistance != terrainDistance)
                updateWorld = true;

            GameManager.Instance.StreamingWorld.TerrainDistance = terrainDistance;
        }

        if (GameManager.Instance.PlayerEnterExit.IsPlayerInside)
        {
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon || GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeonCastle)
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistanceDungeon];
            else
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistanceInterior];
        }
        else
        {
            if (terrainDistanceOverride == 0)
            {
                //use view distance slider if terrain distance override is disabled
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistance];
            }
            else
            {
                float viewDistanceOverride = (4f + terrainDistanceOverride) * 819.2f;
                GameManager.Instance.MainCamera.farClipPlane = viewDistanceOverride;
            }
            if (updateWorld)
                ForceUpdateWorld();
        }
    }

    void ForceUpdateWorld()
    {
        Debug.Log("TAIL OF ARGONIA - FORCE UPDATE WORLD");
        if (mod.IsReady && worldUpdateMessage == null)
        {
            worldUpdateMessage = ForceUpdateWorldCoroutine();
            StartCoroutine(worldUpdateMessage);
        }
    }

    IEnumerator ForceUpdateWorldCoroutine()
    {
        while (!GameManager.Instance.IsPlayingGame())
            yield return new WaitForSeconds(1);

        DaggerfallUI.MessageBox("TerrainDistance setting has changed. Reload a save or travel to another location to apply the new setting.",true);

        worldUpdateMessage = null;
    }

    public void SetDefaultViewDistance()
    {
        if (!view)
            return;

        GameManager.Instance.MainCamera.farClipPlane = 2600;
    }

    public void SetCustomViewDistance()
    {
        if (!view)
            return;

        if (playerEnterExit.IsPlayerInside)
        {
            if (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle)
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistanceDungeon];
            else
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistanceInterior];
        }
        else
        {

            if (terrainDistanceOverride == 0)
            {
                //use view distance slider if terrain distance override is disabled
                GameManager.Instance.MainCamera.farClipPlane = viewDistances[viewDistance];
            }
            else
            {
                float viewDistanceOverride = (4f + terrainDistanceOverride) * 819.2f;
                GameManager.Instance.MainCamera.farClipPlane = viewDistanceOverride;
            }
        }
    }

    public void ApplyFog()
    {
        if (!fog)
            return;

        if (playerEnterExit.IsPlayerInside)
        {
            GameManager.Instance.MainCamera.clearFlags = CameraClearFlags.SolidColor;
            if (playerEnterExit.IsPlayerInsideDungeon || playerEnterExit.IsPlayerInsideDungeonCastle)
            {
                if (fogDungeonColored)
                    GameManager.Instance.MainCamera.backgroundColor = GetDungeonFogColor() * fogDungeonBrightness;
                else
                    GameManager.Instance.MainCamera.backgroundColor = Color.black;
                RenderSettings.fogColor = GameManager.Instance.MainCamera.backgroundColor;
            }
            else
            {
                if (fogInteriorColored)
                    GameManager.Instance.MainCamera.backgroundColor = GetBuildingFogColor() * fogInteriorBrightness;
                else
                    GameManager.Instance.MainCamera.backgroundColor = Color.black;
                RenderSettings.fogColor = GameManager.Instance.MainCamera.backgroundColor;
            }
        }
        else
            GameManager.Instance.MainCamera.clearFlags = CameraClearFlags.Depth;
    }

    public static void ApplyViewDistance_OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
    {
        Instance.SetCustomViewDistance();
        Instance.ApplyFog();
        Instance.ResetMoonPhases();
    }

    public static void ApplyViewDistance_OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
    {
        Instance.SetCustomViewDistance();
        Instance.ApplyFog();
    }

    public static void ApplyViewDistance_OnLoad(SaveData_v1 saveData)
    {
        Instance.SetCustomViewDistance();
        Instance.ApplyFog();
        Instance.ResetMoonPhases();
    }

    public static void ApplyFadeShader_OnUpdateTerrainsEnd()
    {
        Instance.ApplyFadeShaderDelayed();
    }

    public static void ApplyFadeShader_OnTransitionExterior (PlayerEnterExit.TransitionEventArgs args)
    {
        Instance.ApplyFadeShaderDelayed();
    }

    public static void ApplyFadeShader_OnLoad(SaveData_v1 saveData)
    {
        Instance.ApplyFadeShaderDelayed(1);
    }

    public static void ApplyFadeShader_OnPostFastTravel()
    {
        Instance.ApplyFadeShaderDelayed();
        Instance.ResetMoonPhases();
    }

    public static void ApplyFadeShader_OnCityLights()
    {
        Instance.ApplyFadeShaderDelayed();
    }

    public void ApplyFadeShaderDelayed(float delay = 0.1f)
    {
        StartCoroutine(ApplyFadeShaderCoroutine(delay));
    }

    public static void ApplyFadeShader_OnEnemySpawn(GameObject enemy)
    {
        Instance.ApplyFadeShaderToEntityDelayed(enemy);
    }

    public static void ApplyFadeShader_OnMobileNPCCreate(PopulationManager.PoolItem poolItem)
    {
        Instance.ApplyFadeShaderToEntityDelayed(poolItem.npc.gameObject);
    }

    public void ApplyFadeShaderToEntityDelayed(GameObject entity)
    {
        StartCoroutine(ApplyFadeShaderToEntityCoroutine(entity));
    }

    IEnumerator ApplyFadeShaderToEntityCoroutine(GameObject entity)
    {
        yield return new WaitForSeconds(0.1f);

        ApplyFadeShaderToEntity(entity);
    }

    void ApplyFadeShaderToEntity(GameObject entity)
    {
        MeshRenderer renderer = entity.GetComponentInChildren<MeshRenderer>(true);

        if (renderer == null)
            return;

        Material[] materials = renderer.materials;
        for (int i = 0; i < renderer.materials.Length; i++)
        {
            Material material = renderer.materials[i];
            if (material.shader.name == "Daggerfall/Billboard" || material.shader == shaderBillboard)
            {
                Color color = material.GetColor("_Color");
                float scaleCutoff = material.GetFloat("_Cutoff");
                Texture texMain = material.GetTexture("_MainTex");
                Texture texBump = material.GetTexture("_BumpMap");
                Texture texEmission = material.GetTexture("_EmissionMap");
                Color colorEmission = material.GetColor("_EmissionColor");

                Material newMat = new Material(Shader.Find("Daggerfall/Billboard"));
                newMat.name = material.name;
                newMat.SetColor("_Color", color);
                newMat.SetFloat("_Cutoff", scaleCutoff);
                newMat.SetTexture("_MainTex", texMain);
                newMat.SetTexture("_BumpMap", texBump);
                newMat.SetTexture("_EmissionMap", texEmission);
                newMat.SetColor("_EmissionColor", colorEmission);
                if (material.IsKeywordEnabled(KeyWords.Emission))
                    newMat.EnableKeyword(KeyWords.Emission);

                materials[i] = newMat;
            }
        }
    }

    IEnumerator ApplyFadeShaderCoroutine(float delay = 0.1f)
    {
        yield return new WaitForSeconds(delay);

        ApplyFadeShader();
    }

    public void RemoveFadeShader()
    {
        if (Time.time - smoothClipLastUpdate < 2)
            return;

        smoothClipLastUpdate = Time.time;

        if (debugShowMessages)
        {
            Debug.Log("TAIL OF ARGONIA - Removing fade shader!");
            DaggerfallUI.Instance.PopupMessage("Removing fade shader!");
        }

        MeshRenderer[] meshrenderers = FindObjectsOfType<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedmeshrenderers = FindObjectsOfType<SkinnedMeshRenderer>();
        Renderer[] renderers = new Renderer[meshrenderers.Length + skinnedmeshrenderers.Length];
        for (int i = 0; i < meshrenderers.Length; i++)
        {
            renderers[i] = meshrenderers[i];
        }
        for (int i = 0; i < skinnedmeshrenderers.Length; i++)
        {
            renderers[i + meshrenderers.Length] = skinnedmeshrenderers[i];
        }

        //Debug.Log("TAIL OF ARGONIA - INITIALIZING OBJECT SMOOTH CLIPPING");
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                Material material = renderer.materials[i];
                if (material.shader == shaderDefault)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A DEFAULT MATERIAL");
                    Color color = material.GetColor("_Color");
                    Color colorSpec = material.GetColor("_SpecColor");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Texture texParallax = material.GetTexture("_ParallaxMap");
                    float scaleParallax = material.GetFloat("_Parallax");
                    Texture texMetallic = material.GetTexture("_MetallicGlossMap");
                    float scaleSmoothness = material.GetFloat("_Smoothness");

                    Material newMat = new Material(Shader.Find("Daggerfall/Default"));
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetColor("_SpecColor", colorSpec);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetTexture("_ParallaxMap", texParallax);
                    newMat.SetFloat("_Parallax", scaleParallax);
                    newMat.SetTexture("_MetallicGlossMap", texMetallic);
                    newMat.SetFloat("_Smoothness", scaleSmoothness);

                    materials[i] = newMat;
                }
                else if (material.shader == shaderBillboard)
                {
                    Billboard billboard = renderer.GetComponent<Billboard>();
                    /*if (billboard != null)
                    {
                        if (billboard.Summary.IsMobile)
                            continue;
                    }*/
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");

                    Material newMat = new Material(Shader.Find("Daggerfall/Billboard"));
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);

                    materials[i] = newMat;
                }
                else if (material.shader == shaderBillboardBatch)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD BATCH MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Vector4 vectorUp = material.GetVector("_UpVector");

                    Material newMat = new Material(Shader.Find("Daggerfall/BillboardBatch"));
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetVector("_UpVector", vectorUp);

                    materials[i] = newMat;
                }
                else if (material.shader == shaderBillboardBatchNoShadows)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD BATCH NO SHADOWS MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Vector4 vectorUp = material.GetVector("_UpVector");

                    Material newMat = new Material(Shader.Find("Daggerfall/BillboardBatchNoShadows"));
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetVector("_UpVector", vectorUp);

                    materials[i] = newMat;
                }
            }
            renderer.materials = materials;
        }

        Terrain[] terrains = FindObjectsOfType<Terrain>();

        foreach (Terrain terrain in terrains)
        {
            //if (SystemInfo.supports2DArrayTextures && DaggerfallUnity.Settings.EnableTextureArrays && terrain.materialTemplate.shader == shaderTilemapTextureArray)
            if (terrain.materialTemplate.shader == shaderTilemapTextureArray)
            {
                Texture tileTextureArray = terrain.materialTemplate.GetTexture("_TileTexArr");
                Texture tileNormalMapTextureArray = terrain.materialTemplate.GetTexture("_TileNormalMapTexArr");
                Texture tileMetallicGlossMapTextureArray = terrain.materialTemplate.GetTexture("_TileMetallicGlossMapTexArr");
                Texture tileMapTexture = terrain.materialTemplate.GetTexture("_TilemapTex");
                int tileMapDim = terrain.materialTemplate.GetInt("_TilemapDim");

                Material newMat = new Material(Shader.Find("Daggerfall/TilemapTextureArray"));
                newMat.name = terrain.materialTemplate.name;
                newMat.SetTexture("_TileTexArr", tileTextureArray);
                newMat.SetTexture("_TileNormalMapTexArr", tileNormalMapTextureArray);
                if (terrain.materialTemplate.IsKeywordEnabled("_NORMALMAP"))
                    newMat.EnableKeyword("_NORMALMAP");
                else
                    newMat.DisableKeyword("_NORMALMAP");
                newMat.SetTexture("_TileMetallicGlossMapTexArr", tileMetallicGlossMapTextureArray);
                newMat.SetTexture("_TilemapTex", tileMapTexture);
                newMat.SetInt("_TilemapDim", tileMapDim);

                terrain.materialTemplate = newMat;
            }
            else if (terrain.materialTemplate.shader == shaderTilemap)
            {
                Texture tileSetTexture = terrain.materialTemplate.GetTexture("_TileAtlasTex");
                Texture tileMapTexture = terrain.materialTemplate.GetTexture("_TilemapTex");
                int tileMapDim = terrain.materialTemplate.GetInt("_TilemapDim");

                Material newMat = new Material(Shader.Find("Daggerfall/Tilemap"));
                newMat.name = terrain.materialTemplate.name;
                newMat.SetTexture("_TileAtlasTex", tileSetTexture);
                newMat.SetTexture("_TilemapTex", tileMapTexture);
                newMat.SetInt("_TilemapDim", tileMapDim);

                newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                terrain.materialTemplate = newMat;
            }
        }
    }

    public void ApplyFadeShader()
    {
        if (!smoothClip)
            return;

        if (Time.time-smoothClipLastUpdate < 2)
            return;

        if (
            shaderDefault == null ||
            shaderBillboard == null ||
            shaderBillboardBatch == null ||
            shaderBillboardBatchNoShadows == null ||
            shaderTilemap == null ||
            shaderTilemapTextureArray == null
            )
        {
            if (debugShowMessages)
            {
                Debug.Log("TAIL OF ARGONIA - A FADE SHADER WAS NOT FOUND. ABORTING!");
                DaggerfallUI.Instance.PopupMessage("Applying fade shader!");
            }
            return;
        }

        smoothClipLastUpdate = Time.time;

        if (debugShowMessages)
        {
            Debug.Log("TAIL OF ARGONIA - Applying fade shader!");
            DaggerfallUI.Instance.PopupMessage("Applying fade shader!");
        }

        MeshRenderer[] meshrenderers = FindObjectsOfType<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedmeshrenderers = FindObjectsOfType<SkinnedMeshRenderer>();
        Renderer[] renderers = new Renderer[meshrenderers.Length+skinnedmeshrenderers.Length];
        for (int i = 0; i < meshrenderers.Length; i++)
        {
            renderers[i] = meshrenderers[i];
        }
        for (int i = 0; i < skinnedmeshrenderers.Length; i++)
        {
            renderers[i+meshrenderers.Length] = skinnedmeshrenderers[i];
        }

        //Debug.Log("TAIL OF ARGONIA - INITIALIZING OBJECT SMOOTH CLIPPING");
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                Material material = renderer.materials[i];
                if (material.shader.name == "Daggerfall/Default" || material.shader == shaderDefault)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A DEFAULT MATERIAL");
                    Color color = material.GetColor("_Color");
                    Color colorSpec = material.GetColor("_SpecColor");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Texture texParallax = material.GetTexture("_ParallaxMap");
                    float scaleParallax = material.GetFloat("_Parallax");
                    Texture texMetallic = material.GetTexture("_MetallicGlossMap");
                    float scaleSmoothness = material.GetFloat("_Smoothness");

                    Material newMat = new Material(shaderDefault);
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetColor("_SpecColor", colorSpec);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetTexture("_ParallaxMap", texParallax);
                    newMat.SetFloat("_Parallax", scaleParallax);
                    newMat.SetTexture("_MetallicGlossMap", texMetallic);
                    newMat.SetFloat("_Smoothness", scaleSmoothness);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
                else if (material.shader.name == "Daggerfall/Billboard" || material.shader == shaderBillboard)
                {
                    Billboard billboard = renderer.GetComponent<Billboard>();
                    /*if (billboard != null)
                    {
                        if (billboard.Summary.IsMobile)
                            continue;
                    }*/
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");

                    Material newMat = new Material(shaderBillboard);
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
                else if (material.shader.name == "Daggerfall/BillboardBatch" || material.shader == shaderBillboardBatch)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD BATCH MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Vector4 vectorUp = material.GetVector("_UpVector");

                    Material newMat = new Material(shaderBillboardBatch);
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetVector("_UpVector", vectorUp);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
                else if (material.shader.name == "Daggerfall/BillboardBatchNoShadows" || material.shader == shaderBillboardBatchNoShadows)
                {
                    //Debug.Log("TAIL OF ARGONIA - FOUND A BILLBOARD BATCH NO SHADOWS MATERIAL");
                    Color color = material.GetColor("_Color");
                    float scaleCutoff = material.GetFloat("_Cutoff");
                    Texture texMain = material.GetTexture("_MainTex");
                    Texture texBump = material.GetTexture("_BumpMap");
                    Texture texEmission = material.GetTexture("_EmissionMap");
                    Color colorEmission = material.GetColor("_EmissionColor");
                    Vector4 vectorUp = material.GetVector("_UpVector");

                    Material newMat = new Material(shaderBillboardBatchNoShadows);
                    newMat.name = material.name;
                    newMat.SetColor("_Color", color);
                    newMat.SetFloat("_Cutoff", scaleCutoff);
                    newMat.SetTexture("_MainTex", texMain);
                    newMat.SetTexture("_BumpMap", texBump);
                    newMat.SetTexture("_EmissionMap", texEmission);
                    newMat.SetColor("_EmissionColor", colorEmission);
                    if (material.IsKeywordEnabled(KeyWords.Emission))
                        newMat.EnableKeyword(KeyWords.Emission);
                    newMat.SetVector("_UpVector", vectorUp);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
                else if (material.shader.name == "Daggerfall/TilemapTextureArray" || material.shader == shaderTilemapTextureArray)
                {
                    Texture tileTextureArray = material.GetTexture("_TileTexArr");
                    Texture tileNormalMapTextureArray = material.GetTexture("_TileNormalMapTexArr");
                    Texture tileMetallicGlossMapTextureArray = material.GetTexture("_TileMetallicGlossMapTexArr");
                    Texture tileMapTexture = material.GetTexture("_TilemapTex");
                    int tileMapDim = material.GetInt("_TilemapDim");

                    Material newMat = new Material(shaderTilemapTextureArray);
                    newMat.name = material.name;
                    newMat.SetTexture("_TileTexArr", tileTextureArray);
                    newMat.SetTexture("_TileNormalMapTexArr", tileNormalMapTextureArray);
                    if (material.IsKeywordEnabled("_NORMALMAP"))
                        newMat.EnableKeyword("_NORMALMAP");
                    else
                        newMat.DisableKeyword("_NORMALMAP");
                    newMat.SetTexture("_TileMetallicGlossMapTexArr", tileMetallicGlossMapTextureArray);
                    newMat.SetTexture("_TilemapTex", tileMapTexture);
                    newMat.SetInt("_TilemapDim", tileMapDim);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
                else if (material.shader.name == "Daggerfall/Tilemap" || material.shader == shaderTilemap)
                {
                    Texture tileSetTexture = material.GetTexture("_TileAtlasTex");
                    Texture tileMapTexture = material.GetTexture("_TilemapTex");
                    int tileMapDim = material.GetInt("_TilemapDim");

                    Material newMat = new Material(shaderTilemap);
                    newMat.name = material.name;
                    newMat.SetTexture("_TileAtlasTex", tileSetTexture);
                    newMat.SetTexture("_TilemapTex", tileMapTexture);
                    newMat.SetInt("_TilemapDim", tileMapDim);

                    newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                    newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                    newMat.SetFloat("_Brightness", textureBrightness);

                    materials[i] = newMat;
                }
            }
            renderer.materials = materials;
        }

        Terrain[] terrains = FindObjectsOfType<Terrain>();

        foreach (Terrain terrain in terrains)
        {
            //if (SystemInfo.supports2DArrayTextures && DaggerfallUnity.Settings.EnableTextureArrays)
            //if (terrain.materialTemplate.shader.name == "Daggerfall/TilemapTextureArray" || terrain.materialTemplate.shader.name == "Daggerfall/AnimatedWater/TilemapTextureArray" || terrain.materialTemplate.shader == shaderTilemapTextureArray)
            if (terrain.materialTemplate.shader.name == "Daggerfall/TilemapTextureArray" || terrain.materialTemplate.shader == shaderTilemapTextureArray)
            {
                Texture tileTextureArray = terrain.materialTemplate.GetTexture("_TileTexArr");
                Texture tileNormalMapTextureArray = terrain.materialTemplate.GetTexture("_TileNormalMapTexArr");
                Texture tileMetallicGlossMapTextureArray = terrain.materialTemplate.GetTexture("_TileMetallicGlossMapTexArr");
                Texture tileMapTexture = terrain.materialTemplate.GetTexture("_TilemapTex");
                int tileMapDim = terrain.materialTemplate.GetInt("_TilemapDim");

                Material newMat = new Material(shaderTilemapTextureArray);
                newMat.name = terrain.materialTemplate.name;
                newMat.SetTexture("_TileTexArr", tileTextureArray);
                newMat.SetTexture("_TileNormalMapTexArr", tileNormalMapTextureArray);
                if (terrain.materialTemplate.IsKeywordEnabled("_NORMALMAP"))
                    newMat.EnableKeyword("_NORMALMAP");
                else
                    newMat.DisableKeyword("_NORMALMAP");
                newMat.SetTexture("_TileMetallicGlossMapTexArr", tileMetallicGlossMapTextureArray);
                newMat.SetTexture("_TilemapTex", tileMapTexture);
                newMat.SetInt("_TilemapDim", tileMapDim);

                newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                newMat.SetFloat("_Brightness", textureBrightness);

                terrain.materialTemplate = newMat;
            }
            //else if (terrain.materialTemplate.shader.name == "Daggerfall/Tilemap" || terrain.materialTemplate.shader.name == "Daggerfall/AnimatedWater/Tilemap" || terrain.materialTemplate.shader == shaderTilemap)
            else if (terrain.materialTemplate.shader.name == "Daggerfall/Tilemap" || terrain.materialTemplate.shader == shaderTilemap)
            {
                Texture tileSetTexture = terrain.materialTemplate.GetTexture("_TileAtlasTex");
                Texture tileMapTexture = terrain.materialTemplate.GetTexture("_TilemapTex");
                int tileMapDim = terrain.materialTemplate.GetInt("_TilemapDim");

                Material newMat = new Material(shaderTilemap);
                newMat.name = terrain.materialTemplate.name;
                newMat.SetTexture("_TileAtlasTex", tileSetTexture);
                newMat.SetTexture("_TilemapTex", tileMapTexture);
                newMat.SetInt("_TilemapDim", tileMapDim);

                newMat.SetTexture("_DitherPattern", ditherSizeTextures[ditherSize]);
                newMat.SetFloat("_DitherStart", smoothClipStartDistance);

                newMat.SetFloat("_Brightness", textureBrightness);

                terrain.materialTemplate = newMat;
            }
        }
    }
    private KeyCode SetKeyFromText(string text)
    {
        Debug.Log("Setting Key");
        if (System.Enum.TryParse(text, false, out KeyCode result))
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
