using System;
using AshenRealm.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>Icons are photographed from the same mesh recipes as the hands, so their identity cannot drift.</summary>
    internal static class EquipmentIconBuilder
    {
        static Sprite[] classes, goods;
        public static void Reset() { classes = null; goods = null; }

        public static Sprite[] ClassSprites()
        {
            if (classes != null) return classes;
            classes = new Sprite[6];
            Action<MeshBuilder>[] draw =
            {
                b =>
                {
                    b.Push(new Vector3(-0.18f, 0f, 0f), Quaternion.identity, Vector3.one);
                    PlayerWeaponMeshes.CrescentPolearm(b); b.Pop();
                    b.Push(new Vector3(0.36f, 0.25f, 0f), Quaternion.identity, Vector3.one * 0.7f);
                    PlayerWeaponMeshes.SunShield(b); b.Pop();
                },
                PropMeshes.Dagger, PlayerWeaponMeshes.AntlerStaff,
                b => Pair(b, PlayerWeaponMeshes.WardenBlade),
                b => Pair(b, PlayerWeaponMeshes.NecromancerSickle), PlayerWeaponMeshes.ReagentBottle,
            };
            for (int i = 0; i < classes.Length; i++) classes[i] = Capture("Class_" + (CharacterClass)i, draw[i]);
            return classes;
        }

        public static Sprite[] GoodsSprites()
        {
            if (goods != null) return goods;
            Action<MeshBuilder>[] draw = { TownMeshes.RustedBlade, TownMeshes.IronSword, TownMeshes.GraveDagger, TownMeshes.PilgrimStaff };
            goods = new Sprite[draw.Length];
            for (int i = 0; i < draw.Length; i++) goods[i] = Capture("Goods_" + i, draw[i]);
            return goods;
        }

        static void Pair(MeshBuilder b, Action<MeshBuilder> draw)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                b.Push(new Vector3(side * 0.25f, 0f, 0f), Quaternion.identity, new Vector3(-side, 1f, 1f));
                draw(b);
                b.Pop();
            }
        }

        static Sprite Capture(string name, Action<MeshBuilder> draw)
        {
            var builder = new MeshBuilder();
            draw(builder);
            Mesh mesh = builder.ToMesh("Equipment Icon " + name);
            var root = new GameObject("Equipment Icon Studio");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.position = new Vector3(0f, 5000f, 0f);
            root.layer = 31;
            var model = new GameObject("Model");
            model.layer = 31;
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.MaterialFolder + "/M_World.mat"));
            material.SetColor("_ShadowColor", Color.white);
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            Bounds bounds = mesh.bounds;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.14f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.transform.localPosition = bounds.center + Vector3.back * 5f;
            camera.transform.localRotation = Quaternion.identity;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            bool fog = RenderSettings.fog;
            var ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            var probe = RenderSettings.ambientProbe;
            Light sun = RenderSettings.sun;
            bool sunEnabled = sun != null && sun.enabled;
            var target = RenderTexture.GetTemporary(96, 96, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;
                var studioProbe = new SphericalHarmonicsL2();
                studioProbe.AddAmbientLight(Color.white);
                RenderSettings.ambientProbe = studioProbe;
                if (sun != null) sun.enabled = false;
                camera.targetTexture = target;
                // The first offscreen render can precede URP's initial ambient setup in batch mode.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, 96f, 96f), 0, 0);
                texture.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientProbe = probe;
                if (sun != null) sun.enabled = sunEnabled;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
            return GeneratedAssets.WriteTexture<Sprite>(texture, GeneratedAssets.TextureFolder + "/UI_Equipment_" + name + ".png", true, FilterMode.Point);
        }
    }
}
