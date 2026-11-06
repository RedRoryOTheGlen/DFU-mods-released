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
using static WorldwalkingMod.BillboardMobile;

namespace WorldwalkingMod
{
    public class Billboard : MonoBehaviour
    {
        static readonly Type customBlendModeType = typeof(MaterialReader.CustomBlendMode);

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

        float sizeMod
        {
            get
            {
                return 0.019f * scaleOffset;
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

        public Camera mainCamera = null;
        public MeshFilter meshFilter = null;
        public MeshRenderer meshRenderer = null;

        public Vector3 cameraPosition;
        public float currentAngle;

        int frameCurrent;
        float frameTime
        {
            get
            {
                return 0.25f;
            }
        }
        float frameTimer;

        public int archive = 0;
        public int record = 0;
        public int frameCount = 1;

        public class BillboardState
        {
            public string name;
            public List<Texture2D> frames = new List<Texture2D>();
            public bool mirror;
            public Rect rect;

            XMLManager xml;

            public BillboardState(string newName, int archive, int record, int frame, int count, bool newMirror = false)
            {
                name = newName;
                mirror = newMirror;

                InitializeTextures(archive, record, count);

                Debug.Log("PlayerBillboardState " + name + " was initialized with " + frames.Count + " frames");
            }

            void InitializeTextures(int archive, int record, int count)
            {
                //find all frames of the record
                Debug.Log("Initializing textures for archive " + archive.ToString() + " with record " + record.ToString() + " for " + count.ToString() + " frames");
                int current = 0;

                Texture2D texture = null;
                if (archive > 1024)
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

        public BillboardState state;

        public virtual void InitializeStates()
        {
            //set textures to state
            state = new BillboardState("Default", archive, record, 0, frameCount, false);
        }

        // Start is called before the first frame update
        public virtual void Initialize(int newArchive, int newRecord, int newFrameCount, float size, float sizeOffset)
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

            if (archive != newArchive || record != newRecord || frameCount != newFrameCount)
            {
                archive = newArchive;
                record = newRecord;
                frameCount = newFrameCount;
                InitializeStates();
            }

            if (meshRenderer.sharedMaterial == null)
                AssignMeshAndMaterial();

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            UpdateBillboardDelayed(frameCurrent);
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

                LoopIdleBillboard();
            }
        }

        public virtual void LoopIdleBillboard()
        {
            if (state.frames.Count > 1)
            {
                float speed = 1f;

                if (frameTimer > frameTime * speed)
                {
                    if (frameCurrent < state.frames.Count - 1)
                        frameCurrent++;
                    else
                        frameCurrent = 0;

                    // Assign imported texture
                    if (frameCurrent > state.frames.Count - 1)
                        frameCurrent = 0;

                    UpdateBillboardDelayed(frameCurrent);
                    frameTimer = 0;
                }
                else
                {
                    //static billboards should play animations even when timescale is paused
                    //frameTimer += Time.unscaledDeltaTime * Worldwalking.Instance.timeScaleIndex;
                    frameTimer += Time.unscaledDeltaTime;
                }
            }
        }

        //Use this when changing states
        //Delay is to make sure stuff like collider height is properly set before updating the billboard
        void UpdateBillboardDelayed(int frame)
        {
            StartCoroutine(UpdateBillboardDelayedCoroutine(frame));
        }

        IEnumerator UpdateBillboardDelayedCoroutine(int frame)
        {
            for (int i = 0; i < 3; i++)
                yield return null;

            UpdateBillboard(frame);
        }

        /// <summary>
        /// Sets enemy orientation index.
        /// </summary>
        /// <param name="orientation">New orientation index.</param>
        private void UpdateBillboard(int frame)
        {
            // Get mesh filter
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            bool flip = state.mirror;

            // Assign imported texture
            if (frame > state.frames.Count - 1)
                frame = state.frames.Count - 1;

            Rect rect = state.rect;

            Vector2 size = rect.size * sizeMod;

            // Set mesh scale for this state
            transform.localScale = new Vector3(size.x, size.y, 1);
            transform.localPosition = rect.position + (Vector2.up * size.y * 0.5f);

            // Update Record/Frame texture
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            meshRenderer.material.mainTexture = state.frames[frame];

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

