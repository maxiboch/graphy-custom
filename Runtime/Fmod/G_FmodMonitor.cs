/* ---------------------------------------
 * Author:          Maxi Boch (@maxiboch)
 * Contributors:    https://github.com/Tayx94/graphy/graphs/contributors
 * Project:         Graphy - Ultimate Stats Monitor
 * Date:            14-Nov-2024
 * Studio:          Tayx
 *
 * Git repo:        https://github.com/Tayx94/graphy
 *
 * This project is released under the MIT license.
 * Attribution is not required, but it is always welcomed!
 * -------------------------------------*/

#if GRAPHY_FMOD || UNITY_EDITOR

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
        private IntPtr m_masterChannelGroup = IntPtr.Zero;
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
        
        // Audio metering
        private FMOD.DSP_METERING_INFO m_meteringInfo;
        private float[] m_rmsLevels = new float[32]; // Max 32 channels
        private float[] m_peakLevels = new float[32];
        private G_DoubleEndedQueue m_leftRmsSamples;
        private G_DoubleEndedQueue m_rightRmsSamples;
        private G_DoubleEndedQueue m_leftPeakSamples;
        private G_DoubleEndedQueue m_rightPeakSamples;
        private float m_leftRmsSum = 0f;
        private float m_rightRmsSum = 0f;
        
        // FFT Spectrum analysis
        private IntPtr m_fftDsp = IntPtr.Zero;
        private int m_spectrumSize = 512;  // Default spectrum size
        private float[] m_spectrumData = null;
        private FMOD.DSP_PARAMETER_FFT m_fftParameter;
        private IntPtr m_unmanagedSpectrum = IntPtr.Zero;
        private bool m_fftEnabled = false;

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

        // Audio level properties
        public float CurrentLeftRMS { get; private set; } = 0f;
        public float CurrentRightRMS { get; private set; } = 0f;
        public float CurrentLeftPeak { get; private set; } = 0f;
        public float CurrentRightPeak { get; private set; } = 0f;
        public float AverageLeftRMS { get; private set; } = 0f;
        public float AverageRightRMS { get; private set; } = 0f;
        
        // FFT Spectrum properties
        public float[] SpectrumData => m_spectrumData;
        public int SpectrumSize 
        { 
            get => m_spectrumSize;
            set
            {
                if (value != m_spectrumSize && IsPowerOfTwo(value))
                {
                    m_spectrumSize = value;
                    SetupFFT();
                }
            }
        }
        public bool FFTEnabled 
        { 
            get => m_fftEnabled;
            set
            {
                if (value != m_fftEnabled)
                {
                    m_fftEnabled = value;
                    if (m_fftEnabled)
                    {
                        SetupFFT();
                    }
                    else
                    {
                        CleanupFFT();
                    }
                }
            }
        }

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
            
            // Initialize audio level buffers
            m_leftRmsSamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_rightRmsSamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_leftPeakSamples = new G_DoubleEndedQueue(m_samplesCapacity);
            m_rightPeakSamples = new G_DoubleEndedQueue(m_samplesCapacity);

            TryInitializeFmod();
        }

        private void TryInitializeFmod()
        {
            if (m_isInitialized) return;

            try
            {
                // First try to detect if FMOD types exist globally
                var fmodVersionType = System.Type.GetType("FMOD.VERSION");
                if (fmodVersionType != null)
                {
                    Debug.Log("[Graphy] FMOD types detected directly");
                }
                
                // Try multiple approaches to get FMOD system
                // Approach 1: Try FMODUnity.RuntimeManager (FMOD for Unity)
                var fmodUnityType = System.Type.GetType("FMODUnity.RuntimeManager, FMODUnity");
                if (fmodUnityType != null)
                {
                    var studioSystemProp = fmodUnityType.GetProperty("StudioSystem");
                    if (studioSystemProp != null)
                    {
                        var studioSystemObj = studioSystemProp.GetValue(null, null);
                        if (studioSystemObj != null)
                        {
                            // Use dynamic to avoid namespace conflicts
                            dynamic studioSystem = studioSystemObj;
                            if (studioSystem.isValid())
                            {
                                dynamic coreSystem;
                                var result = studioSystem.getCoreSystem(out coreSystem);
                                if (result.ToString() == "OK" && coreSystem.hasHandle())
                                {
                                    m_fmodSystem = coreSystem.handle;
                                    
                                    // Get master channel group for audio metering
                                    dynamic masterGroup;
                                    result = coreSystem.getMasterChannelGroup(out masterGroup);
                                    if (result.ToString() == "OK")
                                    {
                                        m_masterChannelGroup = masterGroup.handle;
                                    }
                                    
                                    // Enable metering on the master channel group
                                    if (m_masterChannelGroup != IntPtr.Zero)
                                    {
                                        masterGroup.setMeteringEnabled(true, true);
                                    }
                                    
                                    m_isInitialized = true;
                                    Debug.Log("[Graphy] FMOD monitoring initialized successfully");
                                }
                            }
                        }
                    }
                }
                
                // Approach 2: Try RuntimeManager.CoreSystem (alternate FMOD Unity integration)
                if (!m_isInitialized)
                {
                    var coreSystemProp = fmodUnityType?.GetProperty("CoreSystem");
                    if (coreSystemProp != null)
                    {
                        var coreSystemObj = coreSystemProp.GetValue(null, null);
                        if (coreSystemObj != null)
                        {
                            dynamic coreSystem = coreSystemObj;
                            var handleProp = coreSystemObj.GetType().GetProperty("handle");
                            if (handleProp != null)
                            {
                                m_fmodSystem = (IntPtr)handleProp.GetValue(coreSystemObj, null);
                                if (m_fmodSystem != IntPtr.Zero)
                                {
                                    Debug.Log("[Graphy] FMOD monitoring initialized via CoreSystem");
                                    m_isInitialized = true;
                                }
                            }
                        }
                    }
                }
                
                // Approach 3: Try to find FMOD system through assembly scanning
                if (!m_isInitialized)
                {
                    // Look for any type in FMOD namespace
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            var types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                if (type.Namespace != null && type.Namespace.StartsWith("FMOD"))
                                {
                                    Debug.Log($"[Graphy] Found FMOD type: {type.FullName} in assembly {assembly.GetName().Name}");
                                    // We found FMOD, but might not have a system reference yet
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            // Skip assemblies we can't access
                        }
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
                    float avgCpu;
                    UpdateStatistic(m_cpuSamples, CurrentFmodCpu, ref m_cpuSum, out avgCpu);
                    AverageFmodCpu = avgCpu;
                    PeakFmodCpu = Mathf.Max(PeakFmodCpu, CurrentFmodCpu);
                }

                // Get memory usage
                result = FMOD.Memory.GetStats(out m_currentAllocated, out m_maxAllocated, false);
                if (result == FMOD.RESULT.OK)
                {
                    CurrentFmodMemoryMB = m_currentAllocated / (1024f * 1024f);
                    float avgMemory;
                    UpdateStatistic(m_memorySamples, CurrentFmodMemoryMB, ref m_memorySum, out avgMemory);
                    AverageFmodMemoryMB = avgMemory;
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
                    float avgFileUsage;
                    UpdateStatistic(m_fileUsageSamples, CurrentFileUsageKBps, ref m_fileUsageSum, out avgFileUsage);
                    AverageFileUsageKBps = avgFileUsage;
                    PeakFileUsageKBps = Mathf.Max(PeakFileUsageKBps, CurrentFileUsageKBps);

                    // Reset the file usage counters after reading
                    system.resetFileUsage();
                }

                // Get audio metering info
                if (m_masterChannelGroup != IntPtr.Zero)
                {
                    FMOD.ChannelGroup masterGroup = new FMOD.ChannelGroup(m_masterChannelGroup);
                    result = masterGroup.getMeteringInfo(out m_meteringInfo);
                    if (result == FMOD.RESULT.OK && m_meteringInfo.numChannels > 0)
                    {
                        // Get RMS and peak levels
                        int numChannels = Math.Min(m_meteringInfo.numChannels, 32);
                        
                        // Copy levels from unmanaged memory
                        if (m_meteringInfo.rmslevel != IntPtr.Zero)
                        {
                            Marshal.Copy(m_meteringInfo.rmslevel, m_rmsLevels, 0, numChannels);
                        }
                        if (m_meteringInfo.peaklevel != IntPtr.Zero)
                        {
                            Marshal.Copy(m_meteringInfo.peaklevel, m_peakLevels, 0, numChannels);
                        }
                        
                        // For stereo, track left and right channels
                        if (numChannels >= 2)
                        {
                            CurrentLeftRMS = LinearToDecibels(m_rmsLevels[0]);
                            CurrentRightRMS = LinearToDecibels(m_rmsLevels[1]);
                            CurrentLeftPeak = LinearToDecibels(m_peakLevels[0]);
                            CurrentRightPeak = LinearToDecibels(m_peakLevels[1]);
                            
                            // Update averages
                            float avgLeftRms, avgRightRms;
                            UpdateStatistic(m_leftRmsSamples, CurrentLeftRMS, ref m_leftRmsSum, out avgLeftRms);
                            UpdateStatistic(m_rightRmsSamples, CurrentRightRMS, ref m_rightRmsSum, out avgRightRms);
                            AverageLeftRMS = avgLeftRms;
                            AverageRightRMS = avgRightRms;
                        }
                        else if (numChannels == 1)
                        {
                            // Mono - use same value for both channels
                            CurrentLeftRMS = CurrentRightRMS = LinearToDecibels(m_rmsLevels[0]);
                            CurrentLeftPeak = CurrentRightPeak = LinearToDecibels(m_peakLevels[0]);
                        }
                    }
                }
                
                // Update FFT spectrum if enabled
                UpdateFFTSpectrum();
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
        
        private float LinearToDecibels(float linear)
        {
            if (linear <= 0f) return -80f;
            float db = 20f * Mathf.Log10(linear);
            return Mathf.Max(db, -80f);
        }
        
        private bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }
        
        private void SetupFFT()
        {
            if (!m_isInitialized || m_fmodSystem == IntPtr.Zero || !m_fftEnabled) return;
            
            try
            {
                CleanupFFT();
                
                FMOD.System system = new FMOD.System(m_fmodSystem);
                
                // Create FFT DSP
                FMOD.RESULT result = system.createDSPByType(FMOD.DSP_TYPE.FFT, out m_fftDsp);
                if (result == FMOD.RESULT.OK && m_fftDsp != IntPtr.Zero)
                {
                    FMOD.DSP fftDsp = new FMOD.DSP(m_fftDsp);
                    
                    // Set window size (spectrum size)
                    result = fftDsp.setParameterInt((int)FMOD.DSP_FFT.WINDOWSIZE, m_spectrumSize);
                    if (result != FMOD.RESULT.OK)
                    {
                        Debug.LogWarning($"[Graphy] Failed to set FFT window size: {result}");
                    }
                    
                    // Set window type (default to Blackman for good frequency resolution)
                    result = fftDsp.setParameterInt((int)FMOD.DSP_FFT.WINDOWTYPE, (int)FMOD.DSP_FFT_WINDOW.BLACKMAN);
                    
                    // Add DSP to master channel group
                    if (m_masterChannelGroup != IntPtr.Zero)
                    {
                        FMOD.ChannelGroup masterGroup = new FMOD.ChannelGroup(m_masterChannelGroup);
                        result = masterGroup.addDSP(0, m_fftDsp);
                        if (result == FMOD.RESULT.OK)
                        {
                            // Allocate spectrum data array
                            m_spectrumData = new float[m_spectrumSize / 2]; // Only need half due to Nyquist
                            Debug.Log($"[Graphy] FFT DSP setup complete with size {m_spectrumSize}");
                        }
                        else
                        {
                            Debug.LogWarning($"[Graphy] Failed to add FFT DSP to master channel group: {result}");
                            CleanupFFT();
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[Graphy] Failed to create FFT DSP: {result}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Graphy] Error setting up FFT: {e.Message}");
                CleanupFFT();
            }
        }
        
        private void CleanupFFT()
        {
            if (m_fftDsp != IntPtr.Zero)
            {
                try
                {
                    FMOD.DSP fftDsp = new FMOD.DSP(m_fftDsp);
                    fftDsp.release();
                }
                catch { }
                m_fftDsp = IntPtr.Zero;
            }
            
            if (m_unmanagedSpectrum != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_unmanagedSpectrum);
                m_unmanagedSpectrum = IntPtr.Zero;
            }
            
            m_spectrumData = null;
        }
        
        private void UpdateFFTSpectrum()
        {
            if (!m_fftEnabled || m_fftDsp == IntPtr.Zero || m_spectrumData == null) return;
            
            try
            {
                FMOD.DSP fftDsp = new FMOD.DSP(m_fftDsp);
                
                // Get FFT spectrum data
                IntPtr unmanagedData;
                int length;
                FMOD.RESULT result = fftDsp.getParameterData((int)FMOD.DSP_FFT.SPECTRUMDATA, out unmanagedData, out length);
                
                if (result == FMOD.RESULT.OK && unmanagedData != IntPtr.Zero)
                {
                    // Marshal the FFT data
                    FMOD.DSP_PARAMETER_FFT fftData = (FMOD.DSP_PARAMETER_FFT)Marshal.PtrToStructure(unmanagedData, typeof(FMOD.DSP_PARAMETER_FFT));
                    
                    if (fftData.spectrum != IntPtr.Zero && fftData.numChannels > 0)
                    {
                        // Copy spectrum data for first channel (or average channels)
                        int spectrumLength = fftData.length / 2; // Only positive frequencies
                        
                        if (spectrumLength > 0 && spectrumLength <= m_spectrumData.Length)
                        {
                            // Get spectrum for first channel
                            IntPtr channelSpectrum = fftData.spectrum;
                            Marshal.Copy(channelSpectrum, m_spectrumData, 0, spectrumLength);
                            
                            // Convert to dB if needed
                            for (int i = 0; i < spectrumLength; i++)
                            {
                                m_spectrumData[i] = LinearToDecibels(m_spectrumData[i]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Graphy] Error updating FFT spectrum: {e.Message}");
            }
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
        
        public enum DSP_TYPE
        {
            FFT = 24,
            // Add other DSP types as needed
        }
        
        public enum DSP_FFT
        {
            WINDOWSIZE = 0,
            WINDOWTYPE = 1,
            SPECTRUMDATA = 2,
            DOMINANT_FREQ = 3
        }
        
        public enum DSP_FFT_WINDOW
        {
            RECT = 0,
            TRIANGLE = 1,
            HAMMING = 2,
            HANNING = 3,
            BLACKMAN = 4,
            BLACKMANHARRIS = 5
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
        
        [StructLayout(LayoutKind.Sequential)]
        public struct DSP_METERING_INFO
        {
            public int numsamples;
            public IntPtr peaklevel;
            public IntPtr rmslevel;
            public int numChannels;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct DSP_PARAMETER_FFT
        {
            public int length;
            public int numChannels;
            public IntPtr spectrum;  // Array of float arrays, one per channel
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
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_System_GetMasterChannelGroup(IntPtr system, out IntPtr channelgroup);
            
            public RESULT getMasterChannelGroup(out ChannelGroup channelgroup)
            {
                IntPtr groupHandle;
                RESULT result = FMOD_System_GetMasterChannelGroup(handle, out groupHandle);
                channelgroup = new ChannelGroup(groupHandle);
                return result;
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_System_CreateDSPByType(IntPtr system, DSP_TYPE type, out IntPtr dsp);
            
            public RESULT createDSPByType(DSP_TYPE type, out IntPtr dsp)
            {
                return FMOD_System_CreateDSPByType(handle, type, out dsp);
            }
        }
        
        public struct ChannelGroup
        {
            public IntPtr handle;
            
            public ChannelGroup(IntPtr ptr)
            {
                handle = ptr;
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_ChannelGroup_SetMeteringEnabled(IntPtr channelgroup, bool inputEnabled, bool outputEnabled);
            
            public RESULT setMeteringEnabled(bool inputEnabled, bool outputEnabled)
            {
                return FMOD_ChannelGroup_SetMeteringEnabled(handle, inputEnabled, outputEnabled);
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_ChannelGroup_GetMeteringInfo(IntPtr channelgroup, IntPtr inputInfo, out DSP_METERING_INFO outputInfo);
            
            public RESULT getMeteringInfo(out DSP_METERING_INFO outputInfo)
            {
                return FMOD_ChannelGroup_GetMeteringInfo(handle, IntPtr.Zero, out outputInfo);
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_ChannelGroup_AddDSP(IntPtr channelgroup, int index, IntPtr dsp);
            
            public RESULT addDSP(int index, IntPtr dsp)
            {
                return FMOD_ChannelGroup_AddDSP(handle, index, dsp);
            }
        }
        
        public struct DSP
        {
            public IntPtr handle;
            
            public DSP(IntPtr ptr)
            {
                handle = ptr;
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_DSP_SetParameterInt(IntPtr dsp, int index, int value);
            
            public RESULT setParameterInt(int index, int value)
            {
                return FMOD_DSP_SetParameterInt(handle, index, value);
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_DSP_GetParameterData(IntPtr dsp, int index, out IntPtr data, out int length);
            
            public RESULT getParameterData(int index, out IntPtr data, out int length)
            {
                return FMOD_DSP_GetParameterData(handle, index, out data, out length);
            }
            
            [DllImport("fmod")]
            private static extern RESULT FMOD_DSP_Release(IntPtr dsp);
            
            public RESULT release()
            {
                return FMOD_DSP_Release(handle);
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

#endif // GRAPHY_FMOD || UNITY_EDITOR
