using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Namako;


[RequireComponent(typeof(NamakoSolver))]
public class ReadState : MonoBehaviour
{

    NamakoSolver solver;

    void Start()
    {
        solver = GetComponent<NamakoSolver>();
    }

    void Update()
    {
        if (solver.IsContact())
        {
            Debug.Log("Force : " + solver.GetForce());
            Debug.Log("Normal: " + solver.GetNormal());
        }
        else
        {
            Debug.Log("No collision");
        }
    }
}
