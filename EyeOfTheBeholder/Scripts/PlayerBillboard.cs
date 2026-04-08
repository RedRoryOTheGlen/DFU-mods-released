using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Serialization;

public class PlayerBillboard : MonoBehaviour
{
    static readonly Type customBlendModeType = typeof(MaterialReader.CustomBlendMode);

    Transform torch = null;
    Vector3 torchPosLocalDefault;

    public static PlayerBillboard Instance;

    public bool IsReady
    {
        get
        {
            if (transform.parent != null)
                return true;
            else
                return false;
        }
    }

    const int numberOrientations = 8;
    const float anglePerOrientation = 360f / numberOrientations;

    bool isTravelling;

    float sizeMod
    {
        get
        {
            if (riding || transformed)
                return 0.029f * scale;
            else
                return 0.019f * scale;
        }
    }
    float scale = 1;
    float scaleOffsetMod = 1;
    float scaleOffset
    {
        get
        {
            return scale * scaleOffsetMod;
        }
    }

    Camera mainCamera = null;
    MeshFilter meshFilter = null;
    MeshRenderer meshRenderer = null;

    public Vector3 cameraPosition;
    public float currentAngle;
    public Vector3 lastMoveDirection;
    public int lastOrientation;
    public Color lastColor;

    float orientationTime = 0.1f;
    float orientationTimer;

    int frameCurrent;
    float frameTime
    {
        get
        {
            if (riding)
                return 0.0625f * (2-walkAnimSpeedMod);
            else
                return 0.25f * (2-walkAnimSpeedMod);
        }
    }
    float frameTimer;

    public PlayerBillboardState[] stateCurrent;
    public PlayerBillboardState[] stateLast;

    PlayerBillboardState[] StatesIdle;
    PlayerBillboardState[] StatesMove;
    PlayerBillboardState[] StatesDeath;
    PlayerBillboardState[] StatesIdleMelee;
    PlayerBillboardState[] StatesMoveMelee;
    PlayerBillboardState[] StatesAttackMelee;
    PlayerBillboardState[] StatesIdleRanged;
    PlayerBillboardState[] StatesMoveRanged;
    PlayerBillboardState[] StatesAttackRanged;
    PlayerBillboardState[] StatesIdleSpell;
    PlayerBillboardState[] StatesMoveSpell;
    PlayerBillboardState[] StatesAttackSpell;

    PlayerBillboardState[] StatesIdleHorse;
    PlayerBillboardState[] StatesMoveHorse;
    PlayerBillboardState[] StatesGallopHorse;

    PlayerBillboardState[] StatesIdleLycan;
    PlayerBillboardState[] StatesMoveLycan;
    PlayerBillboardState[] StatesIdleMeleeLycan;
    PlayerBillboardState[] StatesMoveMeleeLycan;
    PlayerBillboardState[] StatesAttackMeleeLycan;
    PlayerBillboardState[] StatesDeathLycan;

    public bool lengthsChanged = false;

    IEnumerator isAnimating;
    bool died;
    bool riding;
    bool sheathed;
    bool usingBow;
    bool spellcasting;
    bool stopped;
    bool transformed;
    bool floating;
    bool animating;

    int indexFoot = -1;
    int indexHorse = -1;
    //int indexLycan = 0;

    //when will the billboard orientation turn to the view
    public int readyStance = 0; //0 = never, 1 = on idle, 2 = on idle and moving
    public int turnToView = 0; //0 = never, 1 = only when animating, 2 = also when weapon drawn, 3 = always

    public Vector2 offsetDefault = -Vector2.one;
    public Vector2 offsetAttack = -Vector2.one;

    Shader shaderNormal;
    Shader shaderGhost;

    public int attackStrings;
    public float mirrorTime = 3;
    public int pingpongOffset = 0;
    int pingpongCount = 0;

    float mirrorTimer;
    int mirrorCount = 0;

    public bool FP;
    public int visibility;
    public int torchOffset;
    public float walkAnimSpeedMod;

    public bool footsteps;
    SoundClips footstepSound1;
    SoundClips footstepSound2;
    AudioClip footstep1;
    AudioClip footstep2;
    bool footstepAlt;

    bool hasPlayedFootstep;
    bool wasGrounded;

    public class PlayerBillboardState
    {
        public string name;
        public List<Texture2D> frames = new List<Texture2D>();
        public bool mirror;
        public Rect rect;

        XMLManager xml;

        public PlayerBillboardState(string newName, int archive, int record, int frame, bool newMirror = false)
        {
            name = newName;
            mirror = newMirror;

            InitializeTextures(archive,record);

            Debug.Log("PlayerBillboardState " + name + " was initialized with " + frames.Count + " frames");
        }

        void InitializeTextures(int archive, int record)
        {
            //find all frames of the record
            int current = 0;
            Texture2D texture;
            while (TextureReplacement.TryImportTexture(archive, record, current, out texture))
            {
                texture.name = archive.ToString() + "_" + record.ToString() + "-" + current.ToString();
                texture.wrapMode = TextureWrapMode.Clamp;
                frames.Add(texture);
                current++;
            }

            //check for XML of first frame, otherwise get rect from dimensions of first frame
            if (frames.Count > 0)
            {
                if (XMLManager.TryReadXml(TextureReplacement.TexturesPath, frames[0].name, out xml))
                {
                    float scale = 1;
                    xml.TryGetFloat("scale", out scale);

                    Vector2 offset = xml.GetVector2("X", "Y", Vector2.zero);

                    rect = new Rect(offset.x / scale, offset.y / scale, frames[0].width / scale, frames[0].height / scale);

                    /*if (mirror)
                        rect = new Rect(-offset.x / scale, offset.y / scale, frames[0].width / scale, frames[0].height / scale);
                    else
                        rect = new Rect(offset.x / scale, offset.y / scale, frames[0].width / scale, frames[0].height / scale);*/
                }
                else
                {
                    Debug.Log("No XML found in " + TextureReplacement.TexturesPath.ToString() + " - " + frames[0].name + "!");
                    rect = new Rect(0, 0, frames[0].width, frames[0].height);
                }
            }
            else
            {
                Debug.Log("No frames found!");
                rect = new Rect(0, 0, 128, 128);
            }
        }
    }

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

