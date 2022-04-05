
using UnityEngine;

namespace Namako
{

    [System.Serializable]
    public class MeshForJSON
    {
        public float[] pos;
        public int[] tet;
    }

    [System.Serializable]
    public class MeshExtractor
    {
        public int n_vert_all { get; }
        public float[] vmesh_pos { get; }
        public int n_tri_all { get; }
        public int[] indices_all { get; }

        private MeshFilter[] mfs;
        private int n_mesh;
        private int[] vert_offsets;
        private int[] tri_offsets;
        private int[] n_vert;
        private GameObject visualObj;

        public MeshExtractor(GameObject obj)
        {
            visualObj = obj;
            mfs = visualObj.GetComponentsInChildren<MeshFilter>();
            n_mesh = mfs.Length;
            int[] n_tri = new int[n_mesh];
            n_vert = new int[n_mesh];
            n_tri_all = 0;
            n_vert_all = 0;
            vert_offsets = new int[n_mesh];
            tri_offsets = new int[n_mesh];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].sharedMesh;
                // We consider only one submesh context.
                n_tri[i] = mesh.GetIndices(0).GetLength(0) / 3;
                n_vert[i] = mesh.vertices.GetLength(0);
                n_tri_all += n_tri[i];
                n_vert_all += n_vert[i];
                for (int j = 0; j < i; ++j)
                {
                    vert_offsets[i] += n_vert[j];
                    tri_offsets[i] += n_tri[j];
                }
            }
            // Prepare vmesh_indices_cpp
            Vector3[] pos_all = new Vector3[n_vert_all];
            indices_all = new int[3 * n_tri_all];
            for (int i = 0; i < n_mesh; ++i)
            {
                Mesh mesh = mfs[i].sharedMesh;
                for (int j = 0; j < n_vert[i]; ++j)
                {
                    pos_all[vert_offsets[i] + j] = mesh.vertices[j];
                }
                int[] tri = mesh.GetIndices(0);
                for (int j = 0; j < 3 * n_tri[i]; ++j)
                {
                    indices_all[3 * tri_offsets[i] + j] = tri[j] + vert_offsets[i];
                }
            }
            // Prepare vmesh_pos_cpp
            int l = 3 * n_vert_all;
            vmesh_pos = new float[l];
            for (int i = 0; i < n_vert_all; ++i)
            {
                // Convert local coodinate to world coordinate
                Vector3 worldpt = visualObj.transform.localToWorldMatrix.MultiplyPoint3x4(pos_all[i]);
                for (int j = 0; j < 3; ++j)
                {
                    vmesh_pos[3 * i + j] = worldpt[j];
                }
            }
        }

        public void UpdatePosition(float[] new_pos)
        {
            for (int m = 0; m < n_mesh; ++m)
            {
                Mesh mesh = mfs[m].mesh;
                Vector3[] pos = mesh.vertices;
                for (int i = 0; i < n_vert[m]; ++i)
                {
                    int pos_id = vert_offsets[m] + i;
                    pos[i][0] = new_pos[3 * pos_id + 0];
                    pos[i][1] = new_pos[3 * pos_id + 1];
                    pos[i][2] = new_pos[3 * pos_id + 2];
                    pos[i] = visualObj.transform.worldToLocalMatrix.MultiplyPoint3x4(pos[i]);
                }

                //Debug.Log(pos[0]);
                mesh.vertices = pos;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }
        }

        public void UpdateVertexColor(float[] scalar, float lowerLimit, float upperLimit)
        {
            for (int m = 0; m < n_mesh; ++m)
            {
                Mesh mesh = mfs[m].mesh;
                Vector3[] pos = mesh.vertices;
                Color[] colors = new Color[pos.Length];
                for (int i = 0; i < n_vert[m]; ++i)
                {
                    int pos_id = vert_offsets[m] + i;
                    float t = (scalar[pos_id] - lowerLimit) / (upperLimit - lowerLimit);

                    // blue - green - red
                    if (t < 0.5)
                        colors[i] = Color.Lerp(Color.blue, Color.green, t * 2.0f);
                    else
                        colors[i] = Color.Lerp(Color.green, Color.red, (t - 0.5f) * 2.0f);
                }

                mesh.colors = colors;
            }
        }
    }
}