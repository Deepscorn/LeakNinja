using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;

// not importing UnityEngine to write explicitly UnityEngine.Object to not mess up with System.Object

namespace LeakNinja
{
    public interface IWatchIdentity
    {
        // Someone say, it's simpler to use one field Object, which returns null if !HasObject.
        // Actually, it's arguable. And definitely wrong with UnityEngine.Object because
        // UnityEngine.Object overloaded operator, so == null may return true for non-null objects
        // Note: not using TryGet because of debugging experience, tool is heavily used from debugger with evaluate
        bool HasObject { get; }
        object Object { get; }
        int ObjectHashCode { get; } // Need in scenario where I remove watches that does not have underlying object
        // (hash code is used from object - to check if existing objects are already watched and make links)
    }

    public abstract class Watch : IWatchIdentity
    {
        public bool HasObject => ref_.TryGetTarget(out _);
        public object Object => ref_.TryGetTarget(out var result)
            ? result : throw new Exception("LeakNinja: trying to get garbage collected object");
        public abstract int ObjectHashCode { get; }

        public abstract string Name { get; }
        public virtual string FullName => Name;

        private readonly WeakReference<object> ref_;

        internal Watch(object reference) => ref_ = new WeakReference<object>(reference);

        public override string ToString() => $"{Name} Watch";
    }

    internal class SystemWatch : Watch
    {
        public override int ObjectHashCode { get; }

        public override string Name { get; }

        internal SystemWatch(object reference) : base(reference)
        {
            Name = reference.ToString();
            ObjectHashCode = reference.GetHashCode();
        }
    }

    public class UnityWatch : Watch
    {
        public override int ObjectHashCode { get; }

        public override string Name { get; }

        private UnityEngine.Object UnityObject => (UnityEngine.Object)Object;

        internal UnityWatch(object reference) : base(reference)
        {
            var name = UnityObject.name;
            Name = string.IsNullOrEmpty(name) ? reference.ToString() : name;
            ObjectHashCode = UnityObject.GetInstanceID(); // unity object's GetHashCode() not constant - tested, so using instanceId
        }
    }

    public class GameWatch : UnityWatch
    {
        public override string FullName => Parent == null ? Name : $"{Parent.FullName}/{Name}";
        public Watch Parent;

        public GameWatch(Watch parent, UnityEngine.GameObject reference) : base(reference) => Parent = parent;
    }

    internal class ComponentWatch : UnityWatch
    {
        public Watch GameWatch;

        public override string Name { get; }
        public override string FullName => GameWatch == null ? Name : $"{GameWatch.FullName} ({Name})";

        internal ComponentWatch(Watch gameWatch, UnityEngine.Component reference) : base(reference)
        {
            GameWatch = gameWatch;
            Name = reference.GetType().Name; // component.name not fit here, because contains gameobject.name
        }

        public override string ToString() => GameWatch == null ? $"{Name} Watch" : $"{GameWatch.Name} ({Name}) Watch";
    }

    // Special class for TryGetValue(object) while returning IWatchInfo container (different key types)
    internal class WatchSet : IReadOnlyCollection<Watch>
    {
        private class Comparer : IEqualityComparer<IWatchIdentity>
        {
            public bool Equals(IWatchIdentity x, IWatchIdentity y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return false;
                if (!x.HasObject && !y.HasObject)
                    return x == y; // for deletion of freed objects
                if (!x.HasObject || !y.HasObject)
                    return false;
                return ReferenceEquals(x.Object, y.Object);
            }

            public int GetHashCode(IWatchIdentity watch) => watch.ObjectHashCode;
        }

        private class ModifiableWatchIdentity : IWatchIdentity
        {
            public bool HasObject => true;
            public object Object { get; set; }
            public int ObjectHashCode => Object is UnityEngine.Object unityObj
                ? unityObj.GetInstanceID() : Object.GetHashCode();
        }

        private readonly Dictionary<IWatchIdentity, Watch> dictionary_ = new Dictionary<IWatchIdentity, Watch>(new Comparer());
        private readonly ModifiableWatchIdentity requestIdentity_ = new ModifiableWatchIdentity();

        public Watch GetValue(object key)
        {
            Assert.IsNotNull(key);
            requestIdentity_.Object = key;
            var result = dictionary_.TryGetValue(requestIdentity_, out var val) ? val : null;
            requestIdentity_.Object = null; // free reference
            return result;
        }