    void InitializeStates()
    {
        died = false;

        int indexLycan = 0;
        if (GameManager.Instance.PlayerEffectManager.LycanthropyType() == LycanthropyTypes.Wereboar)
            indexLycan = 1;

        int archiveFoot = 112364 + indexFoot;
        int archiveHorse = 112382 + indexHorse;
        int archiveLycan = 112380 + indexLycan;

        StatesIdle = new PlayerBillboardState[numberOrientations];
        StatesIdle[0] = new PlayerBillboardState("IdleForward", archiveFoot, 0, 0, false);
        StatesIdle[1] = new PlayerBillboardState("IdleForwardLeft", archiveFoot, 1, 0, true);
        StatesIdle[2] = new PlayerBillboardState("IdleLeft", archiveFoot, 2, 0, true);
        StatesIdle[3] = new PlayerBillboardState("IdleBackwardLeft", archiveFoot, 3, 0, true);
        StatesIdle[4] = new PlayerBillboardState("IdleBackward", archiveFoot, 4, 0, false);
        StatesIdle[5] = new PlayerBillboardState("IdleBackwardRight", archiveFoot, 3, 0, false);
        StatesIdle[6] = new PlayerBillboardState("IdleRight", archiveFoot, 2, 0, false);
        StatesIdle[7] = new PlayerBillboardState("IdleForwardRight", archiveFoot, 1, 0, false);

        StatesMove = new PlayerBillboardState[numberOrientations];
        StatesMove[0] = new PlayerBillboardState("MoveForward", archiveFoot, 5, 0, false);
        StatesMove[1] = new PlayerBillboardState("MoveForwardLeft", archiveFoot, 6, 0, true);
        StatesMove[2] = new PlayerBillboardState("MoveLeft", archiveFoot, 7, 0, true);
        StatesMove[3] = new PlayerBillboardState("MoveBackwardLeft", archiveFoot, 8, 0, true);
        StatesMove[4] = new PlayerBillboardState("MoveBackward", archiveFoot, 9, 0, false);
        StatesMove[5] = new PlayerBillboardState("MoveBackwardRight", archiveFoot, 8, 0, false);
        StatesMove[6] = new PlayerBillboardState("MoveRight", archiveFoot, 7, 0, false);
        StatesMove[7] = new PlayerBillboardState("MoveForwardRight", archiveFoot, 6, 0, false);

        StatesDeath = new PlayerBillboardState[numberOrientations];
        StatesDeath[0] = new PlayerBillboardState("Death", archiveFoot, 10, 0, false);
        StatesDeath[1] = new PlayerBillboardState("Death", archiveFoot, 11, 0, true);
        StatesDeath[2] = new PlayerBillboardState("Death", archiveFoot, 12, 0, true);
        StatesDeath[3] = new PlayerBillboardState("Death", archiveFoot, 13, 0, true);
        StatesDeath[4] = new PlayerBillboardState("Death", archiveFoot, 14, 0, false);
        StatesDeath[5] = new PlayerBillboardState("Death", archiveFoot, 13, 0, false);
        StatesDeath[6] = new PlayerBillboardState("Death", archiveFoot, 12, 0, false);
        StatesDeath[7] = new PlayerBillboardState("Death", archiveFoot, 11, 0, false);

        StatesIdleMelee = new PlayerBillboardState[numberOrientations];
        StatesIdleMelee[0] = new PlayerBillboardState("ReadyMeleeForward", archiveFoot, 15, 0, false);
        StatesIdleMelee[1] = new PlayerBillboardState("ReadyMeleeForwardLeft", archiveFoot, 16, 0, true);
        StatesIdleMelee[2] = new PlayerBillboardState("ReadyMeleeLeft", archiveFoot, 17, 0, true);
        StatesIdleMelee[3] = new PlayerBillboardState("ReadyMeleeBackwardLeft", archiveFoot, 18, 0, true);
        StatesIdleMelee[4] = new PlayerBillboardState("ReadyMeleeBackward", archiveFoot, 19, 0, false);
        StatesIdleMelee[5] = new PlayerBillboardState("ReadyMeleeBackwardRight", archiveFoot, 18, 0, false);
        StatesIdleMelee[6] = new PlayerBillboardState("ReadyMeleeRight", archiveFoot, 17, 0, false);
        StatesIdleMelee[7] = new PlayerBillboardState("ReadyMeleeForwardRight", archiveFoot, 16, 0, false);

        StatesMoveMelee = new PlayerBillboardState[numberOrientations];
        StatesMoveMelee[0] = new PlayerBillboardState("MoveMeleeForward", archiveFoot, 20, 0, false);
        StatesMoveMelee[1] = new PlayerBillboardState("MoveMeleeForwardLeft", archiveFoot, 21, 0, true);
        StatesMoveMelee[2] = new PlayerBillboardState("MoveMeleeLeft", archiveFoot, 22, 0, true);
        StatesMoveMelee[3] = new PlayerBillboardState("MoveMeleeBackwardLeft", archiveFoot, 23, 0, true);
        StatesMoveMelee[4] = new PlayerBillboardState("MoveMeleeBackward", archiveFoot, 24, 0, false);
        StatesMoveMelee[5] = new PlayerBillboardState("MoveMeleeBackwardRight", archiveFoot, 23, 0, false);
        StatesMoveMelee[6] = new PlayerBillboardState("MoveMeleeRight", archiveFoot, 22, 0, false);
        StatesMoveMelee[7] = new PlayerBillboardState("MoveMeleeForwardRight", archiveFoot, 21, 0, false);

        StatesAttackMelee = new PlayerBillboardState[numberOrientations];
        StatesAttackMelee[0] = new PlayerBillboardState("AttackMeleeForward", archiveFoot, 25, 0, false);
        StatesAttackMelee[1] = new PlayerBillboardState("AttackMeleeForwardLeft", archiveFoot, 26, 0, true);
        StatesAttackMelee[2] = new PlayerBillboardState("AttackMeleeLeft", archiveFoot, 27, 0, true);
        StatesAttackMelee[3] = new PlayerBillboardState("AttackMeleeBackwardLeft", archiveFoot, 28, 0, true);
        StatesAttackMelee[4] = new PlayerBillboardState("AttackMeleeBackward", archiveFoot, 29, 0, false);
        StatesAttackMelee[5] = new PlayerBillboardState("AttackMeleeBackwardRight", archiveFoot, 28, 0, false);
        StatesAttackMelee[6] = new PlayerBillboardState("AttackMeleeRight", archiveFoot, 27, 0, false);
        StatesAttackMelee[7] = new PlayerBillboardState("AttackMeleeForwardRight", archiveFoot, 26, 0, false);

        StatesIdleRanged = new PlayerBillboardState[numberOrientations];
        StatesIdleRanged[0] = new PlayerBillboardState("ReadyRangedForward", archiveFoot, 30, 3, false);
        StatesIdleRanged[1] = new PlayerBillboardState("ReadyRangedForwardLeft", archiveFoot, 31, 3, true);
        StatesIdleRanged[2] = new PlayerBillboardState("ReadyRangedLeft", archiveFoot, 32, 3, true);
        StatesIdleRanged[3] = new PlayerBillboardState("ReadyRangedBackwardLeft", archiveFoot, 33, 3, true);
        StatesIdleRanged[4] = new PlayerBillboardState("ReadyRangedBackward", archiveFoot, 34, 3, false);
        StatesIdleRanged[5] = new PlayerBillboardState("ReadyRangedBackwardRight", archiveFoot, 33, 3, false);
        StatesIdleRanged[6] = new PlayerBillboardState("ReadyRangedRight", archiveFoot, 32, 3, false);
        StatesIdleRanged[7] = new PlayerBillboardState("ReadyRangedForwardRight", archiveFoot, 31, 3, false);

        StatesMoveRanged = new PlayerBillboardState[numberOrientations];
        StatesMoveRanged[0] = new PlayerBillboardState("MoveRangedForward", archiveFoot, 35, 3, false);
        StatesMoveRanged[1] = new PlayerBillboardState("MoveRangedForwardLeft", archiveFoot, 36, 3, true);
        StatesMoveRanged[2] = new PlayerBillboardState("MoveRangedLeft", archiveFoot, 37, 3, true);
        StatesMoveRanged[3] = new PlayerBillboardState("MoveRangedBackwardLeft", archiveFoot, 38, 3, true);
        StatesMoveRanged[4] = new PlayerBillboardState("MoveRangedBackward", archiveFoot, 39, 3, false);
        StatesMoveRanged[5] = new PlayerBillboardState("MoveRangedBackwardRight", archiveFoot, 38, 3, false);
        StatesMoveRanged[6] = new PlayerBillboardState("MoveRangedRight", archiveFoot, 37, 3, false);
        StatesMoveRanged[7] = new PlayerBillboardState("MoveRangedForwardRight", archiveFoot, 36, 3, false);

        StatesAttackRanged = new PlayerBillboardState[numberOrientations];
        StatesAttackRanged[0] = new PlayerBillboardState("AttackRangedForward", archiveFoot, 40, 0, false);
        StatesAttackRanged[1] = new PlayerBillboardState("AttackRangedForwardLeft", archiveFoot, 41, 0, true);
        StatesAttackRanged[2] = new PlayerBillboardState("AttackRangedLeft", archiveFoot, 42, 0, true);
        StatesAttackRanged[3] = new PlayerBillboardState("AttackRangedBackwardLeft", archiveFoot, 43, 0, true);
        StatesAttackRanged[4] = new PlayerBillboardState("AttackRangedBackward", archiveFoot, 44, 0, false);
        StatesAttackRanged[5] = new PlayerBillboardState("AttackRangedBackwardRight", archiveFoot, 43, 0, false);
        StatesAttackRanged[6] = new PlayerBillboardState("AttackRangedRight", archiveFoot, 42, 0, false);
        StatesAttackRanged[7] = new PlayerBillboardState("AttackRangedForwardRight", archiveFoot, 41, 0, false);

        StatesIdleSpell = new PlayerBillboardState[numberOrientations];
        StatesIdleSpell[0] = new PlayerBillboardState("ReadySpellForward", archiveFoot, 45, 0, false);
        StatesIdleSpell[1] = new PlayerBillboardState("ReadySpellForwardLeft", archiveFoot, 46, 0, true);
        StatesIdleSpell[2] = new PlayerBillboardState("ReadySpellLeft", archiveFoot, 47, 0, true);
        StatesIdleSpell[3] = new PlayerBillboardState("ReadySpellBackwardLeft", archiveFoot, 48, 0, true);
        StatesIdleSpell[4] = new PlayerBillboardState("ReadySpellBackward", archiveFoot, 49, 0, false);
        StatesIdleSpell[5] = new PlayerBillboardState("ReadySpellBackwardRight", archiveFoot, 48, 0, false);
        StatesIdleSpell[6] = new PlayerBillboardState("ReadySpellRight", archiveFoot, 47, 0, false);
        StatesIdleSpell[7] = new PlayerBillboardState("ReadySpellForwardRight", archiveFoot, 46, 0, false);

        StatesMoveSpell = new PlayerBillboardState[numberOrientations];
        StatesMoveSpell[0] = new PlayerBillboardState("MoveSpellForward", archiveFoot, 50, 0, false);
        StatesMoveSpell[1] = new PlayerBillboardState("MoveSpellForwardLeft", archiveFoot, 51, 0, true);
        StatesMoveSpell[2] = new PlayerBillboardState("MoveSpellLeft", archiveFoot, 52, 0, true);
        StatesMoveSpell[3] = new PlayerBillboardState("MoveSpellBackwardLeft", archiveFoot, 53, 0, true);
        StatesMoveSpell[4] = new PlayerBillboardState("MoveSpellBackward", archiveFoot, 54, 0, false);
        StatesMoveSpell[5] = new PlayerBillboardState("MoveSpellBackwardRight", archiveFoot, 53, 0, false);
        StatesMoveSpell[6] = new PlayerBillboardState("MoveSpellRight", archiveFoot, 52, 0, false);
        StatesMoveSpell[7] = new PlayerBillboardState("MoveSpellForwardRight", archiveFoot, 51, 0, false);

        StatesAttackSpell = new PlayerBillboardState[numberOrientations];
        StatesAttackSpell[0] = new PlayerBillboardState("AttackSpellForward", archiveFoot, 55, 0, false);
        StatesAttackSpell[1] = new PlayerBillboardState("AttackSpellForwardLeft", archiveFoot, 56, 0, true);
        StatesAttackSpell[2] = new PlayerBillboardState("AttackSpellLeft", archiveFoot, 57, 0, true);
        StatesAttackSpell[3] = new PlayerBillboardState("AttackSpellBackwardLeft", archiveFoot, 58, 0, true);
        StatesAttackSpell[4] = new PlayerBillboardState("AttackSpellBackward", archiveFoot, 59, 0, false);
        StatesAttackSpell[5] = new PlayerBillboardState("AttackSpellBackwardRight", archiveFoot, 58, 0, false);
        StatesAttackSpell[6] = new PlayerBillboardState("AttackSpellRight", archiveFoot, 57, 0, false);
        StatesAttackSpell[7] = new PlayerBillboardState("AttackSpellForwardRight", archiveFoot, 56, 0, false);

        StatesIdleHorse = new PlayerBillboardState[numberOrientations];
        StatesIdleHorse[0] = new PlayerBillboardState("IdleForward", archiveHorse, 0, 0,  false);
        StatesIdleHorse[1] = new PlayerBillboardState("IdleForwardLeft", archiveHorse, 1, 0,  true);
        StatesIdleHorse[2] = new PlayerBillboardState("IdleLeft", archiveHorse, 2, 0,  true);
        StatesIdleHorse[3] = new PlayerBillboardState("IdleBackwardLeft", archiveHorse, 3, 0,  true);
        StatesIdleHorse[4] = new PlayerBillboardState("IdleBackward", archiveHorse, 4, 0,  false);
        StatesIdleHorse[5] = new PlayerBillboardState("IdleBackwardRight", archiveHorse, 3, 0,  false);
        StatesIdleHorse[6] = new PlayerBillboardState("IdleRight", archiveHorse, 2, 0,  false);
        StatesIdleHorse[7] = new PlayerBillboardState("IdleForwardRight", archiveHorse, 1, 0,  false);

        StatesMoveHorse = new PlayerBillboardState[numberOrientations];
        StatesMoveHorse[0] = new PlayerBillboardState("MoveForward", archiveHorse, 5, 0,  false);
        StatesMoveHorse[1] = new PlayerBillboardState("MoveForwardLeft", archiveHorse, 6, 0,  true);
        StatesMoveHorse[2] = new PlayerBillboardState("MoveLeft", archiveHorse, 7, 0,  true);
        StatesMoveHorse[3] = new PlayerBillboardState("MoveBackwardLeft", archiveHorse, 8, 0,  true);
        StatesMoveHorse[4] = new PlayerBillboardState("MoveBackward", archiveHorse, 9, 0,  false);
        StatesMoveHorse[5] = new PlayerBillboardState("MoveBackwardRight", archiveHorse, 8, 0,  false);
        StatesMoveHorse[6] = new PlayerBillboardState("MoveRight", archiveHorse, 7, 0,  false);
        StatesMoveHorse[7] = new PlayerBillboardState("MoveForwardRight", archiveHorse, 6, 0,  false);

        StatesGallopHorse = new PlayerBillboardState[numberOrientations];
        StatesGallopHorse[0] = new PlayerBillboardState("MoveForward", archiveHorse, 10, 0, false);
        StatesGallopHorse[1] = new PlayerBillboardState("MoveForwardLeft", archiveHorse, 11, 0, true);
        StatesGallopHorse[2] = new PlayerBillboardState("MoveLeft", archiveHorse, 12, 0, true);
        StatesGallopHorse[3] = new PlayerBillboardState("MoveBackwardLeft", archiveHorse, 13, 0, true);
        StatesGallopHorse[4] = new PlayerBillboardState("MoveBackward", archiveHorse, 14, 0, false);
        StatesGallopHorse[5] = new PlayerBillboardState("MoveBackwardRight", archiveHorse, 13, 0, false);
        StatesGallopHorse[6] = new PlayerBillboardState("MoveRight", archiveHorse, 12, 0, false);
        StatesGallopHorse[7] = new PlayerBillboardState("MoveForwardRight", archiveHorse, 11, 0, false);

        StatesIdleLycan = new PlayerBillboardState[numberOrientations];
        StatesIdleLycan[0] = new PlayerBillboardState("IdleForward", archiveLycan, 0, 0,  false);
        StatesIdleLycan[1] = new PlayerBillboardState("IdleForwardLeft", archiveLycan, 1, 0,  true);
        StatesIdleLycan[2] = new PlayerBillboardState("IdleLeft", archiveLycan, 2, 0,  true);
        StatesIdleLycan[3] = new PlayerBillboardState("IdleBackwardLeft", archiveLycan, 3, 0,  true);
        StatesIdleLycan[4] = new PlayerBillboardState("IdleBackward", archiveLycan, 4, 0,  false);
        StatesIdleLycan[5] = new PlayerBillboardState("IdleBackwardRight", archiveLycan, 3, 0,  false);
        StatesIdleLycan[6] = new PlayerBillboardState("IdleRight", archiveLycan, 2, 0,  false);
        StatesIdleLycan[7] = new PlayerBillboardState("IdleForwardRight", archiveLycan, 1, 0,  false);

        StatesMoveLycan = new PlayerBillboardState[numberOrientations];
        StatesMoveLycan[0] = new PlayerBillboardState("MoveForward", archiveLycan, 5, 0,  false);
        StatesMoveLycan[1] = new PlayerBillboardState("MoveForwardLeft", archiveLycan, 6, 0,  true);
        StatesMoveLycan[2] = new PlayerBillboardState("MoveLeft", archiveLycan, 7, 0,  true);
        StatesMoveLycan[3] = new PlayerBillboardState("MoveBackwardLeft", archiveLycan, 8, 0,  true);
        StatesMoveLycan[4] = new PlayerBillboardState("MoveBackward", archiveLycan, 9, 0,  false);
        StatesMoveLycan[5] = new PlayerBillboardState("MoveBackwardRight", archiveLycan, 8, 0,  false);
        StatesMoveLycan[6] = new PlayerBillboardState("MoveRight", archiveLycan, 7, 0,  false);
        StatesMoveLycan[7] = new PlayerBillboardState("MoveForwardRight", archiveLycan, 6, 0,  false);

        StatesIdleMeleeLycan = new PlayerBillboardState[numberOrientations];
        StatesIdleMeleeLycan[0] = new PlayerBillboardState("ReadyLycanForward", archiveLycan, 15, 0, false);
        StatesIdleMeleeLycan[1] = new PlayerBillboardState("ReadyLycanForwardLeft", archiveLycan, 16, 0, true);
        StatesIdleMeleeLycan[2] = new PlayerBillboardState("ReadyLycanLeft", archiveLycan, 17, 0, true);
        StatesIdleMeleeLycan[3] = new PlayerBillboardState("ReadyLycanBackwardLeft", archiveLycan, 18, 0, true);
        StatesIdleMeleeLycan[4] = new PlayerBillboardState("ReadyLycanBackward", archiveLycan, 19, 0, false);
        StatesIdleMeleeLycan[5] = new PlayerBillboardState("ReadyLycanBackwardRight", archiveLycan, 18, 0, false);
        StatesIdleMeleeLycan[6] = new PlayerBillboardState("ReadyLycanRight", archiveLycan, 17, 0, false);
        StatesIdleMeleeLycan[7] = new PlayerBillboardState("ReadyLycanForwardRight", archiveLycan, 16, 0, false);

        StatesMoveMeleeLycan = new PlayerBillboardState[numberOrientations];
        StatesMoveMeleeLycan[0] = new PlayerBillboardState("ReadyLycanForward", archiveLycan, 20, 0, false);
        StatesMoveMeleeLycan[1] = new PlayerBillboardState("ReadyLycanForwardLeft", archiveLycan, 21, 0, true);
        StatesMoveMeleeLycan[2] = new PlayerBillboardState("ReadyLycanLeft", archiveLycan, 22, 0, true);
        StatesMoveMeleeLycan[3] = new PlayerBillboardState("ReadyLycanBackwardLeft", archiveLycan, 23, 0, true);
        StatesMoveMeleeLycan[4] = new PlayerBillboardState("ReadyLycanBackward", archiveLycan, 24, 0, false);
        StatesMoveMeleeLycan[5] = new PlayerBillboardState("ReadyLycanBackwardRight", archiveLycan, 23, 0, false);
        StatesMoveMeleeLycan[6] = new PlayerBillboardState("ReadyLycanRight", archiveLycan, 22, 0, false);
        StatesMoveMeleeLycan[7] = new PlayerBillboardState("ReadyLycanForwardRight", archiveLycan, 21, 0, false);

        StatesAttackMeleeLycan = new PlayerBillboardState[numberOrientations];
        StatesAttackMeleeLycan[0] = new PlayerBillboardState("AttackLycanForward", archiveLycan, 25, 0,  false);
        StatesAttackMeleeLycan[1] = new PlayerBillboardState("AttackLycanForwardLeft", archiveLycan, 26, 0,  true);
        StatesAttackMeleeLycan[2] = new PlayerBillboardState("AttackLycanLeft", archiveLycan, 27, 0,  true);
        StatesAttackMeleeLycan[3] = new PlayerBillboardState("AttackLycanBackwardLeft", archiveLycan, 28, 0,  true);
        StatesAttackMeleeLycan[4] = new PlayerBillboardState("AttackLycanBackward", archiveLycan, 29, 0,  false);
        StatesAttackMeleeLycan[5] = new PlayerBillboardState("AttackLycanBackwardRight", archiveLycan, 28, 0,  false);
        StatesAttackMeleeLycan[6] = new PlayerBillboardState("AttackLycanRight", archiveLycan, 27, 0,  false);
        StatesAttackMeleeLycan[7] = new PlayerBillboardState("AttackLycanForwardRight", archiveLycan, 26, 0,  false);

        StatesDeathLycan = new PlayerBillboardState[numberOrientations];
        StatesDeathLycan[0] = new PlayerBillboardState("Death", archiveLycan, 10, 0,  false);
        StatesDeathLycan[1] = new PlayerBillboardState("Death", archiveLycan, 11, 0,  true);
        StatesDeathLycan[2] = new PlayerBillboardState("Death", archiveLycan, 12, 0,  true);
        StatesDeathLycan[3] = new PlayerBillboardState("Death", archiveLycan, 13, 0,  true);
        StatesDeathLycan[4] = new PlayerBillboardState("Death", archiveLycan, 14, 0,  false);
        StatesDeathLycan[5] = new PlayerBillboardState("Death", archiveLycan, 13, 0,  false);
        StatesDeathLycan[6] = new PlayerBillboardState("Death", archiveLycan, 12, 0,  false);
        StatesDeathLycan[7] = new PlayerBillboardState("Death", archiveLycan, 11, 0,  false);

        lengthsChanged = false;
    }

