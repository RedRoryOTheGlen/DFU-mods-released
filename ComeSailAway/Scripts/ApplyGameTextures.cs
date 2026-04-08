using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace ComeSailAwayMod
{
    public class ApplyGameTextures : MonoBehaviour
    {
        private bool hasAppliedMaterials = false;

        void Awake()
        {
            Debug.Log("COME SAIL AWAY - SKINNED RUNTIME MATERIALS SCRIPT AWAKENS!");

            // Apply materials when the gameobject is first instantiated (not when cloned).
            if (!hasAppliedMaterials)
            {
                Debug.Log("COME SAIL AWAY - SKINNED RUNTIME MATERIALS SCRIPT INITIALIZING!");
                ApplyMaterials();
            }
        }

        public void ApplyMaterials()
        {
            SkinnedMeshRenderer meshRenderer = GetComponent<SkinnedMeshRenderer>();
            if (!meshRenderer)
            {
                Debug.LogErrorFormat("Failed to find MeshRenderer on {0}.", name);
                return;
            }

            Material[] materials = meshRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                int archive = 0;
                int record = 0;

                if (transform.childCount < 1)
                    continue;

                //we have to extract the archive and record from SOMEWHERE
                //try recording it in the names of the mesh's child objects
                string childName = transform.GetChild(i).name;
                int midIndex = childName.IndexOf('_');
                archive = Convert.ToInt32(childName.Substring(0, midIndex));
                record = Convert.ToInt32(childName.Substring(midIndex + 1, childName.Length-1-midIndex));

                Debug.Log("COME SAIL AWAY - APPLY GAME TEXTURES CALLS FOR MATERIAL " + archive.ToString() + "_" + record.ToString());

                materials[i] = GetMaterial(archive,record);
            }
            meshRenderer.sharedMaterials = materials;

            hasAppliedMaterials = true;
        }

        public Material GetMaterial(int archive, int record)
        {
            return DaggerfallUnity.Instance.MaterialReader.GetMaterial(archive, record);
        }
    }
}
