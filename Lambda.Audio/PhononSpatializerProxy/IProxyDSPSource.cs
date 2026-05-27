using UnityEngine;

namespace PhononSpatializerProxy
{
    public interface IProxiedAudioSource
    {
        public float spatialBlend { get; set; }
        public bool spatialize { get; set; }
        public bool enabled { get; set; }
    }
}