    // Start is called before the first frame update
    public void Initialize(int foot, int horse, float size, float sizeOffset, int ready, int turn, int attackString, float mirrorDecay, int pingpongAdjust, int firstPerson, int torchFollow)
    {
        if (!IsReady)
            return;

        scale = size;
        scaleOffsetMod = sizeOffset;
        readyStance = ready;
        turnToView = turn;
        attackStrings = attackString;
        mirrorTime = mirrorDecay;
        pingpongOffset = pingpongAdjust;
        visibility = firstPerson;
        torchOffset = torchFollow;
        died = false;

        // Get component references
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (torch == null)
        {
            torch = GameManager.Instance.PlayerObject.GetComponent<EnablePlayerTorch>().PlayerTorch.transform;
            torchPosLocalDefault = torch.localPosition;
        }

        if (foot != indexFoot || horse != indexHorse || lengthsChanged)
        {
            indexFoot = foot;
            indexHorse = horse;
            InitializeStates();
        }

        if (meshRenderer.sharedMaterial == null)
            AssignMeshAndMaterial();

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        mirrorCount = 0;
        mirrorTimer = 0;
        pingpongCount = 0;

        if (stateLast != null)
            stateCurrent = stateLast;
        else
            stateCurrent = StatesIdle;

        if (footsteps)
            DisableVanillaFootsteps();
        else
            EnableVanillaFootsteps();

        UpdateOrientation(true);
    }

