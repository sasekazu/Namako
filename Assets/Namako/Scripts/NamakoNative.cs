using System;
using System.Runtime.InteropServices;

namespace Namako
{
    /// <summary>
    /// Namako ネイティブライブラリとのインターフェース
    /// </summary>
    public static class NamakoNative
    {
        // セットアップ・終了関連
        [DllImport("namako")]
        public static extern void SetupFEM(
            float hip_radius, float young_kPa, float poisson,
            float density, float damping_alpha, float damping_beta,
            IntPtr fem_pos, int fem_nnodes, IntPtr fem_indices4, int fem_ntets,
            int collision_detection_mode);
        
        [DllImport("namako")]
        public static extern void SetupVisMesh(
            IntPtr vismesh_pos, int vismesh_nnodes, 
            IntPtr vismesh_faces, int vismesh_nfaces);
        
        [DllImport("namako")]
        public static extern void StartSimulation();
        
        [DllImport("namako")] 
        public static extern void Terminate();

        // 情報取得
        [DllImport("namako")] 
        public static extern int GetNumNodes();
        
        [DllImport("namako")] 
        public static extern int GetNumElems();

        // 位置・データ取得
        [DllImport("namako")] 
        public static extern void GetNodePos(IntPtr pos);
        
        [DllImport("namako")] 
        public static extern void GetRBPos(IntPtr pos);
        
        [DllImport("namako")] 
        public static extern void GetVisMeshPos(IntPtr pos);
        
        [DllImport("namako")] 
        public static extern void GetVisMeshStress(IntPtr stress);
        
        [DllImport("namako")] 
        public static extern void GetNodePrincipalStress(IntPtr stress);
        
        [DllImport("namako")] 
        public static extern void GetRotationXYZW(IntPtr xyzw);
        
        [DllImport("namako")] 
        public static extern void GetDisplayingForce(IntPtr force);
        
        [DllImport("namako")] 
        public static extern void GetContactNormal(IntPtr n);

        // 物理パラメータ設定        
        [DllImport("namako")] 
        public static extern void ScaleStiffness(float scale);
        
        [DllImport("namako")] 
        public static extern void SetFriction(float friction);
        
        [DllImport("namako")] 
        public static extern void SetVCStiffness(float kc);
        
        [DllImport("namako")] 
        public static extern void SetHandleOffset(float x, float y, float z);
        
        [DllImport("namako")] 
        public static extern void SetGlobalDamping(float damping);
        
        [DllImport("namako")] 
        public static extern void SetGravity(float gx, float gy, float gz);
        
        [DllImport("namako")] 
        public static extern void SetGravityRb(float gx, float gy, float gz);

        // 境界条件設定
        [DllImport("namako")]
        public static extern void SetBoundaryConditions(IntPtr node_id_list, int n_ids, IntPtr displacements);

        // 力覚・衝突設定
        [DllImport("namako")] 
        public static extern void SetHapticEnabled(bool enabled);
        
        [DllImport("namako")] 
        public static extern void SetFloorCollisionEnabled(bool enabled);
        
        [DllImport("namako")] 
        public static extern void SetRBFEMCollisionEnabled(bool enabled);
        
        [DllImport("namako")] 
        public static extern void SetFloorHapticsEnabled(bool enabled);
        
        [DllImport("namako")] 
        public static extern void SetWaitTime(int wait_ms);

        // 状態取得
        [DllImport("namako")] 
        public static extern float GetScaledYoungsModulus();
        
        [DllImport("namako", EntryPoint = "IsContact")] 
        public static extern bool IsContactC();

        // パフォーマンス計測
        [DllImport("namako")] 
        public static extern double GetCalcTime();
        
        [DllImport("namako")] 
        public static extern double GetLoopTime();

        // ログ機能
        [DllImport("namako")] 
        public static extern void StartLog();
        
        [DllImport("namako")] 
        public static extern void StopLog();

        // メッシュ生成
        [DllImport("namako")]
        public static extern void GenerateGridMesh(
            int divisions, IntPtr pos, int n_pos, IntPtr indices, int n_indices,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)] out float[] out_pos,
            out int n_out_pos,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 8)] out int[] out_tet,
            out int n_out_tet);

        // 接触剛体管理
        [DllImport("namako")]
        public static extern void AddContactRigidBody(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr pos, int n_pos,
            IntPtr faces, int n_faces);

        [DllImport("namako")]
        public static extern void UpdateContactRigidBodyPos(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr pos, int n_pos);

        [DllImport("namako")]
        public static extern int GetContactForces(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr forces);

        [DllImport("namako")]
        public static extern void GrabWithRigidBody(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("namako")]
        public static extern void ReleaseWithRigidBody(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        // 無限平面管理
        [DllImport("namako")]
        public static extern void AddInfinitePlane(IntPtr pos, IntPtr normal);

        [DllImport("namako")]
        public static extern void ClearInfinitePlanes();
    }
}
