using UnityEngine;

namespace LeakNinja.Tests
{
    internal static class GameObjectTreeExtensions
    {
        internal static GameObject AddChild(this GameObject parent, GameObject child)
        {
            child.transform.SetParent(parent.transform);
            return parent;
        }
        
        internal static GameObject AddChildren(this GameObject parent, params GameObject[] children)
        {
            foreach (var child in children)
                child.transform.SetParent(parent.transform);
            return parent;
        }
    }
}