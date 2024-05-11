using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MappableObject : MonoBehaviour {
    public bool meshIsContinuous = false;

    public void Setup() {
        gameObject.layer = LayerMask.GetMask("Original Object");
    }
}
