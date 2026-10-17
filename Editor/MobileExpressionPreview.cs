// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CQMHV

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace MobileExpressionConverter.Editor
{
    internal sealed class MobileExpressionPreview : IRenderFilter
    {
        private static readonly TogglablePreviewNode Control = TogglablePreviewNode.Create(
            () => Texts.T("NDMF Preview", "NDMF 预览", "NDMF プレビュー"),
            "com.cqmhv.mobile-expression-converter/preview", true);

        private static readonly PublishedValue<bool> MobileTarget =
            new PublishedValue<bool>(MobileExpressionPlugin.IsMobileTarget(EditorUserBuildSettings.activeBuildTarget));

        static MobileExpressionPreview()
        {
            EditorUserBuildSettings.activeBuildTargetChanged += () =>
                MobileTarget.Value = MobileExpressionPlugin.IsMobileTarget(EditorUserBuildSettings.activeBuildTarget);
        }

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return Control;
        }

        public bool IsEnabled(ComputeContext context) => context.Observe(Control.IsEnabled);

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            if (!context.Observe(MobileTarget))
                return ImmutableList<RenderGroup>.Empty;
            var groups = ImmutableList.CreateBuilder<RenderGroup>();
            foreach (var root in context.GetAvatarRoots())
            {
                var settings = context.GetComponent<MobileExpressionSettings>(root);
                if (settings == null) continue;
                context.Observe(settings);
                if (!settings.enabled || !settings.previewInEditor) continue;
                var descriptor = context.GetComponent<VRCAvatarDescriptor>(root);
                if (descriptor != null) context.Observe(descriptor);
                foreach (var candidate in context.GetComponentsInChildren<SkinnedMeshRenderer>(root, true))
                {
                    context.Observe(candidate, r => r.name);
                    context.Observe(candidate, r => r.sharedMesh);
                    context.ObservePath(candidate.transform);
                }
                var renderers = MobileExpressionPlugin.FindTargetRenderers(settings)
                    .Where(r => r.sharedMesh != null).Cast<Renderer>().ToArray();
                if (renderers.Length != 0)
                    groups.Add(RenderGroup.For(renderers).WithData(settings, EqualityComparer<MobileExpressionSettings>.Default));
            }
            return groups.ToImmutable();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var node = new PreviewNode(group.GetData<MobileExpressionSettings>());
            try
            {
                node.Prepare(proxyPairs, context);
                return Task.FromResult<IRenderFilterNode>(node);
            }
            catch (Exception exception)
            {
                node.Dispose();
                Debug.LogWarning("[Mobile Expression Converter] Preview failed: " + exception.Message);
                return Task.FromResult<IRenderFilterNode>(new PreviewNode(null));
            }
        }

        private sealed class PreviewNode : IRenderFilterNode
        {
            private readonly MobileExpressionSettings settings;
            private readonly Dictionary<Renderer, ExpressionProcessingMode> modes = new Dictionary<Renderer, ExpressionProcessingMode>();
            private readonly Dictionary<(Material, ExpressionProcessingMode), Material> materials =
                new Dictionary<(Material, ExpressionProcessingMode), Material>();
            private readonly Dictionary<Material, Texture2D> textures = new Dictionary<Material, Texture2D>();
            private readonly HashSet<Material> failed = new HashSet<Material>();
            private readonly HashSet<Material> inputs = new HashSet<Material>();
            private readonly List<Object> owned = new List<Object>();
            private ComputeContext context;
            private bool disposed;

            internal PreviewNode(MobileExpressionSettings settings) => this.settings = settings;

            // Outputs do not change when this node is reused for a shape-only update.
            public RenderAspects WhatChanged => 0;

            internal void Prepare(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext computeContext)
            {
                context = computeContext;
                if (settings == null || !settings.enabled || !settings.previewInEditor) return;
                foreach (var pair in proxyPairs)
                {
                    var original = pair.Item1 as SkinnedMeshRenderer;
                    if (original == null || pair.Item2 == null) continue;
                    var mode = settings.GetProcessingMode(original);
                    modes[original] = mode;
                    foreach (var source in original.sharedMaterials.Concat(pair.Item2.sharedMaterials))
                    {
                        if (source == null) continue;
                        inputs.Add(source);
                        Convert(source, mode);
                    }
                }
                ObserveInputs(computeContext);
            }

            private void Convert(Material source, ExpressionProcessingMode mode)
            {
                var key = (source, mode);
                if (materials.ContainsKey(key) || failed.Contains(source) ||
                    !ParticleTextureConverter.IsTransparentExpression(source)) return;
                var shader = Shader.Find(MobileExpressionPlugin.OutputShaderName(mode));
                if (shader == null)
                {
                    failed.Add(source);
                    Debug.LogWarning("[Mobile Expression Converter] Mobile preview shader is unavailable.", source);
                    return;
                }
                Texture2D texture;
                if (!textures.TryGetValue(source, out texture))
                {
                    string reason;
                    if (!ParticleTextureConverter.TryPrepareTexture(source, out texture, out reason))
                    {
                        failed.Add(source);
                        Debug.LogWarning("[Mobile Expression Converter] Preview: " + source.name + ": " + reason, source);
                        return;
                    }
                    textures.Add(source, texture);
                    if (texture != null)
                    {
                        texture.hideFlags = HideFlags.HideAndDontSave;
                        owned.Add(texture);
                    }
                }
                var material = MobileExpressionPlugin.CreateMaterial(source, shader, texture);
                material.hideFlags = HideFlags.HideAndDontSave;
                owned.Add(material);
                materials.Add(key, material);
            }

            private void ObserveInputs(ComputeContext computeContext)
            {
                if (settings != null) computeContext.Observe(settings);
                foreach (var material in inputs)
                {
                    if (material == null) continue;
                    computeContext.Observe(material);
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        if (texture != null) computeContext.Observe(texture);
                    }
                }
            }

            public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext computeContext, RenderAspects updatedAspects)
            {
                if (disposed || updatedAspects == 0 || (updatedAspects & ~RenderAspects.Shapes) != 0 ||
                    owned.Any(item => item == null))
                    return Task.FromResult<IRenderFilterNode>(null);
                context = computeContext;
                ObserveInputs(computeContext);
                return Task.FromResult<IRenderFilterNode>(this);
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                ExpressionProcessingMode mode;
                if (disposed || proxy == null || !modes.TryGetValue(original, out mode)) return;
                var slots = proxy.sharedMaterials;
                var changed = false;
                for (var index = 0; index < slots.Length; index++)
                {
                    var source = slots[index];
                    if (source == null) continue;
                    Material replacement;
                    if (materials.TryGetValue((source, mode), out replacement))
                    {
                        if (replacement != null)
                        {
                            slots[index] = replacement;
                            changed = true;
                        }
                        else context.Invalidate();
                    }
                    else if (!failed.Contains(source) && ParticleTextureConverter.IsTransparentExpression(source))
                    {
                        // Animation or an upstream preview changed the material. Rebuild outside OnFrame
                        // so texture packing and GPU readback never run in the per-frame render callback.
                        context.Invalidate();
                    }
                }
                if (changed) proxy.sharedMaterials = slots;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                foreach (var material in owned.OfType<Material>())
                    if (material != null) Object.DestroyImmediate(material);
                foreach (var texture in owned.OfType<Texture2D>())
                    if (texture != null) Object.DestroyImmediate(texture);
                owned.Clear();
            }
        }
    }
}
