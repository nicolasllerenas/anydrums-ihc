using UnityEngine;

public class VRDoorController : MonoBehaviour
{
    [Header("Door Components")]
    public Animator doorAnimator;
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    private bool isPlayerNearby = false;
    private bool isOpen = false;

    private void OnTriggerEnter(Collider other)
    {
        // Detect Meta Quest VR Camera Rig or hands
        if (other.CompareTag("Player") || other.GetComponentInParent<OVRCameraRig>() != null)
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<OVRCameraRig>() != null)
        {
            isPlayerNearby = false;
        }
    }

    void Update()
    {
        // Pressing Index Trigger on Right Quest Controller
        if (isPlayerNearby && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            ToggleDoor();
        }
    }

    void ToggleDoor()
    {
        isOpen = !isOpen;

        if (isOpen)
        {
            doorAnimator.SetTrigger("open");
            doorAnimator.ResetTrigger("close");
            if (audioSource && openSound) audioSource.PlayOneShot(openSound);
        }
        else
        {
            doorAnimator.SetTrigger("close");
            doorAnimator.ResetTrigger("open");
            if (audioSource && closeSound) audioSource.PlayOneShot(closeSound);
        }
    }
}