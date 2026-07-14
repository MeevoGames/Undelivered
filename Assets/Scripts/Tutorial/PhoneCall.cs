using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

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
        [SerializeField] private AudioSource audioSource;

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
        [Tooltip("Pause after a block finishes typing, before the next block.")]
        [SerializeField] private float blockHoldSeconds = 1.4f;
        [SerializeField] private float afterHangupPause = 0.4f;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>
        /// Rings, waits a beat, turns on the subtitle space, then types the message (two sentences per
        /// block) over the buzzing voice, and hangs up. Yield on this.
        /// </summary>
        public IEnumerator PlayCall(string[] sentences)
        {
            if (panel != null) panel.SetActive(false); // the text space turns on later, after the ring
            SetSubtitle(string.Empty);

            PlayOneShot(ringClip);                               // the phone rings
            yield return new WaitForSeconds(ringToTextSeconds);  // beat before the text space appears

            if (panel != null) panel.SetActive(true);            // the subtitle space turns on
            PlayOneShot(answerClip);                              // you answer
            StartVoice();                                         // the buzzing "voice" under the subtitles

            if (sentences != null)
            {
                int block = Mathf.Max(1, sentencesPerBlock);
                for (int i = 0; i < sentences.Length; i += block)
                {
                    yield return Typewriter(BuildBlock(sentences, i, block));
                    yield return new WaitForSeconds(blockHoldSeconds);
                }
            }

            StopVoice();
            SetSubtitle(string.Empty);
            PlayOneShot(hangupClip);                              // hangs up
            yield return new WaitForSeconds(afterHangupPause);
            if (panel != null) panel.SetActive(false);
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
