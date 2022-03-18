using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CollectionManager : MonoBehaviour
{

    public GameObject Field1;
    public Vector2 Field1BoundXY;
    public Vector2 Field1BoundNegativeXY;

    public GameObject Field2;
    public bool symmetrical = true;

    public GameObject redAgentPrefab;
    public GameObject blueAgentPrefab;
    public GameObject GoalPointPrefab;

    // Variables needed to track Collecting Goals performance
    public GameObject RedAgentsGoalsCollectedText;
    public GameObject BlueAgentsGoalsCollectedText;
    private float timePassed = 0f;
    private bool notEvaluatedRedYet = true;
    private bool notEvaluatedBlueYet = true;
    private int redGoalsCollected = 0;
    private int blueGoalsCollected = 0;
    private CollectionManager cm;

    public int numberOfAgents = 10;
    public int numberOfGoals = 50;
    public int numberOfGoalsAtSameTime = 5;
    public float minDistanceOfGoalToWall = 0.7f;

    // Stores the positions of the goals
    private Vector3[] redGoals;
    private Vector3[] blueGoals;

    void Start()
    {
        cm = this.GetComponent<CollectionManager>();
        System.Random rnd = new System.Random();

        if (Field1 == null || Field2 == null)
        {
            Debug.LogError("Collection Manager requires both Fields to be assigned!");
            return;
        }

        if (redAgentPrefab == null || blueAgentPrefab == null)
        {
            Debug.LogError("Collection Manager requires both Agent Prefabs to be assigned!");
            return;
        }

        GameObject redAgents = new GameObject("Red Agents");
        redAgents.transform.SetParent(Field1.transform);

        List<Vector3> agentPositions = new List<Vector3>();

        // Used for the random positions of agents & goals. 
        float maxX = (Field1BoundXY.x - Field1BoundNegativeXY.x);
        float maxY = (Field1BoundXY.y - Field1BoundNegativeXY.y);

        redGoals = new Vector3[numberOfGoals];

        // Creates Goals at random position within Field1 without them collding with walls
        for (int index = 0; index < numberOfGoals; index++)
        {
            float x = (float)rnd.NextDouble() * maxX + Field1BoundNegativeXY.x;
            float y = (float)rnd.NextDouble() * maxY + Field1BoundNegativeXY.y;
            Vector3 goalPos = new Vector3(x, y, 0f);
            GameObject goal = Instantiate(GoalPointPrefab, goalPos, new Quaternion());
            CircleCollider2D circleCollider = goal.AddComponent<CircleCollider2D>();
            // Circle Collider Added to ensure a certain distance of goals to the wall. 
            circleCollider.radius = minDistanceOfGoalToWall;
            List<Collider2D> collisions = new List<Collider2D>();
            goal.transform.GetComponent<Rigidbody2D>().OverlapCollider(new ContactFilter2D(), collisions);
            // If goal is not colliding with any walls, then it's position is fine and stored.
            if (collisions.Count == 0)
            {
                redGoals[index] = goalPos;
            }
            // if not, look for anothter position
            else
            {
                index--;
            }

            // Destroyed at the End, because only a few are supposed to exist at start (..)
            // (..) and collisions with eachother aren't supposed to matter.
            GameObject.Destroy(goal);
        }

        // Creates all redAgents at random positions within the PlaySpace without them colliding with walls or eachother
        for (int index = 0; index < numberOfAgents; index++)
        {
            float x = (float)rnd.NextDouble() * maxX + Field1BoundNegativeXY.x;
            float y = (float)rnd.NextDouble() * maxY + Field1BoundNegativeXY.y;
            Vector3 agentPos = new Vector3(x, y, 0f);
            GameObject redAgent = Instantiate(redAgentPrefab, agentPos, new Quaternion());
            redAgent.transform.parent = redAgents.transform;
            redAgent.name = "redAgent" + index;
            List<Collider2D> collisions = new List<Collider2D>();
            redAgent.transform.GetChild(0).GetComponent<Rigidbody2D>().OverlapCollider(new ContactFilter2D(), collisions);
            if (collisions.Count > 0)
            {
                GameObject.Destroy(redAgent);
                index--;
            }
            else
            {
                agentPositions.Add(agentPos);
            }
        }

        blueGoals = new Vector3[numberOfGoals];

        if (symmetrical)
        {
            GameObject blueAgents = new GameObject("Blue Agents");
            blueAgents.transform.SetParent(Field2.transform);

            Vector3 offset = Field2.transform.position - Field1.transform.position;

            for (int index = 0; index < numberOfAgents; index++)
            {
                // position of the red agents + the offset between the fields / rooms
                Vector3 agentPos = agentPositions[index] + offset;

                GameObject blueAgent = Instantiate(blueAgentPrefab, agentPos, new Quaternion());
                blueAgent.transform.parent = blueAgents.transform;
                blueAgent.name = "blueAgent" + index;
            }

            for(int index = 0; index < numberOfGoals; index++)
            {
                // position of the redGoals + the offset between the fields / rooms
                blueGoals[index] = redGoals[index] + offset;
            }
        }
        else
        {

        }

        InitStartingGoals();
    }

    // Update is called once per frame
    void Update()
    {
        // Count the time that's passed
        timePassed += Time.deltaTime;

        // If not evaluated yet and all goals are collected, print the duration.
        if(notEvaluatedRedYet && redGoalsCollected == numberOfGoals)
        {
            notEvaluatedRedYet = false;
            Debug.Log("Red collected all Goals and it took: " + timePassed + " seconds.");
        }

        if(notEvaluatedBlueYet && blueGoalsCollected == numberOfGoals)
        {
            notEvaluatedBlueYet = false;
            Debug.Log("Blue collected all Goals and it took:  " + timePassed + " seconds.");
        }
    }

    public void GoalCollected(GameObject goal)
    {
        // Used to check if it was collected by red or blue Agent. 
        string goalTag = goal.transform.root.tag;
        GameObject.Destroy(goal);

        if (goalTag == "Room1")
        {
            int currentGoalIndex = numberOfGoalsAtSameTime + redGoalsCollected;
            // if not all red goals have been placed, then place another goal, so that
            // the amount of goals on the red field is equal to numberOfGoalsAtSameTime
            if (currentGoalIndex < numberOfGoals)
            {
                GameObject redGoal = Instantiate(GoalPointPrefab, redGoals[currentGoalIndex], new Quaternion());
                redGoal.transform.parent = Field1.transform;
                redGoal.name = "goal" + currentGoalIndex;
                redGoal.GetComponent<Goal>().cm = cm;
            }
            
            redGoalsCollected++;
            RedAgentsGoalsCollectedText.GetComponent<Text>().text = redGoalsCollected.ToString();
        }
        else if ( goalTag == "Room2")
        {
            int currentGoalIndex = numberOfGoalsAtSameTime + blueGoalsCollected;
            // if not all blue goals have been placed, then place another goal. 
            if (currentGoalIndex < numberOfGoals)
            {
                GameObject blueGoal = Instantiate(GoalPointPrefab, blueGoals[currentGoalIndex], new Quaternion());
                blueGoal.transform.parent = Field2.transform;
                blueGoal.name = "goal" + currentGoalIndex;
                blueGoal.GetComponent<Goal>().cm = cm;
            }
            blueGoalsCollected++;
            BlueAgentsGoalsCollectedText.GetComponent<Text>().text = blueGoalsCollected.ToString();
        }
    }

    private void InitStartingGoals()
    {
        if(numberOfGoals < numberOfGoalsAtSameTime)
        {
            Debug.Log("Number of Goals must be >= the number of goals at the same time!");
            numberOfGoalsAtSameTime = numberOfGoals;
        }

        for(int index = 0; index < numberOfGoalsAtSameTime; index++)
        {
            GameObject redGoal = Instantiate(GoalPointPrefab, redGoals[index], new Quaternion());
            redGoal.transform.parent = Field1.transform;
            redGoal.name = "goal" + index;
            // cm is needed for the goal to call the "Goal Collected" function
            redGoal.GetComponent<Goal>().cm = cm;

            GameObject blueGoal = Instantiate(GoalPointPrefab, blueGoals[index], new Quaternion());
            blueGoal.transform.parent = Field2.transform;
            blueGoal.name = "goal" + index;
            blueGoal.GetComponent<Goal>().cm = cm;
        }
    }
}