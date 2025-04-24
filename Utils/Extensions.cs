using UnityEngine;

namespace CustomTV.Utils
{
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static void Remove<T>(this Queue<T> queue, T itemToRemove)
        {
            var array = queue.ToArray();
            queue.Clear();
            foreach (var item in array)
            {
                if (!EqualityComparer<T>.Default.Equals(item, itemToRemove))
                {
                    queue.Enqueue(item);
                }
            }
        }

        public static string GetFullPath(this Transform transform)
        {
            if (transform == null)
                return "null";

            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
