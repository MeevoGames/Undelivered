using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// One die face: a sprite plus a signed value (positive, negative or 0). A face can also be
    /// <see cref="Empty"/> (nothing at all — not even 0). Faces are defined inline on a
    /// <see cref="DiceData"/>; a die always has six.
    /// </summary>
    [System.Serializable]
    public class DiceFace
    {
        [SerializeField] private Sprite sprite;

        [Tooltip("If true the face is blank: it has no value at all (not even 0).")]
        [SerializeField] private bool empty;

        [Tooltip("The face's value; can be negative or 0. Ignored when Empty.")]
        [SerializeField] private int value;

        public Sprite Sprite => sprite;
        public bool Empty => empty;

        /// <summary>The value this face contributes; always 0 when the face is empty.</summary>
        public int Value => empty ? 0 : value;
    }
}
