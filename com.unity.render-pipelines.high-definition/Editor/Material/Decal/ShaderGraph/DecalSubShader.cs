using System.Collections.Generic;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    class DecalSubShader : IDecalSubShader
    {
        Pass m_PassMETA = new Pass()
        {
            Name = "META",
            LightMode = "Meta",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_LIGHT_TRANSPORT",
            CullOverride = "Cull Off",
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassLightTransport.hlsl\"",
            },
            RequiredFields = new List<string>()
            {
                "AttributesMesh.normalOS",
                "AttributesMesh.tangentOS",     // Always present as we require it also in case of Variants lighting
                "AttributesMesh.uv0",
                "AttributesMesh.uv1",
                "AttributesMesh.color",
                "AttributesMesh.uv2",           // SHADERPASS_LIGHT_TRANSPORT always uses uv2
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.AlbedoSlotId,
                DecalMasterNode.SpecularOcclusionSlotId,
                DecalMasterNode.NormalSlotId,
                DecalMasterNode.SmoothnessSlotId,
                DecalMasterNode.AmbientOcclusionSlotId,
                DecalMasterNode.SpecularColorSlotId,
                DecalMasterNode.DiffusionProfileSlotId,
                DecalMasterNode.SubsurfaceMaskSlotId,
                DecalMasterNode.ThicknessSlotId,
                DecalMasterNode.TangentSlotId,
                DecalMasterNode.AnisotropySlotId,
                DecalMasterNode.EmissionSlotId,
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId,
            },
            VertexShaderSlots = new List<int>()
            {
                //DecalMasterNode.PositionSlotId
            },
            UseInPreview = false
        };

        Pass m_PassShadowCaster = new Pass()
        {
            Name = "ShadowCaster",
            LightMode = "ShadowCaster",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_SHADOWS",
            BlendOverride = "Blend One Zero",
            ZWriteOverride = "ZWrite On",
            ColorMaskOverride = "ColorMask 0",
            ExtraDefines = new List<string>()
            {
                "#define USE_LEGACY_UNITY_MATRIX_VARIABLES",
            },
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                DecalMasterNode.PositionSlotId
            },
            UseInPreview = false
        };

        Pass m_SceneSelectionPass = new Pass()
        {
            Name = "SceneSelectionPass",
            LightMode = "SceneSelectionPass",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_DEPTH_ONLY",
            ColorMaskOverride = "ColorMask 0",
            ExtraDefines = new List<string>()
            {
                "#define SCENESELECTIONPASS",
            },            
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                DecalMasterNode.PositionSlotId
            },
            UseInPreview = true
        };

        Pass m_PassDepthForwardOnly = new Pass()
        {
            Name = "DepthOnly",
            LightMode = "DepthForwardOnly",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_DEPTH_ONLY",
            ZWriteOverride = "ZWrite On",

            ExtraDefines = new List<string>()
            {
                "#define WRITE_NORMAL_BUFFER",
                "#pragma multi_compile _ WRITE_MSAA_DEPTH"
            },
            
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.NormalSlotId,
                DecalMasterNode.SmoothnessSlotId,
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId
            },

            RequiredFields = new List<string>()
            {
                "AttributesMesh.normalOS",
                "AttributesMesh.tangentOS",     // Always present as we require it also in case of Variants lighting
                "AttributesMesh.uv0",
                "AttributesMesh.uv1",
                "AttributesMesh.color",
                "AttributesMesh.uv2",           // SHADERPASS_LIGHT_TRANSPORT always uses uv2
                "AttributesMesh.uv3",           // DEBUG_DISPLAY

                "FragInputs.worldToTangent",
                "FragInputs.positionRWS",
                "FragInputs.texCoord0",
                "FragInputs.texCoord1",
                "FragInputs.texCoord2",
                "FragInputs.texCoord3",
                "FragInputs.color",
            },

            VertexShaderSlots = new List<int>()
            {
                DecalMasterNode.PositionSlotId
            },
            UseInPreview = false,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                var masterNode = node as DecalMasterNode;

                int stencilDepthPrepassWriteMask = masterNode.receiveDecals.isOn ? (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer:0;
                int stencilDepthPrepassRef = masterNode.receiveDecals.isOn ? (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer:0;
                stencilDepthPrepassWriteMask |= !masterNode.receiveSSR.isOn ? (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR : 0;
                stencilDepthPrepassRef |= !masterNode.receiveSSR.isOn ? (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR : 0;

                if (stencilDepthPrepassWriteMask != 0)
                {
                    pass.StencilOverride = new List<string>()
                    {
                        "// Stencil setup",
                        "Stencil",
                        "{",
                        string.Format("   WriteMask {0}", stencilDepthPrepassWriteMask),
                        string.Format("   Ref  {0}", stencilDepthPrepassRef),
                        "   Comp Always",
                        "   Pass Replace",
                        "}"
                    };
                }
            }           
        };

        Pass m_PassMotionVectors = new Pass()
        {
            Name = "Motion Vectors",
            LightMode = "MotionVectors",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_VELOCITY",
            ExtraDefines = new List<string>()
            {
                "#define WRITE_NORMAL_BUFFER",
                "#pragma multi_compile _ WRITE_MSAA_DEPTH"
            },
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassVelocity.hlsl\"",
            },
            RequiredFields = new List<string>()
            {
                "AttributesMesh.normalOS",
                "AttributesMesh.tangentOS",     // Always present as we require it also in case of Variants lighting
                "AttributesMesh.uv0",
                "AttributesMesh.uv1",
                "AttributesMesh.color",
                "AttributesMesh.uv2",           // SHADERPASS_LIGHT_TRANSPORT always uses uv2
                "AttributesMesh.uv3",           // DEBUG_DISPLAY

                "FragInputs.worldToTangent",
                "FragInputs.positionRWS",
                "FragInputs.texCoord0",
                "FragInputs.texCoord1",
                "FragInputs.texCoord2",
                "FragInputs.texCoord3",
                "FragInputs.color",
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.NormalSlotId,
                DecalMasterNode.SmoothnessSlotId,
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                DecalMasterNode.PositionSlotId
            },
            UseInPreview = false,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                var masterNode = node as DecalMasterNode;

                int stencilWriteMaskMV = (int)HDRenderPipeline.StencilBitMask.ObjectVelocity;
                int stencilRefMV = (int)HDRenderPipeline.StencilBitMask.ObjectVelocity;

                pass.StencilOverride = new List<string>()
                {
                    "// If velocity pass (motion vectors) is enabled we tag the stencil so it don't perform CameraMotionVelocity",
                    "Stencil",
                    "{",
                    string.Format("   WriteMask {0}", stencilWriteMaskMV),
                    string.Format("   Ref  {0}", stencilRefMV),
                    "   Comp Always",
                    "   Pass Replace",
                    "}"
                };
            }
        };

        Pass m_PassForwardOnly = new Pass()
        {
            Name = "Forward",
            LightMode = "ForwardOnly",
            TemplateName = "DecalPass.template",
            MaterialName = "Decal",
            ShaderPassName = "SHADERPASS_FORWARD",
            ExtraDefines = new List<string>()
            {
                "#pragma multi_compile _ DEBUG_DISPLAY",
                "#pragma multi_compile _ LIGHTMAP_ON",
                "#pragma multi_compile _ DIRLIGHTMAP_COMBINED",
                "#pragma multi_compile _ DYNAMICLIGHTMAP_ON",
                "#pragma multi_compile _ SHADOWS_SHADOWMASK",
                "#pragma multi_compile DECALS_OFF DECALS_3RT DECALS_4RT",
                "#define LIGHTLOOP_TILE_PASS",
                "#pragma multi_compile USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST",
                "#pragma multi_compile SHADOW_LOW SHADOW_MEDIUM SHADOW_HIGH"
            },
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForward.hlsl\"",
            },
            RequiredFields = new List<string>()
            {
                "AttributesMesh.normalOS",
                "AttributesMesh.tangentOS",     // Always present as we require it also in case of Variants lighting
                "AttributesMesh.uv0",
                "AttributesMesh.uv1",
                "AttributesMesh.color",
                "AttributesMesh.uv2",           // SHADERPASS_LIGHT_TRANSPORT always uses uv2
                "AttributesMesh.uv3",           // DEBUG_DISPLAY

                "FragInputs.worldToTangent",
                "FragInputs.positionRWS",
                "FragInputs.texCoord0",
                "FragInputs.texCoord1",
                "FragInputs.texCoord2",
                "FragInputs.texCoord3",
                "FragInputs.color",
            },
            PixelShaderSlots = new List<int>()
            {
                DecalMasterNode.AlbedoSlotId,
                DecalMasterNode.SpecularOcclusionSlotId,
                DecalMasterNode.NormalSlotId,
                DecalMasterNode.BentNormalSlotId,
                DecalMasterNode.SmoothnessSlotId,
                DecalMasterNode.AmbientOcclusionSlotId,
                DecalMasterNode.SpecularColorSlotId,
                DecalMasterNode.DiffusionProfileSlotId,
                DecalMasterNode.SubsurfaceMaskSlotId,
                DecalMasterNode.ThicknessSlotId,
                DecalMasterNode.TangentSlotId,
                DecalMasterNode.AnisotropySlotId,
                DecalMasterNode.EmissionSlotId,
                DecalMasterNode.AlphaSlotId,
                DecalMasterNode.AlphaClipThresholdSlotId,
            },
            VertexShaderSlots = new List<int>()
            {
                DecalMasterNode.PositionSlotId
            },
            UseInPreview = true,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                var masterNode = node as DecalMasterNode;
                pass.StencilOverride = new List<string>()
                {
                    "// Stencil setup",
                    "Stencil",
                    "{",
                    string.Format("   WriteMask {0}", (int) HDRenderPipeline.StencilBitMask.LightingMask),
                    string.Format("   Ref  {0}", masterNode.RequiresSplitLighting() ? (int)StencilLightingUsage.SplitLighting : (int)StencilLightingUsage.RegularLighting),
                    "   Comp Always",
                    "   Pass Replace",
                    "}"
                };

                pass.ExtraDefines.Remove("#define SHADERPASS_FORWARD_BYPASS_ALPHA_TEST");
                if (masterNode.surfaceType == SurfaceType.Opaque && masterNode.alphaTest.isOn)
                {
                    pass.ExtraDefines.Add("#define SHADERPASS_FORWARD_BYPASS_ALPHA_TEST");
                    pass.ZTestOverride = "ZTest Equal";
                }
                else
                {
                    pass.ZTestOverride = null;
                }
            }
        };

        private static HashSet<string> GetActiveFieldsFromMasterNode(INode iMasterNode, Pass pass)
        {
            HashSet<string> activeFields = new HashSet<string>();

            DecalMasterNode masterNode = iMasterNode as DecalMasterNode;
            if (masterNode == null)
            {
                return activeFields;
            }

            if (masterNode.doubleSidedMode != DoubleSidedMode.Disabled)
            {
                activeFields.Add("DoubleSided");
                if (pass.ShaderPassName != "SHADERPASS_VELOCITY")   // HACK to get around lack of a good interpolator dependency system
                {                                                   // we need to be able to build interpolators using multiple input structs
                                                                    // also: should only require isFrontFace if Normals are required...
                    if (masterNode.doubleSidedMode == DoubleSidedMode.FlippedNormals)
                    {
                        activeFields.Add("DoubleSided.Flip");
                    }
                    else if (masterNode.doubleSidedMode == DoubleSidedMode.MirroredNormals)
                    {
                        activeFields.Add("DoubleSided.Mirror");
                    }
                    // Important: the following is used in SharedCode.template.hlsl for determining the normal flip mode
                    activeFields.Add("FragInputs.isFrontFace");
                }
            }

            switch (masterNode.materialType)
            {
                case DecalMasterNode.MaterialType.CottonWool:
                    activeFields.Add("Material.CottonWool");
                    break;
                case DecalMasterNode.MaterialType.Silk:
                    activeFields.Add("Material.Silk");
                    break;
                default:
                    UnityEngine.Debug.LogError("Unknown material type: " + masterNode.materialType);
                    break;
            }

            if (masterNode.alphaTest.isOn)
            {
                if (pass.PixelShaderUsesSlot(DecalMasterNode.AlphaClipThresholdSlotId))
                {
                    activeFields.Add("AlphaTest");
                }
            }

            if (masterNode.surfaceType != SurfaceType.Opaque)
            {
                activeFields.Add("SurfaceType.Transparent");

                if (masterNode.alphaMode == AlphaMode.Alpha)
                {
                    activeFields.Add("BlendMode.Alpha");
                }
                else if (masterNode.alphaMode == AlphaMode.Premultiply)
                {
                    activeFields.Add("BlendMode.Premultiply");
                }
                else if (masterNode.alphaMode == AlphaMode.Additive)
                {
                    activeFields.Add("BlendMode.Add");
                }

                if (masterNode.blendPreserveSpecular.isOn)
                {
                    activeFields.Add("BlendMode.PreserveSpecular");
                }

                if (masterNode.transparencyFog.isOn)
                {
                    activeFields.Add("AlphaFog");
                }
            }

            if (!masterNode.receiveDecals.isOn)
            {
                activeFields.Add("DisableDecals");
            }

            if (!masterNode.receiveSSR.isOn)
            {
                activeFields.Add("DisableSSR");
            }

            if (masterNode.energyConservingSpecular.isOn)
            {
                activeFields.Add("Specular.EnergyConserving");
            }

            if (masterNode.transmission.isOn)
            {
                activeFields.Add("Material.Transmission");
            }

            if (masterNode.subsurfaceScattering.isOn && masterNode.surfaceType != SurfaceType.Transparent)
            {
                activeFields.Add("Material.SubsurfaceScattering");
            }

            if (masterNode.IsSlotConnected(DecalMasterNode.BentNormalSlotId) && pass.PixelShaderUsesSlot(DecalMasterNode.BentNormalSlotId))
            {
                activeFields.Add("BentNormal");
            }

            if (masterNode.IsSlotConnected(DecalMasterNode.TangentSlotId) && pass.PixelShaderUsesSlot(DecalMasterNode.TangentSlotId))
            {
                activeFields.Add("Tangent");
            }

            switch (masterNode.specularOcclusionMode)
            {
                case SpecularOcclusionMode.Off:
                    break;
                case SpecularOcclusionMode.FromAO:
                    activeFields.Add("SpecularOcclusionFromAO");
                    break;
                case SpecularOcclusionMode.FromAOAndBentNormal:
                    activeFields.Add("SpecularOcclusionFromAOBentNormal");
                    break;
                case SpecularOcclusionMode.Custom:
                    activeFields.Add("SpecularOcclusionCustom");
                    break;
                default:
                    break;
            }

            if (pass.PixelShaderUsesSlot(DecalMasterNode.AmbientOcclusionSlotId))
            {
                var occlusionSlot = masterNode.FindSlot<Vector1MaterialSlot>(DecalMasterNode.AmbientOcclusionSlotId);

                bool connected = masterNode.IsSlotConnected(DecalMasterNode.AmbientOcclusionSlotId);
                if (connected || occlusionSlot.value != occlusionSlot.defaultValue)
                {
                    activeFields.Add("AmbientOcclusion");
                }
            }

            return activeFields;
        }

        private static bool GenerateShaderPassLit(DecalMasterNode masterNode, Pass pass, GenerationMode mode, ShaderGenerator result, List<string> sourceAssetDependencyPaths)
        {
            if (mode == GenerationMode.ForReals || pass.UseInPreview)
            {
                SurfaceMaterialOptions materialOptions = HDSubShaderUtilities.BuildMaterialOptions(masterNode.surfaceType, masterNode.alphaMode, masterNode.doubleSidedMode != DoubleSidedMode.Disabled, false);

                pass.OnGeneratePass(masterNode);

                // apply master node options to active fields
                HashSet<string> activeFields = GetActiveFieldsFromMasterNode(masterNode, pass);

                // use standard shader pass generation
                bool vertexActive = masterNode.IsSlotConnected(DecalMasterNode.PositionSlotId);
                return HDSubShaderUtilities.GenerateShaderPass(masterNode, pass, mode, materialOptions, activeFields, result, sourceAssetDependencyPaths, vertexActive);
            }
            else
            {
                return false;
            }
        }

        public string GetSubshader(IMasterNode iMasterNode, GenerationMode mode, List<string> sourceAssetDependencyPaths = null)
        {
            if (sourceAssetDependencyPaths != null)
            {
                // DecalSubShader.cs
                sourceAssetDependencyPaths.Add(AssetDatabase.GUIDToAssetPath("059cc3132f0336e40886300f3d2d7f12"));
                // HDSubShaderUtilities.cs
                sourceAssetDependencyPaths.Add(AssetDatabase.GUIDToAssetPath("713ced4e6eef4a44799a4dd59041484b"));
            }

            var masterNode = iMasterNode as DecalMasterNode;

            var subShader = new ShaderGenerator();
            subShader.AddShaderChunk("SubShader", true);
            subShader.AddShaderChunk("{", true);
            subShader.Indent();
            {
                SurfaceMaterialTags materialTags = HDSubShaderUtilities.BuildMaterialTags(masterNode.surfaceType, masterNode.alphaTest.isOn, false, masterNode.sortPriority);

                // Add tags at the SubShader level
                {
                    var tagsVisitor = new ShaderStringBuilder();
                    materialTags.GetTags(tagsVisitor);
                    subShader.AddShaderChunk(tagsVisitor.ToString(), false);
                }

                // generate the necessary shader passes
                bool opaque = (masterNode.surfaceType == SurfaceType.Opaque);
                bool transparent = !opaque;

                GenerateShaderPassLit(masterNode, m_PassMETA, mode, subShader, sourceAssetDependencyPaths);
                GenerateShaderPassLit(masterNode, m_SceneSelectionPass, mode, subShader, sourceAssetDependencyPaths);
                GenerateShaderPassLit(masterNode, m_PassShadowCaster, mode, subShader, sourceAssetDependencyPaths);

                if (opaque)
                {
                    GenerateShaderPassLit(masterNode, m_PassDepthForwardOnly, mode, subShader, sourceAssetDependencyPaths);
                }

                GenerateShaderPassLit(masterNode, m_PassMotionVectors, mode, subShader, sourceAssetDependencyPaths);

                GenerateShaderPassLit(masterNode, m_PassForwardOnly, mode, subShader, sourceAssetDependencyPaths);
            }
            subShader.Deindent();
            subShader.AddShaderChunk("}", true);
            subShader.AddShaderChunk(@"CustomEditor ""UnityEditor.Experimental.Rendering.HDPipeline.DecalGUI""");

            return subShader.GetShaderString(0);
        }

        public bool IsPipelineCompatible(RenderPipelineAsset renderPipelineAsset)
        {
            return renderPipelineAsset is HDRenderPipelineAsset;
        }
    }
}
