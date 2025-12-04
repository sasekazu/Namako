using UnityEngine;
using System.Collections;

namespace Namako.Samples
{
    /// <summary>
    /// Simple key input controller for NamakoRigidBody grab/release functionality
    /// </summary>
    public class NamakoRigidBodyController : MonoBehaviour
    {
        [Header("Key Settings")]
        [SerializeField] private KeyCode grabKey = KeyCode.G;
        [SerializeField] private KeyCode releaseKey = KeyCode.R;
        [SerializeField] private KeyCode toggleRepeatKey = KeyCode.T;
        
        [Header("Manual Control")]
        [SerializeField] private bool isGrabbed = false;
        
        [Header("Repeat Settings")]
        [SerializeField] private float repeatInterval = 1.0f;
        [SerializeField] private bool enableRepeat = false;
        
        private NamakoRigidBody namakoRigidBody;
        private bool isRepeating = false;
        private Coroutine repeatCoroutine;
        
        void Start()
        {
            namakoRigidBody = GetComponent<NamakoRigidBody>();
            if (namakoRigidBody == null)
            {
                Debug.LogError($"[NamakoRigidBodyController] NamakoRigidBody component not found on {gameObject.name}");
                enabled = false;
            }
        }
        
        void Update()
        {
            // Handle manual grab/release checkbox
            if (isGrabbed != namakoRigidBody.IsGrabbing && !isRepeating)
            {
                if (isGrabbed)
                {
                    namakoRigidBody.GrabNodes();
                }
                else
                {
                    namakoRigidBody.ReleaseNodes();
                }
            }
            
            // Handle repeat checkbox toggle
            if (enableRepeat != isRepeating)
            {
                if (enableRepeat)
                {
                    StartRepeat();
                }
                else
                {
                    StopRepeat();
                }
            }
            
            // Handle key input for toggle
            if (Input.GetKeyDown(toggleRepeatKey))
            {
                enableRepeat = !enableRepeat;
            }
            
            // Manual controls (only when not repeating)
            if (!isRepeating)
            {
                if (Input.GetKeyDown(grabKey))
                {
                    namakoRigidBody.GrabNodes();
                    isGrabbed = true;
                }
                else if (Input.GetKeyDown(releaseKey))
                {
                    namakoRigidBody.ReleaseNodes();
                    isGrabbed = false;
                }
            }
        }
        
        private void ToggleRepeat()
        {
            enableRepeat = !enableRepeat;
        }
        
        private void StartRepeat()
        {
            if (repeatCoroutine != null)
            {
                StopCoroutine(repeatCoroutine);
            }
            
            isRepeating = true;
            repeatCoroutine = StartCoroutine(RepeatGrabRelease());
            Debug.Log("[NamakoRigidBodyController] Started grab/release repeat");
        }
        
        private void StopRepeat()
        {
            if (repeatCoroutine != null)
            {
                StopCoroutine(repeatCoroutine);
                repeatCoroutine = null;
            }
            
            isRepeating = false;
            Debug.Log("[NamakoRigidBodyController] Stopped grab/release repeat");
        }
        
        private IEnumerator RepeatGrabRelease()
        {
            while (isRepeating)
            {
                namakoRigidBody.GrabNodes();
                yield return new WaitForSeconds(repeatInterval);
                
                namakoRigidBody.ReleaseNodes();
                yield return new WaitForSeconds(repeatInterval);
            }
        }
        
        void OnDisable()
        {
            StopRepeat();
        }
        
        // Public methods for external control
        /// <summary>
        /// Grab the nodes (can be called from external scripts)
        /// </summary>
        public void Grab()
        {
            if (namakoRigidBody != null && !isRepeating)
            {
                namakoRigidBody.GrabNodes();
                isGrabbed = true;
            }
        }
        
        /// <summary>
        /// Release the nodes (can be called from external scripts)
        /// </summary>
        public void Release()
        {
            if (namakoRigidBody != null && !isRepeating)
            {
                namakoRigidBody.ReleaseNodes();
                isGrabbed = false;
            }
        }
        
        /// <summary>
        /// Toggle between grab and release states
        /// </summary>
        public void ToggleGrab()
        {
            if (isGrabbed)
            {
                Release();
            }
            else
            {
                Grab();
            }
        }
        
        /// <summary>
        /// Get the current grab state
        /// </summary>
        public bool IsGrabbed => isGrabbed;
        
        void OnValidate()
        {
            // Handle manual grab/release checkbox change in editor
            if (Application.isPlaying && namakoRigidBody != null && !isRepeating)
            {
                if (isGrabbed != namakoRigidBody.IsGrabbing)
                {
                    if (isGrabbed)
                    {
                        namakoRigidBody.GrabNodes();
                    }
                    else
                    {
                        namakoRigidBody.ReleaseNodes();
                    }
                }
            }
            
            // Handle repeat checkbox change in editor
            if (Application.isPlaying && enableRepeat != isRepeating)
            {
                if (enableRepeat)
                {
                    StartRepeat();
                }
                else
                {
                    StopRepeat();
                }
            }
        }
    }
}
