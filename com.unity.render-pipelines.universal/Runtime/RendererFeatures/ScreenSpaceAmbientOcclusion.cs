using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable]
    internal class ScreenSpaceAmbientOcclusionSettings
    {
        // Parameters
        [SerializeField] internal AONoiseOptions AONoise = AONoiseOptions.InterleavedGradient;
        [SerializeField] internal bool Downsample = false;
        [SerializeField] internal bool AfterOpaque = false;
        [SerializeField] internal DepthSource Source = DepthSource.DepthNormals;
        [SerializeField] internal NormalQuality NormalSamples = NormalQuality.Medium;
        [SerializeField] internal float Intensity = 3.0f;
        [SerializeField] internal float DirectLightingStrength = 0.25f;
        [SerializeField] internal float Radius = 0.035f;
        [SerializeField] internal int SampleCount = -1;
        [SerializeField] internal AOSampleOption Samples = AOSampleOption.Medium;

        [SerializeField] internal BlurQualityOptions BlurQuality = BlurQualityOptions.High;
        [SerializeField] internal float Falloff = 100f;

        // Enums
        internal enum DepthSource
        {
            Depth = 0,
            DepthNormals = 1
        }

        internal enum NormalQuality
        {
            Low,
            Medium,
            High
        }

        internal enum AOSampleOption
        {
            High,   // 12 Samples
            Medium, // 8 Samples
            Low,    // 4 Samples
        }

        internal enum AONoiseOptions
        {
            InterleavedGradient,
            BlueNoise,
        }

        internal enum BlurQualityOptions
        {
            High,   // Bilateral
            Medium, // Gaussian
            Low,    // Kawase
        }
    }

    [DisallowMultipleRendererFeature("Screen Space Ambient Occlusion")]
    [Tooltip("The Ambient Occlusion effect darkens creases, holes, intersections and surfaces that are close to each other.")]
    [URPHelpURL("post-processing-ssao")]
    internal class ScreenSpaceAmbientOcclusion : ScriptableRendererFeature
    {
        // Serialized Fields
        [SerializeField] private ScreenSpaceAmbientOcclusionSettings m_Settings = new ScreenSpaceAmbientOcclusionSettings();

        [Reload("Textures/BlueNoise256/LDR_LLL1_{0}.png", 0, 7)]
        public Texture2D[] m_BlueNoise256Textures;

        [SerializeField]
        [HideInInspector]
        [Reload("Shaders/Utils/ScreenSpaceAmbientOcclusion.shader")]
        private Shader m_Shader;

        // Private Fields
        private Material m_Material;
        private ScreenSpaceAmbientOcclusionPass m_SSAOPass = null;

        // Constants
        private const string k_AOOriginalKeyword = "_ORIGINAL";
        private const string k_AOBlueNoiseKeyword = "_BLUE_NOISE";
        private const string k_OrthographicCameraKeyword = "_ORTHOGRAPHIC";
        private const string k_NormalReconstructionLowKeyword = "_RECONSTRUCT_NORMAL_LOW";
        private const string k_NormalReconstructionMediumKeyword = "_RECONSTRUCT_NORMAL_MEDIUM";
        private const string k_NormalReconstructionHighKeyword = "_RECONSTRUCT_NORMAL_HIGH";
        private const string k_SourceDepthKeyword = "_SOURCE_DEPTH";
        private const string k_SourceDepthNormalsKeyword = "_SOURCE_DEPTH_NORMALS";
        private const string k_SampleCountLowKeyword = "_SAMPLE_COUNT_LOW";
        private const string k_SampleCountMediumKeyword = "_SAMPLE_COUNT_MEDIUM";
        private const string k_SampleCountHighKeyword = "_SAMPLE_COUNT_HIGH";

        internal bool afterOpaque => m_Settings.AfterOpaque;

        /// <inheritdoc/>
        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
#endif
            // Create the pass...
            if (m_SSAOPass == null)
                m_SSAOPass = new ScreenSpaceAmbientOcclusionPass();

            if (m_Settings.SampleCount > 0)
            {
                if (m_Settings.SampleCount > 11)
                    m_Settings.Samples = ScreenSpaceAmbientOcclusionSettings.AOSampleOption.High;
                else if (m_Settings.SampleCount > 8)
                    m_Settings.Samples = ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Medium;
                else
                    m_Settings.Samples = ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Low;

                m_Settings.SampleCount = -1;
            }

            GetMaterials();
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GetMaterials())
            {
                Debug.LogErrorFormat(
                    "{0}.AddRenderPasses(): Missing material. {1} render pass will not be added. Check for missing reference in the renderer resources.",
                    GetType().Name, name);
                return;
            }

            bool shouldAdd = m_SSAOPass.Setup(ref m_Settings, ref renderer, ref m_Material, ref m_BlueNoise256Textures);
            if (shouldAdd)
                renderer.EnqueuePass(m_SSAOPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_SSAOPass?.Dispose();
            m_SSAOPass = null;
            CoreUtils.Destroy(m_Material);
        }

        private bool GetMaterials()
        {
            if (m_Material == null)
                m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
            return m_Material != null;
        }

        // The SSAO Pass
        private class ScreenSpaceAmbientOcclusionPass : ScriptableRenderPass
        {
            // Properties
            private bool isRendererDeferred => m_Renderer != null && m_Renderer is UniversalRenderer && ((UniversalRenderer)m_Renderer).renderingModeRequested == RenderingMode.Deferred;

            // Internal Variables
            internal string profilerTag;

            // Private Variables
            private bool m_SupportsR8RenderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);
            private int m_BlueNoiseTextureIndex = 0;
            private Material m_Material;
            private Texture2D[] m_BlueNoiseTextures;
            private Vector4[] m_CameraTopLeftCorner = new Vector4[2];
            private Vector4[] m_CameraXExtent = new Vector4[2];
            private Vector4[] m_CameraYExtent = new Vector4[2];
            private Vector4[] m_CameraZExtent = new Vector4[2];
            private RTHandle[] m_SSAOTextures;
            private BlurTypes m_BlurType = BlurTypes.Bilateral;
            private Matrix4x4[] m_CameraViewProjections = new Matrix4x4[2];
            private ProfilingSampler m_ProfilingSampler = ProfilingSampler.Get(URPProfileId.SSAO);
            private ScriptableRenderer m_Renderer = null;
            private RenderTextureDescriptor m_AOPassDescriptor;
            private ScreenSpaceAmbientOcclusionSettings m_CurrentSettings;

            // Constants
            private const int k_FinalTexID = 3;
            private const string k_SSAOTextureName = "_ScreenSpaceOcclusionTexture";
            private const string k_SSAOAmbientOcclusionParamName = "_AmbientOcclusionParam";

            // Statics
            private static readonly int s_BaseMapID = Shader.PropertyToID("_BaseMap");
            private static readonly int s_SSAOParamsID = Shader.PropertyToID("_SSAOParams");
            private static readonly int s_SSAOBlueNoiseParamsID = Shader.PropertyToID("_SSAOBlueNoiseParams");
            private static readonly int s_ScaleBiasRtID = Shader.PropertyToID("_ScaleBiasRt");
            private static readonly int s_LastKawasePass = Shader.PropertyToID("_LastKawasePass");
            private static readonly int s_SSAOTexture0ID = Shader.PropertyToID("_SSAO_OcclusionTexture0");
            private static readonly int s_SSAOTexture1ID = Shader.PropertyToID("_SSAO_OcclusionTexture1");
            private static readonly int s_SSAOTexture2ID = Shader.PropertyToID("_SSAO_OcclusionTexture2");
            private static readonly int s_SSAOTextureFinalID = Shader.PropertyToID("_SSAO_OcclusionTexture");
            private static readonly int s_BlueNoiseTextureID = Shader.PropertyToID("_BlueNoiseTexture");
            private static readonly int s_CameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent");
            private static readonly int s_CameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent");
            private static readonly int s_CameraViewZExtentID = Shader.PropertyToID("_CameraViewZExtent");
            private static readonly int s_ProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2");
            private static readonly int s_KawaseBlurIterationID = Shader.PropertyToID("_KawaseBlurIteration");
            private static readonly int s_CameraViewProjectionsID = Shader.PropertyToID("_CameraViewProjections");
            private static readonly int s_CameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner");

            private static readonly int[] m_BilateralTexturesIndices = { 0, 1, 2, k_FinalTexID };
            private static readonly ShaderPasses[] m_BilateralPasses = { ShaderPasses.BlurHorizontal, ShaderPasses.BlurVertical, ShaderPasses.BlurFinal };
            private static readonly ShaderPasses[] m_BilateralAfterOpaquePasses = { ShaderPasses.BlurHorizontal, ShaderPasses.BlurVertical, ShaderPasses.AfterOpaqueBilateral };

            private static readonly int[] m_GaussianTexturesIndices = { 0, 1, k_FinalTexID, k_FinalTexID };
            private static readonly ShaderPasses[] m_GaussianPasses = { ShaderPasses.BlurHorizontalGaussian, ShaderPasses.BlurVerticalGaussian };
            private static readonly ShaderPasses[] m_GaussianAfterOpaquePasses = { ShaderPasses.BlurHorizontalGaussian, ShaderPasses.AfterOpaqueGaussian };

            private static readonly int[] m_KawaseTexturesIndices = { 0, k_FinalTexID };
            private static readonly ShaderPasses[] m_KawasePasses = { ShaderPasses.KawaseBlur };
            private static readonly ShaderPasses[] m_KawaseAfterOpaquePasses = { ShaderPasses.AfterOpaqueKawase };

            // Enums
            private enum BlurTypes
            {
                Bilateral,
                Gaussian,
                Kawase,
            }

            private enum ShaderPasses
            {
                AO = 0,
                BlurHorizontal = 1,
                BlurVertical = 2,
                BlurFinal = 3,
                BlurHorizontalVertical = 4,
                BlurHorizontalGaussian = 5,
                BlurVerticalGaussian = 6,
                BlurHorizontalVerticalGaussian = 7,
                Upsample = 8,
                KawaseBlur = 9,
                DualKawaseBlur = 10,
                DualFilteringDownsample = 11,
                DualFilteringUpsample = 12,
                AfterOpaque = 13,
                AfterOpaqueBilateral = 14,
                AfterOpaqueGaussian = 15,
                AfterOpaqueKawase = 16,
            }

            internal ScreenSpaceAmbientOcclusionPass()
            {
                m_CurrentSettings = new ScreenSpaceAmbientOcclusionSettings();
                m_SSAOTextures = new[]
                {
                    RTHandles.Alloc(new RenderTargetIdentifier(s_SSAOTexture0ID, 0, CubemapFace.Unknown, -1), "_SSAO_OcclusionTexture0"),
                    RTHandles.Alloc(new RenderTargetIdentifier(s_SSAOTexture1ID, 0, CubemapFace.Unknown, -1), "_SSAO_OcclusionTexture1"),
                    RTHandles.Alloc(new RenderTargetIdentifier(s_SSAOTexture2ID, 0, CubemapFace.Unknown, -1), "_SSAO_OcclusionTexture2"),
                    RTHandles.Alloc(new RenderTargetIdentifier(s_SSAOTextureFinalID, 0, CubemapFace.Unknown, -1), "_SSAO_OcclusionTexture"),
                };
            }

            internal bool Setup(ref ScreenSpaceAmbientOcclusionSettings featureSettings, ref ScriptableRenderer renderer, ref Material material, ref Texture2D[] blueNoiseTextures)
            {
                m_BlueNoiseTextures = blueNoiseTextures;
                m_Material = material;
                m_Renderer = renderer;
                m_CurrentSettings = featureSettings;

                // RenderPass Event + Source Settings (Depth / Depth&Normals
                if (isRendererDeferred)
                {
                    renderPassEvent = m_CurrentSettings.AfterOpaque ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingGbuffer;
                    m_CurrentSettings.Source = ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals;
                }
                else
                {
                    // Rendering after PrePasses is usually correct except when depth priming is in play:
                    // then we rely on a depth resolve taking place after the PrePasses in order to have it ready for SSAO.
                    // Hence we set the event to RenderPassEvent.AfterRenderingPrePasses + 1 at the earliest.
                    renderPassEvent = m_CurrentSettings.AfterOpaque ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingPrePasses + 1;
                }

                // Ask for a Depth or Depth + Normals textures
                switch (m_CurrentSettings.Source)
                {
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth:
                        ConfigureInput(ScriptableRenderPassInput.Depth);
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals:
                        ConfigureInput(ScriptableRenderPassInput.Normal);// need depthNormal prepass for forward-only geometry
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Blur settings
                switch (m_CurrentSettings.BlurQuality)
                {
                    case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.High:
                        m_BlurType = BlurTypes.Bilateral;
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.Medium:
                        m_BlurType = BlurTypes.Gaussian;
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.Low:
                        m_BlurType = BlurTypes.Kawase;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                return m_Material != null
                    && m_CurrentSettings.Intensity > 0.0f
                    && m_CurrentSettings.Radius > 0.0f;
            }

            /// <inheritdoc/>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                int downsampleDivider = m_CurrentSettings.Downsample ? 2 : 1;

                // Update SSAO parameters in the material
                m_Material.SetVector(s_SSAOParamsID, new Vector4(
                    m_CurrentSettings.Intensity,// Intensity
                    m_CurrentSettings.Radius,   // Radius
                    1.0f / downsampleDivider,   // Downsampling
                    m_CurrentSettings.Falloff   // Falloff
                ));

#if ENABLE_VR && ENABLE_XR_MODULE
                int eyeCount = renderingData.cameraData.xr.enabled && renderingData.cameraData.xr.singlePassEnabled ? 2 : 1;
#else
                int eyeCount = 1;
#endif
                for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
                {
                    Matrix4x4 view = renderingData.cameraData.GetViewMatrix(eyeIndex);
                    Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix(eyeIndex);
                    m_CameraViewProjections[eyeIndex] = proj * view;

                    // camera view space without translation, used by SSAO.hlsl ReconstructViewPos() to calculate view vector.
                    Matrix4x4 cview = view;
                    cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                    Matrix4x4 cviewProj = proj * cview;
                    Matrix4x4 cviewProjInv = cviewProj.inverse;

                    Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, 1, -1, 1));
                    Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1, 1, -1, 1));
                    Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, -1, -1, 1));
                    Vector4 farCentre = cviewProjInv.MultiplyPoint(new Vector4(0, 0, 1, 1));
                    m_CameraTopLeftCorner[eyeIndex] = topLeftCorner;
                    m_CameraXExtent[eyeIndex] = topRightCorner - topLeftCorner;
                    m_CameraYExtent[eyeIndex] = bottomLeftCorner - topLeftCorner;
                    m_CameraZExtent[eyeIndex] = farCentre;
                }

                m_Material.SetVector(s_ProjectionParams2ID, new Vector4(1.0f / renderingData.cameraData.camera.nearClipPlane, 0.0f, 0.0f, 0.0f));
                m_Material.SetMatrixArray(s_CameraViewProjectionsID, m_CameraViewProjections);
                m_Material.SetVectorArray(s_CameraViewTopLeftCornerID, m_CameraTopLeftCorner);
                m_Material.SetVectorArray(s_CameraViewXExtentID, m_CameraXExtent);
                m_Material.SetVectorArray(s_CameraViewYExtentID, m_CameraYExtent);
                m_Material.SetVectorArray(s_CameraViewZExtentID, m_CameraZExtent);

                // Update keywords
                CoreUtils.SetKeyword(m_Material, k_OrthographicCameraKeyword, renderingData.cameraData.camera.orthographic);
                CoreUtils.SetKeyword(m_Material, k_AOBlueNoiseKeyword, false);
                CoreUtils.SetKeyword(m_Material, k_AOOriginalKeyword, false);
                switch (m_CurrentSettings.AONoise)
                {
                    case ScreenSpaceAmbientOcclusionSettings.AONoiseOptions.BlueNoise:
                        CoreUtils.SetKeyword(m_Material, k_AOBlueNoiseKeyword, true);
                        m_BlueNoiseTextureIndex = (m_BlueNoiseTextureIndex + 1) % m_BlueNoiseTextures.Length;
                        Texture2D noiseTexture = m_BlueNoiseTextures[m_BlueNoiseTextureIndex];
                        m_Material.SetTexture(s_BlueNoiseTextureID, noiseTexture);

                        float rndOffsetX = Random.value;
                        float rndOffsetY = Random.value;
                        m_Material.SetVector(s_SSAOBlueNoiseParamsID, new Vector4(
                            renderingData.cameraData.pixelWidth / (float)noiseTexture.width,
                            renderingData.cameraData.pixelHeight / (float)noiseTexture.height,
                            rndOffsetX,
                            rndOffsetY
                        ));
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.AONoiseOptions.InterleavedGradient:
                        CoreUtils.SetKeyword(m_Material, k_AOOriginalKeyword, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CoreUtils.SetKeyword(m_Material, k_SampleCountLowKeyword, false);
                CoreUtils.SetKeyword(m_Material, k_SampleCountMediumKeyword, false);
                CoreUtils.SetKeyword(m_Material, k_SampleCountHighKeyword, false);
                switch (m_CurrentSettings.Samples)
                {
                    case ScreenSpaceAmbientOcclusionSettings.AOSampleOption.High:
                        CoreUtils.SetKeyword(m_Material, k_SampleCountHighKeyword, true);
                        break;
                    case ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Medium:
                        CoreUtils.SetKeyword(m_Material, k_SampleCountMediumKeyword, true);
                        break;
                    default:
                        CoreUtils.SetKeyword(m_Material, k_SampleCountLowKeyword, true);
                        break;
                }
                CoreUtils.SetKeyword(m_Material, k_OrthographicCameraKeyword, renderingData.cameraData.camera.orthographic);

                if (m_CurrentSettings.Source == ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth)
                {
                    switch (m_CurrentSettings.NormalSamples)
                    {
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Low:
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionLowKeyword, true);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionMediumKeyword, false);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionHighKeyword, false);
                            break;
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Medium:
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionLowKeyword, false);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionMediumKeyword, true);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionHighKeyword, false);
                            break;
                        case ScreenSpaceAmbientOcclusionSettings.NormalQuality.High:
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionLowKeyword, false);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionMediumKeyword, false);
                            CoreUtils.SetKeyword(m_Material, k_NormalReconstructionHighKeyword, true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                // Set the source keywords...
                switch (m_CurrentSettings.Source)
                {
                    case ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals:
                        CoreUtils.SetKeyword(m_Material, k_SourceDepthKeyword, false);
                        CoreUtils.SetKeyword(m_Material, k_SourceDepthNormalsKeyword, true);
                        break;
                    default:
                        CoreUtils.SetKeyword(m_Material, k_SourceDepthKeyword, true);
                        CoreUtils.SetKeyword(m_Material, k_SourceDepthNormalsKeyword, false);
                        break;
                }

                // Set up the descriptors
                RenderTextureDescriptor descriptor = cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;

                // AO PAss
                m_AOPassDescriptor = descriptor;
                m_AOPassDescriptor.width /= downsampleDivider;
                m_AOPassDescriptor.height /= downsampleDivider;
                bool useRedComponentOnly = m_SupportsR8RenderTextureFormat && m_BlurType > BlurTypes.Bilateral;
                m_AOPassDescriptor.colorFormat = useRedComponentOnly ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

                // Get temporary render textures
                cmd.GetTemporaryRT(s_SSAOTexture0ID, m_AOPassDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(s_SSAOTexture1ID, m_AOPassDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(s_SSAOTexture2ID, m_AOPassDescriptor, FilterMode.Bilinear);

                // Configure targets and clear color
                ConfigureTarget(m_CurrentSettings.AfterOpaque ? m_Renderer.cameraColorTargetHandle : m_SSAOTextures[k_FinalTexID]);
                ConfigureClear(ClearFlag.None, Color.white);

                // Upsample setup
                m_AOPassDescriptor.width *= downsampleDivider;
                m_AOPassDescriptor.height *= downsampleDivider;
                m_AOPassDescriptor.colorFormat = m_SupportsR8RenderTextureFormat ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

                cmd.GetTemporaryRT(s_SSAOTextureFinalID, m_AOPassDescriptor, FilterMode.Bilinear);

                // Configure targets and clear color
                ConfigureTarget(m_SSAOTextures[k_FinalTexID]);
                ConfigureClear(ClearFlag.None, Color.white);
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null)
                {
                    Debug.LogErrorFormat("{0}.Execute(): Missing material. ScreenSpaceAmbientOcclusion pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
                    return;
                }

                var cmd = renderingData.commandBuffer;
                using (new ProfilingScope(cmd, m_ProfilingSampler))
                {
                    // We only want URP shaders to sample SSAO if After Opaque is off.
                    if (!m_CurrentSettings.AfterOpaque)
                        CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, true);

                    PostProcessUtils.SetSourceSize(cmd, m_AOPassDescriptor);

                    Vector4 scaleBiasRt = new Vector4(-1, 1.0f, -1.0f, 1.0f);
                    cmd.SetGlobalVector(s_ScaleBiasRtID, scaleBiasRt);
                    cmd.SetGlobalTexture(k_SSAOTextureName, m_SSAOTextures[k_FinalTexID]);

                    if (m_BlurType == BlurTypes.Kawase)
                    {
                        cmd.SetGlobalInt(s_LastKawasePass, 1);
                        cmd.SetGlobalFloat(s_KawaseBlurIterationID, 0);
                    }

                    // Execute the SSAO
                    Render(ref cmd, ref m_Material, ref m_SSAOTextures[0], ShaderPasses.AO);

                    // Execute the Blur Passes
                    GetPassOrder(m_BlurType, m_CurrentSettings.AfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses);
                    for (int i = 0; i < shaderPasses.Length; i++)
                    {
                        int baseMapIndex = textureIndices[i];
                        int targetIndex = textureIndices[i + 1];
                        RenderAndSetBaseMap(ref cmd, ref renderingData, ref m_Renderer, ref m_Material, ref m_SSAOTextures[baseMapIndex], ref m_SSAOTextures[targetIndex], shaderPasses[i]);
                    }

                    // Set the global SSAO Params
                    cmd.SetGlobalVector(k_SSAOAmbientOcclusionParamName, new Vector4(0f, 0f, 0f, m_CurrentSettings.DirectLightingStrength));
                }
            }

            private static void GetPassOrder(BlurTypes blurType, bool isAfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses)
            {
                switch (blurType)
                {
                    case BlurTypes.Bilateral:
                        textureIndices = m_BilateralTexturesIndices;
                        shaderPasses = isAfterOpaque ? m_BilateralAfterOpaquePasses : m_BilateralPasses;
                        break;
                    case BlurTypes.Gaussian:
                        textureIndices = m_GaussianTexturesIndices;
                        shaderPasses = isAfterOpaque ? m_GaussianAfterOpaquePasses : m_GaussianPasses;
                        break;
                    case BlurTypes.Kawase:
                        textureIndices = m_KawaseTexturesIndices;
                        shaderPasses = isAfterOpaque ? m_KawaseAfterOpaquePasses : m_KawasePasses;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private static void RenderAndSetBaseMap(ref CommandBuffer cmd, ref RenderingData renderingData, ref ScriptableRenderer renderer, ref Material mat, ref RTHandle baseMap, ref RTHandle target, ShaderPasses pass)
            {
                cmd.SetGlobalTexture(s_BaseMapID, baseMap.nameID);

                if (pass >= ShaderPasses.AfterOpaque)
                    RenderAfterOpaque(ref cmd, ref renderingData, ref renderer, ref mat, pass);
                else
                    Render(ref cmd, ref mat, ref target, pass);
            }

            private static void Render(ref CommandBuffer cmd, ref Material mat, ref RTHandle target, ShaderPasses pass)
            {
                CoreUtils.SetRenderTarget(
                    cmd,
                    target,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.clear
                );
                DrawFullScreenMesh(ref cmd, ref mat, pass);
            }

            private static void RenderAfterOpaque(ref CommandBuffer cmd, ref RenderingData renderingData, ref ScriptableRenderer renderer, ref Material mat, ShaderPasses pass)
            {
                // SetRenderTarget has logic to flip projection matrix when rendering to render texture. Flip the uv to account for that case.
                CameraData cameraData = renderingData.cameraData;
                bool isCameraColorFinalTarget = (cameraData.cameraType == CameraType.Game && renderer.cameraColorTargetHandle.nameID == BuiltinRenderTextureType.CameraTarget && cameraData.camera.targetTexture == null);
                bool yFlip = !isCameraColorFinalTarget;
                float flipSign = yFlip ? -1.0f : 1.0f;
                Vector4 scaleBiasRt = (flipSign < 0.0f)
                    ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                    : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);

                cmd.SetGlobalVector(s_ScaleBiasRtID, scaleBiasRt);
                CoreUtils.SetRenderTarget(
                    cmd,
                    renderer.cameraColorTargetHandle,
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    renderer.cameraDepthTargetHandle,
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.clear
                );

                DrawFullScreenMesh(ref cmd, ref mat, pass);
            }

            private static void DrawFullScreenMesh(ref CommandBuffer cmd, ref Material mat, ShaderPasses pass, int submeshIndex = 0)
            {
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, mat, submeshIndex, (int)pass);
            }

            /// <inheritdoc/>
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (cmd == null)
                    throw new ArgumentNullException("cmd");

                if (!m_CurrentSettings.AfterOpaque)
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, false);

                cmd.ReleaseTemporaryRT(s_SSAOTexture0ID);
                cmd.ReleaseTemporaryRT(s_SSAOTexture1ID);
                cmd.ReleaseTemporaryRT(s_SSAOTexture2ID);
                cmd.ReleaseTemporaryRT(s_SSAOTextureFinalID);
            }

            public void Dispose()
            {
                m_SSAOTextures[0].Release();
                m_SSAOTextures[1].Release();
                m_SSAOTextures[2].Release();
                m_SSAOTextures[3].Release();
            }
        }
    }
}
