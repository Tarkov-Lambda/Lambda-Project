namespace Lambda.Audio
{
    public interface IProxiedAudioSource
    {
        public float spatialBlend { get; set; }
        public bool spatialize { get; set; }
        public bool isBypass { get; set; }
    }
}