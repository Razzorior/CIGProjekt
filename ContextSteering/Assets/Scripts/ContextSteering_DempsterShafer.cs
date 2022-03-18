using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContextSteering_DempsterShafer : MonoBehaviour
{
    public bool evacuationMode = false;
    // Not used anymore - Idea was to only change direction when the new argmaxValue is 
    // noticabily better than the old one. 
    public double argmaxThreshold = 0.00;
    // Evacuation Points is filled by the ContextSteeringComparisonManager
    public List<Vector2> evacuationPoints;
    private static int number_of_searchVectors = 16;
    public float lookAheadDistance = 7f;
    // Steer Force determines Speed of the agent per sec. Should be the same value as the
    // Interpolation steer force.
    private float steer_force = 2f;
    // Angle threshold for mass = 0 towards other object. PI / 2 => 90 deg
    private float threshold = Mathf.PI / 2f;


    public Vector2[] ray_directions;

    private Vector2 agent_position;

    public double[,] masses;
    public double[] mass1 = new double[17];
    public int argmax;
    private double[] oldDirectionValues;
    public double[] directionValues;

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

        directionValues = new double[number_of_searchVectors + 1];
        oldDirectionValues = new double[number_of_searchVectors + 1];
    }

    public List<GameObject> currentCollisions = new List<GameObject>();

    void OnCollisionEnter2D(Collision2D col)
    {
        
        GameObject collidedObject = col.gameObject;

        if (evacuationMode && collidedObject.tag == "Interest")
        {
            return;
        }

        // Evacuation objects are managed differntly. 
        if(collidedObject.tag == "Evacuation")
        {
            return;
        }

        // Check if it's an object of other field
        if (collidedObject.transform.root.gameObject.tag == "Room1")
        {
            return;
        }
        // Check if it's just sphere collider of other Agent (This must be ignored as its not the body of the agent)
        if (collidedObject.tag == "AgentMom")
        {
            return;
        }

        // Check if it's just the childs Collider - CollisionExit doesn't matter as they always overlap and thus collide
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
        if (collidedObject.transform.root.gameObject.tag == "Room1")
        {
            return;
        }
        if (collidedObject.tag == "AgentMom")
        {
            return;
        }
        currentCollisions.Remove(collidedObject);
    }

    void Update()
    {
        // Saves time if there are no objects in sight that have to be computed. 
        if(currentCollisions.Count + evacuationPoints.Count <= 0 )
        {
            return;
        }

        agent_position = this.transform.position;
        masses = CreateMasses();
      
        for (int i = 0; i <= number_of_searchVectors; i++)
        {
            mass1[i] = masses[0, i];
        }

        directionValues = GetDirectionValues(masses);
        argmax = ArgMaxIgnoringUncertainty(directionValues);

        Vector3 newPos = this.transform.position + (new Vector3(ray_directions[argmax].x, ray_directions[argmax].y, 0f) * steer_force * Time.deltaTime);
        Vector3 moveDirection = newPos - this.transform.position;
        if (moveDirection != Vector3.zero)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
        this.transform.position = newPos;
        oldDirectionValues = directionValues;
    }

    private double[,] CreateMasses()
    {
        double[,] massMatrix;
        if (!evacuationMode)
        {
            massMatrix = new double[currentCollisions.Count, number_of_searchVectors + 1];
        }
        else
        {
            massMatrix = new double[currentCollisions.Count + evacuationPoints.Count, number_of_searchVectors + 1];
        }

        for(int collisionIndex = 0; collisionIndex < currentCollisions.Count; collisionIndex++)
        {
            double uncertainty = 0.0;
            try
            {
                Vector3 posO = currentCollisions[collisionIndex].GetComponent<Collider2D>().ClosestPoint(this.transform.position);
                Vector3 distanceVec = posO - this.transform.position;
                double confidence = 0.0;
                if(currentCollisions[collisionIndex].tag == "Interest")
                {
                    confidence = EvacuationReciprocalFunction(distanceVec.magnitude);
                }
                else
                {
                    confidence = ReciprocalFunction(distanceVec.magnitude); 
                }

                double totalMass = 0.0;

                if (currentCollisions[collisionIndex].tag == "Danger" || currentCollisions[collisionIndex].tag == "Agent")
                {
                    distanceVec = -distanceVec;
                }

                for (int directionIndex = 0; directionIndex < ray_directions.Length; directionIndex++)
                {
                    float angle = Vector2.Angle(ray_directions[directionIndex], new Vector2(distanceVec.x, distanceVec.y)) * Mathf.Deg2Rad;
                    double massValue = 0f;
                    if (angle < threshold)
                    {
                        // currently using equal masses for any vector that does not head towards danger
                        if (currentCollisions[collisionIndex].tag == "Danger" || currentCollisions[collisionIndex].tag == "Agent")
                        {
                            massValue = 1.0;
                        }
                        else
                        {
                            massValue = GaussianDensityFunction(angle, 0f, 0.22f * Mathf.PI);
                        }
                        //massValue = GaussianDensityFunction(angle, 0f, 0.25f * Mathf.PI);
                        massMatrix[collisionIndex, directionIndex] = massValue;
                        totalMass += massValue;
                    }
                }

                 
                double tmp = 0.0;

                for (int directionIndex = 0; directionIndex < ray_directions.Length; directionIndex++)
                {
                    // This is a softmax (Mass devided by sum of masses)
                    massMatrix[collisionIndex, directionIndex] /= totalMass;
                    tmp = massMatrix[collisionIndex, directionIndex] * confidence;
                    uncertainty += massMatrix[collisionIndex, directionIndex] - tmp;
                    massMatrix[collisionIndex, directionIndex] = tmp;
                }
            }
            catch
            {
                uncertainty = 1.0;
            }

            massMatrix[collisionIndex, number_of_searchVectors] = uncertainty;

        }

        if(evacuationMode)
        {
            for (int collisionIndex = currentCollisions.Count; collisionIndex < currentCollisions.Count + evacuationPoints.Count; collisionIndex++)
            {
                double uncertainty = 0.0;
                try
                {
                    Vector3 posO = new Vector3(evacuationPoints[collisionIndex - currentCollisions.Count].x, evacuationPoints[collisionIndex - currentCollisions.Count].y, 0f);
                    Vector3 distanceVec = posO - this.transform.position;
                    double confidence = EvacuationReciprocalFunction(distanceVec.magnitude);
                    double totalMass = 0.0;

                    for (int directionIndex = 0; directionIndex < ray_directions.Length; directionIndex++)
                    {
                        float angle = Vector2.Angle(ray_directions[directionIndex], new Vector2(distanceVec.x, distanceVec.y)) * Mathf.Deg2Rad;
                        double massValue = 0f;
                        if (angle < threshold)
                        {
                            massValue = GaussianDensityFunction(angle, 0f, 0.25f * Mathf.PI);
                            massMatrix[collisionIndex, directionIndex] = massValue;
                            totalMass += massValue;
                        }
                    }


                    double tmp = 0.0;

                    for (int directionIndex = 0; directionIndex < ray_directions.Length; directionIndex++)
                    {
                        // Softmax
                        massMatrix[collisionIndex, directionIndex] /= totalMass;
                        // Measuring confidence impact
                        tmp = massMatrix[collisionIndex, directionIndex] * confidence;
                        uncertainty += massMatrix[collisionIndex, directionIndex] - tmp;
                        massMatrix[collisionIndex, directionIndex] = tmp;
                    }
                }
                catch
                {
                    uncertainty = 1.0;
                }

                massMatrix[collisionIndex, number_of_searchVectors] = uncertainty;

            }
        }
        
        return massMatrix;
    }

    private double[] GetDirectionValues(double[,] masses)
    {
        double[] values = new double[number_of_searchVectors + 1];
        for (int j = 0; j <= number_of_searchVectors; j++)
        {
            values[j] = masses[0, j];
        }
        for (int i = 1; i < currentCollisions.Count + evacuationPoints.Count; i++)
        {
            for (int j = 0; j < number_of_searchVectors; j++)
            {
                // A mass * Buncertainty + Amass *Bmass + Auncerrtainty * Bmass
                values[j] = values[j] * masses[i, number_of_searchVectors] + values[j] * masses[i, j] + values[number_of_searchVectors] * masses[i, j];
            }
            values[number_of_searchVectors] *= masses[i, number_of_searchVectors];
        }
        
        return values;
    }

    private int ArgMaxIgnoringUncertainty(double[] values)
    {
        double highestNumber = values[0];
        int indexOfHighestNumber = 0;
        // Length-1 to ignore the uncertainty value
        for(int index=1; index<values.Length-1; index++)
        {
            if(values[index]>highestNumber)
            {
                highestNumber = values[index];
                indexOfHighestNumber = index;
            }
        }

        return indexOfHighestNumber;
    }

    private double GaussianDensityFunction(float number, float mean, float variance)
    {
        // Might have to catch zeroes in the denominator
        return ((1.0 / Math.Sqrt(2.0 * Mathf.PI * variance)) * Math.Exp(-((Math.Pow(number - mean, 2.0)) / (2.0 * variance))));
    }

    private double ReciprocalFunction(float number)
    {
        return (number > 0) ? Math.Min((1.0 / Math.Pow(number+0.5,2.0)), 1.0) : 0.0;
    }

    private double InterestReciprocalFunction(float number)
    {
        return (number > 0) ? Math.Min((1.0 / number * 2.0), 1.0) : 0.0;
    }

    private double EvacuationReciprocalFunction(float number)
    {
        return (number > 0) ? Math.Min((1.0 / Math.Sqrt(number)), 1.0) : 0.0;
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

    int mod(int x, int m)
    {
        return (x % m + m) % m;
    }
}