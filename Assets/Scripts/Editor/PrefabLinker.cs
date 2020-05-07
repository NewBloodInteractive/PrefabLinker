using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace NewBlood
{ 
    public static class PrefabLinker
    {
        /// <summary>Creates a new variant of <paramref name="prefab"/> based on <paramref name="instance"/>.</summary>
        /// <param name="instance">A GameObject with a hierarchy compatible with instances of <paramref name="prefab"/>.</param>
        /// <param name="prefab">The prefab upon which the returned variant will be based.</param>
        /// <returns>The new prefab variant, or <see langword="null"/> upon failure.</returns>
        public static GameObject CreatePrefabVariant(GameObject instance, GameObject prefab)
        {
            var variant  = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            variant.name = instance.name;

            if (DuplicatePrefabHierarchy(instance, variant) && DuplicatePrefabComponents(instance, variant))
            {
                RecoverObjectReferences(instance, variant);
                return variant;
            }

            return null;
        }

        static void RecoverObjectReferences(GameObject src, GameObject dst)
        {
            RecoverObjectReferences(src.transform, src, dst.transform, dst);
        }

        // This method will recover references to game objects within the prefab instance hierarchy.
        // Without it, the newly created prefab variant would simply lose these references altogether.
        static void RecoverObjectReferences(Transform srcRoot, GameObject src, Transform dstRoot, GameObject dst)
        {
            var srcChildren = GetChildrenAndSelf(src.transform);
            var dstChildren = GetChildrenAndSelf(dst.transform);

            for (int i = 0; i < dstChildren.Length; i++)
            {
                src               = srcChildren[i].gameObject;
                dst               = dstChildren[i].gameObject;
                var srcComponents = src.GetComponents<Component>();
                var dstComponents = dst.GetComponents<Component>();

                for (int j = 0; j < srcComponents.Length; j++)
                {
                    var srcObject   = new SerializedObject(srcComponents[j]);
                    var dstObject   = new SerializedObject(dstComponents[j]);
                    var srcIterator = srcObject.GetIterator();
                    var dstIterator = dstObject.GetIterator();

                    while (srcIterator.Next(true) && dstIterator.Next(true))
                    {
                        if (srcIterator.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        if (!TryGetTransform(srcIterator.objectReferenceValue, out var transform))
                            continue;

                        if (!transform.IsChildOf(srcRoot))
                            continue;

                        var path  = AnimationUtility.CalculateTransformPath(transform, srcRoot.parent);
                        var value = FindTransformByPath(dstRoot, path);

                        if (value)
                        {
                            if (srcIterator.objectReferenceValue is GameObject)
                                dstIterator.objectReferenceValue = value.gameObject;

                            if (srcIterator.objectReferenceValue is Component component)
                                dstIterator.objectReferenceValue = value.GetComponent(component.GetType());
                        }
                    }

                    dstObject.ApplyModifiedProperties();
                }

                if (i > 0)
                    RecoverObjectReferences(srcRoot, src, dstRoot, dst);
            }
        }

        static bool DuplicatePrefabComponents(GameObject src, GameObject dst)
        {
            var srcChildren = GetChildrenAndSelf(src.transform);
            var dstChildren = GetChildrenAndSelf(dst.transform);

            for (int i = 0; i < dstChildren.Length; i++)
            {
                src               = srcChildren[i].gameObject;
                dst               = dstChildren[i].gameObject;
                var srcComponents = src.GetComponents<Component>();
                var dstComponents = dst.GetComponents<Component>();

                // Copy over any changes made to the source prefab's components.
                for (int j = 0; j < dstComponents.Length; j++)
                {
                    // No support for reordered or completely removed components.
                    if (srcComponents[j].GetType() != dstComponents[j].GetType())
                    {
                        Debug.LogError("Component list is incompatible.", dst);
                        return false;
                    }

                    EditorUtility.CopySerializedIfDifferent(srcComponents[j], dstComponents[j]);
                }

                // Copy over any newly added components.
                for (int j = dstComponents.Length; j < srcComponents.Length; j++)
                {
                    var component = dst.AddComponent(srcComponents[j].GetType());
                    EditorUtility.CopySerialized(srcComponents[j], component);
                }

                if (i > 0 && !DuplicatePrefabComponents(src, dst))
                    return false;
            }

            return true;
        }

        static bool DuplicatePrefabHierarchy(GameObject src, GameObject dst)
        {
            var srcChildren = GetChildrenAndSelf(src.transform);
            var dstChildren = GetChildrenAndSelf(dst.transform);

            for (int i = 0; i < dstChildren.Length; i++)
            {
                if (srcChildren[i].name != dstChildren[i].name)
                {
                    // Currently there is no support for hierarchies with renamed
                    // or reordered game objects, and I'm sure that hierarchies
                    // containing several game objects with the same name at the
                    // same level might fail too.
                    Debug.LogError("Hierarchy is incompatible.", dst);
                    return false;
                }

                dstChildren[i].gameObject.tag   = srcChildren[i].gameObject.tag;
                dstChildren[i].gameObject.layer = srcChildren[i].gameObject.layer;

                if (i > 0 && !DuplicatePrefabHierarchy(srcChildren[i].gameObject, dstChildren[i].gameObject))
                    return false;
            }

            for (int i = dstChildren.Length; i < srcChildren.Length; i++)
            {
                var child  = InstantiateAndMaintainPrefabLink(srcChildren[i].gameObject, dst.transform);
                child.name = srcChildren[i].name;
            }

            return true;
        }

        // Using Instantiate() will break any links to nested prefabs, so we need to use this
        // specialized method for instantiating the children of the prefab instance.
        static GameObject InstantiateAndMaintainPrefabLink(GameObject source, Transform parent)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(source))
            {
                var path  = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
                var asset = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(source, path);
                var clone = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
                var mods  = PrefabUtility.GetPropertyModifications(source);
                PrefabUtility.SetPropertyModifications(clone, mods);
                return clone;
            }

            return Object.Instantiate(source, parent);
        }

        static Transform[] GetChildrenAndSelf(Transform parent)
        {
            var children = new Transform[parent.childCount + 1];
            children[0]  = parent;

            for (int i = 0; i < parent.childCount; i++)
                children[i + 1] = parent.GetChild(i);

            return children;
        }

        static bool TryGetTransform(Object obj, out Transform transform)
        {
            if (obj is GameObject gameObject)
                transform = gameObject.transform;
            else if (obj is Component component)
                transform = component.transform;
            else
            {
                transform = null;
                return false;
            }

            return true;
        }

        // Similar to Transform.Find, but acts as if it were called from
        // a (potentially non-existent) parent of the supplied transform.
        static Transform FindTransformByPath(Transform root, string path)
        {
            if (path == root.name)
                return root;

            if (path.StartsWith(root.name + '/'))
                path = path.Substring(root.name.Length + 1);

            return root.Find(path);
        }
    }
}
