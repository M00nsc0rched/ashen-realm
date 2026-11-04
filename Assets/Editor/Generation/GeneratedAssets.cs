using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Paths and create-or-update helpers for generated assets. Existing assets are updated in place so their
    /// GUIDs (and every reference to them) survive regeneration.
    /// </summary>
    internal static class GeneratedAssets
    {
        public const string ScenePath = "Assets/Scenes/Prototype_Valley.unity";
        public const string MeshFolder = "Assets/Art/Generated/Meshes";
        public const string TextureFolder = "Assets/Art/Generated/Textures";
        public const string MaterialFolder = "Assets/Materials";
        public const string PrefabFolder = "Assets/Prefabs";
        public const string SettingsFolder = "Assets/Settings";
        public const string DetailArrayPath = TextureFolder + "/T_DetailArray.asset";
        public const string SkyPath = TextureFolder + "/T_PaintedSky.png";

        /// <summary>Saves a generated texture array, replacing the contents of an existing asset to keep its GUID.</summary>
        public static Texture2DArray SaveTextureArray(Texture2DArray array, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(array, path);
                return array;
            }
            EditorUtility.CopySerialized(array, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(array);
            return existing;
        }

        static readonly HashSet<string> meshesWritten = new HashSet<string>();

        public static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         "Assets/Art", "Assets/Art/Generated", MeshFolder, TextureFolder, MaterialFolder, PrefabFolder,
                         "Assets/Scenes", SettingsFolder,
                     })
                EnsureFolder(folder);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void BeginMeshPass() => meshesWritten.Clear();

        /// <summary>Saves a generated mesh as an asset, reusing the existing asset (and GUID) when present.</summary>
        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshFolder}/{name}.asset";
            meshesWritten.Add(path);
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetColors(mesh.colors);
            var uvs = new List<Vector4>();
            for (int channel = 0; channel < 2; channel++)
            {
                mesh.GetUVs(channel, uvs);
                existing.SetUVs(channel, uvs);
            }
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
                existing.SetTriangles(mesh.GetTriangles(i), i, true);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        public static Mesh SaveMesh(MeshBuilder builder, string name) => SaveMesh(builder.ToMesh(name), name);

        /// <summary>Deletes mesh assets from previous runs that this run did not produce.</summary>
        public static int DeleteStaleMeshes()
        {
            int deleted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (meshesWritten.Contains(path)) continue;
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
            return deleted;
        }

        public static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Writes a texture to PNG and imports it with the given settings. Returns the imported asset.</summary>
        public static T WriteTexture<T>(Texture2D texture, string path, bool asSprite, FilterMode filter, bool srgb = true,
            TextureWrapMode wrapU = TextureWrapMode.Clamp) where T : Object
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (asSprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = asSprite;
            importer.mipmapEnabled = false;
            importer.filterMode = filter;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.wrapModeU = wrapU;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static T CreateOrReplaceScriptable<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
