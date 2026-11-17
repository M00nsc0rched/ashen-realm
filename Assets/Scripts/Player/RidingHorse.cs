using System.Linq;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// The one horse a soul can own, bought from Patkós Hilda. It lives only in the valley: Use mounts it, and in the saddle the
    /// same object rides under the player, so the rider sees its real neck and ears and its real shadow runs beside them.
    /// Nothing about it is saved but the owner's flag: whenever the player arrives in the valley, by a door, a load, a respawn or
    /// a fall, it waits at the horse post nearest to them; within one stay it stays where it was left.
    /// </summary>
    public sealed class RidingHorse : MonoBehaviour, IInteractable
    {
        public const string OwnedFlag = "horse_owned";
        const float StillSpeed = 1f;

        [SerializeField] FirstPersonController rider;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Health health;
        [SerializeField] PlayerLifecycle lifecycle;
        [SerializeField] ZoneManager zones;
        [Tooltip("Everything drawn: hidden until the horse is bought.")]
        [SerializeField] GameObject model;
        [SerializeField] BoxCollider body;
        [Tooltip("The Standing Spot of every horse post, facing the post.")]
        [SerializeField] Transform[] posts;
        [Tooltip("The stable's hitching rail, where a bought horse first stands.")]
        [SerializeField] Transform rail;
        [SerializeField] FirstPersonController.Saddle saddle = new FirstPersonController.Saddle
        {
            Canter = 7f, Gallop = 12.5f, GallopStaminaPerSecond = 8f, TurnDegreesPerSecond = 110f,
            EyeHeight = 2.45f, Height = 2.6f, Radius = 0.5f, StepOffset = 0.45f, HeadReach = 1.75f, TailReach = 1.15f, StepScale = 0.4f,
        };

        bool shown;
        Transform home;

        public bool Ridden { get; private set; }
        /// <summary>The rider while ridden, else null: the animator reads the gait from them.</summary>
        public FirstPersonController Rider => Ridden ? rider : null;
        static bool Owned => GameSession.Instance != null && GameSession.Instance.HasFlag(OwnedFlag);

        public string InteractionPrompt => Ridden ? GameText.Dismount : GameText.Mount;

        public bool CanInteract(GameObject interactor) =>
            Ridden ? Flat(rider.Velocity).magnitude < StillSpeed : shown && !zones.Travelling;

        public void Interact(GameObject interactor)
        {
            if (!Ridden) Mount();
            else if (!TryDismount()) GameMessages.Notify(GameText.NoRoomToDismount);
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            lifecycle.ReturnedToSpawn += OnReturnedToSpawn;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            lifecycle.ReturnedToSpawn -= OnReturnedToSpawn;
        }

        void Update()
        {
            // Bought just now: Hilda saddles it at the rail.
            bool owned = Owned;
            if (owned == shown) return;
            Show(owned);
            if (owned && !Ridden) StandAt(rail);
        }

        /// <summary>The player has just arrived in the valley: a horse not ridden waits at the post nearest to them.</summary>
        public void ArriveInValley()
        {
            Release();
            Show(Owned);
            if (!shown) return;
            Vector3 at = rider.transform.position;
            StandAt(posts.OrderBy(p => Flat(p.position - at).sqrMagnitude).First());
        }

        /// <summary>Off the horse where it stands, before a door or a load moves the player.</summary>
        public void Release()
        {
            if (Ridden) Leave(rider.transform.position);
        }

        void Mount()
        {
            float yaw = transform.eulerAngles.y;
            home = transform.parent;
            body.enabled = false;
            rider.Teleport(transform.position + Vector3.up * 0.05f, yaw, rider.Pitch);
            rider.Mount(saddle);
            Ridden = true;
            transform.SetParent(rider.transform, false);
            // The controller floats its capsule a skin's width above the ground; the hooves belong on it.
            transform.localPosition = Vector3.down * rider.GetComponent<CharacterController>().skinWidth;
            transform.localRotation = Quaternion.identity;
            combat.SetArmed(false);
        }

        /// <summary>Down on the left, else the right, else behind; false when none of them has room for a person.</summary>
        bool TryDismount()
        {
            Transform seat = rider.transform;
            foreach (Vector3 side in new[] { Vector3.left * 1.2f, Vector3.right * 1.2f, Vector3.back * 1.7f })
            {
                Vector3 p = seat.TransformPoint(side);
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < 0.7f || Mathf.Abs(ground.point.y - seat.position.y) > 0.6f) continue;
                Vector3 feet = ground.point + Vector3.up * 0.05f;
                if (Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.IsChildOf(seat))) continue;
                Leave(feet);
                return true;
            }
            return false;
        }

        void Leave(Vector3 feet)
        {
            Vector3 at = transform.position;
            float yaw = rider.transform.eulerAngles.y;
            transform.SetParent(home, true);
            transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Ridden = false;
            rider.Dismount();
            rider.Teleport(feet, yaw, rider.Pitch);
            body.enabled = shown;
            combat.SetArmed(!zones.WeaponsAway(zones.Current));
        }

        void OnDamaged(DamageInfo info, float amount)
        {
            if (!Ridden || amount <= 0f) return;
            if (!TryDismount()) Leave(rider.transform.position);
            GameMessages.Notify(GameText.ThrownFromSaddle);
        }

        void OnReturnedToSpawn()
        {
            if (zones.CurrentIndex == ZoneNumbers.Valley) ArriveInValley();
        }

        void Show(bool owned)
        {
            shown = owned;
            model.SetActive(owned);
            body.enabled = owned && !Ridden;
        }

        void StandAt(Transform spot) => transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, spot.eulerAngles.y, 0f));

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