        public void Add(Watch watch) => dictionary_.Add(watch, watch);

        public void Remove(Watch watch) => dictionary_.Remove(watch);

        public IEnumerator<Watch> GetEnumerator() => dictionary_.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => dictionary_.Count;

        public void RemoveWhere(Predicate<Watch> match)
        {
            var itemsToRemove = new List<IWatchIdentity>();
            foreach (var kv in dictionary_)
            {
                if (match(kv.Value))
                    itemsToRemove.Add(kv.Key);
            }

            foreach (var item in itemsToRemove)
                dictionary_.Remove(item);
        }

        public override string ToString() => $"{nameof(WatchSet)} Count = {Count}";
    }

    public static class GcHelper
    {
        private static readonly UnityEngine.WaitForFixedUpdate WaitFixedUpdate = new UnityEngine.WaitForFixedUpdate();

        // Method to wait references freed, so gc will be able to collect them
        // To free references we clean stack. Using yield for that.
        // Note: in editor this is not enough. Editor holds references for some little time, just waiting it
        // (tested on destroyed gameObjects)
        public static IEnumerator WaitReferencesFreed()
        {
#if UNITY_EDITOR 
            var prevTime = UnityEngine.Time.time;
            // TODO find out what is it (probably unity not destroyes objects after update pass sometimes)
            // TODO check needed in batchmode (CI speedup)
            while (UnityEngine.Time.time < prevTime + 0.25f * UnityEngine.Time.timeScale)
                yield return WaitFixedUpdate;
#else
            yield return null; // TODO check yield return WaitFixedUpdate works (will speed up timescaled tests)
#endif
        }
    }

    // Class to add objects to watches and check if it's leaking. All is done manually with method calls.
    // Nothing happens automatically
    public class ManualLeakNinja
    {
        private readonly WatchSet leakingObjects_ = new WatchSet();
        private readonly WatchSet watchedObjects_ = new WatchSet();

        public int TotalWatchedReferencesCount => watchedObjects_.Count + leakingObjects_.Count;

        public IReadOnlyCollection<Watch> WatchedReferences => watchedObjects_;
        public IReadOnlyCollection<Watch> LeakedReferences => leakingObjects_;

        public Watch WatchComponent(Watch gameObject, UnityEngine.Component component)
        {
            var result = new ComponentWatch(gameObject, component);
            watchedObjects_.Add(result);
            return result;
        }

        public Watch WatchGameObject(Watch parent, UnityEngine.GameObject obj)
        {
            var result = new GameWatch(parent, obj);
            watchedObjects_.Add(result);
            return result;
        }

        public Watch WatchUnityObject(UnityEngine.Object obj)
        {
            var result = new UnityWatch(obj);
            watchedObjects_.Add(result);
            return result;
        }

        // General WatchObject. Not make links
        // The only point for regular system objects
        // You can use it e.g. to ensure Disposed object not leaked
        public void WatchObject(object obj)
        {
            if (obj is UnityEngine.Object unityObj)
            {
                if (obj is UnityEngine.GameObject gameObject)
                    WatchGameObject(null, gameObject);
                else if (obj is UnityEngine.Component component)
                    WatchComponent(null, component);
                else
                    WatchUnityObject(unityObj);
                return;
            }
            leakingObjects_.Add(new SystemWatch(obj));
        }

        public void ForceFreeUpdateLeaks()
        {
            GC.Collect();

            leakingObjects_.RemoveWhere(o => !o.HasObject);
            watchedObjects_.RemoveWhere(o => !o.HasObject);

            var recentlyDestroyedObjects = new List<Watch>();
            foreach (var watch in watchedObjects_)
            {
                // ReSharper disable once RedundantCast
                if ((UnityEngine.Object)watch.Object == null)
                    recentlyDestroyedObjects.Add(watch);
            }

            foreach (var objectInfo in recentlyDestroyedObjects)
            {
                watchedObjects_.Remove(objectInfo);
                leakingObjects_.Add(objectInfo);
            }
        }

        public bool IsWatched(object obj) => GetWatchInfo(obj) != null;

        public Watch GetWatchInfo(object obj) => watchedObjects_.GetValue(obj) ?? leakingObjects_.GetValue(obj);

        // ReSharper disable once UnusedMember.Global (used for tests)
        public Watch GetLeakInfo(object obj) => leakingObjects_.GetValue(obj);
    }
}