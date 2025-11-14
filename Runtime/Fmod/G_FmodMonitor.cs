/* ---------------------------------------
 * Author:          Custom Graphy Fork Contributors
 * Contributors:    https://github.com/Tayx94/graphy/graphs/contributors
 * Project:         Graphy - Ultimate Stats Monitor
 * Date:            14-Nov-2025
 * Studio:          Tayx
 *
 * Git repo:        https://github.com/Tayx94/graphy
 *
 * This project is released under the MIT license.
 * Attribution is not required, but it is always welcomed!
 * -------------------------------------*/

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Tayx.Graphy.Utils;

namespace Tayx.Graphy.Fmod
{
    public class G_FmodMonitor : MonoBehaviour
    {
        #region Variables -> Private

        private GraphyManager m_graphyManager = null;

        // FMOD System reference - we'll get this dynamically
        private IntPtr m_fmodSystem = IntPtr.Zero;
        private bool m_isInitialized = false;

        // Pre-allocated buffers for GC-free operation
        private G_DoubleEndedQueue m_cpuSamples;
        private G_DoubleEndedQueue m_memorySamples;
        private G_DoubleEndedQueue m_channelsSamples;
        private G_DoubleEndedQueue m_fileUsageSamples;

        private short m_samplesCapacity = 512;

        // Running sums for averages (avoid recalculating)
        private float m_cpuSum = 0f;
        private float m_memorySum = 0f;
        private float m_channelsSum = 0f;
        private float m_fileUsageSum = 0f;

        // Update frequency control
        private float m_updateInterval = 0.1f; // Update every 100ms
        private float m_timeSinceLastUpdate = 0f;

        // FMOD Stats structure (pre-allocated)
        private FMOD.CPU_USAGE m_cpuUsage;
        private int m_currentAllocated;
        private int m_maxAllocated;

        #endregion

        #region Properties -> Public

        // Current values
        public float CurrentFmodCpu { get; private set; } = 0f;
        public float CurrentFmodMemoryMB { get; private set; } = 0f;
        public int CurrentChannelsPlaying { get; private set; } = 0;
        public float CurrentFileUsageKBps { get; private set; } = 0f;

        // Average values
        public float AverageFmodCpu { get; private set; } = 0f;
        public float AverageFmodMemoryMB { get; private set; } = 0f;
        public float AverageChannelsPlaying { get; private set; } = 0f;
        public float AverageFileUsageKBps { get; private set; } = 0f;

        // Peak values
        public float PeakFmodCpu { get; private set; } = 0f;
        public float PeakFmodMemoryMB { get; private set; } = 0f;
        public int PeakChannelsPlaying { get; private set; } = 0;
        public float PeakFileUsageKBps { get; private set; } = 0f;

        public bool IsAvailable => m_isInitialized && m_fmodSystem != IntPtr.Zero;

        #endregion

        #region Methods -> Unity Callbacks

        private void Awake()
        {
            Init();
        }

        private void Update()
        {
            if (!m_isInitialized || m_fmodSystem == IntPtr.Zero)
            {
                // Try to initialize if not ready
                TryInitializeFmod();
                return;
            }

            m_timeSinceLastUpdate += Time.unscaledDeltaTime;
            
            if (m_timeSinceLastUpdate >= m_updateInterval)
            {
                m_timeSinceLastUpdate = 0f;
                UpdateFmodStats();
            }
        }

