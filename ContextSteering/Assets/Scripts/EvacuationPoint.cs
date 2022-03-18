using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvacuationPoint : MonoBehaviour
{
    public ContextSteeringComparisonManager cscm = null;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Agent")
        {
            cscm.AgentEvacuated(collision.transform.parent.gameObject.GetInstanceID());
            GameObject.Destroy(collision.transform.parent.gameObject);
        }
    }
}
