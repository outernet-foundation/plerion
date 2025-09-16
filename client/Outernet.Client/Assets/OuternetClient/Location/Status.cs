using System.Collections.Generic;
using UnityEngine;

namespace Outernet.Client.Location
{
    public class Status
    {
        public float localizationCount;
        public float successCount;
        public List<float> confidenceHistory = new List<float>();

        public void AddLocalization(CameraPoseEstimate? cameraPoseEstimation)
        {
            localizationCount++;

            if (cameraPoseEstimation != null)
            {
                successCount++;
                confidenceHistory.Add(cameraPoseEstimation.Value.confidence);
            }
        }
    }
}