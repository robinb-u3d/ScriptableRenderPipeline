using UnityEngine;
using UnityEditorInternal;
using System.IO;
using UnityEngine.Rendering;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    //should have been as simple as
    // [FilePathAttribute("ProjectSettings/HDProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    // HDProjectSettings : ScriptableSingleton<HDProjectSettings>
    // {
    //     //preparing to eventual migration later
    //     enum Version
    //     {
    //         None,
    //         First
    //     }
    //     [SerializeField]
    //     Version version = Version.First;
    // 
    //     public string defaultVolumeRenderingSettingsPath;
    //     public string defaultPostProcessSettingsPath;
    // }
    //but for unknown reason FilePathAttribute is internal which lead to have an
    //unusable ScriptableSingleton class. Copying mechanism here...

    internal class HDProjectSettings : ScriptableObject
    {
        const string filePath = "ProjectSettings/HDRPProjectSettings.asset";

        //preparing to eventual migration later
        enum Version
        {
            None,
            First
        }
        [SerializeField]
        Version version = Version.First;

        [SerializeField]
        string m_DefaultVolumeRenderingSettingsPath;
        [SerializeField]
        string m_ProjectSettingFolderPath = "HDRPProjectSettings";
        [SerializeField]
        bool m_PopupAtStart = true;

        static VolumeProfile m_DefaultVolumeProfile;

        public static VolumeProfile defaultVolumeProfile
        {
            get => m_DefaultVolumeProfile ?? (m_DefaultVolumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(instance.m_DefaultVolumeRenderingSettingsPath));
            set
            {
                instance.m_DefaultVolumeRenderingSettingsPath = AssetDatabase.GetAssetPath(m_DefaultVolumeProfile = value);
                Save();
            }
        }

        public static string projectSettingsFolderPath
        {
            get => instance.m_ProjectSettingFolderPath;
            set
            {
                instance.m_ProjectSettingFolderPath = value;
                Save();
            }
        }

        public static bool haveStartPopup
        {
            get => instance.m_PopupAtStart;
            set
            {
                instance.m_PopupAtStart = value;
                Save();
            }
        }

        //singleton pattern
        static HDProjectSettings s_Instance;
        static HDProjectSettings instance => s_Instance ?? CreateOrLoad();
        HDProjectSettings()
        {
            s_Instance = this;
        }
        
        static HDProjectSettings CreateOrLoad()
        {
            //try load
            InternalEditorUtility.LoadSerializedFileAndForget(filePath);

            //else create
            if (s_Instance == null)
            {
                HDProjectSettings created = CreateInstance<HDProjectSettings>();
                created.hideFlags = HideFlags.HideAndDontSave;
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
            return s_Instance;
        }

        static void Save()
        {
            if (s_Instance == null)
            {
                Debug.Log("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            string folderPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            InternalEditorUtility.SaveToSerializedFileAndForget(new[] { s_Instance }, filePath, allowTextSerialization: true);
        }
    }
}
