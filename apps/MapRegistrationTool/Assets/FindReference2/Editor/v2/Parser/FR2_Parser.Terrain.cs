using System;
using UnityEngine;
using UnityEditor;
using TerrainTextureData = vietlabs.fr2.FR2_Terrain.TerrainTextureData;
using AddUsageCB = System.Action<string, long>;

namespace vietlabs.fr2
{
    internal static partial class FR2_Parser // Terrain
    {
        // BWCompatible
        internal static void LoadTerrainData(this FR2_Asset asset, TerrainData data)
        {
            Read_TerrainData(data, (string guid, long fileId) => asset.AddUseGUID(guid, fileId));
        }
        
        private static void Read_TerrainData(TerrainData terrain, AddUsageCB callback)
        {
#if UNITY_2018_3_OR_NEWER
            TerrainLayer[] layers = terrain.terrainLayers;
            for (var i = 0; i < layers.Length; i++)
            {
                AddObjectUsage(layers[i], callback);
            }
#endif
            DetailPrototype[] details = terrain.detailPrototypes;
            for (var i = 0; i < details.Length; i++)
            {
                AddObjectUsage(details[i].prototypeTexture, callback);
            }

            TreePrototype[] trees = terrain.treePrototypes;
            for (var i = 0; i < trees.Length; i++)
            {
                AddObjectUsage(trees[i].prefab, callback);
            }
            
            TerrainTextureData[] texDatas = FR2_Terrain.GetTerrainTextureDatas(terrain);
            for (var i = 0; i < texDatas.Length; i++)
            {
                TerrainTextureData texs = texDatas[i];
                for (var k = 0; k < texs.textures.Length; k++)
                {
                    Texture2D tex = texs.textures[k];
                    if (tex == null) continue;

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(tex, out string refGUID, out long fileId);
                    callback(refGUID, fileId);
                }
            }
        }
    }
}
