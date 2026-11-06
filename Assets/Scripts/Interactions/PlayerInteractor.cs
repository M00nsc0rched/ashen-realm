using System;
using AshenRealm.Input;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>
    /// Finds the interactable under the crosshair and activates it on the Interact input.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        /// <summary>Unity's Ignore Raycast layer: a rail stops the feet, not the word across it.</summary>
        public const int SeeThroughLayer = 2;

        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] Transform aimOrigin;
        [SerializeField] float reach = 3f;
        [SerializeField] float probeRadius = 0.2f;
        [SerializeField] LayerMask mask = ~0;

        IPlayerInput input;
        IInteractable current;

        public event Action<IInteractable> TargetChanged;

        public IInteractable Current => current;
        public string CurrentPrompt => current != null ? current.InteractionPrompt : string.Empty;

        void Awake() => input = inputSource;

        void OnDisable() => SetTarget(null);

        void Update()
        {
            SetTarget(FindTarget());

            if (input != null && input.ConsumeInteract() && current != null)
            {
                current.Interact(transform.root.gameObject);
                // Re-query so the prompt updates immediately (e.g. chest no longer interactable).
                SetTarget(FindTarget());
            }
        }

        IInteractable FindTarget()
        {
            if (aimOrigin == null) return null;
            int layers = mask.value & ~(1 << SeeThroughLayer);
            if (!Physics.SphereCast(aimOrigin.position, probeRadius, aimOrigin.forward, out RaycastHit hit, reach, layers, QueryTriggerInteraction.Collide))
                return null;
            if (hit.collider.transform.IsChildOf(transform.root)) return null;

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(transform.root.gameObject)) return null;
            return interactable;
        }

        void SetTarget(IInteractable target)
        {
            if (ReferenceEquals(target, current)) return;
            current = target;
            TargetChanged?.Invoke(current);
        }
    }
}
