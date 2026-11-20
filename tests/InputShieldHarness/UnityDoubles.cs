using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

// Only model the component lifetime, ancestry, and properties InputShield uses.
// There is deliberately no rendering, coordinate hit testing, or Unity event dispatch.
namespace UnityEngine
{
    internal class Object
    {
        private static readonly Queue<Object> PendingDestroy = new();
        private bool _destroyed;

        public static implicit operator bool([NotNullWhen(true)] Object? obj) => obj is not null && !obj._destroyed;
        public static bool operator ==(Object? left, Object? right) => left is null ? !right :
            right is null ? !left : ReferenceEquals(left, right);
        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => base.GetHashCode();

        internal static void DontDestroyOnLoad(Object obj) { }
        internal static void Destroy(Object? obj)
        {
            if (obj is not null && obj) PendingDestroy.Enqueue(obj);
        }

        internal static void EndFrame()
        {
            MonoBehaviour.RunEndOfFrame();
            while (PendingDestroy.TryDequeue(out var obj)) obj.DestroyNow();
        }

        protected void RequireAlive()
        {
            if (_destroyed) throw new InvalidOperationException("Access to destroyed Unity object.");
        }

        internal virtual void DestroyNow() => _destroyed = true;
    }

    internal sealed class GameObject : Object
    {
        internal static readonly List<GameObject> Created = new();
        private readonly List<Component> _components = new();
        private readonly Transform _transform;
        private bool _active = true;

        internal GameObject(string name, params Type[] types)
        {
            Name = name;
            foreach (var type in types) Attach((Component)Activator.CreateInstance(type)!);
            _transform = GetComponent<Transform>() ?? AddComponent<Transform>();
            Created.Add(this);
        }

        internal string Name { get; }
        internal int layer { get; set; }
        internal bool activeSelf { get { RequireAlive(); return _active; } }
        internal bool activeInHierarchy => activeSelf && (_transform.Parent?.gameObject.activeInHierarchy ?? true);
        internal Transform transform { get { RequireAlive(); return _transform; } }
        internal void SetActive(bool value) { RequireAlive(); _active = value; }
#nullable disable annotations
        // Match the unannotated Unity API so the linked file compiles as it does in the plugin.
        internal T GetComponent<T>() where T : Component
        {
            RequireAlive();
            return _components.OfType<T>().FirstOrDefault(component => component);
        }
#nullable restore annotations

        internal T AddComponent<T>() where T : Component, new()
        {
            RequireAlive();
            var component = new T();
            Attach(component);
            return component;
        }

        private void Attach(Component component)
        {
            component.Host = this;
            _components.Add(component);
        }

        internal override void DestroyNow()
        {
            if (!this) return;
            foreach (var child in Created.Where(root => root && root.transform.Parent == _transform))
                child.DestroyNow();
            foreach (var component in _components)
                if (component) component.DestroyNow();
            base.DestroyNow();
        }
    }

    internal class Component : Object
    {
        internal GameObject Host { get; set; } = null!;
        internal GameObject gameObject { get { RequireAlive(); return Host; } }
        internal T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }

    internal class Behaviour : Component
    {
        internal bool enabled { get; set; } = true;
        internal bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }

    internal class MonoBehaviour : Behaviour
    {
        private static readonly List<(MonoBehaviour Host, IEnumerator Routine)> EndOfFrameRoutines = new();

        internal void StartCoroutine(IEnumerator routine)
        {
            if (!gameObject.activeSelf) throw new InvalidOperationException("Inactive coroutine host.");
            if (!routine.MoveNext()) return;
            if (routine.Current is not WaitForEndOfFrame)
                throw new InvalidOperationException("Only WaitForEndOfFrame is modeled.");
            EndOfFrameRoutines.Add((this, routine));
        }

        internal static void RunEndOfFrame()
        {
            var routines = EndOfFrameRoutines.ToArray();
            EndOfFrameRoutines.Clear();
            foreach (var (host, routine) in routines)
                if (host && host.gameObject.activeSelf && routine.MoveNext())
                    throw new InvalidOperationException("Only a single frame wait is modeled.");
        }

        internal override void DestroyNow()
        {
            var callback = GetType().GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            callback?.Invoke(this, null);
            base.DestroyNow();
        }
    }

    internal sealed class WaitForEndOfFrame { }

    internal sealed class CanvasGroup : Behaviour
    {
        internal float alpha { get; set; } = 1f;
        internal bool interactable { get; set; } = true;
        internal bool blocksRaycasts { get; set; } = true;
    }

    internal class Transform : Component
    {
        internal Transform? Parent { get; private set; }
        internal void SetParent(Transform parent, bool worldPositionStays) => Parent = parent;
        internal bool IsChildOf(Transform parent)
        {
            for (var node = Parent; node; node = node!.Parent)
                if (node == parent) return true;
            return false;
        }
    }

    internal sealed class RectTransform : Transform
    {
        internal Vector2 anchorMin, anchorMax, offsetMin, offsetMax;
    }

    internal sealed class Canvas : Behaviour
    {
        internal bool overrideSorting { get; set; }
        internal int sortingOrder { get; set; }
        internal int sortingLayerID { get; set; }
        internal RenderMode renderMode { get; set; }
    }

    internal enum RenderMode { ScreenSpaceOverlay }
    internal readonly record struct Vector2(float X, float Y)
    {
        internal static Vector2 zero => new(0, 0);
        internal static Vector2 one => new(1, 1);
    }
    internal readonly struct Color { internal static Color clear => default; }
    internal static class LayerMask { internal static int NameToLayer(string name) => 5; }
}

namespace UnityEngine.UI
{
    internal sealed class CanvasRenderer { internal bool cullTransparentMesh { get; set; } = true; }
    internal class Graphic : Behaviour
    {
        internal bool raycastTarget { get; set; } = true;
        internal CanvasRenderer canvasRenderer { get; } = new();
    }
    internal sealed class Image : Graphic
    {
        internal Color color { get; set; }
        internal float alphaHitTestMinimumThreshold { get; set; }
    }
    internal sealed class GraphicRaycaster : Behaviour { }
}

namespace UnityEngine.EventSystems
{
    internal struct RaycastResult
    {
        internal UnityEngine.GameObject gameObject;
        internal UnityEngine.Vector2 screenPosition;
    }
}

namespace UnknownInventoryTerminal
{
    internal sealed class InvButton : UnityEngine.MonoBehaviour { }

    internal sealed class PlayerCamera : UnityEngine.Object
    {
        internal static PlayerCamera? main;
        internal UnityEngine.Component? radialMenu, dragImage;
        internal UnityEngine.GameObject? containerMenu;
    }

    internal sealed class GlobalDark : UnityEngine.Object
    {
        internal static GlobalDark? main { get; set; }
        internal UnityEngine.RectTransform? tooltipRect { get; set; }
    }

}
