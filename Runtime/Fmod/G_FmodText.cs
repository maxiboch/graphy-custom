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

#if GRAPHY_FMOD || UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;
using Tayx.Graphy.Utils.NumString;

namespace Tayx.Graphy.Fmod
{
    public class G_FmodText : MonoBehaviour
    {
        #region Variables -> Serialized Private

        [SerializeField] private Text m_fmodCpuText = null;
        [SerializeField] private Text m_fmodMemoryText = null;
        [SerializeField] private Text m_channelsText = null;
        [SerializeField] private Text m_fileUsageText = null;

        [SerializeField] private Text m_fmodCpuAvgText = null;
        [SerializeField] private Text m_fmodMemoryAvgText = null;
        [SerializeField] private Text m_channelsAvgText = null;
        [SerializeField] private Text m_fileUsageAvgText = null;

        [SerializeField] private Text m_fmodCpuPeakText = null;
        [SerializeField] private Text m_fmodMemoryPeakText = null;
        [SerializeField] private Text m_channelsPeakText = null;
        [SerializeField] private Text m_fileUsagePeakText = null;

        #endregion

        #region Variables -> Private

        private GraphyManager m_graphyManager = null;
        private G_FmodMonitor m_fmodMonitor = null;

        private float m_updateRate = 0.5f; // Update text every 500ms
        private float m_timeSinceUpdate = 0f;

        // String formats for display
        private const string CPU_FORMAT = "0.0";
        private const string MEMORY_FORMAT = "0.0";
        private const string CHANNELS_FORMAT = "0";
        private const string FILE_USAGE_FORMAT = "0.0";

        // Color thresholds
        private Color m_goodColor = Color.green;
        private Color m_cautionColor = Color.yellow;
        private Color m_criticalColor = Color.red;

        private float m_cpuCautionThreshold = 5f;    // 5% CPU
        private float m_cpuCriticalThreshold = 10f;  // 10% CPU
        private float m_memoryCautionThreshold = 50f;    // 50 MB
        private float m_memoryCriticalThreshold = 100f;  // 100 MB
        private int m_channelsCautionThreshold = 32;
        private int m_channelsCriticalThreshold = 64;

        #endregion

        #region Methods -> Unity Callbacks

        private void Awake()
        {
            Init();
        }

