using System.Collections;
using UnityEngine;

namespace MirrorTrial.Feedback
{
    public class HitStopService : MonoBehaviour
    {
        static HitStopService instance;
        Coroutine routine;
        float defaultFixedDeltaTime;
        float resumeAtRealtime;
        float timeScaleBeforeHitStop = 1f;

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
            if (duration <= 0f) return;
            if (!instance)
            {
                var serviceObject = new GameObject("HitStopService");
                instance = serviceObject.AddComponent<HitStopService>();
            }
            instance.Play(duration);
        }

        void Play(float duration)
        {
            resumeAtRealtime = Mathf.Max(resumeAtRealtime, Time.realtimeSinceStartup + duration);
            if (routine == null) routine = StartCoroutine(Routine());
        }

        IEnumerator Routine()
        {
            timeScaleBeforeHitStop = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            while (Time.realtimeSinceStartup < resumeAtRealtime) yield return null;
            Time.timeScale = timeScaleBeforeHitStop;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(0f, timeScaleBeforeHitStop);
            resumeAtRealtime = 0f;
            routine = null;
        }

        void OnDisable()
        {
            if (instance != this) return;
            Time.timeScale = timeScaleBeforeHitStop;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(0f, timeScaleBeforeHitStop);
            instance = null;
        }
    }
}
