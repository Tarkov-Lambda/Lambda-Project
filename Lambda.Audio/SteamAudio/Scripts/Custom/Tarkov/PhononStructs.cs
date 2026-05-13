using System;
using System.Runtime.InteropServices;

namespace Lambda.Audio
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioSettings
    {
        public int samplingRate;
        public int frameSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PVec3
    {
        public float x, y, z;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectSettings
    {
        public IntPtr hrtf;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectParams
    {
        public PVec3 direction;
        public int interpolation;
        public float spatialBlend;
        public IntPtr hrtf;
        public IntPtr peakDelays;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectSettings
    {
        public int numChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectParams
    {
        public int flags;
        public int transmissionType;
        public float distanceAttenuation;
        public float airAbsorptionLow, airAbsorptionMid, airAbsorptionHigh;
        public float directivity;
        public float occlusion;
        public float transmissionLow, transmissionMid, transmissionHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PReflectionEffectSettings
    {
        public int type;        // IPLReflectionEffectType
        public int numChannels; // (maxOrder+1)^2
        public int irSize;      // maxDuration * samplingRate
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PSpeakerLayout
    {
        public int type;        // IPLSpeakerLayoutType: 1 = Stereo
        public int numSpeakers; // 0 for standard layouts
        public IntPtr speakers; // nullptr for standard layouts
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectSettings
    {
        public PSpeakerLayout speakerLayout;
        public IntPtr hrtf;
        public int maxOrder;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PCoordinateSpace3
    {
        public PVec3 right;
        public PVec3 up;
        public PVec3 ahead;
        public PVec3 origin;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectParams
    {
        public int order;
        public IntPtr hrtf;
        public PCoordinateSpace3 orientation;
        public int binaural; // IPLBool: 0 = false, 1 = true
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioBuffer
    {
        public int numChannels;
        public int numSamples;
        public IntPtr data; // float** — one float* per channel
    }
}