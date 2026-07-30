using System.Collections;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    public enum StorySequenceTriggerMode
    {
        Manual,
        SceneStart,
        PlayerEnter,
        EncounterCleared,
        MirrorSmashed,
        MirrorCompleted,
        PreviousSequenceCompleted
    }

    /// <summary>
    /// Connects a narrative sequence to level events without requiring code changes.
    /// </summary>
    public sealed class StorySequenceTrigger : MonoBehaviour
    {
        [SerializeField] StoryTutorialSequence sequence;
        [SerializeField] StorySequenceTriggerMode triggerMode = StorySequenceTriggerMode.Manual;
        [SerializeField] bool enabledAtStart = true;
        [SerializeField] bool oneShot = true;
        [SerializeField, Min(0f)] float delay;
        [SerializeField] CombatEncounter encounter;
        [SerializeField] MirrorGate mirrorGate;
        [SerializeField] StoryTutorialSequence previousSequence;
        [SerializeField] StoryTutorialSequence nextSequence;
        [SerializeField, Min(0f)] float nextSequenceDelay;

        bool fired;
        bool subscribed;

        public StoryTutorialSequence Sequence => sequence;
        public StorySequenceTriggerMode TriggerMode => triggerMode;
        public StoryTutorialSequence NextSequence => nextSequence;

        void Awake()
        {
            if (triggerMode == StorySequenceTriggerMode.PlayerEnter)
            {
                var collider = GetComponent<Collider2D>();
                if (collider) collider.isTrigger = true;
            }
        }

        void OnEnable()
        {
            Subscribe();
        }

        IEnumerator Start()
        {
            yield return null;
            if (triggerMode == StorySequenceTriggerMode.SceneStart)
                Fire();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (triggerMode != StorySequenceTriggerMode.PlayerEnter) return;
            if (other.CompareTag("Player") || other.GetComponent<PlayerInputReader>())
                Fire();
        }

        public void Fire()
        {
            if (!enabledAtStart || (oneShot && fired) || !sequence) return;
            fired = true;
            if (delay > 0f) StartCoroutine(PlayAfter(delay));
            else sequence.Play();
        }

        public void ResetTrigger()
        {
            fired = false;
        }

        IEnumerator PlayAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (sequence) sequence.Play();
        }

        IEnumerator PlayNextAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (nextSequence) nextSequence.Play();
        }

        void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            if (encounter) encounter.OnCleared += OnEncounterCleared;
            if (mirrorGate)
            {
                mirrorGate.OnSmashed += OnMirrorSmashed;
                mirrorGate.OnCompleted += OnMirrorCompleted;
            }
            if (previousSequence) previousSequence.SequenceCompleted += OnPreviousSequenceCompleted;
            if (sequence) sequence.SequenceCompleted += OnSequenceCompleted;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;

            if (encounter) encounter.OnCleared -= OnEncounterCleared;
            if (mirrorGate)
            {
                mirrorGate.OnSmashed -= OnMirrorSmashed;
                mirrorGate.OnCompleted -= OnMirrorCompleted;
            }
            if (previousSequence) previousSequence.SequenceCompleted -= OnPreviousSequenceCompleted;
            if (sequence) sequence.SequenceCompleted -= OnSequenceCompleted;
        }

        void OnEncounterCleared(CombatEncounter value)
        {
            if (triggerMode == StorySequenceTriggerMode.EncounterCleared) Fire();
        }

        void OnMirrorSmashed(MirrorGate value)
        {
            if (triggerMode == StorySequenceTriggerMode.MirrorSmashed) Fire();
        }

        void OnMirrorCompleted(MirrorGate value)
        {
            if (triggerMode == StorySequenceTriggerMode.MirrorCompleted) Fire();
        }

        void OnPreviousSequenceCompleted()
        {
            if (triggerMode == StorySequenceTriggerMode.PreviousSequenceCompleted) Fire();
        }

        void OnSequenceCompleted()
        {
            if (nextSequence) StartCoroutine(PlayNextAfter(nextSequenceDelay));
        }

        void OnDrawGizmos()
        {
            if (triggerMode != StorySequenceTriggerMode.PlayerEnter) return;
            var collider = GetComponent<BoxCollider2D>();
            if (!collider) return;
            Gizmos.color = new Color(0.3f, 0.95f, 1f, 0.16f);
            Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
            Gizmos.color = new Color(0.3f, 0.95f, 1f, 0.9f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
