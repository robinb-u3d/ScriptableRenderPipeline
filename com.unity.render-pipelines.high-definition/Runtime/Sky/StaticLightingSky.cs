using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [ExecuteAlways]
    public class StaticLightingSky : MonoBehaviour
    {
        [SerializeField]
        VolumeProfile m_Profile;
        [SerializeField]
        int m_StaticLightingSkyUniqueID = 0;

        // We need to keep a reference in order to unregister it upon change.
        SkySettings m_StaticLightingSky = null;

        List<SkySettings> m_VolumeSkyList = new List<SkySettings>();


        public VolumeProfile profile
        {
            get
            {
                return m_Profile;
            }
            set
            {
                // Changing the volume is considered a destructive operation => reset the static lighting sky.
                if (value != m_Profile)
                {
                    m_StaticLightingSkyUniqueID = 0;
                }

                m_Profile = value;
            }
        }

        public int staticLightingSkyUniqueID
        {
            get
            {
                return m_StaticLightingSkyUniqueID;
            }
            set
            {
                m_StaticLightingSkyUniqueID = value;
                UpdateCurrentStaticLighting();
            }
        }

        void UpdateCurrentStaticLighting()
        {
            SkySettings newStaticLightingSky = GetSkyFromIDAndVolume(m_StaticLightingSkyUniqueID, m_Profile);

            if (newStaticLightingSky != m_StaticLightingSky)
            {
                SkyManager.UnRegisterStaticLightingSky(m_StaticLightingSky);
                if (newStaticLightingSky != null)
                    SkyManager.RegisterStaticLightingSky(newStaticLightingSky);

                m_StaticLightingSky = newStaticLightingSky;
            }
        }

        SkySettings GetSkyFromIDAndVolume(int skyUniqueID, VolumeProfile profile)
        {
            if (profile != null && skyUniqueID != 0)
            {
                m_VolumeSkyList.Clear();
                if (m_Profile.TryGetAllSubclassOf<SkySettings>(typeof(SkySettings), m_VolumeSkyList))
                {
                    foreach (var sky in m_VolumeSkyList)
                    {
                        if (skyUniqueID == SkySettings.GetUniqueID(sky.GetType()))
                        {
                            return sky;
                        }
                    }
                }
            }

            return null;
        }

        // All actions done in this method are because Editor won't go through setters so we need to manually check consistency of our data.
        void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            // If we detect that the profile has been removed we need to reset the static lighting sky.
            if (m_Profile == null)
            {
                m_StaticLightingSkyUniqueID = 0;
            }

            // If we detect that the profile has changed, we need to reset the static lighting sky.
            // We have to do that manually because PropertyField won't go through setters.
            if (profile != null && m_StaticLightingSky != null)
            {
                if (!profile.components.Find(x => x == m_StaticLightingSky))
                {
                    m_StaticLightingSkyUniqueID = 0;
                }
            }

            UpdateCurrentStaticLighting();
        }

        void OnEnable()
        {
            UpdateCurrentStaticLighting();
        }

        void OnDisable()
        {
            SkyManager.UnRegisterStaticLightingSky(m_StaticLightingSky);
            m_StaticLightingSky = null;
        }
    }
}
