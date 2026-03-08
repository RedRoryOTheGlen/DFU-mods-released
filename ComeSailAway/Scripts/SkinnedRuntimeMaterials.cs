using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;

namespace ComeSailAwayMod
{
    public class SkinnedRuntimeMaterials : MonoBehaviour
    {
        private bool hasAppliedMaterials = false;
        public RuntimeMaterial[] Materials;

        void Start()
        {
            Debug.Log("COME SAIL AWAY - SKINNED RUNTIME MATERIALS SCRIPT AWAKENS!");

            // Apply materials when the gameobject is first instantiated (not when cloned).
            if (!hasAppliedMaterials)
            {
                Debug.Log("COME SAIL AWAY - SKINNED RUNTIME MATERIALS SCRIPT INITIALIZING!");
                ApplyMaterials(false);
            }
        }

        public void ApplyMaterials(bool force)
        {
            if (Materials == null || Materials.Length == 0)
                return;

            SkinnedMeshRenderer meshRenderer = GetComponent<SkinnedMeshRenderer>();
            if (!meshRenderer)
            {
                Debug.LogErrorFormat("Failed to find MeshRenderer on {0}.", name);
                return;
            }

            Material[] materials = meshRenderer.sharedMaterials;
            for (int i = 0; i < Materials.Length; i++)
            {
                int index = Materials[i].Index;

                materials[index] = GetMaterial(Materials[i]);
                if (!materials[index])
                    Debug.LogErrorFormat("Failed to find material for {0} (index {1}).", meshRenderer.name, i);
            }
            meshRenderer.sharedMaterials = materials;

            hasAppliedMaterials = true;
        }

        public Material GetMaterial(RuntimeMaterial runtimeMaterial)
        {
            int archive = runtimeMaterial.Archive;
            int record = runtimeMaterial.Record;

            return DaggerfallUnity.Instance.MaterialReader.GetMaterial(archive, record);
        }
    }
}