        private void Update()
        {
            if (m_fmodMonitor == null || !m_fmodMonitor.IsAvailable)
            {
                SetUnavailableText();
                return;
            }

            m_timeSinceUpdate += Time.unscaledDeltaTime;

            if (m_timeSinceUpdate >= m_updateRate)
            {
                m_timeSinceUpdate = 0f;
                UpdateTexts();
            }
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters()
        {
            if (m_graphyManager != null)
            {
                m_updateRate = m_graphyManager.FmodTextUpdateRate;
                
                // Get colors from GraphyManager if available
                m_goodColor = m_graphyManager.GoodFPSColor;
                m_cautionColor = m_graphyManager.CautionFPSColor;
                m_criticalColor = m_graphyManager.CriticalFPSColor;
            }
        }

        #endregion

        #region Methods -> Private

        private void Init()
        {
            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();
            m_fmodMonitor = GetComponent<G_FmodMonitor>();

            if (m_fmodMonitor == null)
            {
                m_fmodMonitor = gameObject.AddComponent<G_FmodMonitor>();
            }

            UpdateParameters();
        }

        private void UpdateTexts()
        {
            // Update current values
            if (m_fmodCpuText != null)
            {
                m_fmodCpuText.text = $"FMOD CPU: {m_fmodMonitor.CurrentFmodCpu.ToStringNonAlloc(CPU_FORMAT)}%";
                SetTextColor(m_fmodCpuText, m_fmodMonitor.CurrentFmodCpu, m_cpuCautionThreshold, m_cpuCriticalThreshold);
            }

            if (m_fmodMemoryText != null)
            {
                m_fmodMemoryText.text = $"FMOD Mem: {m_fmodMonitor.CurrentFmodMemoryMB.ToStringNonAlloc(MEMORY_FORMAT)} MB";
                SetTextColor(m_fmodMemoryText, m_fmodMonitor.CurrentFmodMemoryMB, m_memoryCautionThreshold, m_memoryCriticalThreshold);
            }

            if (m_channelsText != null)
            {
                m_channelsText.text = $"Channels: {m_fmodMonitor.CurrentChannelsPlaying.ToStringNonAlloc()}";
                SetTextColor(m_channelsText, m_fmodMonitor.CurrentChannelsPlaying, m_channelsCautionThreshold, m_channelsCriticalThreshold);
            }

            if (m_fileUsageText != null)
            {
                m_fileUsageText.text = $"File I/O: {m_fmodMonitor.CurrentFileUsageKBps.ToStringNonAlloc(FILE_USAGE_FORMAT)} KB/s";
            }

            // Update average values
            if (m_fmodCpuAvgText != null)
            {
                m_fmodCpuAvgText.text = $"Avg: {m_fmodMonitor.AverageFmodCpu.ToStringNonAlloc(CPU_FORMAT)}%";
            }

            if (m_fmodMemoryAvgText != null)
            {
                m_fmodMemoryAvgText.text = $"Avg: {m_fmodMonitor.AverageFmodMemoryMB.ToStringNonAlloc(MEMORY_FORMAT)} MB";
            }

            if (m_channelsAvgText != null)
            {
                m_channelsAvgText.text = $"Avg: {m_fmodMonitor.AverageChannelsPlaying.ToStringNonAlloc(CHANNELS_FORMAT)}";
            }

            if (m_fileUsageAvgText != null)
            {
                m_fileUsageAvgText.text = $"Avg: {m_fmodMonitor.AverageFileUsageKBps.ToStringNonAlloc(FILE_USAGE_FORMAT)} KB/s";
            }

            // Update peak values
            if (m_fmodCpuPeakText != null)
            {
                m_fmodCpuPeakText.text = $"Peak: {m_fmodMonitor.PeakFmodCpu.ToStringNonAlloc(CPU_FORMAT)}%";
            }

            if (m_fmodMemoryPeakText != null)
            {
                m_fmodMemoryPeakText.text = $"Peak: {m_fmodMonitor.PeakFmodMemoryMB.ToStringNonAlloc(MEMORY_FORMAT)} MB";
            }

            if (m_channelsPeakText != null)
            {
                m_channelsPeakText.text = $"Peak: {m_fmodMonitor.PeakChannelsPlaying.ToStringNonAlloc()}";
            }

            if (m_fileUsagePeakText != null)
            {
                m_fileUsagePeakText.text = $"Peak: {m_fmodMonitor.PeakFileUsageKBps.ToStringNonAlloc(FILE_USAGE_FORMAT)} KB/s";
            }
        }

        private void SetUnavailableText()
        {
            string unavailableText = "FMOD N/A";

            if (m_fmodCpuText != null) m_fmodCpuText.text = unavailableText;
            if (m_fmodMemoryText != null) m_fmodMemoryText.text = unavailableText;
            if (m_channelsText != null) m_channelsText.text = unavailableText;
            if (m_fileUsageText != null) m_fileUsageText.text = unavailableText;

            if (m_fmodCpuAvgText != null) m_fmodCpuAvgText.text = "";
            if (m_fmodMemoryAvgText != null) m_fmodMemoryAvgText.text = "";
            if (m_channelsAvgText != null) m_channelsAvgText.text = "";
            if (m_fileUsageAvgText != null) m_fileUsageAvgText.text = "";

            if (m_fmodCpuPeakText != null) m_fmodCpuPeakText.text = "";
            if (m_fmodMemoryPeakText != null) m_fmodMemoryPeakText.text = "";
            if (m_channelsPeakText != null) m_channelsPeakText.text = "";
            if (m_fileUsagePeakText != null) m_fileUsagePeakText.text = "";
        }

        private void SetTextColor(Text text, float value, float cautionThreshold, float criticalThreshold)
        {
            if (value >= criticalThreshold)
            {
                text.color = m_criticalColor;
            }
            else if (value >= cautionThreshold)
            {
                text.color = m_cautionColor;
            }
            else
            {
                text.color = m_goodColor;
            }
        }

        #endregion
    }
}

#endif // GRAPHY_FMOD || UNITY_EDITOR
