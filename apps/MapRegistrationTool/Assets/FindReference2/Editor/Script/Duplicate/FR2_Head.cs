using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace vietlabs.fr2
{
    internal class FR2_Head
    {
        public const int chunkSize = 10240;

        public List<FR2_Chunk> chunkList;
        public int currentChunk;
        public long fileSize;
        public int nChunk;
        public int size; //last stream read size
        public string groupKey; // For tracking group identity in verification queue

        public bool isDead => currentChunk == nChunk || chunkList.Count == 1;

        public List<string> GetFiles()
        {
            return chunkList.Select(item => item.file).ToList();
        }

        public void AddToDict(byte b, FR2_Chunk chunk, Dictionary<byte, List<FR2_Chunk>> dict)
        {
            List<FR2_Chunk> list;
            if (!dict.TryGetValue(b, out list))
            {
                list = new List<FR2_Chunk>();
                dict.Add(b, list);
            }

            list.Add(chunk);
        }

        public void CloseChunk()
        {
            for (var i = 0; i < chunkList.Count; i++)
            {
                FR2_FileCompare.streamClosedCount++;

                if (chunkList[i].stream != null)
                {
                    chunkList[i].stream.Close();
                    chunkList[i].stream = null;
                }
            }
        }

        public void ReadChunk()
        {
            if (currentChunk == nChunk)
            {
                FR2_LOG.LogWarning("Something wrong, should dead <" + isDead + ">");
                return;
            }

            int from = currentChunk * chunkSize;
            size = (int)Mathf.Min(fileSize - from, chunkSize);

            for (var i = 0; i < chunkList.Count; i++)
            {
                FR2_Chunk chunk = chunkList[i];
                if (chunk.streamError) continue;
                chunk.size = size;

                if (chunk.streamInited == false)
                {
                    chunk.streamInited = true;

                    try
                    {
                        chunk.stream = new FileStream(chunk.file, FileMode.Open, FileAccess.Read);
                    }
                    catch
                    {
                        chunk.streamError = true;
                        if (chunk.stream != null) // just to make sure we close the stream
                        {
                            chunk.stream.Close();
                            chunk.stream = null;
                        }
                    }

                    if (chunk.stream == null)
                    {
                        chunk.streamError = true;
                        continue;
                    }
                }

                try
                {
                    chunk.stream.Seek(from, SeekOrigin.Begin);
                    chunk.stream.Read(chunk.buffer, 0, size);
                } 
                catch (Exception e)
                {
                    FR2_LOG.LogWarning(e + "\n" + chunk.file);

                    chunk.streamError = true;
                    chunk.stream.Close();
                }
            }

            // clean up dead chunks
            for (int i = chunkList.Count - 1; i >= 0; i--)
            {
                if (chunkList[i].streamError) chunkList.RemoveAt(i);
            }

            if (chunkList.Count == 1) Debug.LogWarning("No more chunk in list");

            currentChunk++;
        }

        public void CompareChunk(List<FR2_Head> heads)
        {
            int idx = chunkList.Count;
            byte[] buffer = chunkList[idx - 1].buffer;

            while (--idx >= 0)
            {
                FR2_Chunk chunk = chunkList[idx];
                int diff = FirstDifferentIndex(buffer, chunk.buffer, size);
                if (diff == -1) continue;

                byte v = buffer[diff];

                var d = new Dictionary<byte, List<FR2_Chunk>>(); //new heads
                chunkList.RemoveAt(idx);
                FR2_FileCompare.HashChunksNotComplete.Add(chunk);

                AddToDict(chunk.buffer[diff], chunk, d);

                for (int j = idx - 1; j >= 0; j--)
                {
                    FR2_Chunk tChunk = chunkList[j];
                    byte tValue = tChunk.buffer[diff];
                    if (tValue == v) continue;

                    idx--;
                    FR2_FileCompare.HashChunksNotComplete.Add(tChunk);
                    chunkList.RemoveAt(j);
                    AddToDict(tChunk.buffer[diff], tChunk, d);
                }

                foreach (KeyValuePair<byte, List<FR2_Chunk>> item in d)
                {
                    List<FR2_Chunk> list = item.Value;
                    if (list.Count == 1)
                    {
                        if (list[0].stream != null) list[0].stream.Close();
                    } 
                    else if (list.Count > 1) // 1 : dead head
                    {
                        heads.Add(new FR2_Head
                        {
                            nChunk = nChunk,
                            fileSize = fileSize,
                            currentChunk = currentChunk - 1,
                            chunkList = list,
                            groupKey = groupKey // Propagate the groupKey to new heads
                        });
                    }
                }
            }
        }

        internal static int FirstDifferentIndex(byte[] arr1, byte[] arr2, int maxIndex)
        {
            for (var i = 0; i < maxIndex; i++)
            {
                if (arr1[i] != arr2[i]) return i;
            }

            return -1;
        }
    }
}
