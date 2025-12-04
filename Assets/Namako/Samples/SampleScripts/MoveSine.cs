using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveSine : MonoBehaviour
{

    public float amp = 0.1f;
    public float freq = 0.5f;

    float time = 0.0f;
    Vector3 initPos;

    void Start()
    {
        initPos = transform.position;
    }

    void Update()
    {
        time += Time.deltaTime;
        Vector3 p = initPos;
        p.y = initPos.y + amp * (Mathf.Cos(2.0f * Mathf.PI * freq * time) - 1.0f);
        transform.position = p;
    }
}
