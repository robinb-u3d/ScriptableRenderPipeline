using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.LightweightRenderPipeline
{
    class MaterialModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        static void OnWillCreateAsset(string asset)
        {
            if (!asset.ToLowerInvariant().EndsWith(".mat"))
            {
                return;
            }
            MaterialPostprocessor.s_CreatedAssets.Add(asset);
        }
    }

    class MaterialPostprocessor : AssetPostprocessor
    {
        static List<string> s_LWRPShaders = new List<string>
        {
            // TODO: Populate this
            // Lit
            "933532a4fcc9baf4fa0491de14d08ed7",
        };

        public static List<string> s_CreatedAssets = new List<string>();

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var asset in importedAssets)
            {
                if (!asset.ToLowerInvariant().EndsWith(".mat"))
                {
                    continue;
                }

                var material = (Material)AssetDatabase.LoadAssetAtPath(asset, typeof(Material));
                var shaderGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material.shader));
                if (!s_LWRPShaders.Contains(shaderGuid))
                {
                    continue;
                }

                var wasUpgraded = false;
                var assetVersion = (AssetVersion)AssetDatabase.LoadAssetAtPath(asset, typeof(AssetVersion));

                if (!assetVersion)
                {
                    wasUpgraded = true;
                    assetVersion = ScriptableObject.CreateInstance<AssetVersion>();
                    if (s_CreatedAssets.Contains(asset))
                    {
                        assetVersion.version = k_Upgraders.Length;
                        s_CreatedAssets.Remove(asset);
                        InitializeLatest(material);
                    }
                    else
                    {
                        assetVersion.version = 0;
                    }

                    assetVersion.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
                    AssetDatabase.AddObjectToAsset(assetVersion, asset);
                }

                while (assetVersion.version < k_Upgraders.Length)
                {
                    k_Upgraders[assetVersion.version](material);
                    assetVersion.version++;
                    wasUpgraded = true;
                }

                if (wasUpgraded)
                {
                    EditorUtility.SetDirty(assetVersion);
                }
            }
        }

        static readonly Action<Material>[] k_Upgraders = { };

        static void InitializeLatest(Material material)
        {
            Debug.Log("Initializing V2: " + material.name);
        }

        static void UpgradeV0(Material material)
        {
            Debug.Log("Upgrading V0 to V1: " + material.name);
        }
    }
}
