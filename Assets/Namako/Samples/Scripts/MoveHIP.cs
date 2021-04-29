using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveHIP : MonoBehaviour
{

    public float amp = 0.1f;
    public float freq = 0.5f;

    Namako.NamakoSolver solver;
    float time = 0.0f;
    Vector3 initPos;

    void Start()
    {
        solver = this.GetComponent<Namako.NamakoSolver>();
        initPos = solver.devicePosOffset;
    }

    void Update()
    {
        time += Time.deltaTime;
        Vector3 p = initPos;
        p.x = initPos.x + amp * ( - Mathf.Cos(2.0f * Mathf.PI * freq * time));
        solver.devicePosOffset = p;
    }
}
