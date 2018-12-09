using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Experimental.Rendering.HDPipeline.Drawing;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering.HDPipeline;


namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    [Serializable]
    [Title("Master", "Decal")]
    class DecalMasterNode : MasterNode<IDecalSubShader>, IMayRequirePosition, IMayRequireNormal, IMayRequireTangent
    {
        public const string PositionSlotName = "Position";
        public const int PositionSlotId = 0;

        public const string AlbedoSlotName = "Albedo";
        public const string AlbedoDisplaySlotName = "BaseColor";
        public const int AlbedoSlotId = 1;

        public const string MetallicSlotName = "Metallic";
        public const int MetallicSlotId = 2;

        public const string NormalSlotName = "Normal";
        public const int NormalSlotId = 3;

        public const string SmoothnessSlotName = "Smoothness";
        public const int SmoothnessSlotId = 4;

        public const string AmbientOcclusionSlotName = "Occlusion";
        public const string AmbientOcclusionDisplaySlotName = "AmbientOcclusion";
        public const int AmbientOcclusionSlotId = 5;

        public const string EmissionSlotName = "Emission";
        public const int EmissionSlotId = 6;

        public const string AlphaSlotName = "Alpha";
        public const int AlphaSlotId = 7;

        public const string AlphaSlotName = "AlphaAlbedo";
        public const string AlphaAlbedoDisplaySlotName = "BaseColor Opacity";
        public const int AlphaSlotId = 8;

        public const string AlphaSlotName = "AlphaMettalic";
        public const string AlphaAlbedoDisplaySlotName = "Metallic Opacity";
        public const int AlphaSlotId = 9;

        public const string AlphaSlotName = "AlphaNormal";
        public const string AlphaAlbedoDisplaySlotName = "Normal Opacity";
        public const int AlphaSlotId = 10;

        public const string AlphaSlotName = "AlphaSmoothness";
        public const string AlphaAlbedoDisplaySlotName = "Smoothness Opacity";
        public const int AlphaSlotId = 11;

        public const string AlphaSlotName = "AlphaAmbientOcclusion";
        public const string AlphaAlbedoDisplaySlotName = "Ambient Occlusion Opacity";
        public const int AlphaSlotId = 12;

        // Just for convenience of doing simple masks. We could run out of bits of course.
        [Flags]
        enum SlotMask
        {
            None = 0,
            Position = 1 << PositionSlotId,
            Albedo = 1 << AlbedoSlotId,
            Normal = 1 << NormalSlotId,
            Metallic = 1 << MetallicSlotId,
            Smoothness = 1 << SmoothnessSlotId,
            Occlusion = 1 << AmbientOcclusionSlotId,
            Emission = 1 << EmissionSlotId,
            Alpha = 1 << AlphaSlotId,
            AlphaAlbedo = 1 << AlphaAlbedoSlotId,
            AlphaNormal = 1 << AlphaNormalSlotId,
            AlphaMetal = 1 << AlphaMetalSlotId,
            AlphaSmoothness = 1 << AlphaSmoothnessSlotId,
            AlphaOcclusion = 1 << AlphaOcclusionSlotId,
        }

        const SlotMask commonDecalParameter = SlotMask.Position | SlotMask.Albedo | SlotMask.Normal | SlotMask.Metallic | SlotMask.Smoothness | SlotMask.Occlusion | SlotMask.Emission;
        const SlotMask regularDecalParameter = commonDecalParameter | SlotMask.Alpha;
        const SlotMask detailedDecalParameter = commonDecalParameter | SlotMask.AlphaAlbedo | SlotMask.AlphaNormal | SlotMask.AlphaNormal | SlotMask.AlphaMetal | SlotMask.AlphaSmoothness | SlotMask.AlphaOcclusionSlotId;

        // This could also be a simple array. For now, catch any mismatched data.
        SlotMask GetActiveSlotMask()
        {
            switch (materialType)
            {
                case DecalType.Regular:
                    return regularDecalParameter;

                case DecalType.Detailed:
                    return detailedDecalParameter;

                default:
                    return regularDecalParameter;
            }
        }

        bool MaterialTypeUsesSlotMask(SlotMask mask)
        {
            SlotMask activeMask = GetActiveSlotMask();
            return (activeMask & mask) != 0;
        }

        [SerializeField]
        DecalType m_DecalType;

        public DecalType decalType
        {
            get { return m_DecalType; }
            set
            {
                if (m_DecalType == value)
                    return;

                m_DecalType = value;
                UpdateNodeAfterDeserialization();
                Dirty(ModificationScope.Topological);
            }
        }

        public DecalMasterNode()
        {
            UpdateNodeAfterDeserialization();
        }

        public override string documentationURL
        {
            get { return "https://github.com/Unity-Technologies/ShaderGraph/wiki/Decal-Master-Node"; }
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            base.UpdateNodeAfterDeserialization();
            name = "Decal Master";

            List<int> validSlots = new List<int>();

            // Position
            if (MaterialTypeUsesSlotMask(SlotMask.Position))
            {
                AddSlot(new PositionMaterialSlot(PositionSlotId, PositionSlotName, PositionSlotName, CoordinateSpace.Object, ShaderStageCapability.Vertex));
                validSlots.Add(PositionSlotId);
            }

            // Albedo
            if (MaterialTypeUsesSlotMask(SlotMask.Albedo))
            {
                AddSlot(new ColorRGBMaterialSlot(AlbedoSlotId, AlbedoDisplaySlotName, AlbedoSlotName, SlotType.Input, Color.white, ColorMode.Default, ShaderStageCapability.Fragment));
                validSlots.Add(AlbedoSlotId);
            }

            // Normal
            if (MaterialTypeUsesSlotMask(SlotMask.Normal))
            {
                AddSlot(new NormalMaterialSlot(NormalSlotId, NormalSlotName, NormalSlotName, CoordinateSpace.Tangent, ShaderStageCapability.Fragment));
                validSlots.Add(NormalSlotId);
            }

            // Metal
            if (MaterialTypeUsesSlotMask(SlotMask.Metal))
            {
                AddSlot(new ColorRGBMaterialSlot(MetalSlotId, MetalDisplaySlotName, MetalSlotName, SlotType.Input, Color.white, ColorMode.Default, ShaderStageCapability.Fragment));
                validSlots.Add(MetalSlotId);
            }

            // Smoothness
            if (MaterialTypeUsesSlotMask(SlotMask.Smoothness))
            {
                AddSlot(new Vector1MaterialSlot(SmoothnessSlotId, SmoothnessSlotName, SmoothnessSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(SmoothnessSlotId);
            }

            // Ambient Occlusion
            if (MaterialTypeUsesSlotMask(SlotMask.Occlusion))
            {
                AddSlot(new Vector1MaterialSlot(AmbientOcclusionSlotId, AmbientOcclusionDisplaySlotName, AmbientOcclusionSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AmbientOcclusionSlotId);
            }

            // Emission
            if (MaterialTypeUsesSlotMask(SlotMask.Emission))
            {
                AddSlot(new ColorRGBMaterialSlot(EmissionSlotId, EmissionSlotName, EmissionSlotName, SlotType.Input, Color.black, ColorMode.HDR, ShaderStageCapability.Fragment));
                validSlots.Add(EmissionSlotId);
            }

            // Alpha
            if (MaterialTypeUsesSlotMask(SlotMask.Alpha))
            {
                AddSlot(new Vector1MaterialSlot(AlphaSlotId, AlphaSlotName, AlphaSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaSlotId);
            }

            // Alpha
            if (MaterialTypeUsesSlotMask(SlotMask.Alpha))
            {
                AddSlot(new Vector1MaterialSlot(AlphaSlotId, AlphaSlotName, AlphaSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaSlotId);
            }

            // AlphaAlbedo
            if (MaterialTypeUsesSlotMask(SlotMask.AlphaAlbedo))
            {
                AddSlot(new Vector1MaterialSlot(AlphaAlbedoSlotId, AlphaAlbedoSlotName, AlphaAlbedoSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaAlbedoSlotId);
            }

            // AlphaNormal
            if (MaterialTypeUsesSlotMask(SlotMask.AlphaNormal))
            {
                AddSlot(new Vector1MaterialSlot(AlphaNormalSlotId, AlphaNormalSlotName, AlphaNormalSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaNormalSlotId);
            }

            // AlphaMetal
            if (MaterialTypeUsesSlotMask(SlotMask.AlphaMetal))
            {
                AddSlot(new Vector1MaterialSlot(AlphaMetalSlotId, AlphaMetalSlotName, AlphaMetalSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaMetalSlotId);
            }

            // AlphaSmoothness
            if (MaterialTypeUsesSlotMask(SlotMask.AlphaSmoothness))
            {
                AddSlot(new Vector1MaterialSlot(AlphaSmoothnessSlotId, AlphaSmoothnessSlotName, AlphaSmoothnessSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaSmoothnessSlotId);
            }

            // AlphaOcclusion
            if (MaterialTypeUsesSlotMask(SlotMask.AlphaOcclusion))
            {
                AddSlot(new Vector1MaterialSlot(AlphaOcclusionSlotId, AlphaOcclusionSlotName, AlphaOcclusionSlotName, SlotType.Input, 1.0f, ShaderStageCapability.Fragment));
                validSlots.Add(AlphaOcclusionSlotId);
            }

            RemoveSlotsNameNotMatching(validSlots, true);
        }

        protected override VisualElement CreateCommonSettingsElement()
        {
            return new DecalSettingsView(this);
        }

        public NeededCoordinateSpace RequiresNormal(ShaderStageCapability stageCapability)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequireNormal>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresNormal(stageCapability));
        }

        public NeededCoordinateSpace RequiresTangent(ShaderStageCapability stageCapability)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequireTangent>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresTangent(stageCapability));
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability)
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

            List<MaterialSlot> validSlots = new List<MaterialSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].stageCapability != ShaderStageCapability.All && slots[i].stageCapability != stageCapability)
                    continue;

                validSlots.Add(slots[i]);
            }
            return validSlots.OfType<IMayRequirePosition>().Aggregate(NeededCoordinateSpace.None, (mask, node) => mask | node.RequiresPosition(stageCapability));
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // Trunk currently relies on checking material property "_EmissionColor" to allow emissive GI. If it doesn't find that property, or it is black, GI is forced off.
            // ShaderGraph doesn't use this property, so currently it inserts a dummy color (white). This dummy color may be removed entirely once the following PR has been merged in trunk: Pull request #74105
            // The user will then need to explicitly disable emissive GI if it is not needed.
            // To be able to automatically disable emission based on the ShaderGraph config when emission is black,
            // we will need a more general way to communicate this to the engine (not directly tied to a material property).
            collector.AddShaderProperty(new ColorShaderProperty()
            {
                overrideReferenceName = "_EmissionColor",
                hidden = true,
                value = new Color(1.0f, 1.0f, 1.0f, 1.0f)
            });

            base.CollectShaderProperties(collector, generationMode);
        }
    }
}
