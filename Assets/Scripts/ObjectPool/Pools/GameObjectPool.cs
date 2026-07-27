using UnityEngine;
using Object = UnityEngine.Object;

namespace ObjectPool
{
    public class GameObjectPool : PoolBase<GameObject>
    {
        #region Constructor

        public GameObjectPool(GameObject prefab, int preloadCount) : base(() => Preload(prefab), GetAction, ReturnAction, preloadCount)
        {
        }

        #endregion

        #region Public Methods

        public static GameObject Preload(GameObject prefab) => Object.Instantiate(prefab,poolContainer);
        public static void GetAction(GameObject @object) => @object.SetActive(true);
        public static void ReturnAction(GameObject @object) { @object.gameObject.SetActive(false); @object.transform.parent = poolContainer; }

        #endregion
    }
}