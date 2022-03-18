using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Goal : MonoBehaviour
{
    public CollectionManager cm = null;
    // prevents two or more Objects colliding the goal at the same frame
    private bool alreadyCollected = false;

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if already collected (without this check goal could count twice)
        if (alreadyCollected)
        {
            return;
        }
        if (collision.gameObject.tag == "Agent")
        {
            alreadyCollected = true;
            // Notify the Collection Manager that this Object was collected
            cm.GoalCollected(this.gameObject);
        }
    }
}