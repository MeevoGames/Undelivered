using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// Code-driven combat animation shared by every combatant (enemies and the player), so they all move
    /// the same way. Four states:
    ///   • Idle  — a tiny squash & stretch, looping while still (suspended while attacking/hurt/dead).
    ///   • Attack — rise + wind up backward, then a fast forceful rotation forward (toward the opponent),
    ///              then return.
    ///   • Hurt  — tint red, snap backward + up and freeze a moment, then recompose smoothly.
    ///   • Death — rotate backward to 90° with a small hop back, a little floor bounce, then dissolve
    ///              (a CanvasGroup fade, so the sprite, health bar and numbers all fade together).
    ///
    /// "Forward" is toward the opponent; set <see cref="facingSign"/> to +1 if the opponent is to the
    /// right, -1 if to the left (enemies usually face the player on their left).
    /// </summary>
    public class CombatantAnimator : MonoBehaviour
    {
        private enum State { Idle, Throwing, Attacking, Hurt, Dead }

        [Tooltip("The visual to move/rotate/scale (usually the sprite). Defaults to this transform.")]
        [SerializeField] private RectTransform body;
        [Tooltip("Sprite tinted red when hurt.")]
        [SerializeField] private Graphic tintTarget;
        [Tooltip("Faded on death (put it on the whole combatant root so sprite + bars + numbers fade). Auto-added to this object if empty.")]
        [SerializeField] private CanvasGroup fadeGroup;
        [Tooltip("+1 if the opponent is to the right, -1 if to the left.")]
        [SerializeField] private float facingSign = -1f;

        [Header("Idle")]
        [SerializeField] private float idleAmplitude = 0.012f;
        [SerializeField] private float idleSpeed = 3f;

        [Header("Attack")]
        [SerializeField] private float attackRise = 25f;
        [SerializeField] private float attackLunge = 30f;
        [SerializeField] private float windupAngle = 18f;
        [SerializeField] private float strikeAngle = 22f;
        [SerializeField] private float windupTime = 0.18f;
        [SerializeField] private float strikeTime = 0.08f;
        [SerializeField] private float attackReturnTime = 0.15f;

        [Header("Throw (die wind-up: a small forward lean, grounded, no colour)")]
        [SerializeField] private float throwAngle = 10f;
        [SerializeField] private float throwSnapTime = 0.08f;
        [SerializeField] private float throwHoldTime = 0.1f;
        [SerializeField] private float throwReturnTime = 0.2f;

        [Header("Hurt")]
        [SerializeField] private Color hurtColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private float hurtAngle = 15f;
        [SerializeField] private float hurtSnapTime = 0.06f;
        [SerializeField] private float hurtHoldTime = 0.18f;
        [SerializeField] private float hurtReturnTime = 0.25f;

        [Header("Death")]
        [SerializeField] private float deathHopBack = 30f;
        [Tooltip("How high it jumps up during the fall before landing back on the floor.")]
        [SerializeField] private float deathHopArc = 20f;
        [SerializeField] private float deathFallTime = 0.35f;
        [SerializeField] private float deathBounceHeight = 15f;
        [SerializeField] private float deathBounceBack = 8f;
        [SerializeField] private float deathBounceTime = 0.18f;
        [SerializeField] private float deathRestTime = 0.6f;
        [SerializeField] private float deathFadeTime = 0.5f;

        private RectTransform _body;
        private Vector2 _baseAnchored;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private Color _baseColor;

        private State _state = State.Idle;
        private bool _dead;
        private Coroutine _pose;

        private void Awake()
        {
            _body = body != null ? body : transform as RectTransform;
            if (fadeGroup == null)
            {
                fadeGroup = GetComponent<CanvasGroup>();
                if (fadeGroup == null) fadeGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_body != null)
            {
                _baseAnchored = _body.anchoredPosition;
                _baseRotation = _body.localRotation;
                _baseScale = _body.localScale;
            }
            _baseColor = tintTarget != null ? tintTarget.color : Color.white;
        }

        private void Update()
        {
            if (_state != State.Idle || _body == null) return;
            float s = Mathf.Sin(Time.unscaledTime * idleSpeed) * idleAmplitude;
            float scaleX = 1f - s, scaleY = 1f + s;
            _body.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);
            _body.localRotation = _baseRotation;
            _body.anchoredPosition = _baseAnchored + GroundOffset(scaleX, scaleY, 0f); // squash from the floor up
        }

        public void PlayAttack()
        {
            if (_dead) return;
            StartPose(AttackRoutine());
        }

        /// <summary>A small forward lean (grounded, no colour) for throwing a die — like Hurt but forward.</summary>
        public void PlayThrow()
        {
            if (_dead) return;
            StartPose(ThrowRoutine());
        }

        public void PlayHurt()
        {
            if (_dead) return;
            StartPose(HurtRoutine());
        }

        public void PlayDeath()
        {
            if (_dead) return;
            _dead = true;
            if (_pose != null) StopCoroutine(_pose);
            SetTint(_baseColor);
            _pose = StartCoroutine(DeathRoutine());
        }

        /// <summary>Revives to the resting state for a new combat: not dead, idle, full opacity, base pose.</summary>
        public void ResetState()
        {
            if (_pose != null) { StopCoroutine(_pose); _pose = null; }
            _dead = false;
            _state = State.Idle;
            SetTint(_baseColor);
            if (fadeGroup != null) fadeGroup.alpha = 1f;
            ResetPose();
        }

        private void StartPose(IEnumerator routine)
        {
            if (_pose != null) StopCoroutine(_pose);
            SetTint(_baseColor); // clear any leftover hurt tint
            _pose = StartCoroutine(routine);
        }

        private IEnumerator AttackRoutine()
        {
            _state = State.Attacking;
            ResetPose();

            Vector2 up = Vector2.up * attackRise;
            Vector2 strikePos = up + new Vector2(facingSign * attackLunge, 0f);
            float back = facingSign * windupAngle;      // wind up away from the opponent
            float forward = -facingSign * strikeAngle;  // strike toward the opponent

            // 1-2. rise + wind up backward
            yield return Tween(windupTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetFreePose(Vector2.Lerp(Vector2.zero, up, e), Mathf.Lerp(0f, back, e));
            });

            // 3. fast, forceful rotation forward + lunge
            yield return Tween(strikeTime, k =>
            {
                float e = k * k; // accelerate
                SetFreePose(Vector2.Lerp(up, strikePos, e), Mathf.Lerp(back, forward, e));
            });

            // 4. return
            yield return Tween(attackReturnTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetFreePose(Vector2.Lerp(strikePos, Vector2.zero, e), Mathf.Lerp(forward, 0f, e));
            });

            ResetPose();
            _state = State.Idle;
        }

        private IEnumerator ThrowRoutine()
        {
            _state = State.Throwing;
            ResetPose();

            float forward = -facingSign * throwAngle; // lean toward the opponent, staying on the floor

            yield return Tween(throwSnapTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.zero, Mathf.Lerp(0f, forward, e));
            });
            SetGroundedPose(Vector2.zero, forward);

            yield return new WaitForSecondsRealtime(throwHoldTime);

            yield return Tween(throwReturnTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.zero, Mathf.Lerp(forward, 0f, e));
            });

            ResetPose();
            _state = State.Idle;
        }

        private IEnumerator HurtRoutine()
        {
            _state = State.Hurt;
            ResetPose();
            SetTint(hurtColor);

            float back = facingSign * hurtAngle;

            // snap: tilt backward, staying planted on the floor
            yield return Tween(hurtSnapTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.zero, Mathf.Lerp(0f, back, e));
            });
            SetGroundedPose(Vector2.zero, back);

            // freeze
            yield return new WaitForSecondsRealtime(hurtHoldTime);

            // recompose smoothly + fade the red back out
            yield return Tween(hurtReturnTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.zero, Mathf.Lerp(back, 0f, e));
                SetTint(Color.Lerp(hurtColor, _baseColor, e));
            });

            ResetPose();
            SetTint(_baseColor);
            _state = State.Idle;
        }

        private IEnumerator DeathRoutine()
        {
            _state = State.Dead;

            float angle = facingSign * 90f;          // fall over backward, flat on the floor
            float back = -facingSign * deathHopBack;  // short hop backward (away from the opponent)

            // 1-3. rise a little, arc backward, rotate to 90°, and land back at floor height. Grounded,
            //      so the rotation pivots on the feet instead of sinking below the floor.
            yield return Tween(deathFallTime, k =>
            {
                float jump = Mathf.Sin(k * Mathf.PI) * deathHopArc; // up, then back down to the floor
                SetGroundedPose(new Vector2(back * k, jump), angle * k);
            });

            // 4. little bounce on the floor: up a bit, slightly further back, settle.
            yield return Tween(deathBounceTime, k =>
            {
                float hop = Mathf.Sin(k * Mathf.PI) * deathBounceHeight;
                SetGroundedPose(new Vector2(back + (-facingSign * deathBounceBack * k), hop), angle);
            });
            SetGroundedPose(new Vector2(back + (-facingSign * deathBounceBack), 0f), angle);

            // rest, then dissolve (fades everything under the CanvasGroup)
            yield return new WaitForSecondsRealtime(deathRestTime);

            float startAlpha = fadeGroup != null ? fadeGroup.alpha : 1f;
            yield return Tween(deathFadeTime, k =>
            {
                if (fadeGroup != null) fadeGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
            });
            if (fadeGroup != null) fadeGroup.alpha = 0f;
        }

        private void SetFreePose(Vector2 positionOffset, float rotationZ)
        {
            if (_body == null) return;
            _body.anchoredPosition = _baseAnchored + positionOffset;
            _body.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, rotationZ);
            _body.localScale = _baseScale;
        }

        // Like SetFreePose but keeps the feet (bottom-centre) planted on the floor while it rotates.
        private void SetGroundedPose(Vector2 positionOffset, float rotationZ)
        {
            if (_body == null) return;
            _body.anchoredPosition = _baseAnchored + positionOffset + GroundOffset(1f, 1f, rotationZ);
            _body.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, rotationZ);
            _body.localScale = _baseScale;
        }

        private void ResetPose()
        {
            SetFreePose(Vector2.zero, 0f);
        }

        // Offset that keeps the body's bottom-centre point fixed under the given scale + rotation, so it
        // looks anchored to the floor instead of pivoting from its centre.
        private Vector2 GroundOffset(float scaleX, float scaleY, float rotationZ)
        {
            if (_body == null) return Vector2.zero;
            Vector2 pivot = _body.pivot;
            Rect rect = _body.rect;
            Vector2 foot = new Vector2((0.5f - pivot.x) * rect.width * _baseScale.x, -pivot.y * rect.height * _baseScale.y);

            Vector2 scaled = new Vector2(foot.x * scaleX, foot.y * scaleY);
            float r = rotationZ * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            Vector2 rotated = new Vector2(scaled.x * cos - scaled.y * sin, scaled.x * sin + scaled.y * cos);
            return foot - rotated;
        }

        private void SetTint(Color color)
        {
            if (tintTarget != null) tintTarget.color = color;
        }

        private IEnumerator Tween(float duration, System.Action<float> step)
        {
            if (duration <= 0f) { step(1f); yield break; }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }
    }
}
