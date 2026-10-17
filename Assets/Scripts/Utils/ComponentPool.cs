using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>Simple GameObject pool for frequently spawned things (sparks, numbers, projectiles).</summary>
    public sealed class ComponentPool<T> where T : Component
    {
        readonly Stack<T> _free = new Stack<T>();
        readonly Func<T> _create;

        public ComponentPool(Func<T> create) => _create = create;

        public T Get()
        {
            while (_free.Count > 0)
            {
                T item = _free.Pop();
                if (item == null) continue; // destroyed with its scene
                item.gameObject.SetActive(true);
                return item;
            }
            T made = _create();
            made.gameObject.SetActive(true);
            return made;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            _free.Push(item);
        }
    }
}
