using System.Collections;
using UnityEngine;

namespace MirrorTrial.Feedback
{
    public class HitStopService : MonoBehaviour
    {
        static HitStopService instance;
        Coroutine routine;
        float defaultFixedDeltaTime;

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        public static void Request(float duration)
        {
            if (duration <= 0f)
                return;

            if (!instance)
            {
                var serviceObject = new GameObject("HitStopService");
                instance = serviceObject.AddComponent<HitStopService>();
            }

            instance.Play(duration);
        }

        void Play(float duration)
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(Routine(duration));
        }

        IEnumerator Routine(float duration)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
            routine = null;
        }
    }
}
