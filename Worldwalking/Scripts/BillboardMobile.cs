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
    public class BillboardMobile : MonoBehaviour
    {
        static readonly Type customBlendModeType = typeof(MaterialReader.CustomBlendMode);

        public const int numberOrientations = 8;
        public const float anglePerOrientation = 360f / numberOrientations;

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

        public virtual float GetSizeMod()
        {
            return 0.019f * scaleOffset;
        }
        public float scale = 1;
        public float scaleOffsetMod = 1;
        public float scaleOffset
        {
            get
            {
                return scale * scaleOffsetMod;
            }

        }

        public Camera mainCamera = null;
        public MeshFilter meshFilter = null;
        public MeshRenderer meshRenderer = null;
        public Vector3 lastMoveDirection;
        public int lastOrientation;
        public Color lastColor;

        public Vector3 cameraPosition;
        public float currentAngle;

        public float orientationTime = 0.1f;
        public float orientationTimer;

        public int frameCurrent;

        public float walkAnimSpeedMod = 1;
        public virtual float GetFrameTime()
        {
            if (isRiding)
                return 0.0625f * (2 - walkAnimSpeedMod);
            else
                return 0.25f * (2 - walkAnimSpeedMod);
        }

        public float frameTimer;

        public class MobileBillboardState
        {
            public string name;
            public List<Texture2D> frames = new List<Texture2D>();
            public bool mirror;
            public Rect rect;

            XMLManager xml;

            public MobileBillboardState(string newName, int archive, int record, int frame, int count, bool newMirror = false)
            {
                name = newName;
                mirror = newMirror;

                InitializeTextures(archive, record, count);

                Debug.Log("BillboardState " + name + " was initialized with " + frames.Count + " frames");
            }

            void InitializeTextures(int archive, int record, int count)
            {
                //find all frames of the record
                int current = 0;

                Texture2D texture = null;
                if (archive > 512)
                {
                    while (TextureReplacement.TryImportTexture(archive, record, current, out texture))
                    {
                        texture.name = archive.ToString() + "_" + record.ToString() + "-" + current.ToString();
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                        frames.Add(texture);
                        current++;
                    }
                }
                else
                {
                    while (current < count)
                    {
                        texture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(archive, record, current);
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = (FilterMode)DaggerfallUnity.Settings.MainFilterMode;
                        frames.Add(texture);
                        current++;
                    }
                }

                /*Texture2D texture = null;
                while (current < 20)
                {
                    texture = DaggerfallUnity.Instance.MaterialReader.TextureReader.GetTexture2D(archive, record, current);
                    if (texture != null)
                    {
                        frames.Add(texture);
                        current++;
                    }
                    else
                        break;
                }*/

                //check for XML of first frame, otherwise get rect from dimensions of first frame
                if (frames.Count > 0)
                {
                    if (XMLManager.TryReadXml(TextureReplacement.TexturesPath, frames[0].name, out xml))
                    {
                        Vector2 scale = Vector2.zero;
                        xml.TryGetVector2("scaleX", "scaleY", out scale);
                        rect = new Rect(0, 0, frames[0].width * scale.x, frames[0].height * scale.y);
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

        public MobileBillboardState[] stateCurrent;
        public MobileBillboardState[] stateLast;

        MobileBillboardState[] StatesIdle;
        MobileBillboardState[] StatesMove;
        MobileBillboardState[] StatesAttack;

        public int archive = -1;

        public Vector2 offsetDefault = -Vector2.one;
        public Vector2 offsetAttack = -Vector2.one;

        public bool isMoving;
        public bool isSwimming;
        public bool isRiding;
        public bool isFlying;

        int[] riderArchives = new int[]
        {
            491,
            492,
            493,
            494,
            495,
            1500,
            1501,
            1502,
            1503,
            1506,
            1530,
            1531,
            1532,
            1533,
        };

        int[] commonerArchives = new int[]
        {
            381,
            382,
            383,
            384,
            385,
            386,
            387,
            388,
            389,
            390,
            391,
            392,
            393,
            394,
            395,
            396,
            397,
            398,
            451,
            452,
            453,
            454,
            455,
            456,
        };

        //uses the first movement frames per orientation for idle
        int[] lycanArchives = new int[]
        {
            264,
            269,
        };

        //plays movement animation when idle
        int[] flyerArchives = new int[]
        {
            266,
            268,
            296,
            297,
        };

        public bool IsCommonerArchive(int testArchive)
        {
            bool value = false;
            foreach (int commonerArchive in commonerArchives)
            {
                if (commonerArchive == testArchive)
                {
                    value = true;
                    break;
                }
            }

            return value;
        }

        public bool IsRiderArchive(int testArchive)
        {
            bool value = false;
            foreach (int riderArchive in riderArchives)
            {
                if (riderArchive == testArchive)
                {
                    value = true;
                    break;
                }
            }

            return value;
        }

        public bool IsLycanArchive(int testArchive)
        {
            bool value = false;
            foreach (int lycanArchive in lycanArchives)
            {
                if (lycanArchive == testArchive)
                {
                    value = true;
                    break;
                }
            }

            return value;
        }

        public bool IsFlyerArchive(int testArchive)
        {
            bool value = false;
            foreach (int flyerArchive in flyerArchives)
            {
                if (flyerArchive == testArchive)
                {
                    value = true;
                    break;
                }
            }

            return value;
        }

        public virtual void InitializeStates()
        {
            if (IsCommonerArchive(archive))
            {
                //commoner archive
                StatesIdle = new MobileBillboardState[numberOrientations];
                StatesIdle[0] = new MobileBillboardState("IdleForward", archive, 0, 0, 1, false);
                StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archive, 1, 0, 1, false);
                StatesIdle[2] = new MobileBillboardState("IdleLeft", archive, 2, 0, 2, false);
                StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archive, 3, 0, 1, false);
                StatesIdle[4] = new MobileBillboardState("IdleBackward", archive, 4, 0, 1, false);
                StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archive, 3, 0, 1, false);
                StatesIdle[6] = new MobileBillboardState("IdleRight", archive, 2, 0, 1, false);
                StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archive, 1, 0, 1, false);

                StatesMove = new MobileBillboardState[numberOrientations];
                StatesMove[0] = new MobileBillboardState("MoveForward", archive, 0, 0, 4, false);
                StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archive, 1, 0, 4, true);
                StatesMove[2] = new MobileBillboardState("MoveLeft", archive, 2, 0, 4, true);
                StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archive, 3, 0, 4, true);
                StatesMove[4] = new MobileBillboardState("MoveBackward", archive, 4, 0, 4, false);
                StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archive, 3, 0, 4, false);
                StatesMove[6] = new MobileBillboardState("MoveRight", archive, 2, 0, 4, false);
                StatesMove[7] = new MobileBillboardState("MoveForwardRight", archive, 1, 0, 4, false);
            }
            else if (IsLycanArchive(archive))
            {
                //lycan archive
                StatesIdle = new MobileBillboardState[numberOrientations];
                StatesIdle[0] = new MobileBillboardState("IdleForward", archive, 0, 0, 1, false);
                StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archive, 1, 0, 1, false);
                StatesIdle[2] = new MobileBillboardState("IdleLeft", archive, 2, 0, 2, false);
                StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archive, 3, 0, 1, false);
                StatesIdle[4] = new MobileBillboardState("IdleBackward", archive, 4, 0, 1, false);
                StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archive, 3, 0, 1, false);
                StatesIdle[6] = new MobileBillboardState("IdleRight", archive, 2, 0, 1, false);
                StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archive, 1, 0, 1, false);

                StatesMove = new MobileBillboardState[numberOrientations];
                StatesMove[0] = new MobileBillboardState("MoveForward", archive, 0, 0, 4, false);
                StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archive, 1, 0, 4, true);
                StatesMove[2] = new MobileBillboardState("MoveLeft", archive, 2, 0, 4, true);
                StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archive, 3, 0, 4, true);
                StatesMove[4] = new MobileBillboardState("MoveBackward", archive, 4, 0, 4, false);
                StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archive, 3, 0, 4, false);
                StatesMove[6] = new MobileBillboardState("MoveRight", archive, 2, 0, 4, false);
                StatesMove[7] = new MobileBillboardState("MoveForwardRight", archive, 1, 0, 4, false);
            }
            else if (IsFlyerArchive(archive))
            {
                //flyer archive
                StatesIdle = new MobileBillboardState[numberOrientations];
                StatesIdle[0] = new MobileBillboardState("IdleForward", archive, 0, 0, 4, false);
                StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archive, 1, 0, 4, false);
                StatesIdle[2] = new MobileBillboardState("IdleLeft", archive, 2, 0, 2, false);
                StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archive, 3, 0, 4, false);
                StatesIdle[4] = new MobileBillboardState("IdleBackward", archive, 4, 0, 4, false);
                StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archive, 3, 0, 4, false);
                StatesIdle[6] = new MobileBillboardState("IdleRight", archive, 2, 0, 1, false);
                StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archive, 1, 0, 4, false);

                StatesMove = new MobileBillboardState[numberOrientations];
                StatesMove[0] = new MobileBillboardState("MoveForward", archive, 0, 0, 4, false);
                StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archive, 1, 0, 4, true);
                StatesMove[2] = new MobileBillboardState("MoveLeft", archive, 2, 0, 4, true);
                StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archive, 3, 0, 4, true);
                StatesMove[4] = new MobileBillboardState("MoveBackward", archive, 4, 0, 4, false);
                StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archive, 3, 0, 4, false);
                StatesMove[6] = new MobileBillboardState("MoveRight", archive, 2, 0, 4, false);
                StatesMove[7] = new MobileBillboardState("MoveForwardRight", archive, 1, 0, 4, false);
            }
            else if (IsRiderArchive(archive))
            {
                //horse archive
                isRiding = true;

                StatesIdle = new MobileBillboardState[numberOrientations];
                StatesIdle[0] = new MobileBillboardState("IdleForward", archive, 0, 0, 1, false);
                StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archive, 1, 0, 1, true);
                StatesIdle[2] = new MobileBillboardState("IdleLeft", archive, 2, 0, 1, true);
                StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archive, 3, 0, 1, true);
                StatesIdle[4] = new MobileBillboardState("IdleBackward", archive, 4, 0, 1, false);
                StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archive, 3, 0, 1, false);
                StatesIdle[6] = new MobileBillboardState("IdleRight", archive, 2, 0, 1, false);
                StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archive, 1, 0, 1, false);

                StatesMove = new MobileBillboardState[numberOrientations];
                StatesMove[0] = new MobileBillboardState("MoveForward", archive, 0, 0, 8, false);
                StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archive, 1, 0, 8, true);
                StatesMove[2] = new MobileBillboardState("MoveLeft", archive, 2, 0, 8, true);
                StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archive, 3, 0, 8, true);
                StatesMove[4] = new MobileBillboardState("MoveBackward", archive, 4, 0, 8, false);
                StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archive, 3, 0, 8, false);
                StatesMove[6] = new MobileBillboardState("MoveRight", archive, 2, 0, 4, false);
                StatesMove[7] = new MobileBillboardState("MoveForwardRight", archive, 1, 0, 8, false);
            }
            else
            {
                StatesIdle = new MobileBillboardState[numberOrientations];
                StatesIdle[0] = new MobileBillboardState("IdleForward", archive, 15, 0, 1, false);
                StatesIdle[1] = new MobileBillboardState("IdleForwardLeft", archive, 16, 0, 1, true);
                StatesIdle[2] = new MobileBillboardState("IdleLeft", archive, 17, 0, 1, true);
                StatesIdle[3] = new MobileBillboardState("IdleBackwardLeft", archive, 18, 0, 1, true);
                StatesIdle[4] = new MobileBillboardState("IdleBackward", archive, 19, 0, 1, false);
                StatesIdle[5] = new MobileBillboardState("IdleBackwardRight", archive, 18, 0, 1, false);
                StatesIdle[6] = new MobileBillboardState("IdleRight", archive, 17, 0, 1, false);
                StatesIdle[7] = new MobileBillboardState("IdleForwardRight", archive, 16, 0, 1, false);

                StatesMove = new MobileBillboardState[numberOrientations];
                StatesMove[0] = new MobileBillboardState("MoveForward", archive, 0, 0, 4, false);
                StatesMove[1] = new MobileBillboardState("MoveForwardLeft", archive, 1, 0, 4, true);
                StatesMove[2] = new MobileBillboardState("MoveLeft", archive, 2, 0, 4, true);
                StatesMove[3] = new MobileBillboardState("MoveBackwardLeft", archive, 3, 0, 4, true);
                StatesMove[4] = new MobileBillboardState("MoveBackward", archive, 4, 0, 4, false);
                StatesMove[5] = new MobileBillboardState("MoveBackwardRight", archive, 3, 0, 4, false);
                StatesMove[6] = new MobileBillboardState("MoveRight", archive, 2, 0, 4, false);
                StatesMove[7] = new MobileBillboardState("MoveForwardRight", archive, 1, 0, 4, false);
            }
        }

        // Start is called before the first frame update
        public virtual void Initialize(int newArchive, float size, float sizeOffset)
        {
            if (!IsReady)
                return;

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

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            if (stateLast != null)
                stateCurrent = stateLast;
            else
                stateCurrent = StatesIdle;

            UpdateOrientation(true);
        }

        public virtual void Update()
        {
            if (orientationTime > 0)
            {
                if (orientationTimer <= orientationTime)
                    orientationTimer += Time.unscaledDeltaTime;
            }
        }

        public virtual void LateUpdate()
        {
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

        public virtual void LoopIdleBillboard()
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
                stateCurrent = StatesMove;
            }
            else
            {
                stateCurrent = StatesIdle;
            }

            if (forceUpdate || stateLast != stateCurrent)
                UpdateBillboardDelayed(frameCurrent, lastOrientation, stateCurrent);

            stateLast = stateCurrent;
        }

        public virtual void UpdateOrientation(bool force = false)
        {
            Transform parent = transform.parent;
            if (parent == null)
                return;

            if (orientationTime > 0 && !force)
            {
                if (orientationTimer > orientationTime)
                    orientationTimer = 0;
                else
                    return;
            }

            // Get direction normal to camera, ignore y axis
            Vector3 dir = Vector3.Normalize(
                new Vector3(cameraPosition.x, 0, cameraPosition.z) -
                new Vector3(parent.position.x, 0, parent.position.z));

            //handle vertical movement
            Vector3 currentMoveDirection = GameManager.Instance.PlayerMotor.MoveDirection;
            currentMoveDirection.y = 0;

            if (currentMoveDirection == Vector3.zero)
            {
                if (lastMoveDirection == Vector3.zero)
                    currentMoveDirection = parent.forward;
                else
                    currentMoveDirection = lastMoveDirection;
            }

            // Get parent forward normal, ignore y axis
            Vector3 parentForward = parent.forward;

            lastMoveDirection = parentForward;

            currentAngle = Vector3.SignedAngle(dir, parentForward, Vector3.up);

            // Facing index
            int orientation = -Mathf.RoundToInt(currentAngle / anglePerOrientation);
            // Wrap values to 0 .. numberOrientations-1
            orientation = (orientation + numberOrientations) % numberOrientations;

            // Change enemy to this orientation
            if (orientation != lastOrientation || force || meshRenderer.material.mainTexture == null)
            {
                UpdateBillboardDelayed(frameCurrent, orientation, stateCurrent);
            }
        }

        //Use this when changing states
        //Delay is to make sure stuff like collider height is properly set before updating the billboard
        public virtual void UpdateBillboardDelayed(int frame, int orientation, MobileBillboardState[] states)
        {
            StartCoroutine(UpdateBillboardDelayedCoroutine(frame, orientation, states));
        }

        IEnumerator UpdateBillboardDelayedCoroutine(int frame, int orientation, MobileBillboardState[] states)
        {
            for (int i = 0; i < 3; i++)
                yield return null;

            UpdateBillboard(frame, orientation, states);
        }

        /// <summary>
        /// Sets enemy orientation index.
        /// </summary>
        /// <param name="orientation">New orientation index.</param>
        public virtual void UpdateBillboard(int frame, int orientation, MobileBillboardState[] states)
        {
            // Get mesh filter
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            bool flip = states[orientation].mirror;

            // Assign imported texture
            if (frame > states[orientation].frames.Count - 1)
                frame = states[orientation].frames.Count - 1;

            Rect rect = states[orientation].rect;

            Vector2 size = rect.size * GetSizeMod();

            // Set mesh scale for this state
            transform.localScale = new Vector3(size.x, size.y, 1);
            if (isFlying)
                transform.localPosition = rect.position + (Vector2.up * size.y);
            else if (isSwimming)
                transform.localPosition = rect.position;
            else
                transform.localPosition = rect.position + (Vector2.up * size.y * 0.5f);

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
        }

        /// <summary>
        /// Creates mesh and material for this enemy.
        /// </summary>
        /// <param name="dfUnity">DaggerfallUnity singleton. Required for content readers and settings.</param>
        /// <param name="archive">Texture archive index derived from type and gender.</param>
        public void AssignMeshAndMaterial()
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
    }
}

