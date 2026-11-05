using System;
using System.Collections;
using AshenRealm.Combat;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Handles player death, respawn and the out-of-world safety net.
    /// </summary>
    public sealed class PlayerLifecycle : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Stamina stamina;
        [SerializeField] FirstPersonController controller;
        [SerializeField] Behaviour[] disableWhileDead;
        [SerializeField] Transform spawnPoint;
        [SerializeField] float respawnDelay = 4f;
        [Tooltip("Falling below this height teleports the player back to the spawn point.")]
        [SerializeField] float killPlaneY = -40f;

        public event Action Died;
        public event Action Respawned;

        public bool IsDead { get; private set; }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        void Update()
        {
            if (transform.position.y < killPlaneY) ReturnToSpawn();
        }

        void OnDied(Health _)
        {
            if (IsDead) return;
            IsDead = true;
            controller.InputEnabled = false;
            foreach (var b in disableWhileDead)
                if (b != null) b.enabled = false;
            Died?.Invoke();
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            ReturnToSpawn();
            health.ResetHealth();
            if (stamina != null) stamina.Refill();
            if (TryGetComponent(out StatusEffects status)) status.Clear();
            foreach (var b in disableWhileDead)
                if (b != null) b.enabled = true;
            controller.InputEnabled = true;
            IsDead = false;
            Respawned?.Invoke();
        }

        public void SetSpawnPoint(Transform point) => spawnPoint = point;

        void ReturnToSpawn()
        {
            if (spawnPoint != null) controller.Teleport(spawnPoint.position, spawnPoint.eulerAngles.y);
        }
    }
}
