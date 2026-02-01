namespace TS.Audio
{
    public struct ProximityMaterial
    {
        public float AbsorptionLow;
        public float AbsorptionMid;
        public float AbsorptionHigh;
        public float Scattering;
        public float TransmissionLow;
        public float TransmissionMid;
        public float TransmissionHigh;

        public static ProximityMaterial Neutral => new ProximityMaterial
        {
            AbsorptionLow = 0f,
            AbsorptionMid = 0f,
            AbsorptionHigh = 0f,
            Scattering = 0f,
            TransmissionLow = 0f,
            TransmissionMid = 0f,
            TransmissionHigh = 0f
        };
    }

    public struct ProximityAcoustics
    {
        public bool HasProximity;
        public float LeftMeters;
        public float RightMeters;
        public float FrontMeters;
        public float BackMeters;
        public float CeilingMeters;
        public ProximityMaterial LeftMaterial;
        public ProximityMaterial RightMaterial;
        public ProximityMaterial FrontMaterial;
        public ProximityMaterial BackMaterial;
        public ProximityMaterial CeilingMaterial;

        public static ProximityAcoustics None => new ProximityAcoustics
        {
            HasProximity = false,
            LeftMeters = -1f,
            RightMeters = -1f,
            FrontMeters = -1f,
            BackMeters = -1f,
            CeilingMeters = -1f,
            LeftMaterial = ProximityMaterial.Neutral,
            RightMaterial = ProximityMaterial.Neutral,
            FrontMaterial = ProximityMaterial.Neutral,
            BackMaterial = ProximityMaterial.Neutral,
            CeilingMaterial = ProximityMaterial.Neutral
        };
    }
}
