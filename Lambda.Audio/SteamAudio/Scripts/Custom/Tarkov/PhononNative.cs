using System;
using System.Runtime.InteropServices;
using SteamAudio;

namespace Lambda.Audio
{
    internal static class PhononNative
    {
        private const string PHONON = "phonon";

        // Binaural Effect
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplBinauralEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PBinauralEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplBinauralEffectApply(IntPtr effect, ref PBinauralEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplBinauralEffectRelease(ref IntPtr effect);

        // Direct Effect
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplDirectEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PDirectEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplDirectEffectApply(IntPtr effect, ref PDirectEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplDirectEffectRelease(ref IntPtr effect);

        // Reflection Effect
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplReflectionEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PReflectionEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplReflectionEffectApply(IntPtr effect, ref ReflectionEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf, IntPtr mixer);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplReflectionEffectRelease(ref IntPtr effect);

        // Ambisonics Decode Effect
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplAmbisonicsDecodeEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PAmbisonicsDecodeEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplAmbisonicsDecodeEffectApply(IntPtr effect, ref PAmbisonicsDecodeEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplAmbisonicsDecodeEffectRelease(ref IntPtr effect);

        // Native Audio Buffer
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iplAudioBufferAllocate(IntPtr ctx, int numChannels, int numSamples, out PAudioBuffer buf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplAudioBufferFree(IntPtr ctx, ref PAudioBuffer buf);
    }
}