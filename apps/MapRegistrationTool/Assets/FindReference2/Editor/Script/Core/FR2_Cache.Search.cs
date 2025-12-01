using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace vietlabs.fr2
{
    internal partial class FR2_Cache
    {
        internal static List<string> FindUsage(string[] listGUIDs)
        {
            if (!isReady) return null;

            var refs = _inst.FindAssets(listGUIDs, true);

            for (var i = 0; i < refs.Count; i++)
            {
                var tmp = FR2_Asset.FindUsage(refs[i]);

                for (var j = 0; j < tmp.Count; j++)
                {
                    var itm = tmp[j];
                    if (refs.Contains(itm)) continue;

                    refs.Add(itm);
                }
            }

            return refs.Select(item => item.guid).ToList();
        }


        internal List<FR2_Asset> FindAssetsOfType(FR2_Asset.AssetType type)
        {
            if (!isReady)
            {
                FR2_LOG.LogWarning("Cache not ready !");
                return null;
            }
            
            var result = new List<FR2_Asset>();
            foreach (KeyValuePair<string, FR2_Asset> item in _map)
            {
                if (item.Value.type != type) continue;
                result.Add(item.Value);
            }

            return result;
        }


        internal List<FR2_Asset> FindAssets(string[] guids, bool scanFolder)
        {
            var result = new List<FR2_Asset>();
            if (!isReady)
            {
                FR2_LOG.LogWarning("Cache not ready !");
                return result;
            }

            if (!isReady)
            {
			    FR2_LOG.LogWarning("Cache not ready !");
                return result;
            }

            var folderList = new List<FR2_Asset>();

            if (guids.Length == 0) return result;

            for (var i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                FR2_Asset asset;
                if (!_map.TryGetValue(guid, out asset)) continue;

                if (asset.IsMissing) continue;

                if (asset.IsFolder)
                {
                    if (!folderList.Contains(asset)) folderList.Add(asset);
                } else
                {
                    result.Add(asset);
                }
            }

            if (!scanFolder || folderList.Count == 0) return result;

            int count = folderList.Count;
            for (var i = 0; i < count; i++)
            {
                FR2_Asset item = folderList[i];

                // for (var j = 0; j < item.UseGUIDs.Count; j++)
                // {
                //     FR2_Asset a;
                //     if (!AssetMap.TryGetValue(item.UseGUIDs[j], out a)) continue;
                foreach (KeyValuePair<string, HashSet<long>> useM in item.UseGUIDs)
                {
                    FR2_Asset a;
                    if (!_map.TryGetValue(useM.Key, out a)) continue;

                    if (a.IsMissing) continue;

                    if (a.IsFolder)
                    {
                        if (!folderList.Contains(a))
                        {
                            folderList.Add(a);
                            count++;
                        }
                    } else
                    {
                        result.Add(a);
                    }
                }
            }

            return result;
        }
    }
} 