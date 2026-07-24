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

        [Header("Attack — dash (die 4-6): accelerate forward, shake at the far point, return")]
        [SerializeField] private float dashDistance = 90f;
        [SerializeField] private float dashAngle = 14f;
        [SerializeField] private float dashTime = 0.13f;
        [SerializeField] private float dashShakeTime = 0.12f;
        [SerializeField] private float dashShakeMagnitude = 7f;
        [SerializeField] private float dashReturnTime = 0.2f;

        [Header("Attack — close in (die 7-9): move beside the target, heavier strike, return")]
        [Tooltip("Gap left between the attacker and its target when it closes in.")]
        [SerializeField] private float approachGap = 120f;
        [Tooltip("Distance used when no target was given.")]
        [SerializeField] private float approachFallback = 160f;
        [SerializeField] private float approachTime = 0.18f;
        [Tooltip("How much heavier the strike is than the plain one.")]
        [SerializeField] private float heavyMultiplier = 1.5f;
        [SerializeField] private float heavyShakeTime = 0.16f;
        [SerializeField] private float heavyShakeMagnitude = 11f;
        [SerializeField] private float approachReturnTime = 0.28f;

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

        [Header("Hurt — pushed back (die 4-6): shoved back, front legs up, then recompose")]
        [SerializeField] private float pushBackDistance = 45f;
        [Tooltip("How far it rears up, as if about to fall over backward.")]
        [SerializeField] private float pushRearAngle = 26f;
        [SerializeField] private float pushSnapTime = 0.09f;
        [SerializeField] private float pushHoldTime = 0.16f;
        [Tooltip("The rotation is recomposed FIRST, so it never drags along the floor while tilted.")]
        [SerializeField] private float pushUprightTime = 0.16f;
        [Tooltip("Only then does it walk forward to where it stood.")]
        [SerializeField] private float pushWalkBackTime = 0.22f;

        [Header("Hurt — stunned (die 7-9): staggers back, head down, slow recovery")]
        [SerializeField] private float stunBackDistance = 30f;
        [Tooltip("How far the head dips forward, dazed.")]
        [SerializeField] private float stunHeadAngle = 20f;
        [SerializeField] private float stunSnapTime = 0.1f;
        [SerializeField] private float stunHoldTime = 0.35f;
        [Tooltip("Slower than the other recoveries — it is shaking off the daze.")]
        [SerializeField] private float stunReturnTime = 0.55f;

        [Header("Hit FX — a decorative mark on whoever takes the hit, by die tier")]
        [Tooltip("Impact lines (impact.png) — a light hit, die 1-3, at a random point on the sprite.")]
        [SerializeField] private Sprite impactSprite;
        [Tooltip("Knock mark (knock.png) — a medium hit, die 4-6, over the head.")]
        [SerializeField] private Sprite knockSprite;
        [Tooltip("Accent mark (accent.png, the red X) — a heavy hit, die 7-9, at a random point on the sprite.")]
        [SerializeField] private Sprite accentSprite;
        [SerializeField] private float fxSize = 120f;
        [SerializeField] private float fxLifeSeconds = 0.35f;
        [Tooltip("How far above the top of the sprite the knock mark sits.")]
        [SerializeField] private float knockHeadOffset = 20f;
        [SerializeField] private Color fxColor = Color.white;

        [Header("Slash trail — afterimages behind a dashing attack (die 4-6)")]
        [Tooltip("Image the afterimages copy. Defaults to the tint target when it is an Image.")]
        [SerializeField] private Image trailSource;
        [Tooltip("Seconds between afterimages.")]
        [SerializeField] private float trailInterval = 0.03f;
        [Tooltip("How long each afterimage takes to fade out.")]
        [SerializeField] private float trailFadeSeconds = 0.22f;
        [SerializeField, Range(0f, 1f)] private float trailStartAlpha = 0.45f;
        [SerializeField] private Color trailColor = Color.white;

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

        /// <summary>
        /// Attacks with the weight of the die that was thrown (its raw face, whatever the final damage):
        /// 0-3 the plain strike, 4-6 a dashing lunge, 7-9 closing in on <paramref name="target"/> first.
        /// </summary>
        public void PlayAttack(int dieValue = 0, RectTransform target = null)
        {
            if (_dead) return;
            switch (Tier(dieValue))
            {
                case 2: StartPose(ApproachAttackRoutine(target)); break;
                case 1: StartPose(DashAttackRoutine()); break;
                default: StartPose(AttackRoutine()); break;
            }
        }

        // 0-3 → the plain animation, 4-6 → the stronger one, 7+ → the heaviest.
        private static int Tier(int dieValue) => dieValue >= 7 ? 2 : dieValue >= 4 ? 1 : 0;

        /// <summary>A small forward lean (grounded, no colour) for throwing a die — like Hurt but forward.</summary>
        public void PlayThrow()
        {
            if (_dead) return;
            StartPose(ThrowRoutine());
        }

        /// <summary>
        /// Reacts with the weight of the die that dealt the hit (its raw face): 1-3 the plain flinch,
        /// 4-6 being shoved back onto its hind legs, 7-9 a dazed stagger with a slow recovery.
        /// </summary>
        public void PlayHurt(int dieValue = 0)
        {
            if (_dead) return;
            switch (Tier(dieValue))
            {
                case 2: StartPose(StunnedRoutine()); break;
                case 1: StartPose(PushedRoutine()); break;
                default: StartPose(HurtRoutine()); break;
            }
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

        // Die 4-6: accelerate forward into a lunge, shake hard at the far point, then slide back.
        private IEnumerator DashAttackRoutine()
        {
            _state = State.Attacking;
            ResetPose();

            Vector2 far = new Vector2(facingSign * dashDistance, 0f);
            float lean = -facingSign * dashAngle; // leaning into the charge

            StartCoroutine(TrailRoutine(dashTime + dashShakeTime)); // a slash trail follows the charge

            yield return Tween(dashTime, k =>
            {
                float e = k * k; // accelerate
                SetGroundedPose(Vector2.Lerp(Vector2.zero, far, e), Mathf.Lerp(0f, lean, e));
            });

            yield return ShakeAt(far, lean, dashShakeTime, dashShakeMagnitude); // impact

            yield return Tween(dashReturnTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(far, Vector2.zero, e), Mathf.Lerp(lean, 0f, e));
            });

            ResetPose();
            _state = State.Idle;
        }

        // Die 7-9: rush over next to the target, land a heavier version of the plain strike, then go back.
        private IEnumerator ApproachAttackRoutine(RectTransform target)
        {
            _state = State.Attacking;
            ResetPose();

            Vector2 near = ApproachOffset(target);
            float back = facingSign * windupAngle * heavyMultiplier;     // wind up away from the target
            float forward = -facingSign * strikeAngle * heavyMultiplier; // and swing through it

            // 1. close the distance fast
            yield return Tween(approachTime, k => SetGroundedPose(Vector2.Lerp(Vector2.zero, near, k * k), 0f));

            // 2. heavier strike, in place beside the target
            yield return Tween(windupTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(near, Mathf.Lerp(0f, back, e));
            });
            yield return Tween(strikeTime, k =>
            {
                float e = k * k;
                SetGroundedPose(near, Mathf.Lerp(back, forward, e));
            });
            yield return ShakeAt(near, forward, heavyShakeTime, heavyShakeMagnitude);

            // 3. back to its own spot
            yield return Tween(approachReturnTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(near, Vector2.zero, e), Mathf.Lerp(forward, 0f, e));
            });

            ResetPose();
            _state = State.Idle;
        }

        // Die 4-6: shoved backward onto its hind legs, then recomposed — rotation first, THEN the walk
        // forward, so it never drags along the floor while still tilted.
        private IEnumerator PushedRoutine()
        {
            _state = State.Hurt;
            ResetPose();
            SetTint(hurtColor);

            SpawnFx(knockSprite, HeadPoint()); // a knock mark pops over its head

            Vector2 back = new Vector2(facingSign * pushBackDistance, 0f); // away from the attacker
            float rear = facingSign * pushRearAngle;                        // front legs come off the floor

            yield return Tween(pushSnapTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(Vector2.zero, back, e), Mathf.Lerp(0f, rear, e));
            });
            SetGroundedPose(back, rear);
            yield return new WaitForSecondsRealtime(pushHoldTime);

            // put the legs back down first...
            yield return Tween(pushUprightTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(back, Mathf.Lerp(rear, 0f, e));
                SetTint(Color.Lerp(hurtColor, _baseColor, e));
            });

            // ...and only then walk back to where it stood
            yield return Tween(pushWalkBackTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(back, Vector2.zero, e), 0f);
            });

            ResetPose();
            SetTint(_baseColor);
            _state = State.Idle;
        }

        // Die 7-9: staggers back with its head hanging, dazed, and takes its time coming back.
        private IEnumerator StunnedRoutine()
        {
            _state = State.Hurt;
            ResetPose();
            SetTint(hurtColor);

            SpawnFx(accentSprite, RandomPointInBody()); // the red X marks the heavy hit

            Vector2 back = new Vector2(facingSign * stunBackDistance, 0f);
            float headDown = -facingSign * stunHeadAngle; // head dips forward

            yield return Tween(stunSnapTime, k =>
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(Vector2.zero, back, e), Mathf.Lerp(0f, headDown, e));
            });
            SetGroundedPose(back, headDown);

            yield return new WaitForSecondsRealtime(stunHoldTime); // dazed

            yield return Tween(stunReturnTime, k => // slow recovery
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                SetGroundedPose(Vector2.Lerp(back, Vector2.zero, e), Mathf.Lerp(headDown, 0f, e));
                SetTint(Color.Lerp(hurtColor, _baseColor, e));
            });

            ResetPose();
            SetTint(_baseColor);
            _state = State.Idle;
        }

        // Jitters around a held pose, damping out — the impact of a heavy blow.
        private IEnumerator ShakeAt(Vector2 position, float rotationZ, float duration, float magnitude)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float damp = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                SetGroundedPose(position + UnityEngine.Random.insideUnitCircle * (magnitude * damp), rotationZ);
                yield return null;
            }
            SetGroundedPose(position, rotationZ);
        }

        // How far to travel to end up beside the target (stopping short of it), along the facing axis.
        private Vector2 ApproachOffset(RectTransform target)
        {
            if (_body == null) return Vector2.zero;
            if (target == null) return new Vector2(facingSign * approachFallback, 0f);

            Vector3 worldDelta = target.position - _body.position;
            RectTransform parent = _body.parent as RectTransform;
            Vector2 local = parent != null ? (Vector2)parent.InverseTransformVector(worldDelta) : (Vector2)worldDelta;

            float distance = Mathf.Max(0f, Mathf.Abs(local.x) - approachGap);
            return new Vector2(facingSign * distance, 0f); // stay on our own floor line
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
            SpawnFx(impactSprite, RandomPointInBody()); // impact lines where the blow landed

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

        // ----- decorative FX -----

        // Drops a short-lived sprite at a point given in the body's local space, then fades it out.
        private void SpawnFx(Sprite sprite, Vector2 bodyLocalPoint)
        {
            if (sprite == null || _body == null) return;
            RectTransform parent = _body.parent as RectTransform;
            if (parent == null) return;

            var go = new GameObject("CombatFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.SetAsLastSibling();                 // over the combatant
            rect.sizeDelta = new Vector2(fxSize, fxSize);
            rect.position = _body.TransformPoint(bodyLocalPoint);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = fxColor;
            image.raycastTarget = false;

            StartCoroutine(FadeAndDestroy(go, image, fxLifeSeconds));
        }

        // A random spot inside the sprite — where the blow "landed".
        private Vector2 RandomPointInBody()
        {
            Rect r = _body != null ? _body.rect : new Rect();
            return new Vector2(UnityEngine.Random.Range(r.xMin, r.xMax), UnityEngine.Random.Range(r.yMin, r.yMax));
        }

        // Just above the top of the sprite.
        private Vector2 HeadPoint()
        {
            Rect r = _body != null ? _body.rect : new Rect();
            return new Vector2(r.center.x, r.yMax + knockHeadOffset);
        }

        // Leaves fading copies of the sprite behind while the body charges forward.
        private IEnumerator TrailRoutine(float duration)
        {
            Image source = trailSource != null ? trailSource : tintTarget as Image;
            RectTransform parent = _body != null ? _body.parent as RectTransform : null;
            if (source == null || parent == null) yield break;

            for (float t = 0f; t < duration; t += trailInterval)
            {
                SpawnAfterimage(source, parent);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, trailInterval));
            }
        }

        private void SpawnAfterimage(Image source, RectTransform parent)
        {
            var go = new GameObject("SlashTrail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(_body.GetSiblingIndex()); // slots in behind the body
            rect.position = _body.position;
            rect.rotation = _body.rotation;
            rect.localScale = _body.localScale;
            rect.sizeDelta = _body.rect.size;

            var image = go.GetComponent<Image>();
            image.sprite = source.sprite;
            image.preserveAspect = source.preserveAspect;
            image.raycastTarget = false;
            Color color = trailColor;
            color.a = trailStartAlpha;
            image.color = color;

            StartCoroutine(FadeAndDestroy(go, image, trailFadeSeconds));
        }

        private static IEnumerator FadeAndDestroy(GameObject go, Image image, float duration)
        {
            float startAlpha = image != null ? image.color.a : 1f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (image == null) yield break;
                Color color = image.color;
                color.a = Mathf.Lerp(startAlpha, 0f, t / Mathf.Max(0.01f, duration));
                image.color = color;
                yield return null;
            }
            if (go != null) Destroy(go);
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
