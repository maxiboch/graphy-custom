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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Tayx.Graphy.UI;
using Tayx.Graphy.Utils;

namespace Tayx.Graphy.Fmod
{
    public class G_FmodManager : MonoBehaviour, IMovable, IModifiableState
    {
        #region Variables -> Serialized Private

        [SerializeField] private GameObject m_fmodGraphGameObject = null;
        [SerializeField] private GameObject m_fmodTextGameObject = null;

        [SerializeField] private List<GameObject> m_backgroundImages = new List<GameObject>();
        
        [SerializeField] private List<Image> m_graphsImages = new List<Image>();

        [SerializeField] private GraphyManager.ModulePreset m_modulePreset = GraphyManager.ModulePreset.FPS_BASIC_ADVANCED_FULL;

        #endregion

        #region Variables -> Private

        private GraphyManager m_graphyManager = null;
        private G_FmodGraph m_fmodGraph = null;
        private G_FmodText m_fmodText = null;
        private G_FmodMonitor m_fmodMonitor = null;

        private RectTransform m_rectTransform = null;
        private RectTransform m_fmodGraphGameObjectRectTransform = null;
        private RectTransform m_fmodTextGameObjectRectTransform = null;

        private GraphyManager.ModuleState m_previousModuleState = GraphyManager.ModuleState.FULL;
        private GraphyManager.ModuleState m_currentModuleState = GraphyManager.ModuleState.FULL;

        private bool m_isInitialized = false;

        #endregion

        #region Methods -> Unity Callbacks

        private void Awake()
        {
            Init();
        }

        private void Start()
        {
            UpdateParameters();
        }

        #endregion

        #region Methods -> Public

        public void SetPosition(GraphyManager.ModulePosition newModulePosition)
        {
            if (m_rectTransform == null || m_graphyManager == null)
            {
                return;
            }

            float xSideOffset = Mathf.Abs(m_rectTransform.anchoredPosition.x);
            float ySideOffset = Mathf.Abs(m_rectTransform.anchoredPosition.y);

            switch (newModulePosition)
            {
                case GraphyManager.ModulePosition.TOP_LEFT:
                    m_rectTransform.anchorMax = Vector2.up;
                    m_rectTransform.anchorMin = Vector2.up;
                    m_rectTransform.anchoredPosition = new Vector2(xSideOffset, -ySideOffset);
                    break;

                case GraphyManager.ModulePosition.TOP_RIGHT:
                    m_rectTransform.anchorMax = Vector2.one;
                    m_rectTransform.anchorMin = Vector2.one;
                    m_rectTransform.anchoredPosition = new Vector2(-xSideOffset, -ySideOffset);
                    break;

                case GraphyManager.ModulePosition.BOTTOM_LEFT:
                    m_rectTransform.anchorMax = Vector2.zero;
                    m_rectTransform.anchorMin = Vector2.zero;
                    m_rectTransform.anchoredPosition = new Vector2(xSideOffset, ySideOffset);
                    break;

                case GraphyManager.ModulePosition.BOTTOM_RIGHT:
                    m_rectTransform.anchorMax = Vector2.right;
                    m_rectTransform.anchorMin = Vector2.right;
                    m_rectTransform.anchoredPosition = new Vector2(-xSideOffset, ySideOffset);
                    break;

                case GraphyManager.ModulePosition.FREE:
                    break;
            }
        }

        public void SetState(GraphyManager.ModuleState state, bool silentUpdate = false)
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_previousModuleState = m_currentModuleState;
            m_currentModuleState = state;

            switch (state)
            {
                case GraphyManager.ModuleState.FULL:
                    gameObject.SetActive(true);
                    m_fmodGraphGameObject?.SetActive(true);
                    m_fmodTextGameObject?.SetActive(true);
                    SetGraphActive(true);
                    
                    if (!silentUpdate)
                    {
                        m_graphyManager.SetModuleMode(this, true);
                    }
                    break;

                case GraphyManager.ModuleState.TEXT:
                case GraphyManager.ModuleState.BASIC:
                    gameObject.SetActive(true);
                    m_fmodGraphGameObject?.SetActive(false);
                    m_fmodTextGameObject?.SetActive(true);
                    SetGraphActive(false);
                    
                    if (!silentUpdate)
                    {
                        m_graphyManager.SetModuleMode(this, true);
                    }
                    break;

                case GraphyManager.ModuleState.BACKGROUND:
                    gameObject.SetActive(true);
                    SetGraphActive(false);
                    m_fmodTextGameObject?.SetActive(false);
                    
                    if (!silentUpdate)
                    {
                        m_graphyManager.SetModuleMode(this, false);
                    }
                    break;

                case GraphyManager.ModuleState.OFF:
                    gameObject.SetActive(false);
                    
                    if (!silentUpdate)
                    {
                        m_graphyManager.SetModuleMode(this, false);
                    }
                    break;
            }
        }

        public void RestorePreviousState()
        {
            SetState(m_previousModuleState);
        }

        public void UpdateParameters()
        {
            if (m_graphyManager == null)
            {
                return;
            }

            if (m_fmodMonitor != null)
            {
                m_fmodMonitor.UpdateParameters();
            }

            if (m_fmodGraph != null)
            {
                m_fmodGraph.UpdateParameters();
            }

            if (m_fmodText != null)
            {
                m_fmodText.UpdateParameters();
            }

            SetState(m_graphyManager.FmodModuleState, true);
        }

        public void RefreshParameters()
        {
            if (m_fmodMonitor != null)
            {
                m_fmodMonitor.UpdateParameters();
            }

            if (m_fmodGraph != null)
            {
                m_fmodGraph.UpdateParameters();
            }

            if (m_fmodText != null)
            {
                m_fmodText.UpdateParameters();
            }

            SetState(m_currentModuleState, true);
        }

        #endregion

        #region Methods -> Private

        private void Init()
        {
            m_rectTransform = GetComponent<RectTransform>();

            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();

            m_fmodMonitor = GetComponent<G_FmodMonitor>();
            if (m_fmodMonitor == null)
            {
                m_fmodMonitor = gameObject.AddComponent<G_FmodMonitor>();
            }

            m_fmodGraph = GetComponent<G_FmodGraph>();
            if (m_fmodGraph == null && m_fmodGraphGameObject != null)
            {
                m_fmodGraph = m_fmodGraphGameObject.GetComponent<G_FmodGraph>();
            }

            m_fmodText = GetComponent<G_FmodText>();
            if (m_fmodText == null && m_fmodTextGameObject != null)
            {
                m_fmodText = m_fmodTextGameObject.GetComponent<G_FmodText>();
            }

            if (m_fmodGraphGameObject != null)
            {
                m_fmodGraphGameObjectRectTransform = m_fmodGraphGameObject.GetComponent<RectTransform>();
            }

            if (m_fmodTextGameObject != null)
            {
                m_fmodTextGameObjectRectTransform = m_fmodTextGameObject.GetComponent<RectTransform>();
            }

            m_isInitialized = true;
        }

        private void SetGraphActive(bool active)
        {
            if (m_fmodGraphGameObject != null)
            {
                m_fmodGraphGameObject.SetActive(active);
            }

            foreach (var image in m_graphsImages)
            {
                if (image != null)
                {
                    image.enabled = active;
                }
            }
        }

        #endregion
    }
}

#endif // GRAPHY_FMOD || UNITY_EDITOR
