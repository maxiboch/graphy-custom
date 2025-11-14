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

using UnityEngine;
using UnityEngine.UI;
using Tayx.Graphy;
using Tayx.Graphy.Graph;

namespace Tayx.Graphy.Fmod
{
    public class G_FmodGraph : G_Graph
    {
        #region Variables -> Serialized Private

        [SerializeField] private Image m_cpuGraph = null;
        [SerializeField] private Image m_memoryGraph = null;
        [SerializeField] private Image m_channelsGraph = null;

        [SerializeField] private Shader m_graphShader = null;
        [SerializeField] private bool m_isInitialized = false;

        #endregion

        #region Variables -> Private

        private GraphyManager m_graphyManager = null;
        private G_FmodMonitor m_fmodMonitor = null;

        private int m_resolution = 150;

        private G_GraphShader m_cpuGraphShader = null;
        private G_GraphShader m_memoryGraphShader = null;
        private G_GraphShader m_channelsGraphShader = null;

        private float[] m_cpuGraphArray;
        private float[] m_memoryGraphArray;
        private float[] m_channelsGraphArray;

        private float m_highestCpuValue = 0f;
        private float m_highestMemoryValue = 0f;
        private float m_highestChannelsValue = 0f;

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
                return;
            }

            UpdateGraph();
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters()
        {
            if (m_graphyManager != null)
            {
#if GRAPHY_FMOD
                m_resolution = m_graphyManager.FmodGraphResolution;
#else
                m_resolution = 150; // Default resolution
#endif

                CreatePoints();
                UpdateGraph();
            }
        }

        #endregion

        #region Methods -> Protected Override

        protected override void UpdateGraph()
        {
            if (m_fmodMonitor == null || !m_fmodMonitor.IsAvailable)
            {
                return;
            }

            // Update CPU graph
            if (m_cpuGraphShader != null)
            {
                float cpuValue = m_fmodMonitor.CurrentFmodCpu;
                m_highestCpuValue = Mathf.Max(m_highestCpuValue, cpuValue);

                for (int i = 0; i <= m_resolution - 1; i++)
                {
                    if (i >= m_resolution - 1)
                    {
                        m_cpuGraphArray[i] = cpuValue;
                    }
                    else
                    {
                        m_cpuGraphArray[i] = m_cpuGraphArray[i + 1];
                    }
                }

                m_cpuGraphShader.ShaderArrayValues = m_cpuGraphArray;
                m_cpuGraphShader.UpdatePoints();
                m_cpuGraphShader.UpdateArrayValuesLength();
                m_cpuGraphShader.Average = m_fmodMonitor.AverageFmodCpu;
                m_cpuGraphShader.UpdateAverage();
                m_cpuGraphShader.GoodThreshold = 5f;
                m_cpuGraphShader.CautionThreshold = 10f;
                m_cpuGraphShader.UpdateThresholds();
            }

            // Update Memory graph
            if (m_memoryGraphShader != null)
            {
                float memoryValue = m_fmodMonitor.CurrentFmodMemoryMB;
                m_highestMemoryValue = Mathf.Max(m_highestMemoryValue, memoryValue);

                for (int i = 0; i <= m_resolution - 1; i++)
                {
                    if (i >= m_resolution - 1)
                    {
                        m_memoryGraphArray[i] = memoryValue;
                    }
                    else
                    {
                        m_memoryGraphArray[i] = m_memoryGraphArray[i + 1];
                    }
                }

                m_memoryGraphShader.ShaderArrayValues = m_memoryGraphArray;
                m_memoryGraphShader.UpdatePoints();
                m_memoryGraphShader.UpdateArrayValuesLength();
                m_memoryGraphShader.Average = m_fmodMonitor.AverageFmodMemoryMB;
                m_memoryGraphShader.UpdateAverage();
                m_memoryGraphShader.GoodThreshold = 50f;
                m_memoryGraphShader.CautionThreshold = 100f;
                m_memoryGraphShader.UpdateThresholds();
            }

            // Update Channels graph
            if (m_channelsGraphShader != null)
            {
                float channelsValue = m_fmodMonitor.CurrentChannelsPlaying;
                m_highestChannelsValue = Mathf.Max(m_highestChannelsValue, channelsValue);

                for (int i = 0; i <= m_resolution - 1; i++)
                {
                    if (i >= m_resolution - 1)
                    {
                        m_channelsGraphArray[i] = channelsValue;
                    }
                    else
                    {
                        m_channelsGraphArray[i] = m_channelsGraphArray[i + 1];
                    }
                }

                m_channelsGraphShader.ShaderArrayValues = m_channelsGraphArray;
                m_channelsGraphShader.UpdatePoints();
                m_channelsGraphShader.UpdateArrayValuesLength();
                m_channelsGraphShader.Average = m_fmodMonitor.AverageChannelsPlaying;
                m_channelsGraphShader.UpdateAverage();
                m_channelsGraphShader.GoodThreshold = 32f;
                m_channelsGraphShader.CautionThreshold = 64f;
                m_channelsGraphShader.UpdateThresholds();
            }
        }

        protected override void CreatePoints()
        {
            m_cpuGraphArray = new float[m_resolution];
            m_memoryGraphArray = new float[m_resolution];
            m_channelsGraphArray = new float[m_resolution];

            for (int i = 0; i < m_resolution; i++)
            {
                m_cpuGraphArray[i] = 0;
                m_memoryGraphArray[i] = 0;
                m_channelsGraphArray[i] = 0;
            }

            if (m_cpuGraphShader != null)
            {
                m_cpuGraphShader.ShaderArrayValues = m_cpuGraphArray;
                m_cpuGraphShader.UpdatePoints();
                m_cpuGraphShader.UpdateArrayValuesLength();
            }

            if (m_memoryGraphShader != null)
            {
                m_memoryGraphShader.ShaderArrayValues = m_memoryGraphArray;
                m_memoryGraphShader.UpdatePoints();
                m_memoryGraphShader.UpdateArrayValuesLength();
            }

            if (m_channelsGraphShader != null)
            {
                m_channelsGraphShader.ShaderArrayValues = m_channelsGraphArray;
                m_channelsGraphShader.UpdatePoints();
                m_channelsGraphShader.UpdateArrayValuesLength();
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

            if (m_isInitialized) return;

            if (m_graphShader == null)
            {
                m_graphShader = Shader.Find("Graphy/Graph Standard");
            }

            if (m_cpuGraph != null && m_graphShader != null)
            {
                m_cpuGraphShader = new G_GraphShader
                {
                    Image = m_cpuGraph
                };

                m_cpuGraphShader.InitializeShader();
            }

            if (m_memoryGraph != null && m_graphShader != null)
            {
                m_memoryGraphShader = new G_GraphShader
                {
                    Image = m_memoryGraph
                };

                m_memoryGraphShader.InitializeShader();
            }

            if (m_channelsGraph != null && m_graphShader != null)
            {
                m_channelsGraphShader = new G_GraphShader
                {
                    Image = m_channelsGraph
                };

                m_channelsGraphShader.InitializeShader();
            }

            UpdateParameters();

            m_isInitialized = true;
        }

        #endregion
    }
}

#endif // GRAPHY_FMOD || UNITY_EDITOR
