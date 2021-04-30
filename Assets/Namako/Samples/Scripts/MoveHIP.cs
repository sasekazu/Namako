﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveHIP : MonoBehaviour
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
        p.x = initPos.x + amp * ( - Mathf.Cos(2.0f * Mathf.PI * freq * time));
    }
}