        private void OnDestroy()
        {
            m_isInitialized = false;
            m_fmodSystem = IntPtr.Zero;
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters()
        {
            // This can be called to refresh settings from GraphyManager
            if (m_graphyManager != null)
            {
                // Get any FMOD-specific settings if added to GraphyManager
            }
        }

        public void Reset()
        {
            // Clear all samples and reset statistics
            m_cpuSamples?.Clear();
            m_memorySamples?.Clear();
            m_channelsSamples?.Clear();
            m_fileUsageSamples?.Clear();

            m_cpuSum = 0f;
            m_memorySum = 0f;
            m_channelsSum = 0f;
            m_fileUsageSum = 0f;

            CurrentFmodCpu = 0f;
            CurrentFmodMemoryMB = 0f;
            CurrentChannelsPlaying = 0;
            CurrentFileUsageKBps = 0f;

            AverageFmodCpu = 0f;
            AverageFmodMemoryMB = 0f;
            AverageChannelsPlaying = 0f;
            AverageFileUsageKBps = 0f;

            PeakFmodCpu = 0f;
            PeakFmodMemoryMB = 0f;
            PeakChannelsPlaying = 0;
            PeakFileUsageKBps = 0f;
        }

        #endregion

        #region Methods -> Private

        private void Init()
        {
            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();

            // Initialize sample buffers
            m_cpuSamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_memorySamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_channelsSamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_fileUsageSamples = new G_DoubleEndedQueue(m_samplesCapacity);

            TryInitializeFmod();
        }

        private void TryInitializeFmod()
        {
            if (m_isInitialized) return;

            try
            {
                // Try to get FMOD system instance
                // This assumes FMOD for Unity is being used
                var studioSystem = FMODUnity.RuntimeManager.StudioSystem;
                if (studioSystem.isValid())
                {
                    FMOD.System coreSystem;
                    var result = studioSystem.getCoreSystem(out coreSystem);
                    if (result == FMOD.RESULT.OK && coreSystem.hasHandle())
                    {
                        m_fmodSystem = coreSystem.handle;
                        m_isInitialized = true;
                        Debug.Log("[Graphy] FMOD monitoring initialized successfully");
                    }
                }
            }
            catch (Exception e)
            {
                // FMOD might not be available or initialized yet
                Debug.LogWarning($"[Graphy] FMOD monitoring not available: {e.Message}");
            }
        }

        private void UpdateFmodStats()
        {
            if (!m_isInitialized || m_fmodSystem == IntPtr.Zero) return;

            try
            {
                // Create a System wrapper for the handle
                FMOD.System system = new FMOD.System(m_fmodSystem);

                // Get CPU usage
                FMOD.RESULT result = system.getCPUUsage(out m_cpuUsage);
                if (result == FMOD.RESULT.OK)
                {
                    // FMOD returns individual CPU percentages, we'll track the sum
                    CurrentFmodCpu = m_cpuUsage.dsp + m_cpuUsage.stream + m_cpuUsage.geometry + m_cpuUsage.update + m_cpuUsage.studio;
                    UpdateStatistic(m_cpuSamples, CurrentFmodCpu, ref m_cpuSum, out AverageFmodCpu);
                    PeakFmodCpu = Mathf.Max(PeakFmodCpu, CurrentFmodCpu);
                }

                // Get memory usage
                result = FMOD.Memory.GetStats(out m_currentAllocated, out m_maxAllocated, false);
                if (result == FMOD.RESULT.OK)
                {
                    CurrentFmodMemoryMB = m_currentAllocated / (1024f * 1024f);
                    UpdateStatistic(m_memorySamples, CurrentFmodMemoryMB, ref m_memorySum, out AverageFmodMemoryMB);
                    PeakFmodMemoryMB = Mathf.Max(PeakFmodMemoryMB, CurrentFmodMemoryMB);
                }

                // Get channels playing
                int channelsPlaying;
                int realChannelsPlaying;
                result = system.getChannelsPlaying(out channelsPlaying, out realChannelsPlaying);
                if (result == FMOD.RESULT.OK)
                {
                    CurrentChannelsPlaying = channelsPlaying;
                    UpdateStatistic(m_channelsSamples, channelsPlaying, ref m_channelsSum, out float avgChannels);
                    AverageChannelsPlaying = avgChannels;
                    PeakChannelsPlaying = Mathf.Max(PeakChannelsPlaying, channelsPlaying);
                }

                // Get file usage
                long sampleBytesRead;
                long streamBytesRead;
                long otherBytesRead;
                result = system.getFileUsage(out sampleBytesRead, out streamBytesRead, out otherBytesRead);
                if (result == FMOD.RESULT.OK)
                {
                    // Convert to KB/s (assuming our update interval)
                    float totalBytesPerSecond = (sampleBytesRead + streamBytesRead + otherBytesRead) / m_updateInterval;
                    CurrentFileUsageKBps = totalBytesPerSecond / 1024f;
                    UpdateStatistic(m_fileUsageSamples, CurrentFileUsageKBps, ref m_fileUsageSum, out AverageFileUsageKBps);
                    PeakFileUsageKBps = Mathf.Max(PeakFileUsageKBps, CurrentFileUsageKBps);

                    // Reset the file usage counters after reading
                    system.resetFileUsage();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Graphy] Error updating FMOD stats: {e.Message}");
                m_isInitialized = false;
            }
        }

        private void UpdateStatistic(G_DoubleEndedQueue samples, float newValue, ref float sum, out float average)
        {
            // Convert float to short for storage (multiply by 100 for precision)
            short storedValue = (short)(newValue * 100);

            if (samples.Full)
            {
                short removedValue = samples.PopFront();
                sum -= removedValue / 100f;
            }

            samples.PushBack(storedValue);
            sum += newValue;

            average = samples.Count > 0 ? sum / samples.Count : 0f;
        }

        #endregion
    }

    #region FMOD Bindings

    // Minimal FMOD bindings for the stats we need
    // These should match the FMOD API exactly
    namespace FMOD
    {
        public enum RESULT
        {
            OK = 0,
            ERR_BADCOMMAND = 1,
            // Add other error codes as needed
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CPU_USAGE
        {
            public float dsp;
            public float stream;
            public float geometry;
            public float update;
            public float studio;
        }

        public struct System
        {
            public IntPtr handle;

            public System(IntPtr ptr)
            {
                handle = ptr;
            }

            public bool hasHandle()
            {
                return handle != IntPtr.Zero;
            }

            [DllImport("fmod")]
            private static extern RESULT FMOD_System_GetCPUUsage(IntPtr system, out CPU_USAGE usage);
            
            public RESULT getCPUUsage(out CPU_USAGE usage)
            {
                return FMOD_System_GetCPUUsage(handle, out usage);
            }

            [DllImport("fmod")]
            private static extern RESULT FMOD_System_GetChannelsPlaying(IntPtr system, out int channels, out int realchannels);

            public RESULT getChannelsPlaying(out int channels, out int realchannels)
            {
                return FMOD_System_GetChannelsPlaying(handle, out channels, out realchannels);
            }

            [DllImport("fmod")]
            private static extern RESULT FMOD_System_GetFileUsage(IntPtr system, out long sampleBytesRead, out long streamBytesRead, out long otherBytesRead);

            public RESULT getFileUsage(out long sampleBytesRead, out long streamBytesRead, out long otherBytesRead)
            {
                return FMOD_System_GetFileUsage(handle, out sampleBytesRead, out streamBytesRead, out otherBytesRead);
            }

            [DllImport("fmod")]
            private static extern RESULT FMOD_System_ResetFileUsage(IntPtr system);

            public RESULT resetFileUsage()
            {
                return FMOD_System_ResetFileUsage(handle);
            }
        }

        public static class Memory
        {
            [DllImport("fmod")]
            private static extern RESULT FMOD_Memory_GetStats(out int currentalloced, out int maxalloced, bool blocking);

            public static RESULT GetStats(out int currentalloced, out int maxalloced, bool blocking)
            {
                return FMOD_Memory_GetStats(out currentalloced, out maxalloced, blocking);
            }
        }
    }

    #endregion
}