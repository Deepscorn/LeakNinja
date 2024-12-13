using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeakNinja.Tests
{
    internal static class WeakRefExtensions
    {
        internal static T GetTarget<T>(this WeakReference<T> weakRef) where T : class 
            => weakRef.TryGetTarget(out var result) ? result : throw new Exception("trying to get garbage collected object");
    }
    
    internal class ObjectRelationsTest
    {
        [SetUp]
        public void SetUp() => Time.timeScale = 100f;
        
        private class EmptyComponent : MonoBehaviour { }
        
        [UnityTest]
        public IEnumerator TestComponentNotHoldGameObject()
        {
            var gameObjectRef = new WeakReference<GameObject>(new GameObject("gameObject"));
            var component = gameObjectRef.GetTarget().AddComponent<EmptyComponent>();

            yield return LeakTestUtils.DestroyAndWait(gameObjectRef.GetTarget());
            if (Application.isEditor)
                yield return GcHelper.WaitReferencesFreed();
            GC.Collect();
            
            Assert.IsFalse(gameObjectRef.TryGetTarget(out _));
            Assert.Throws<MissingReferenceException>(() =>
            {
                // ReSharper disable once UnusedVariable
                var gameObject = component.gameObject;
            });
        }

        [UnityTest]
        public IEnumerator TestGameObjectNotHoldParent()
        {
            var parentRef = new WeakReference<GameObject>(new GameObject("parent"));
            var gameObject = new GameObject("gameObject");
            gameObject.transform.SetParent(parentRef.GetTarget().transform);

            yield return LeakTestUtils.DestroyAndWait(parentRef.GetTarget());
            if (Application.isEditor)
                yield return GcHelper.WaitReferencesFreed();
            GC.Collect();
            
            Assert.IsFalse(parentRef.TryGetTarget(out _));
            Assert.Throws<MissingReferenceException>(() =>
            {
                // ReSharper disable once UnusedVariable
                var parent = gameObject.transform.parent;
            });
        }
        
        [UnityTest]
        public IEnumerator TestGetDestroyedChild()
        {
            var child = new GameObject("child");
            var gameObject = new GameObject("gameObject");
            child.transform.SetParent(gameObject.transform);
            Assert.AreEqual(1, gameObject.transform.childCount);

            yield return LeakTestUtils.DestroyAndWait(child);
            // ReSharper disable once Unity.InefficientPropertyAccess
            Assert.AreEqual(0, gameObject.transform.childCount);
        }
        
        [UnityTest]
        public IEnumerator TestGetDestroyedComponent()
        {
            var gameObject = new GameObject("gameObject");
            var component = gameObject.AddComponent<EmptyComponent>();
            Assert.IsNotNull(gameObject.GetComponent<EmptyComponent>());

            yield return LeakTestUtils.DestroyAndWait(component);
            // ReSharper disable once Unity.InefficientPropertyAccess
            Assert.IsNull(gameObject.GetComponent<EmptyComponent>());
        }
    }
}