using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObjectPool
{
    public class PoolBase<T>
    {
        #region Fields

        private readonly Func<T> preloadFunc;
        private readonly Action<T> getAction;
        private readonly Action<T> returnAction;

        private Queue<T> pool = new();
        private List<T> active = new();

        static protected Transform poolContainer;

        #endregion

        #region Constructor

        public PoolBase(Func<T> preloadFunc, Action<T> getAction, Action<T> returnAction, int preloadCount)
        {
            if (poolContainer == null)
                poolContainer = new GameObject("Pool").transform;

            this.preloadFunc = preloadFunc;
            this.getAction = getAction;
            this.returnAction = returnAction;

            if (preloadFunc == null)
            {
                Debug.LogError("Preload function is null!");
                return;
            }

            for (int i = 0; i < preloadCount; i++)
                Return(preloadFunc());
        }

        #endregion

        #region Public Methods

        public T Get() 
        {
            T item = pool.Count > 0 ? pool.Dequeue() : preloadFunc();
            getAction(item);
            active.Add(item);

            return item;
        }

        public void Return(T item)
        {
            returnAction(item);
            pool.Enqueue(item);
            active.Remove(item);
        }

        public void ReturnAll()
        {
            foreach (var item in active)
            {
                Return(item);
            }
        }

        #endregion
    }
}