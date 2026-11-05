using System;
using System.Collections;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Player;
using AshenRealm.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.World
{
    /// <summary>
    /// Moves the player between zones behind a fade: activates the target zone, switches sun, fog and ambient, moves the
    /// respawn point and shows the zone's title card. Underground there is no sun; the player's torch is the light.
    /// </summary>
    public sealed class ZoneManager : MonoBehaviour
    {
        const float FadeTime = 0.45f;
        const float TitleTime = 3.2f;

        [SerializeField] Zone[] zones;
        [SerializeField] FirstPersonController player;
        [SerializeField] PlayerLifecycle lifecycle;
        [SerializeField] AtmosphereController atmosphere;
        [SerializeField] Light sun;
        [SerializeField] Light torch;
        [SerializeField] Camera worldCamera;
        [SerializeField] CompassStrip compass;
        [SerializeField] CanvasGroup fade;
        [SerializeField] CanvasGroup titleGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;

        bool travelling;
        float titleTimer;

        public Zone Current { get; private set; }
        public int CurrentIndex => Array.IndexOf(zones, Current);
        public bool Travelling => travelling;
        public int ZoneCount => zones.Length;

        /// <summary>Raised after the player arrives in a zone.</summary>
        public event Action<Zone> Arrived;

        void Awake()
        {
            fade.alpha = 0f;
            titleGroup.alpha = 0f;
            for (int i = 0; i < zones.Length; i++) zones[i].gameObject.SetActive(i == 0);
            Enter(zones[0]);
        }

        void Update()
        {
            titleTimer -= Time.unscaledDeltaTime;
            float target = titleTimer > 0f ? 1f : 0f;
            titleGroup.alpha = Mathf.MoveTowards(titleGroup.alpha, target, Time.unscaledDeltaTime * (target > 0f ? 2f : 0.8f));
        }

        /// <summary>Shows the current zone's title card (first arrival, after the class is chosen).</summary>
        public void ShowTitle()
        {
            titleText.text = Current.Title;
            subtitleText.text = Current.Subtitle;
            titleTimer = TitleTime;
        }

        public void Travel(int index, bool arriveAtExit)
        {
            if (travelling || index < 0 || index >= zones.Length) return;
            StartCoroutine(TravelRoutine(zones[index], arriveAtExit));
        }

        IEnumerator TravelRoutine(Zone target, bool arriveAtExit)
        {
            travelling = true;
            player.InputEnabled = false;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeTime)
            {
                fade.alpha = t;
                yield return null;
            }
            fade.alpha = 1f;

            Current.gameObject.SetActive(false);
            target.gameObject.SetActive(true);
            Enter(target);

            Transform arrival = arriveAtExit ? target.Exit : target.Entry;
            player.Teleport(arrival.position, arrival.eulerAngles.y);
            Physics.SyncTransforms();
            ShowTitle();

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeTime)
            {
                fade.alpha = 1f - t;
                yield return null;
            }
            fade.alpha = 0f;
            player.InputEnabled = true;
            travelling = false;
            Arrived?.Invoke(target);
        }

        void Enter(Zone zone)
        {
            Current = zone;
            lifecycle.SetSpawnPoint(zone.Entry);
            atmosphere.Apply(zone.Ambient, zone.Fog, zone.FogStart, zone.FogEnd);
            sun.enabled = !zone.Underground;
            torch.enabled = zone.Underground;
            worldCamera.clearFlags = zone.Underground ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            worldCamera.backgroundColor = zone.Fog;
            RefreshObjective();
        }

        public void RefreshObjective()
        {
            if (Current.Objective != null) compass.SetObjective(Current.Objective.position);
        }
    }
}
