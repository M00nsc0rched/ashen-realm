using AshenRealm.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    internal static class TorchBuilder
    {
        public static void Build(GameObject player, Transform camera, Light light, PlayerClassRig classRig,
            FirstPersonController controller, Material world)
        {
            Texture2D warm = Import("TorchWarm.png"), cold = Import("TorchCold.png");
            Shader shader = Shader.Find("AshenRealm/TorchStrip");
            if (shader == null) throw new System.InvalidOperationException("The carried torch shader is missing.");
            Material material = GeneratedAssets.GetOrCreateMaterial("M_CarriedTorch", shader);
            material.SetTexture("_MainTex", warm);
            material.SetFloat("_Frame", 0f);

            var mount = new GameObject("Carried Torch").transform;
            mount.SetParent(camera, false);
            mount.localPosition = new Vector3(-0.43f, -0.28f, 0.78f);
            var card = new MeshBuilder();
            card.Card(new Vector3(-0.205f, -0.36f, 0f), new Vector3(0.205f, -0.36f, 0f),
                new Vector3(0.205f, 0.735f, 0f), new Vector3(-0.205f, 0.735f, 0f), Vector3.back, 0f, false);
            var picture = new GameObject("Animated Flame Strip");
            picture.transform.SetParent(mount, false);
            picture.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(card, "Player_TorchStrip");
            var renderer = picture.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var hand = new MeshBuilder();
            PlayerWeaponMeshes.Glove(hand);
            var glove = new GameObject("Torch Hand");
            glove.transform.SetParent(mount, false);
            glove.transform.localPosition = new Vector3(0f, -0.1f, -0.035f);
            glove.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.SaveMesh(hand, "Player_TorchHand");
            glove.AddComponent<MeshRenderer>().sharedMaterial = world;
            light.transform.localPosition = new Vector3(-0.3f, 0.3f, 1.2f);

            var torch = player.AddComponent<PlayerTorch>();
            SceneWiring.Set(torch, "mount", mount);
            SceneWiring.Set(torch, "flame", renderer);
            SceneWiring.Set(torch, "torchLight", light);
            SceneWiring.Set(torch, "warmStrip", warm);
            SceneWiring.Set(torch, "coldStrip", cold);
            SceneWiring.Set(torch, "controller", controller);
            SceneWiring.Set(torch, "classRig", classRig);
            mount.gameObject.SetActive(false);
        }

        static Texture2D Import(string file)
        {
            string path = "Assets/Art/Menu/Torch/" + file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException("Missing supplied torch strip " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
