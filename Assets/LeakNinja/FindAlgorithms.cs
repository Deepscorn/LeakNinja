using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeakNinja
{
    // Provides search objects abilities: e.g. in scenes or by adding all (slower).
    // Also, encapsulates logic of not adding objects twice
    public static class FindAlgorithms
    {
        // known limitations: not found non-scene gameobjects (prefabs, dontdestroyonload)
        // (can workaround: e.g. another slower scan algorithm can find some roots for us)
        // Thoughts on optimization: not scan all at once, scan by parts (coroutine)
        public static void WatchAllSceneGameObjects(ManualLeakNinja monitor)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                WatchSceneGameObjects(monitor, scene);
            }
        }

        public static void WatchSceneGameObjects(ManualLeakNinja monitor, Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                WatchRecursive(monitor, root, null);
        }

        public static void WatchAllObjectsThatUnityHaveNow(ManualLeakNinja monitor)
        {
            var all = Resources.FindObjectsOfTypeAll<Object>();
            WatchObjects(monitor, all);
        }

        public static void WatchObjects(ManualLeakNinja monitor, Object[] objects)
        {
            foreach (var obj in objects)
            {
                if (monitor.IsWatched(obj)) // skip those added from previous calls 
                    continue;

                switch (obj)
                {
                    case GameObject gameObj:
                        monitor.WatchGameObject(null, gameObj);
                        gameObjectsBuffer_.Add(gameObj);
                        continue;
                    case Component component:
                        monitor.WatchComponent(null, component);
                        // TODO optimization: probably iterate gameobject components will be better
                        componentsBuffer_.Add(component);
                        continue;
                    default:
                        monitor.WatchUnityObject(obj);
                        break;
                }
            }

            foreach (var component in componentsBuffer_)
            {
                var info = (ComponentWatch)monitor.GetWatchInfo(component);
                var gameObjInfo = monitor.GetWatchInfo(component.gameObject);
                info.GameWatch = gameObjInfo;
            }

            foreach (var gameObject in gameObjectsBuffer_)
            {
                // ReSharper disable once Unity.NoNullPropagation
                var parent = gameObject.transform.parent?.gameObject;
                // ReSharper disable once RedundantCast.0
                if ((System.Object)parent == null)
                    continue;
                var info = (GameWatch)monitor.GetWatchInfo(gameObject);
                var parentInfo = monitor.GetWatchInfo(parent);
                info.Parent = parentInfo;
            }
            gameObjectsBuffer_.Clear();
            componentsBuffer_.Clear();
        }

        private static readonly List<Component> componentsBuffer_ = new List<Component>();
        private static readonly List<GameObject> gameObjectsBuffer_ = new List<GameObject>();

        // searches from gameObj downwards the hierarchy for new gameobjects and components
        public static void WatchRecursive(ManualLeakNinja monitor, GameObject gameObj, Watch parentWatch)
        {
            var gameObjInfo = monitor.GetWatchInfo(gameObj) ?? WatchWithComponents(monitor, gameObj, parentWatch);

            foreach (Transform childTransform in gameObj.transform)
            {
                var child = childTransform.gameObject;
                WatchRecursive(monitor, child, gameObjInfo);
            }
        }

        private static Watch WatchWithComponents(ManualLeakNinja monitor, GameObject gameObject, Watch parent)
        {
            var gameObjectInfo = monitor.WatchGameObject(parent, gameObject);
            foreach (var component in gameObject.GetComponents<Component>())
                monitor.WatchComponent(gameObjectInfo, component);

            return gameObjectInfo;
        }
    }
}