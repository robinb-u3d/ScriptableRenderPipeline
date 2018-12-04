using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [ExecuteAlways]
    public class StaticLightingSky : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        VolumeProfile m_Profile;
        [SerializeField]
        int m_StaticLightingSkyUniqueID = 0;
        [SerializeField]
        float[] m_AmbientProbeCoefs = new float[3*9];

        public SphericalHarmonicsL2 ambientProbe;

        public SkySettings skySettings { get; private set; }

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
                UpdateCurrentStaticLightingSky();
            }
        }

        void UpdateCurrentStaticLightingSky()
        {
            skySettings = GetSkyFromIDAndVolume(m_StaticLightingSkyUniqueID, m_Profile);
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
            if (profile != null && skySettings != null)
            {
                if (!profile.components.Find(x => x == skySettings))
                {
                    m_StaticLightingSkyUniqueID = 0;
                }
            }

            UpdateCurrentStaticLightingSky();
        }

        void OnEnable()
        {
            UpdateCurrentStaticLightingSky();
            SkyManager.RegisterStaticLightingSky(this);
        }

        void OnDisable()
        {
            SkyManager.UnRegisterStaticLightingSky(this);
            skySettings = null;
        }

        public void OnBeforeSerialize()
        {
            for (int channel = 0; channel < 3; ++channel)
            {
                for (int coef = 0; coef < 9; ++coef)
                {
                    m_AmbientProbeCoefs[channel * 9 + coef] = ambientProbe[channel, coef];
                }
            }
        }

        public void OnAfterDeserialize()
        {
            for (int channel = 0; channel < 3; ++channel)
            {
                for (int coef = 0; coef < 9; ++coef)
                {
                    ambientProbe[channel, coef] = m_AmbientProbeCoefs[channel * 9 + coef];
                }
            }
        }

    }
}
