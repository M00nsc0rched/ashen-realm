using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Assigns private [SerializeField] values through SerializedObject so changes are persisted
    /// (and recorded as overrides on prefab instances).
    /// </summary>
    internal static class SceneWiring
    {
        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");

            switch (value)
            {
                case null:
                    p.objectReferenceValue = null;
                    break;
                case Object o:
                    p.objectReferenceValue = o;
                    break;
                case float f:
                    p.floatValue = f;
                    break;
                case bool b:
                    p.boolValue = b;
                    break;
                case string s:
                    p.stringValue = s;
                    break;
                case Color c:
                    p.colorValue = c;
                    break;
                case Vector2 v2:
                    p.vector2Value = v2;
                    break;
                case Vector3 v3:
                    p.vector3Value = v3;
                    break;
                case LayerMask mask:
                    p.intValue = mask.value;
                    break;
                case Enum e:
                    p.intValue = Convert.ToInt32(e);
                    break;
                case int i:
                    p.intValue = i;
                    break;
                case Vector3[] points:
                    p.arraySize = points.Length;
                    for (int k = 0; k < points.Length; k++) p.GetArrayElementAtIndex(k).vector3Value = points[k];
                    break;
                case Object[] objects:
                    p.arraySize = objects.Length;
                    for (int k = 0; k < objects.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = objects[k];
                    break;
                default:
                    throw new ArgumentException($"Unsupported value type {value.GetType().Name} for '{field}'.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
