using AshenRealm.Core;
using AshenRealm.Interactions;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>Authored camera poses keep each generated location's visual review repeatable.</summary>
    internal static class WorldVista
    {
        public static void Add(Transform environment, string name, Vector3 position, Vector3 lookAt)
        {
            Transform views = environment.Find("Vistas");
            if (views == null)
            {
                views = new GameObject("Vistas").transform;
                views.SetParent(environment, false);
            }
            var view = new GameObject(name).transform;
            view.SetParent(views, false);
            view.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        public static void Inspect(Transform environment, string name, Vector3 position, Vector3 size, string lore)
        {
            var site = new GameObject("Discovery " + name);
            site.transform.SetParent(environment, false);
            site.transform.position = position;
            var trigger = site.AddComponent<BoxCollider>();
            trigger.size = size;
            trigger.isTrigger = true;
            var inspectable = site.AddComponent<LoreInspectable>();
            SceneWiring.Set(inspectable, "prompt", GameText.Inspect);
            SceneWiring.Set(inspectable, "loreText", lore);
        }
    }
}
