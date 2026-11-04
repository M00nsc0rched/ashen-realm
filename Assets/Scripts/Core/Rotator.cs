using UnityEngine;

namespace AshenRealm.Core
{
    /// <summary>Constant local rotation (windmill sails and similar ambient motion).</summary>
    public sealed class Rotator : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 0f, -14f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
