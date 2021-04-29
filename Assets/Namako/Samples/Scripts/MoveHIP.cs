using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveHIP : MonoBehaviour
{
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
        p.x = initPos.x + 0.03f * Mathf.Sin(2.0f * Mathf.PI * time);
        solver.devicePosOffset = p;
    }
}
