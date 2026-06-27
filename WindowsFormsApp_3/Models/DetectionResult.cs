using System;

namespace ImageBatchSystem.Models
{
    public class DetectionResult
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public double BlurScore { get; set; }
        public bool BlurPassed { get; set; }
        public int ResolutionW { get; set; }
        public int ResolutionH { get; set; }
        public bool ResPassed { get; set; }
        public double ColorBiasR { get; set; }
        public double ColorBiasG { get; set; }
        public double ColorBiasB { get; set; }
        public bool ColorPassed { get; set; }
        public bool SuggestPass { get; set; }
        public DateTime DetectedAt { get; set; }

        public DetectionResult()
        {
            DetectedAt = DateTime.Now;
        }
    }
}