    private void Update()
    {
        if (GameManager.IsGamePaused)
            return;

        if (orientationTime > 0)
        {
            if (orientationTimer <= orientationTime)
                orientationTimer += Time.deltaTime;
        }

        if (attackStrings == 1 || attackStrings == 3)
        {
            float mirrorInt = mirrorTime;
            if (FP)
                mirrorInt = 0.1f;
            if (mirrorCount > 0 && isAnimating == null && mirrorInt > 0)
            {
                if (mirrorTimer > mirrorInt)
                {
                    mirrorCount = 0;
                    mirrorTimer = 0;
                    UpdateBillboardDelayed(frameCurrent,lastOrientation,stateCurrent);
                }
                else
                {
                    mirrorTimer += Time.deltaTime;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (GameManager.IsGamePaused)
            return;

        // Rotate to face camera in game
        if (mainCamera && Application.isPlaying)
        {
            Vector3 viewDirection = -new Vector3(mainCamera.transform.forward.x, 0, mainCamera.transform.forward.z);

            // Rotate billboard based on camera facing
            if (FP)
            {
                if (visibility == 2)
                    viewDirection = viewDirection * -1;

                if (EyeOfTheBeholder.Instance.hasFreeRein && GameManager.Instance.PlayerMotor.IsRiding)
                    cameraPosition = transform.parent.position + (Vector3.up * 0.9f) + (mainCamera.transform.forward * -2);
                else
                    cameraPosition = transform.parent.position + (Vector3.up * 0.9f);
            }
            else
                cameraPosition = mainCamera.transform.position;

            transform.LookAt(transform.position + viewDirection);

            if (died)
                return;

            if (GameManager.Instance.PlayerDeath.DeathInProgress && !died)
            {
                died = true;
                PlayDeathAnimation();
                return;
            }

            // Orient enemy based on camera position
            UpdateOrientation();

            UpdateMaterial();

            /*if (GameManager.Instance.PlayerMotor.FreezeMotor > 0)
            {
                if (stateCurrent)
                return;
            }*/

            if (isAnimating == null)
            {
                //weapon attacks
                if (GameManager.Instance.WeaponManager.ScreenWeapon.IsAttacking())
                {
                    if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
                    {
                        PlayLycanAttackAnimation();
                    }
                    else if (GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType != WeaponTypes.Bow)
                        PlayMeleeAttackAnimation();
                    else
                    {
                        if (DaggerfallUnity.Settings.BowDrawback)
                            PlayRangedAttackAnimationHold();
                        else
                            PlayRangedAttackAnimation();
                    }
                }

                //spell attacks
                if (GameManager.Instance.PlayerSpellCasting.IsPlayingAnim)
                    PlaySpellAttackAnimation();

                LoopIdleBillboard();
            }

            if (footsteps)
            {
                //play footstep sound when landing
                bool isGrounded = GameManager.Instance.PlayerMotor.IsGrounded;
                if (isGrounded && !wasGrounded)
                    PlayFootstep();
                wasGrounded = isGrounded;
            }

        }
    }

    private void FixedUpdate()
    {
        if (footsteps)
        {
            if (EyeOfTheBeholder.Instance.hasTravelOptions && wasGrounded)
            {
                DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
                {
                    isTravelling = (bool)data;
                });
            }
        }
    }
    void LoopIdleBillboard()
    {
        bool isGrounded = GameManager.Instance.PlayerMotor.IsGrounded;
        bool isSpellcasting = GameManager.Instance.PlayerEffectManager.HasReadySpell;
        bool isStopped = (GameManager.Instance.PlayerMotor.IsStandingStill || GameManager.Instance.PlayerMotor.FreezeMotor > 0) ? true : false;
        bool isSheathed = GameManager.Instance.WeaponManager.Sheathed;
        bool isUsingBow = GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType == WeaponTypes.Bow;
        bool isRiding = GameManager.Instance.PlayerMotor.IsRiding;
        bool isTransformed = GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope();
        bool isLevitating = GameManager.Instance.PlayerMotor.IsLevitating;
        bool isSwimming = GameManager.Instance.PlayerMotor.IsSwimming;

        if (isStopped && !stopped)
            stopped = true;
        else if (!isStopped && stopped)
            stopped = false;

        if (isSheathed && !sheathed)
            sheathed = true;
        else if (!isSheathed && sheathed)
            sheathed = false;

        if (isSpellcasting && !spellcasting)
            spellcasting = true;
        else if (!isSpellcasting && spellcasting)
            spellcasting = false;

        if (isUsingBow && !usingBow)
            usingBow = true;
        else if (!isUsingBow && usingBow)
            usingBow = false;

        if (isRiding && !riding)
            riding = true;
        else if (!isRiding && riding)
            riding = false;

        if (isTransformed && !transformed)
            transformed = true;
        else if (!isTransformed && transformed)
            transformed = false;

        if ((isLevitating || isSwimming) && !floating)
            floating = true;
        else if (floating)
            floating = false;

        if (stateCurrent[lastOrientation].frames.Count > 1)
        {
            float speed = 1f;

            if (GameManager.Instance.PlayerMotor.IsRunning)
                speed *= 0.5f;

            if (GameManager.Instance.PlayerMotor.IsCrouching)
                speed *= 2f;

            if (GameManager.Instance.SpeedChanger.isSneaking)
                speed *= 2f;

            if (footsteps)
            {
                if (!isStopped && isGrounded && !animating)
                {
                    int step = 2;
                    if (GameManager.Instance.PlayerMotor.IsRiding)
                        step = 4;

                    if (frameCurrent % step == 0)
                    {
                        if (!hasPlayedFootstep)
                            PlayFootstep();
                    }
                    else
                        hasPlayedFootstep = false;
                }
            }

            if (frameTimer > frameTime * speed)
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
                frameTimer += Time.deltaTime;
            }
        }

        if (isTransformed)
        {
            if (isStopped)
            {
                if (!isSheathed)
                    stateCurrent = StatesIdleMeleeLycan;
                else
                    stateCurrent = StatesIdleLycan;
            }
            else
                stateCurrent = StatesMoveLycan;
        }
        else
        {
            if (isStopped)
            {
                if (isRiding)
                    stateCurrent = StatesIdleHorse;
                else
                {
                    if (readyStance > 0)
                    {
                        if (isSpellcasting)
                            stateCurrent = StatesIdleSpell;
                        else if (!isSheathed)
                        {
                            if (isUsingBow)
                                stateCurrent = StatesIdleRanged;
                            else
                                stateCurrent = StatesIdleMelee;
                        }
                        else
                            stateCurrent = StatesIdle;
                    }
                    else
                        stateCurrent = StatesIdle;
                }
            }
            else
            {
                if (isRiding)
                {
                    Vector3 speed = new Vector3(GameManager.Instance.PlayerMotor.MoveDirection.x,0, GameManager.Instance.PlayerMotor.MoveDirection.z);
                    if (speed.magnitude <= 10)
                        stateCurrent = StatesMoveHorse;
                    else
                        stateCurrent = StatesGallopHorse;
                }
                else
                {
                    if (readyStance > 1)
                    {
                        if (isSpellcasting)
                            stateCurrent = StatesMoveSpell;
                        else if (!isSheathed)
                        {
                            if (isUsingBow)
                                stateCurrent = StatesMoveRanged;
                            else
                                stateCurrent = StatesMoveMelee;
                        }
                        else
                            stateCurrent = StatesMove;
                    }
                    else
                        stateCurrent = StatesMove;
                }
            }
        }

        if (stateLast != stateCurrent || (animating && isAnimating == null))
            UpdateBillboardDelayed(frameCurrent, lastOrientation, stateCurrent);

        if (isAnimating != null && !animating)
            animating = true;
        else if (isAnimating == null && animating)
            animating = false;

        stateLast = stateCurrent;
    }

    void UpdateOrientation(bool force = false)
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        if (orientationTime > 0)
        {
            if (orientationTimer > orientationTime)
                orientationTimer = 0;
            else
                return;
        }

        // Get direction normal to camera, ignore y axis
        Vector3 dir = Vector3.Normalize(
            new Vector3(cameraPosition.x, 0, cameraPosition.z) -
            new Vector3(transform.parent.position.x, 0, transform.parent.position.z));

        //handle vertical movement
        Vector3 currentMoveDirection = GameManager.Instance.PlayerMotor.MoveDirection;
        currentMoveDirection.y = 0;

        if (currentMoveDirection == Vector3.zero)
        {
            if (lastMoveDirection == Vector3.zero)
                currentMoveDirection = mainCamera.transform.forward;
            else
                currentMoveDirection = lastMoveDirection;
        }

        // Get parent forward normal, ignore y axis
        Vector3 parentForward = mainCamera.transform.forward;
        if (!floating)
        {
            if (turnToView > 2) //always turn to view
            {
                parentForward = mainCamera.transform.forward;
            }
            else if (turnToView > 1) //turn to view when weapon readied or animating
            {
                if (isAnimating == null)  //if not animating
                {
                    if (sheathed && !spellcasting)
                    {
                        if (!stopped)
                        {
                            parentForward = currentMoveDirection;
                        }
                        else
                            parentForward = lastMoveDirection;
                    }
                    else
                        parentForward = mainCamera.transform.forward;
                }
                else //turn if animating
                    parentForward = mainCamera.transform.forward;
            }
            else if (turnToView > 0) //turn to view when animating only
            {
                if (isAnimating == null)  //if not animating
                {
                    if (!stopped)
                    {
                        parentForward = currentMoveDirection;
                    }
                    else
                        parentForward = lastMoveDirection;
                }
                else //turn if animating
                    parentForward = mainCamera.transform.forward;
            }
            else //never turn to view
            {
                if (!stopped)
                {
                    parentForward = currentMoveDirection;
                }
                else
                    parentForward = lastMoveDirection;
            }
        }

        if (EyeOfTheBeholder.Instance.hasFreeRein)
        {
            if (GameManager.Instance.PlayerMotor.IsRiding)
                parentForward = EyeOfTheBeholder.Instance.FreeRein_GetMoveVector();
        }

        if (EyeOfTheBeholder.Instance.isSailing)
        {
            if (EyeOfTheBeholder.Instance.boatDriveObject != null)
                parentForward = EyeOfTheBeholder.Instance.boatDriveObject.transform.forward;
            else
                parentForward = EyeOfTheBeholder.Instance.boatMeshObject.transform.forward;
        }

        parentForward.y = 0;

        lastMoveDirection = parentForward;

        currentAngle = Vector3.SignedAngle(dir, parentForward,Vector3.up);

        // Facing index
        int orientation = -Mathf.RoundToInt(currentAngle / anglePerOrientation);
        // Wrap values to 0 .. numberOrientations-1
        orientation = (orientation + numberOrientations) % numberOrientations;

        // Change enemy to this orientation
        if (orientation != lastOrientation || force || meshRenderer.material.mainTexture == null)
        {
            UpdateBillboardDelayed(frameCurrent, orientation, stateCurrent);
        }

        if (torch != null)
        {
            if (!FP)
            {
                if (torchOffset == 2)
                {
                    // Selfie mode
                    Vector3 start = transform.parent.position + (Vector3.up * 0.9f);
                    Vector3 end = GameManager.Instance.MainCameraObject.transform.position;
                    torch.transform.position = start + ((end-start) * 0.5f);
                }
                else if (torchOffset == 1)
                    torch.transform.position = transform.position + (Vector3.up * 0.45f) + (lastMoveDirection.normalized * 0.5f);
            }
            /*else
                torch.transform.localPosition = torchPosLocalDefault;*/
        }
    }

