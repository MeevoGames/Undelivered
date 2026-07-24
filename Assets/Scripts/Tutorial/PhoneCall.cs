using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Undelivered.Tutorial
{
    /// <summary>
    /// Plays a scripted phone call: the phone rings, then (after a beat) the subtitle space turns on, a
    /// buzzing "voice" plays and the message is typed out letter by letter — two sentences per block —
    /// before it hangs up. Reused by the tutorial for both the intro call and the later "labeler" call.
    /// The subtitle panel starts hidden and only shows while a call is being read.
    /// </summary>
    public class PhoneCall : MonoBehaviour
    {
        [Tooltip("The subtitle space. Starts hidden; turns on a beat after the ring.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text subtitleText;
        [Tooltip("\"Clic para continuar\" text — shown after a block finishes; waits for a key/click. Starts hidden.")]
        [SerializeField] private GameObject continuePrompt;
        [SerializeField] private AudioSource audioSource;

        [Header("Position (for a phone parked off-screen, e.g. the night one)")]
        [Tooltip("Move this phone's own RectTransform to 'shownPosition' while a call plays, then back to where it started.")]
        [SerializeField] private bool moveWhileActive;
        [Tooltip("Anchored position the phone sits at while a call plays (used only with 'moveWhileActive').")]
        [SerializeField] private Vector2 shownPosition;
        [Tooltip("Slide speed (px/sec) of the bounce in from its parked spot.")]
        [SerializeField] private float bounceSpeed = 3500f;
        [Tooltip("How far past its resting spot it overshoots before settling.")]
        [SerializeField] private float bounceOvershoot = 40f;
        [SerializeField] private float bounceMinSegment = 0.06f;

        private RectTransform _rect;
        private Vector2 _startPos;

        [Header("Clips")]
        [Tooltip("Phone ringing.")]
        [SerializeField] private AudioClip ringClip;
        [Tooltip("Picking up the phone.")]
        [SerializeField] private AudioClip answerClip;
        [Tooltip("The buzzing 'voice' that plays (looped) under the subtitles.")]
        [SerializeField] private AudioClip voiceBuzzClip;
        [Tooltip("Hanging up.")]
        [SerializeField] private AudioClip hangupClip;

        [Header("Timing (seconds)")]
        [Tooltip("Pause from the ring until the subtitle space turns on and the voice starts.")]
        [SerializeField] private float ringToTextSeconds = 1f;
        [Tooltip("Typewriter speed (characters per second) — medium-fast.")]
        [SerializeField] private float charsPerSecond = 30f;
        [Tooltip("How many sentences are typed together per subtitle block.")]
        [SerializeField] private int sentencesPerBlock = 2;
        [Tooltip("Delay after a block is complete before a click can advance it, so a fast double click can't fill AND skip.")]
        [SerializeField] private float continueDelaySeconds = 0.35f;
        [SerializeField] private float afterHangupPause = 0.4f;

        private void Awake()
        {
            _rect = transform as RectTransform;
            if (_rect != null) _startPos = _rect.anchoredPosition;
            if (panel != null) panel.SetActive(false);
            if (continuePrompt != null) continuePrompt.SetActive(false);
        }

        /// <summary>
        /// Rings, waits a beat, turns on the subtitle space, then types the message (two sentences per
        /// block) over the buzzing voice, and hangs up. Yield on this.
        /// </summary>
        public IEnumerator PlayCall(string[] sentences)
        {
            if (panel != null) panel.SetActive(false);  // the text space turns on later, after the ring
            SetSubtitle(string.Empty);
            yield return ShowRoot();                    // bounces in from the right, over the other layers

            PlayOneShot(ringClip);                               // the phone rings
            yield return new WaitForSeconds(ringToTextSeconds);  // beat before the text space appears

            if (panel != null) panel.SetActive(true); // the subtitle space turns on
            PlayOneShot(answerClip);                  // you answer

            if (sentences != null)
            {
                int block = Mathf.Max(1, sentencesPerBlock);
                for (int i = 0; i < sentences.Length; i += block)
                {
                    StartVoice();                                // the buzzing "voice" plays while typing this block
                    yield return Typewriter(BuildBlock(sentences, i, block)); // a click fills the rest at once
                    StopVoice();                                 // nothing plays while the player reads it

                    // The block stays on screen (not erased). The prompt shows as soon as the content is
                    // complete, but the advancing click is only accepted after a short delay — so the
                    // same double click that filled the text can't also skip it.
                    if (continuePrompt != null) continuePrompt.SetActive(true);
                    yield return new WaitForSeconds(continueDelaySeconds);
                    yield return new WaitUntil(ContinuePressed);
                    yield return null; // let the advancing click's frame pass, so it can't also fill the next block
                    if (continuePrompt != null) continuePrompt.SetActive(false);
                }
            }

            SetSubtitle(string.Empty);
            PlayOneShot(hangupClip);                              // hangs up
            yield return new WaitForSeconds(afterHangupPause);
            if (panel != null) panel.SetActive(false);
            yield return HideRoot();                              // slides back out to its parked spot
        }

        /// <summary>Aborts a call in progress: stops the voice and hides the panel (used by "jumptuto").</summary>
        public void Cancel()
        {
            StopVoice();
            if (continuePrompt != null) continuePrompt.SetActive(false);
            SetSubtitle(string.Empty);
            if (panel != null) panel.SetActive(false);
            SnapRootHidden();
        }

        // Brings the phone over the other layers and bounces it in from where it's parked (off-screen right).
        private IEnumerator ShowRoot()
        {
            transform.SetAsLastSibling();
            if (!moveWhileActive || _rect == null) yield break;

            _rect.anchoredPosition = _startPos;
            yield return MoveRootTo(shownPosition + new Vector2(-bounceOvershoot, 0f)); // overshoot past it
            yield return MoveRootTo(shownPosition);                                     // then settle
        }

        // Slides the phone back out to where it started.
        private IEnumerator HideRoot()
        {
            if (!moveWhileActive || _rect == null) yield break;
            yield return MoveRootTo(_startPos);
        }

        // Snaps it back with no animation (used when a call is aborted).
        private void SnapRootHidden()
        {
            if (moveWhileActive && _rect != null) _rect.anchoredPosition = _startPos;
        }

        private IEnumerator MoveRootTo(Vector2 target)
        {
            Vector2 start = _rect.anchoredPosition;
            float distance = Vector2.Distance(target, start);
            float duration = Mathf.Max(bounceMinSegment, distance / Mathf.Max(1f, bounceSpeed));

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _rect.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _rect.anchoredPosition = target;
        }

        // True on the frame the player presses any key or mouse button.
        private static bool ContinuePressed()
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            return (k != null && k.anyKey.wasPressedThisFrame)
                || (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
        }

        // Reveals the block character by character via TMP's visible-character count.
        private IEnumerator Typewriter(string text)
        {
            if (subtitleText == null) yield break;

            subtitleText.text = text;
            subtitleText.ForceMeshUpdate();
            int total = subtitleText.textInfo.characterCount;
            subtitleText.maxVisibleCharacters = 0;

            float shown = 0f;
            while (shown < total)
            {
                if (ContinuePressed()) break; // a click while typing fills the rest of the block at once
                shown += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
                subtitleText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            subtitleText.maxVisibleCharacters = total;
        }

        // Joins up to 'count' sentences (period-then-next) starting at 'start' into one block.
        private static string BuildBlock(string[] sentences, int start, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < start + count && i < sentences.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sentences[i])) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(sentences[i].Trim());
            }
            return sb.ToString();
        }

        private void SetSubtitle(string text)
        {
            if (subtitleText != null)
            {
                subtitleText.maxVisibleCharacters = int.MaxValue;
                subtitleText.text = text;
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void StartVoice()
        {
            if (audioSource == null || voiceBuzzClip == null) return;
            audioSource.clip = voiceBuzzClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        private void StopVoice()
        {
            if (audioSource == null) return;
            audioSource.loop = false;
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}
