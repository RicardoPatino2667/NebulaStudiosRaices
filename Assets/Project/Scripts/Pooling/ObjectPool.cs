using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance;

    private Dictionary<GameObject,
        Queue<GameObject>> pool = new();

    private void Awake()
    {
        Instance = this;
    }

    public GameObject Get(GameObject prefab)
    {
        if (!pool.ContainsKey(prefab))
            pool[prefab] = new Queue<GameObject>();

        if (pool[prefab].Count > 0)
        {
            GameObject obj = pool[prefab].Dequeue();

            obj.SetActive(true);

            return obj;
        }

        return Instantiate(prefab);
    }

    public void Return(
        GameObject obj,
        GameObject prefab)
    {
        obj.SetActive(false);

        pool[prefab].Enqueue(obj);
    }
}