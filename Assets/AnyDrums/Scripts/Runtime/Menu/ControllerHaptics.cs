using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Vibración corta en el mando que realizó la acción (el que tiene el gatillo presionado).
    /// Con seguimiento de manos no hace nada.
    /// </summary>
    public class ControllerHaptics : MonoBehaviour
    {
        const float TriggerPressed = 0.35f;

        static ControllerHaptics _instance;

        float _leftUntil, _rightUntil;
        bool _leftActive, _rightActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public static void PulsePressingController(float amplitude, float duration = 0.05f)
        {
            if (amplitude <= 0f) return;
            bool left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > TriggerPressed;
            bool right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > TriggerPressed;
            if (!left && !right) return;

            if (_instance == null)
            {
                var go = new GameObject("[ControllerHaptics]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ControllerHaptics>();
            }
            if (left) _instance.Pulse(OVRInput.Controller.LTouch, amplitude, duration);
            if (right) _instance.Pulse(OVRInput.Controller.RTouch, amplitude, duration);
        }

        void Pulse(OVRInput.Controller controller, float amplitude, float duration)
        {
            OVRInput.SetControllerVibration(1f, Mathf.Clamp01(amplitude), controller);
            float until = Time.unscaledTime + duration;
            if (controller == OVRInput.Controller.LTouch)
            {
                _leftUntil = until;
                _leftActive = true;
            }
            else
            {
                _rightUntil = until;
                _rightActive = true;
            }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (_leftActive && now >= _leftUntil)
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                _leftActive = false;
            }
            if (_rightActive && now >= _rightUntil)
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
                _rightActive = false;
            }
        }
    }
}
