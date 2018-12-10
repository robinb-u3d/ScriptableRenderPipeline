using System;
using UnityEngine.Serialization;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public partial class HDAdditionalCameraData : IVersionable<HDAdditionalCameraData.Version>
    {
        protected enum Version
        {
            None,
            First,
            SeparatePassThrough
        }

        [SerializeField, FormerlySerializedAs("version")]
        Version m_Version;

        protected static readonly MigrationDescription<Version, HDAdditionalCameraData> k_Migration = MigrationDescription.New(
            MigrationStep.New(Version.SeparatePassThrough, (HDAdditionalCameraData data) =>
            {
                if ((int)data.renderingPath == 2)   //2 = former RenderingPath.FullscreenPassthrough
                {
                    data.fullscreenPassthrough = true;
                    data.renderingPath = RenderingPath.UseGraphicsSettings;
                }
            })
        );

        Version IVersionable<Version>.version { get => m_Version; set => m_Version = value; }
    }
}
