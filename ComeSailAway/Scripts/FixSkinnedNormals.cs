using UnityEngine;
using System.Collections;

namespace ComeSailAwayMod
{
    public class FixDeformations : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;

        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        Mesh bakedMesh;

        float interval = 0.1f;
        float timer = 0;

        void Awake()
        {
            skinnedMeshRenderer = transform.parent.GetComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.enabled = false;

            meshFilter = gameObject.AddComponent<MeshFilter>();

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.materials = skinnedMeshRenderer.materials;

            bakedMesh = meshFilter.mesh;
        }

        /*void FixedUpdate()
        {
            skinnedMeshRenderer.BakeMesh(bakedMesh);
            bakedMesh.RecalculateNormals();
        }*/

        void LateUpdate()
        {
            if (timer > interval)
            {
                skinnedMeshRenderer.BakeMesh(bakedMesh);
                bakedMesh.RecalculateNormals();
                timer = 0;
            }
            else
                timer += Time.deltaTime;
        }
    }
}