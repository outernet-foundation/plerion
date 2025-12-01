using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // ----------------------------- GUID MANAGEMENT ---------------------------------------

        public Dictionary<string, HashSet<long>> UseGUIDs
        {
            get
            {
                if (_UseGUIDs != null) return _UseGUIDs;

                _UseGUIDs = new Dictionary<string, HashSet<long>>(UseGUIDsList.Count);
                for (var i = 0; i < UseGUIDsList.Count; i++)
                {
                    string guid = UseGUIDsList[i].guid;
                    if (_UseGUIDs.ContainsKey(guid))
                    {
                        for (var j = 0; j < UseGUIDsList[i].ids.Count; j++)
                        {
                            long val = UseGUIDsList[i].ids[j];
                            if (_UseGUIDs[guid].Contains(val)) continue;

                            _UseGUIDs[guid].Add(UseGUIDsList[i].ids[j]);
                        }
                    } else
                    {
                        _UseGUIDs.Add(guid, new HashSet<long>(UseGUIDsList[i].ids));
                    }
                }

                return _UseGUIDs;
            }
        }

        internal void AddUseGUID(string fguid, long fFileId = -1)
        {
            AddUseGUID(fguid, fFileId, true);
        }

        internal void AddUseGUID(string fguid, long fFileId, bool checkExist)
        {
            // if (checkExist && UseGUIDs.ContainsKey(fguid)) return;
            if (!IsValidGUID(fguid)) return;

            if (!UseGUIDs.ContainsKey(fguid))
            {
                UseGUIDsList.Add(new Classes
                {
                    guid = fguid,
                    ids = new List<long>()
                });
                UseGUIDs.Add(fguid, new HashSet<long>());
            }

            if (fFileId == -1) return;
            if (UseGUIDs[fguid].Contains(fFileId)) return;

            UseGUIDs[fguid].Add(fFileId);
            Classes i = UseGUIDsList.FirstOrDefault(x => x.guid == fguid);
            if (i != null) i.ids.Add(fFileId);
        }

        public void AddUsedBy(string guid, FR2_Asset asset)
        {
            if (guid == this.guid) return;
            if (UsedByMap.ContainsKey(guid)) return;
            
            _usedByMap.Add(guid, asset);
            if (HashUsedByClassesIds == null) HashUsedByClassesIds = new HashSet<long>();

            if (asset.UseGUIDs.TryGetValue(this.guid, out HashSet<long> output))
            {
                foreach (int item in output)
                {
                    HashUsedByClassesIds.Add(item);
                }
            }
        }

        public int UsageCount()
        {
            return _usedByMap.Count;
        }

        public int UseGUIDsCount
        {
            get
            {
                // Return 0 for ignored assets and package assets because we don't scan their content
                if (IsExcluded || inPackages)
                {
                    return 0;
                }
                return UseGUIDs.Count;
            }
        }
        public string DebugUseGUID()
        {
            return $"{guid} : {assetPath}\n{string.Join("\n", UseGUIDsList.Select(item => item.guid).ToArray())}";
        }

        internal static bool IsValidGUID(string guid)
        {
            return guid != FR2_Cache._instGUID;
        }

        internal static List<string> FindUsageGUIDs(FR2_Asset asset, bool includeScriptSymbols)
        {
            var result = new HashSet<string>();
            if (asset == null)
            {
                FR2_LOG.LogWarning("Asset invalid : " + asset.m_assetName);
                return result.ToList();
            }

            foreach (KeyValuePair<string, HashSet<long>> item in asset.UseGUIDs)
            {
                result.Add(item.Key);
            }

            return result.ToList();
        }

        internal static List<string> FindUsedByGUIDs(FR2_Asset asset)
        {
            return asset._usedByMap.Keys.ToList();
        }
    }
} 