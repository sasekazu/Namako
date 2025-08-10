using UnityEngine;

namespace Namako
{
    /// <summary>
    /// Haptic interface management component
    /// Should be attached to HapticInterfaceObject GameObject
    /// </summary>
    public class NamakoHaptics : MonoBehaviour
    {
        [Tooltip("剛体半径"), Range(0.001f, 0.1f), Header("Haptic Parameters")]
        public float HIPRad = 0.03f;
        
        [Tooltip("バーチャルカップリングのばね定数"), Range(0.0f, 1000.0f)]
        public float VCStiffness = 300.0f;
        
        [Tooltip("剛体にかかる重力")]
        public Vector3 gravityRb = Vector3.zero;
        
        [Tooltip("力覚提示を有効にする"), Header("Haptic Control")]
        public bool hapticEnabled = true;
        
        [Tooltip("床に触れるようにする")]
        public bool floorEnabled = true;
        
        [Tooltip("力覚提示を開始するまでの猶予時間[s]")]
        public float waitTime = 0.5f;

        private GameObject inputObj;
        private GameObject proxyObj;
        private NamakoSolver namakoSolver;
        private float time = 0.0f;

        void Start()
        {
            // Find NamakoSolver instance
            namakoSolver = NamakoSolver.Instance;
            if (namakoSolver == null)
            {
                Debug.LogError("NamakoSolver not found in scene!");
                return;
            }

            // Find Input and Proxy objects in the scene
            FindHapticObjects();
        }

        void Update()
        {
            if (namakoSolver == null || !namakoSolver.IsFEMStarted) return;
            if (inputObj == null || proxyObj == null) return;

            time += Time.deltaTime;

            // Handle haptic enabled state based on wait time
            if (time < waitTime)
            {
                NamakoNative.SetHapticEnabled(false);
            }
            else
            {
                NamakoNative.SetHapticEnabled(hapticEnabled);
            }

            // Set handle offset from input object position
            Vector3 handleOffset = inputObj.transform.position;
            NamakoNative.SetHandleOffset(handleOffset.x, handleOffset.y, handleOffset.z);

            // Update rigid body position and rotation
            UpdateRigidBody();

            // Update haptic parameters
            UpdateHapticParameters();
        }

        private void FindHapticObjects()
        {
            // Search for Input and Proxy objects
            GameObject hapticInterfaceObject = GameObject.Find("HapticInterfaceObject");
            if (hapticInterfaceObject != null)
            {
                Transform inputTransform = hapticInterfaceObject.transform.Find("Input");
                Transform proxyTransform = hapticInterfaceObject.transform.Find("Proxy");
                
                inputObj = inputTransform != null ? inputTransform.gameObject : null;
                proxyObj = proxyTransform != null ? proxyTransform.gameObject : null;
            }

            if (inputObj == null)
            {
                inputObj = GameObject.Find("Input");
            }
            
            if (proxyObj == null)
            {
                proxyObj = GameObject.Find("Proxy");
            }

            if (inputObj == null || proxyObj == null)
            {
                Debug.LogWarning("Input or Proxy object not found. Please ensure they exist in the scene.");
            }
        }

        private void UpdateRigidBody()
        {
            if (proxyObj == null) return;

            // Get position from native library
            System.IntPtr p_cpp = System.Runtime.InteropServices.Marshal.AllocHGlobal(3 * sizeof(float));
            NamakoNative.GetRBPos(p_cpp);
            float[] p = new float[3];
            Vector3 pVec;
            System.Runtime.InteropServices.Marshal.Copy(p_cpp, p, 0, 3);
            pVec.x = p[0];
            pVec.y = p[1];
            pVec.z = p[2];
            if (!float.IsNaN(pVec.magnitude))
            {
                proxyObj.transform.position = pVec;
            }
            System.Runtime.InteropServices.Marshal.FreeHGlobal(p_cpp);

            // Get rotation from native library
            System.IntPtr q_cpp = System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * sizeof(float));
            NamakoNative.GetRotationXYZW(q_cpp);
            float[] q = new float[4];
            System.Runtime.InteropServices.Marshal.Copy(q_cpp, q, 0, 4);
            proxyObj.transform.rotation = new Quaternion(q[0], q[1], q[2], q[3]);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(q_cpp);

            // Set gravity for rigid body
            NamakoNative.SetGravityRb(gravityRb.x, gravityRb.y, gravityRb.z);
        }

        private void UpdateHapticParameters()
        {
            // Update haptic parameters in native library
            NamakoNative.SetVCStiffness(VCStiffness);
            NamakoNative.SetFloorHapticsEnabled(floorEnabled);
        }

        /// <summary>
        /// Get haptic force vector
        /// </summary>
        public Vector3 GetForce()
        {
            System.IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            NamakoNative.GetDisplayingForce(ptr);
            System.Runtime.InteropServices.Marshal.Copy(ptr, arr, 0, 3);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }

        /// <summary>
        /// Get contact normal vector
        /// </summary>
        public Vector3 GetNormal()
        {
            System.IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(3 * sizeof(float));
            var arr = new float[3];
            NamakoNative.GetContactNormal(ptr);
            System.Runtime.InteropServices.Marshal.Copy(ptr, arr, 0, 3);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            return new Vector3(arr[0], arr[1], arr[2]);
        }

        /// <summary>
        /// Check if haptic device is in contact
        /// </summary>
        public bool IsContact()
        {
            return NamakoNative.IsContactC();
        }

        /// <summary>
        /// Get HIP radius for external access
        /// </summary>
        public float GetHIPRadius()
        {
            return HIPRad;
        }
    }
}
