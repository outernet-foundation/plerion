using System.Collections.Generic;

namespace Plerion.VPS
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