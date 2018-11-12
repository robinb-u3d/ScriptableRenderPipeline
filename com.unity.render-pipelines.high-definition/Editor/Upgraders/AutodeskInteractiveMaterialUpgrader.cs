using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;


namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class AutodeskInteractiveMaterialUpgrader : MaterialUpgrader
    {
        public AutodeskInteractiveMaterialUpgrader(string sourceShaderName, string destShaderName, MaterialFinalizer finalizer = null)
        {
            RenameShader(sourceShaderName, destShaderName, finalizer);
        }

        public override void Convert(Material srcMaterial, Material dstMaterial)
        {
            base.Convert(srcMaterial, dstMaterial);
            dstMaterial.SetFloat("_UseColorMap", srcMaterial.GetTexture("_MainTex") ? 1.0f : .0f);
            dstMaterial.SetFloat("_UseMetallicMap", srcMaterial.GetTexture("_MetallicGlossMap") ? 1.0f : .0f);
            dstMaterial.SetFloat("_UseNormalMap", srcMaterial.GetTexture("_BumpMap") ? 1.0f : .0f);
            dstMaterial.SetFloat("_UseRoughnessMap", srcMaterial.GetTexture("_SpecGlossMap") ? 1.0f : .0f);
            dstMaterial.SetFloat("_UseEmissiveMap", srcMaterial.GetTexture("_EmissionMap") ? 1.0f : .0f);
            dstMaterial.SetFloat("_UseAoMap", srcMaterial.GetTexture("_OcclusionMap") ? 1.0f : .0f);
            dstMaterial.SetVector("_UvOffset", srcMaterial.GetTextureOffset("_MainTex"));
            dstMaterial.SetVector("_UvTiling", srcMaterial.GetTextureScale("_MainTex"));
        }
    }
}
