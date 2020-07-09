using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Rendering.CustomRenderPipeline
{
    public sealed class CustomRenderPipeline : RenderPipeline
    {
        static Mesh s_FullscreenMesh = null;
        public static Mesh fullscreenMesh
        {
            get
            {
                if (s_FullscreenMesh != null)
                    return s_FullscreenMesh;

                float topV = 1.0f;
                float bottomV = 0.0f;

                Mesh mesh = new Mesh { name = "Fullscreen Quad" };
                mesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                mesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                mesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
        private Material tonemappingMat;
        private Material deferredLightingMat;
        public CustomRenderPipeline(CustomRenderPipelineAsset asset)
        {
            tonemappingMat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/ToneMapping"));
            deferredLightingMat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/DeferredLighting"));
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            ShaderBindings.SetPerFrameShaderVariables(context);
            foreach (Camera camera in cameras)
            {
#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                CullingResults cullingResults = Cull(context, camera);
                ShaderBindings.SetPerCameraShaderVariables(context, camera);
                DrawCamera(context, cullingResults, camera);
            }
        }

        CullingResults Cull(ScriptableRenderContext context, Camera camera)
        {
            // Culling. Adjust culling parameters for your needs. One could enable/disable
            // per-object lighting or control shadow caster distance.
            camera.TryGetCullingParameters(out var cullingParameters);
            return context.Cull(ref cullingParameters);
        }

        void DrawCamera(ScriptableRenderContext context, CullingResults cullingResults, Camera camera)
        {
            

            // Sets active render target and clear based on camera background color.
            var cmd = CommandBufferPool.Get();
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            cmd.ClearRenderTarget(true, true, camera.backgroundColor);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            bool useForward = false;
            if (useForward)
            {
                DrawForward(camera, cullingResults,context);
            }
            else
            {
                DrawDeferred(camera, cullingResults,context);
            }
            
            

            // Submit commands to GPU. Up to this point all commands have been enqueued in the context.
            // Several submits can be done in a frame to better controls CPU/GPU workload.
            context.Submit();
        }

        void DrawForward(Camera camera, CullingResults cullingResults, ScriptableRenderContext context)
        {
            bool enableDynamicBatching = false;
            bool enableInstancing = false;
            PerObjectData perObjectData = PerObjectData.None;

            FilteringSettings opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            FilteringSettings transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent);

            SortingSettings opaqueSortingSettings = new SortingSettings(camera);
            opaqueSortingSettings.criteria = SortingCriteria.CommonOpaque;

            // ShaderTagId must match the "LightMode" tag inside the shader pass.
            // If not "LightMode" tag is found the object won't render.
            DrawingSettings opaqueDrawingSettings = new DrawingSettings(ShaderPassTag.forwardLit, opaqueSortingSettings);
            opaqueDrawingSettings.enableDynamicBatching = enableDynamicBatching;
            opaqueDrawingSettings.enableInstancing = enableInstancing;
            opaqueDrawingSettings.perObjectData = perObjectData;
            
            // Render Opaque objects given the filtering and settings computed above.
            // This functions will sort and batch objects.
            context.DrawRenderers(cullingResults, ref opaqueDrawingSettings, ref opaqueFilteringSettings);

            // Renders skybox if required
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
                context.DrawSkybox(camera);
        }

        void RenderGBuffer(Camera camera, CullingResults cullingResults, ScriptableRenderContext context)
        {
            bool enableDynamicBatching = false;
            bool enableInstancing = false;
            PerObjectData perObjectData = PerObjectData.None;

            FilteringSettings opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            FilteringSettings transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent);

            SortingSettings opaqueSortingSettings = new SortingSettings(camera);
            opaqueSortingSettings.criteria = SortingCriteria.CommonOpaque;

            // ShaderTagId must match the "LightMode" tag inside the shader pass.
            // If not "LightMode" tag is found the object won't render.
            DrawingSettings opaqueDrawingSettings = new DrawingSettings(ShaderPassTag.deferredGBuffer, opaqueSortingSettings);
            opaqueDrawingSettings.enableDynamicBatching = enableDynamicBatching;
            opaqueDrawingSettings.enableInstancing = enableInstancing;
            opaqueDrawingSettings.perObjectData = perObjectData;
            
            context.DrawRenderers(cullingResults, ref opaqueDrawingSettings, ref opaqueFilteringSettings);
        }

        void RenderDeferredLighting(Camera camera, CullingResults cullingResults, ScriptableRenderContext context)
        {
            // only output world position for now
            // try to reconstruct world pos
            CommandBuffer cmd = CommandBufferPool.Get("Reconstruct World Pos");
            cmd.DrawMesh(CustomRenderPipeline.fullscreenMesh, Matrix4x4.identity, deferredLightingMat, 0, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        void RenderDeferredFinalPass(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Render ToneMapping");
            cmd.DrawMesh(CustomRenderPipeline.fullscreenMesh, Matrix4x4.identity, tonemappingMat, 0, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            
        }

        void DrawDeferred(Camera camera, CullingResults cullingResults, ScriptableRenderContext context)
        {
            var albedo = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
            var specRough = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
            var normal = new AttachmentDescriptor(RenderTextureFormat.ARGB2101010);
            var emission = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf);
            var depth = new AttachmentDescriptor(RenderTextureFormat.Depth);
            var depthSRV = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf);
            
            
            // At the beginning of the render pass, clear the emission buffer to all black, and the depth buffer to 1.0f
            emission.ConfigureClear(new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, 0);
            depth.ConfigureClear(new Color(), 1.0f, 0); 
            //depthSRV.ConfigureClear(new Color(), 1.0f, 0);
            albedo.ConfigureTarget(BuiltinRenderTextureType.CameraTarget, false, true);
            var attachments = new NativeArray<AttachmentDescriptor>(5, Allocator.Temp);
            const int depthIndex = 0, albedoIndex = 1, specRoughIndex = 2, normalIndex = 3, emissionIndex = 4, depthSRVIndex = 5;
            attachments[depthIndex] = depth;
            attachments[albedoIndex] = albedo;
            attachments[specRoughIndex] = specRough;
            attachments[normalIndex] = normal;
            attachments[emissionIndex] = emission;
            //attachments[depthSRVIndex] = depthSRV;

            using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments, depthIndex))
            {
                attachments.Dispose();
                // Start the first subpass, GBuffer creation: render to albedo, specRough, normal and emission, no need to read any input attachments
                var gbufferColors = new NativeArray<int>(4, Allocator.Temp);
                gbufferColors[0] = albedoIndex;
                gbufferColors[1] = specRoughIndex;
                gbufferColors[2] = normalIndex;
                gbufferColors[3] = emissionIndex;
                //gbufferColors[4] = depthSRVIndex;
                using (context.BeginScopedSubPass(gbufferColors))
                {
                    gbufferColors.Dispose();

                    // Render the deferred G-Buffer
                    RenderGBuffer(camera, cullingResults, context);
                }

                // Second subpass, lighting: Render to the emission buffer, read from albedo, specRough, normal and depth.
                // The last parameter indicates whether the depth buffer can be bound as read-only.
                // Note that some renderers (notably iOS Metal) won't allow reading from the depth buffer while it's bound as Z-buffer,
                // so those renderers should write the Z into an additional FP32 render target manually in the pixel shader and read from it instead
                var lightingColors = new NativeArray<int>(1, Allocator.Temp);
                lightingColors[0] = albedoIndex;
                var lightingInputs = new NativeArray<int>(1, Allocator.Temp);
                lightingInputs[0] = albedoIndex;
                // lightingInputs[1] = specRoughIndex;
                // lightingInputs[2] = normalIndex;
                // //lightingInputs[3] = depthSRVIndex;
                using (context.BeginScopedSubPass(lightingColors, lightingInputs, true))
                {
                    lightingColors.Dispose();
                    lightingInputs.Dispose();
                
                    // PushGlobalShadowParams(context);
                    RenderDeferredLighting(camera, cullingResults, context);
                }

                // Third subpass, tonemapping: Render to albedo (which is bound to the camera target), read from emission.
                var tonemappingColors = new NativeArray<int>(1, Allocator.Temp);
                tonemappingColors[0] = albedoIndex;
                var tonemappingInputs = new NativeArray<int>(1, Allocator.Temp);
                tonemappingInputs[0] = albedoIndex;
                using (context.BeginScopedSubPass(tonemappingColors, tonemappingInputs, true))
                {
                    tonemappingColors.Dispose();
                    tonemappingInputs.Dispose();
                
                    // present frame buffer.
                    RenderDeferredFinalPass(context);
                }
            }
        }
    }
}
