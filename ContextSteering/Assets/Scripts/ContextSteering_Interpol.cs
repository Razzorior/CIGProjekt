using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class ContextSteering_Interpol : MonoBehaviour
{
    public bool evacuationMode = false;
    public List<Vector2> evacuationPoints;
    private static int number_of_searchVectors = 16;
    public float lookAheadDistance = 7f;
    private float steer_force = 2f;
    private float threshold = 1.8f;
    private float epsilonValue = 0.18f;

    private float alpha = 0.5f;

    public Vector2[] ray_directions;

    private Vector2 agent_position;

    public Vector3 moveDir;
    public float pr;
    public float zr;
    public int x;

    public float[] dangerValues;
    public float[] interestValues;

    private float[] oldDangerValues;
    private float[] oldInterestValues;

    private Vector3 previousPos1;
    private Vector3 previousPos2;
    private Vector3 previousPos3;
    private Vector3 previousPos4;

    public Vector3 weightedoldPos;

    // Start is called before the first frame update
    void Start()
    {
        CircleCollider2D visionCollider = this.gameObject.AddComponent<CircleCollider2D>();
        visionCollider.radius = lookAheadDistance;
        visionCollider.offset = new Vector2(0f, 0f);
        ray_directions = new Vector2[number_of_searchVectors];

        for (int index = 0; index < number_of_searchVectors; index++)
        {
            float angle = index * 2 * Mathf.PI / number_of_searchVectors;
            ray_directions[index] = Get_vector_from_angle(Vector2.up, angle);
        }

        oldDangerValues = new float[number_of_searchVectors];
        oldInterestValues = new float[number_of_searchVectors];

        previousPos1 = this.transform.position;
        previousPos2 = this.transform.position;
        previousPos3 = this.transform.position;
        previousPos4 = this.transform.position;
    }

    public List<GameObject> currentCollisions = new List<GameObject>();

    void OnCollisionEnter2D(Collision2D col)
    {
        GameObject collidedObject = col.gameObject;
        // Check if it's object of other field
        if (collidedObject.transform.root.gameObject.tag == "Room2")
        {
            return;
        }
        // Check if it's just sphere collider of other Agent (This must be ignored as its not the body of the agent)
        if (collidedObject.tag == "AgentMom")
        {
            return;
        }

        // Check if it's just the childs Polygon Collider - CollisionExit doesn't matter as they always overlap and thus collide
        if (GameObject.ReferenceEquals(collidedObject, this.transform.GetChild(0).gameObject))
        {
            return;
        }
        // Add the GameObject collided with to the list.
        currentCollisions.Add(collidedObject);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        GameObject collidedObject = col.gameObject;
        if (collidedObject.transform.root.gameObject.tag == "Room2")
        {
            return;
        }
        if (collidedObject.tag == "AgentMom")
        {
            return;
        }
        // Remove the GameObject collided with from the list.
        currentCollisions.Remove(collidedObject);
    }

    // Update is called once per frame
    void Update()
    {
        agent_position = this.transform.position;
        dangerValues = CreateDangerValues(agent_position);
        interestValues = CreateInterestValues(agent_position);

        interestValues = HistoryBlending(oldInterestValues, GaussianBlurring(interestValues), alpha);
        dangerValues = HistoryBlending(oldDangerValues, GaussianBlurring(dangerValues), alpha);
        interestValues = EpsilonConstraintMethod(dangerValues, interestValues, epsilonValue);

        // Could change this with Random Movement or avoiding danger
        if (CheckIfNoInteresetIsGiven(interestValues))
        {
            return;
        }
        moveDir = Vector3.zero;

        float[] bestPrAndZr = GetBestPRAndZR(interestValues);
        pr = bestPrAndZr[0];
        zr = bestPrAndZr[1];
        x = (int)bestPrAndZr[2];

        moveDir = pr * ray_directions[mod(x - 1, number_of_searchVectors)] + (1 - pr) * ray_directions[x];
        moveDir = moveDir.normalized;
        Vector3 newPos = (this.transform.position + moveDir * steer_force * Time.deltaTime);

        Vector3 moveDirection = newPos - this.transform.position;
        if (moveDirection != Vector3.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        this.transform.position = newPos;

        previousPos4 = previousPos3;
        previousPos3 = previousPos2;
        previousPos2 = previousPos1;
        previousPos1 = newPos;
        oldDangerValues = dangerValues;
        oldInterestValues = interestValues;

    }

    private float[] CreateDangerValues(Vector3 posAlpha)
    {
        float[] dangerValues = new float[number_of_searchVectors];
        float angle;
        foreach (GameObject go in currentCollisions)
        {
            try
            {
                if (go.tag == "Danger" || go.tag == "Agent")
                {
                    Vector3 posO;
                    if (go.tag == "Danger") posO = go.GetComponent<Collider2D>().ClosestPoint(posAlpha);
                    else posO = go.GetComponentInChildren<Collider2D>().ClosestPoint(posAlpha);

                    Vector3 vs = posO - posAlpha;

                    for (int index = 0; index < number_of_searchVectors; index++)
                    {
                        Vector3 vr = new Vector3(ray_directions[index].x, ray_directions[index].y, 0f);
                        angle = Vector3.Angle(vr, vs) * Mathf.Deg2Rad;

                        if (angle < threshold)
                        {
                            float zr = GaussianDensityFunction(angle, 0f, 0.25f) * ReciprocalFunction(vs.magnitude);
                            dangerValues[index] = Mathf.Max(dangerValues[index], zr);
                        }
                    }

                }
            }
            catch
            {
                Debug.Log("Caught Danger that was removed from the Game!");
            }
        }

        weightedoldPos = previousPos3; //* 0.7f + previousPos3 * 0.2f + previousPos2 * 0.1f; //0.6f * previousPos3 + 0.4f * previousPos2 + 0.2f * previousPos1;
        Vector3 vs1 = weightedoldPos - posAlpha;

        for (int index = 0; index < number_of_searchVectors; index++)
        {
            Vector3 vr = new Vector3(ray_directions[index].x, ray_directions[index].y, 0f);
            angle = Vector3.Angle(vr, vs1) * Mathf.Deg2Rad;

            if (angle < threshold)
            {
                float zr = GaussianDensityFunction(angle, 0f, 0.25f) * ReciprocalFunction(vs1.magnitude * 1f);
                dangerValues[index] = Mathf.Max(dangerValues[index], zr);
            }
        }
        return dangerValues;
    }

    private float[] CreateInterestValues(Vector3 posAlpha)
    {
        float[] interestValues = new float[number_of_searchVectors];
        float angle;

        bool removeDeletedInterest = false;
        GameObject interestToBeRemoved = null;

        if (!evacuationMode)
        {
            foreach (GameObject go in currentCollisions)
            {
                try
                {
                    if (go.tag == "Interest")
                    {
                        Vector3 posO = go.GetComponent<Collider2D>().ClosestPoint(posAlpha);
                        Vector3 vs = posO - posAlpha;

                        for (int index = 0; index < number_of_searchVectors; index++)
                        {
                            Vector3 vr = new Vector3(ray_directions[index].x, ray_directions[index].y, 0f);
                            angle = Vector3.Angle(vr, vs) * Mathf.Deg2Rad;

                            if (angle < threshold)
                            {
                                float zr = GaussianDensityFunction(angle, 0f, 0.25f * Mathf.PI) * ReciprocalFunction(vs.magnitude);
                                interestValues[index] = Mathf.Max(interestValues[index], zr);
                            }
                        }
                    }
                }
                catch
                {
                    removeDeletedInterest = true;
                    interestToBeRemoved = go;
                }
            }

            // Cant be done in foreach Loop
            if(removeDeletedInterest)
            {
                removeDeletedInterest = false;
                currentCollisions.Remove(interestToBeRemoved);
            }
        }
        else
        {
            foreach (Vector2 ep in evacuationPoints)
            {
                Vector3 vs = new Vector3(ep.x, ep.y, 0f) - posAlpha;

                for (int index = 0; index < number_of_searchVectors; index++)
                {
                    Vector3 vr = new Vector3(ray_directions[index].x, ray_directions[index].y, 0f);
                    angle = Vector3.Angle(vr, vs) * Mathf.Deg2Rad;

                    if (angle < threshold)
                    {
                        float zr = GaussianDensityFunction(angle, 0f, 0.25f * Mathf.PI) * ReciprocalFunction(vs.magnitude / 10f); //damped reciprocal function
                        interestValues[index] = Mathf.Max(interestValues[index], zr);
                    }
                }
            }
        }

        return interestValues;
    }

    private float[] EpsilonConstraintMethod(float[] danger, float[] interest, float epsilon)
    {
        for (int index = 0; index < number_of_searchVectors; index++)
        {
            if (danger[index] > epsilon)
            {
                interest[index] = 0f;
            }
            else
            {
                //interest[index] = Mathf.Max(0, interest[index] - danger[index]*0.15f);
            }
        }
        return interest;
    }

    private float GaussianDensityFunction(float number, float mean, float variance)
    {
        return ((1 / Mathf.Sqrt(2 * Mathf.PI * variance)) * Mathf.Exp(-((Mathf.Pow(number - mean, 2)) / (2 * variance))));
    }

    private float ReciprocalFunction(float number)
    {
        return (number > 0) ? Mathf.Min((1f / (Mathf.Pow(number, 3f))), 1) : 0f;
    }

    private float[] GaussianBlurring(float[] oldInterest)
    {
        float[] newInterest = new float[number_of_searchVectors];

        for (int index = 0; index < number_of_searchVectors; index++)
        {
            newInterest[index] = oldInterest[mod(index - 1, number_of_searchVectors)] * 0.10558f + oldInterest[index] * 0.78884f + oldInterest[mod(index + 1, number_of_searchVectors)] * 0.10558f;
        }

        return newInterest;
    }

    private float[] HistoryBlending(float[] oldFunction, float[] newFunction, float alpha)
    {
        for (int index = 0; index < number_of_searchVectors; index++)
        {
            newFunction[index] = alpha * oldFunction[index] + (1 - alpha) * newFunction[index];
        }
        return newFunction;
    }

    private float[] GetPrAndZrOfFX(int x, float[] interest)
    {
        float gradientXMinus1 = interest[mod(x - 1, number_of_searchVectors)] - interest[mod(x - 2, number_of_searchVectors)];
        float gradientXPlus1 = interest[mod(x + 1, number_of_searchVectors)] - interest[x];

        float denominator = (gradientXMinus1 - gradientXPlus1);

        float pr = (interest[x] - gradientXPlus1 - interest[mod(x - 1, number_of_searchVectors)]) / denominator;
        float zr = gradientXMinus1 * pr + interest[mod(x - 1, number_of_searchVectors)];

        return new float[] { pr, zr, x };
    }

    private float[] GetBestPRAndZR(float[] interestValues)
    {
        float[] bestInterpolatedPRandZR = new float[3];

        for (int index = 0; index < number_of_searchVectors; index++)
        {
            float[] tmp = GetPrAndZrOfFX(index, interestValues);
            if (tmp[1] >= bestInterpolatedPRandZR[1])
            {
                bestInterpolatedPRandZR = tmp;
            }
        }

        return bestInterpolatedPRandZR;
    }

    private bool CheckIfNoInteresetIsGiven(float[] interest)
    {

        for (int index = 0; index < interest.Length; index++)
        {
            if (interest[index] != 0f)
            {
                return false;
            }
        }
        return true;

    }

    private Vector2 Get_vector_from_angle(Vector2 vec, float angle)
    {
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);

        float old_x = vec.x;
        float old_y = vec.y;

        vec.x = (cos * old_x) - (sin * old_y);
        vec.y = (sin * old_x) + (cos * old_y);

        return vec;
    }

    // Needed as C# modulu gives you remainder and thus negative numbers
    int mod(int x, int m)
    {
        return (x % m + m) % m;
    }
}