    //Use this when changing states
    //Delay is to make sure stuff like collider height is properly set before updating the billboard
    void UpdateBillboardDelayed(int frame, int orientation, PlayerBillboardState[] states)
    {
        StartCoroutine(UpdateBillboardDelayedCoroutine(frame, orientation, states));
    }

    IEnumerator UpdateBillboardDelayedCoroutine(int frame, int orientation, PlayerBillboardState[] states)
    {
        for (int i = 0; i < 3; i++)
            yield return null;

        UpdateBillboard(frame, orientation, states);
    }

    /// <summary>
    /// Sets enemy orientation index.
    /// </summary>
    /// <param name="orientation">New orientation index.</param>
    private void UpdateBillboard(int frame, int orientation, PlayerBillboardState[] states)
    {
        // Get mesh filter
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        bool flip = states[orientation].mirror;

        if (attackStrings == 1 || attackStrings == 3)
        {
            if ((states == StatesIdle || states == StatesIdleLycan || states == StatesMove || states == StatesMoveMelee || states == StatesMoveSpell || states == StatesMoveLycan || states == StatesIdleMelee || states == StatesIdleSpell || states == StatesIdleMeleeLycan || states == StatesAttackMelee || states == StatesAttackSpell || states == StatesAttackMeleeLycan) && (orientation == 0 || orientation == 4))
            //if ((states == StatesIdle || states == StatesIdleLycan || states == StatesMove || states == StatesMoveMelee || states == StatesMoveSpell || states == StatesMoveLycan || states == StatesIdleMelee || states == StatesIdleSpell || states == StatesIdleMeleeLycan || states == StatesAttackMelee || states == StatesAttackSpell || states == StatesAttackMeleeLycan))
            {
                if (mirrorCount % 2 != 0)
                    flip = !flip;
            }
        }

        // Assign imported texture
        if (frame > states[orientation].frames.Count - 1)
            frame = states[orientation].frames.Count - 1;

        Rect rect = states[orientation].rect;

        Vector2 size = rect.size*sizeMod;

        // Set mesh scale for this state
        transform.localScale = new Vector3(size.x, size.y, 1);
        //transform.localPosition = rect.position * (new Vector2(flipMod, 1)) + (Vector2.up * size.y * 0.5f) - (Vector2.up * GameManager.Instance.PlayerController.height * 0.5f);
        if (GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming)
            transform.localPosition = rect.position - (Vector2.up * size.y * 0.5f);
        else if (GameManager.Instance.PlayerMotor.IsCrouching)
            transform.localPosition = rect.position + (Vector2.up * size.y * 0.5f) - (Vector2.up * GameManager.Instance.PlayerController.height);
        else
            transform.localPosition = rect.position + (Vector2.up * size.y * 0.5f) - (Vector2.up * GameManager.Instance.PlayerController.height * 0.5f);


        if (FP)
        {
            if ((visibility == 1 && !riding) || (visibility == 2 && riding))
                flip = !flip;
        }

        //transform.localPosition = rect.position + (Vector2.up * size.y * 0.5f);

        // Update Record/Frame texture
        if (meshRenderer == null)
        meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.material.mainTexture = states[orientation].frames[frame];

        frameCurrent = frame;

        // Update UVs on mesh
        Vector2[] uvs = new Vector2[4];
        if (flip)
        {
            transform.localPosition *= new Vector2(-1, 1);
            uvs[0] = new Vector2(1, 1);
            uvs[1] = new Vector2(0, 1);
            uvs[2] = new Vector2(1, 0);
            uvs[3] = new Vector2(0, 0);
        }
        else
        {
            uvs[0] = new Vector2(0, 1);
            uvs[1] = new Vector2(1, 1);
            uvs[2] = new Vector2(0, 0);
            uvs[3] = new Vector2(1, 0);
        }
        meshFilter.sharedMesh.uv = uvs;

        // Assign new orientation
        lastOrientation = orientation;

        /*if (FP)
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        else
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;*/

        if (FP && visibility > 0)
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, -0.25f);
    }

    /// <summary>
    /// Creates mesh and material for this enemy.
    /// </summary>
    /// <param name="dfUnity">DaggerfallUnity singleton. Required for content readers and settings.</param>
    /// <param name="archive">Texture archive index derived from type and gender.</param>
    private void AssignMeshAndMaterial()
    {
        // Get mesh filter
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        // Vertices for a 1x1 unit quad
        // This is scaled to correct size depending on facing and orientation
        float hx = 0.5f, hy = 0.5f;
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(hx, hy, 0);
        vertices[1] = new Vector3(-hx, hy, 0);
        vertices[2] = new Vector3(hx, -hy, 0);
        vertices[3] = new Vector3(-hx, -hy, 0);

        // Indices
        int[] indices = new int[6]
        {
                0, 1, 2,
                3, 2, 1,
        };

        // Normals
        Vector3 normal = Vector3.Normalize(Vector3.up + Vector3.forward);
        Vector3[] normals = new Vector3[4];
        normals[0] = normal;
        normals[1] = normal;
        normals[2] = normal;
        normals[3] = normal;

        // Create mesh
        Mesh mesh = new Mesh();
        mesh.name = string.Format("MobileEnemyMesh");
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.normals = normals;

        // Assign mesh
        meshFilter.sharedMesh = mesh;

        // Create material
        Material material = MakeBillboardMaterial(null);

        // Set new enemy material
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;


        shaderNormal = meshRenderer.sharedMaterial.shader;
        shaderGhost = Shader.Find(MaterialReader._DaggerfallGhostShaderName);
    }

    void UpdateMaterial()
    {
        bool isInvisible = GameManager.Instance.PlayerEntity.IsInvisible;
        bool isShadow = GameManager.Instance.PlayerEntity.IsAShade;
        bool isBlending = GameManager.Instance.PlayerEntity.IsBlending;

        lastColor = meshRenderer.sharedMaterial.GetColor("_Color");

        if (isInvisible || isShadow || isBlending)
        {
            if (meshRenderer.sharedMaterial.shader != shaderGhost)
            {
                meshRenderer.sharedMaterial.shader = shaderGhost;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (isInvisible)
            {
                if (lastColor.a != 0.4f)
                {
                    Color currentColor = Color.white;
                    currentColor.a = 0.4f;
                    meshRenderer.sharedMaterial.SetColor("_Color", currentColor);
                }
            }
            else if (isShadow)
            {
                if (lastColor.a != 0.6f)
                {
                    Color currentColor = Color.black;
                    currentColor.a = 0.6f;
                    meshRenderer.sharedMaterial.SetColor("_Color", currentColor);
                }
            }
            else
            {
                if (lastColor.a != 0.8f)
                {
                    Color currentColor = Color.white;
                    currentColor.a = 0.8f;
                    meshRenderer.sharedMaterial.SetColor("_Color", currentColor);
                }
            }
        }
        else
        {
            if (meshRenderer.sharedMaterial.shader != shaderNormal)
            {
                meshRenderer.sharedMaterial.shader = shaderNormal;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            if (lastColor.a != 1)
            {
                Color currentColor = Color.white;
                meshRenderer.sharedMaterial.SetColor("_Color", currentColor);
            }
        }
    }

    void PlayMeleeAttackAnimation()
    {
        if (riding || isAnimating != null)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        bool pingpong = false;

        if (attackStrings == 3)
        {
            //if (UnityEngine.Random.value < 0.33f)
            if (pingpongCount % 4 == 0)
                pingpong = true;
        }
        else if (attackStrings == 2)
            pingpong = true;

        float speed;

        if (pingpong)
        {
            speed = GetMeleeAnimTickTime((((StatesAttackMelee[lastOrientation].frames.Count / 2)+pingpongOffset)*2)-1);
            isAnimating = PlayAnimationPingPongCoroutine(StatesAttackMelee, speed);
        }
        else
        {
            speed = GetMeleeAnimTickTime(StatesAttackMelee[lastOrientation].frames.Count);
            isAnimating = PlayAnimationCoroutine(StatesAttackMelee, speed);
        }

        if (EyeOfTheBeholder.Instance != null)
            EyeOfTheBeholder.Instance.HasSwungWeapon = true;

        if (StatesAttackSpell.Length > 1)
            StartCoroutine(isAnimating);
        else
            isAnimating = null;
    }

    void PlayRangedAttackAnimation()
    {
        if (riding || isAnimating != null)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        isAnimating = PlayAnimationCoroutine(StatesAttackRanged, GameManager.classicUpdateInterval * 2);

        if (EyeOfTheBeholder.Instance != null)
            EyeOfTheBeholder.Instance.HasFiredMissile = true;

        if (StatesAttackSpell.Length > 1)
            StartCoroutine(isAnimating);
        else
            isAnimating = null;
    }

    void PlayRangedAttackAnimationHold()
    {
        if (riding || isAnimating != null)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        isAnimating = PlayAnimationHoldCoroutine(StatesAttackRanged, GameManager.classicUpdateInterval * 2,10);

        StartCoroutine(isAnimating);
    }

    void PlaySpellAttackAnimation()
    {
        if (riding || isAnimating != null)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        isAnimating = PlayAnimationCoroutine(StatesAttackSpell, GameManager.classicUpdateInterval * 2);

        if (EyeOfTheBeholder.Instance != null)
            EyeOfTheBeholder.Instance.HasFiredMissile = true;

        if (StatesAttackSpell.Length > 1)
            StartCoroutine(isAnimating);
        else
            isAnimating = null;
    }

    void PlayLycanAttackAnimation()
    {
        if (riding || isAnimating != null)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        isAnimating = PlayAnimationCoroutine(StatesAttackMeleeLycan, GameManager.classicUpdateInterval * 2);

        if (EyeOfTheBeholder.Instance != null)
            EyeOfTheBeholder.Instance.HasSwungWeapon = true;

        if (StatesAttackSpell.Length > 1)
            StartCoroutine(isAnimating);
        else
            isAnimating = null;
    }

    void PlayDeathAnimation()
    {
        if (riding)
            return;

        if (isAnimating != null)
        {
            StopCoroutine(isAnimating);
            isAnimating = null;
        }

        if (GameManager.Instance.PlayerEffectManager.IsTransformedLycanthrope())
            isAnimating = PlayAnimationCoroutine(StatesDeathLycan, GameManager.classicUpdateInterval * 8, true);
        else
            isAnimating = PlayAnimationCoroutine(StatesDeath, GameManager.classicUpdateInterval * 8, true);


        if (StatesAttackSpell.Length > 1)
            StartCoroutine(isAnimating);
        else
            isAnimating = null;
    }

    IEnumerator PlayAnimationCoroutine(PlayerBillboardState[] states, float interval, bool freeze = false)
    {
        int animFrameCurrent = 0;
        while (animFrameCurrent < states[lastOrientation].frames.Count)
        {
            UpdateBillboard(animFrameCurrent,lastOrientation,states);
            animFrameCurrent++;
            yield return new WaitForSeconds(interval);
        }

        if (attackStrings == 1 || attackStrings == 3)
        {
            if (states == StatesAttackMelee || states == StatesAttackMeleeLycan)
            {
                mirrorTimer = 0;
                mirrorCount++;
            }
            else
                mirrorCount = 0;
        }

        if (attackStrings == 3)
            pingpongCount++;

        isAnimating = null;

        if (!freeze)
            UpdateOrientation(true);
    }

    IEnumerator PlayAnimationPingPongCoroutine(PlayerBillboardState[] states, float interval, bool freeze = false)
    {
        int animFrameCurrent = 0;

        while (animFrameCurrent < (states[lastOrientation].frames.Count / 2)+pingpongOffset)
        {
            UpdateBillboard(animFrameCurrent, lastOrientation, states);
            animFrameCurrent++;
            yield return new WaitForSeconds(interval);
        }

        animFrameCurrent--;

        while (animFrameCurrent > 0)
        {
            UpdateBillboard(animFrameCurrent, lastOrientation, states);
            animFrameCurrent--;
            yield return new WaitForSeconds(interval);
        }

        if (attackStrings == 3)
            pingpongCount++;

        isAnimating = null;

        if (!freeze)
            UpdateOrientation(true);
    }

    IEnumerator PlayAnimationHoldCoroutine(PlayerBillboardState[] states, float interval, int maxDuration)
    {
        int animFrameCurrent = states[lastOrientation].frames.Count - 1;
        bool trigger = false;

        //play in reverse
        while (!trigger)
        {
            while (animFrameCurrent > 0) {
                animFrameCurrent--;
                UpdateBillboard(animFrameCurrent, lastOrientation, states);
                yield return new WaitForSeconds(interval);
            }

            if (((EyeOfTheBeholder.Instance.tomeOfBattleSwingKeyCode == KeyCode.None && InputManager.Instance.HasAction(InputManager.Actions.SwingWeapon)) ||
                (EyeOfTheBeholder.Instance.tomeOfBattleSwingKeyCode != KeyCode.None && InputManager.Instance.GetKey(EyeOfTheBeholder.Instance.tomeOfBattleSwingKeyCode))
                ))
            {
                if (InputManager.Instance.HasAction(InputManager.Actions.ActivateCenterObject))
                {
                    isAnimating = null;
                    UpdateOrientation(true);
                    yield break;
                }

                UpdateBillboard(animFrameCurrent, lastOrientation, states);
                yield return new WaitForEndOfFrame();
            }
            else
            {
                trigger = true;
                if (EyeOfTheBeholder.Instance != null)
                    EyeOfTheBeholder.Instance.HasFiredMissile = true;
            }
        }

        animFrameCurrent = 0;

        //play normally
        while (animFrameCurrent < states[lastOrientation].frames.Count)
        {
            UpdateBillboard(animFrameCurrent, lastOrientation, states);
            animFrameCurrent++;
            yield return new WaitForSeconds(interval);
        }

        isAnimating = null;

        UpdateOrientation(true);
    }

    float GetMeleeAnimTickTime(int length)
    {
        float baseTickTime = FormulaHelper.GetMeleeWeaponAnimTime(GameManager.Instance.PlayerEntity, GameManager.Instance.WeaponManager.ScreenWeapon.WeaponType, GameManager.Instance.WeaponManager.ScreenWeapon.WeaponHands);
        float baseAnimTime = baseTickTime * 5;

        return baseAnimTime / length;
    }
    private static Material MakeBillboardMaterial(string renderMode = null)
    {
        // Parse blendMode from string or use Cutout if no custom blendMode specified
        MaterialReader.CustomBlendMode blendMode =
            renderMode != null && Enum.IsDefined(customBlendModeType, renderMode) ?
            (MaterialReader.CustomBlendMode)Enum.Parse(customBlendModeType, renderMode) :
            MaterialReader.CustomBlendMode.Cutout;

        // Use Daggerfall/Billboard material for standard cutout billboards or create a Standard material if using any other custom blendMode
        if (blendMode == MaterialReader.CustomBlendMode.Cutout)
            return MaterialReader.CreateBillboardMaterial();
        else
            return MaterialReader.CreateStandardMaterial(blendMode);
    }

    void DisableVanillaFootsteps()
    {
        PlayerFootsteps playerFootsteps = GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>();

        playerFootsteps.FootstepSoundDungeon1 = SoundClips.None;
        playerFootsteps.FootstepSoundDungeon2 = SoundClips.None;
        playerFootsteps.FootstepSoundOutside1 = SoundClips.None;
        playerFootsteps.FootstepSoundOutside2 = SoundClips.None;
        playerFootsteps.FootstepSoundSnow1 = SoundClips.None;
        playerFootsteps.FootstepSoundSnow2 = SoundClips.None;
        playerFootsteps.FootstepSoundBuilding1 = SoundClips.None;
        playerFootsteps.FootstepSoundBuilding2 = SoundClips.None;
        playerFootsteps.FootstepSoundShallow = SoundClips.None;
        playerFootsteps.FootstepSoundSubmerged = SoundClips.None;
    }

    void EnableVanillaFootsteps()
    {
        PlayerFootsteps playerFootsteps = GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>();

        playerFootsteps.FootstepSoundDungeon1 = SoundClips.PlayerFootstepStone1;
        playerFootsteps.FootstepSoundDungeon2 = SoundClips.PlayerFootstepStone2;
        playerFootsteps.FootstepSoundOutside1 = SoundClips.PlayerFootstepOutside1;
        playerFootsteps.FootstepSoundOutside2 = SoundClips.PlayerFootstepOutside2;
        playerFootsteps.FootstepSoundSnow1 = SoundClips.PlayerFootstepSnow1;
        playerFootsteps.FootstepSoundSnow2 = SoundClips.PlayerFootstepSnow2;
        playerFootsteps.FootstepSoundBuilding1 = SoundClips.PlayerFootstepWood1;
        playerFootsteps.FootstepSoundBuilding2 = SoundClips.PlayerFootstepWood2;
        playerFootsteps.FootstepSoundShallow = SoundClips.SplashSmallLow;
        playerFootsteps.FootstepSoundSubmerged = SoundClips.SplashSmall;
    }

    void PlayFootstep()
    {
        //this condition helps prevent making a nuisance footstep noise when the player first
        //loads a save, or into an interior or exterior location
        if (GameManager.Instance.SaveLoadManager.LoadInProgress || GameManager.Instance.StreamingWorld.IsRepositioningPlayer)
        {
            wasGrounded = true;
            return;
        }

        if (isTravelling)
            return;

        AudioSource audioSource = GameManager.Instance.PlayerObject.GetComponent<AudioSource>();
        DaggerfallAudioSource dfAudioSource = GameManager.Instance.PlayerObject.GetComponent<DaggerfallAudioSource>();

        PlayerFootsteps playerFootsteps = GameManager.Instance.PlayerObject.GetComponent<PlayerFootsteps>();
        PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
        PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;

        DaggerfallDateTime.Seasons playerSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
        int playerClimateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;

        // Get player inside flag
        // Can only do this when PlayerEnterExit is available, otherwise default to true
        bool playerInside = (playerEnterExit == null) ? true : playerEnterExit.IsPlayerInside;
        bool playerInBuilding = (playerEnterExit == null) ? false : playerEnterExit.IsPlayerInsideBuilding;

        // Play splash footsteps whether player is walking on or swimming in exterior water
        bool playerOnExteriorWater = (GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.Swimming || GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.WaterWalking);

        bool playerOnExteriorPath = GameManager.Instance.PlayerMotor.OnExteriorPath;
        bool playerOnStaticGeometry = GameManager.Instance.PlayerMotor.OnExteriorStaticGeometry;

        SoundClips currentFootstepSound1;
        SoundClips currentFootstepSound2;

        // Change footstep sounds between winter/summer variants, when player enters/exits an interior space, or changes between path, water, or other outdoor ground
        if (!playerInside && !playerOnStaticGeometry)
        {
            if (playerSeason == DaggerfallDateTime.Seasons.Winter && !WeatherManager.IsSnowFreeClimate(playerClimateIndex))
            {
                currentFootstepSound1 = SoundClips.PlayerFootstepSnow1;
                currentFootstepSound2 = SoundClips.PlayerFootstepSnow2;
            }
            else
            {
                currentFootstepSound1 = SoundClips.PlayerFootstepOutside1;
                currentFootstepSound2 = SoundClips.PlayerFootstepOutside2;
            }
        }
        else if (playerInBuilding)
        {
            currentFootstepSound1 = SoundClips.PlayerFootstepWood1;
            currentFootstepSound2 = SoundClips.PlayerFootstepWood2;
        }
        else // in dungeon
        {
            currentFootstepSound1 = SoundClips.PlayerFootstepStone1;
            currentFootstepSound2 = SoundClips.PlayerFootstepStone2;
        }

        // walking on water tile
        if (playerOnExteriorWater)
        {
            currentFootstepSound1 = SoundClips.SplashSmall;
            currentFootstepSound2 = SoundClips.SplashSmall;
        }

        // walking on path tile
        if (playerOnExteriorPath)
        {
            currentFootstepSound1 = SoundClips.PlayerFootstepStone1;
            currentFootstepSound2 = SoundClips.PlayerFootstepStone2;
        }

        // Use water sounds if in dungeon water
        if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && playerEnterExit.blockWaterLevel != 10000)
        {
            // In water, deep depth
            if ((currentFootstepSound1 != SoundClips.SplashSmall) && playerEnterExit.IsPlayerSwimming)
            {
                currentFootstepSound1 = SoundClips.SplashSmall;
                currentFootstepSound2 = SoundClips.SplashSmall;
            }
            // In water, shallow depth
            else if ((currentFootstepSound1 != SoundClips.SplashSmallLow) && !playerEnterExit.IsPlayerSwimming && (playerMotor.transform.position.y - 0.57f) < (playerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale))
            {
                currentFootstepSound1 = SoundClips.SplashSmallLow;
                currentFootstepSound2 = SoundClips.SplashSmallLow;
            }
        }

        // Not in water, reset footsteps to normal
        if ((!playerOnExteriorWater)
            && (currentFootstepSound1 == SoundClips.SplashSmall || currentFootstepSound1 == SoundClips.SplashSmallLow)
            && (playerEnterExit.blockWaterLevel == 10000 || (playerMotor.transform.position.y - 0.95f) >= (playerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale)))
        {
            currentFootstepSound1 = SoundClips.PlayerFootstepStone1;
            currentFootstepSound2 = SoundClips.PlayerFootstepStone2;
        }

        // Check whether player is on foot and abort playing footsteps if not.
        if (playerMotor.IsLevitating || !GameManager.Instance.TransportManager.IsOnFoot && playerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.None)
        {
            return;
        }

        if (footstepSound1 != currentFootstepSound1 || footstepSound2 != currentFootstepSound2)
        {
            footstepSound1 = currentFootstepSound1;
            footstepSound2 = currentFootstepSound2;

            footstep1 = dfAudioSource.GetAudioClip((int)footstepSound1);
            footstep2 = dfAudioSource.GetAudioClip((int)footstepSound2);
        }

        // Check if player is grounded
        // Note: In classic, submerged "footstep" sound is only played when walking on the floor while in the water, but it sounds like a swimming sound
        // and when outside is played while swimming at the water's surface, so it seems better to play it all the time while submerged in water.
        if (!playerMotor.IsSwimming)
        {
            float povMod = 2;

            if (FP)
                povMod = 1;

            if (!footstepAlt)
                audioSource.PlayOneShot(footstep1, playerFootsteps.FootstepVolumeScale * DaggerfallUnity.Settings.SoundVolume * povMod);
            else
                audioSource.PlayOneShot(footstep2, playerFootsteps.FootstepVolumeScale * DaggerfallUnity.Settings.SoundVolume * povMod);

            footstepAlt = !footstepAlt;
        }

        hasPlayedFootstep = true;
    }
}
