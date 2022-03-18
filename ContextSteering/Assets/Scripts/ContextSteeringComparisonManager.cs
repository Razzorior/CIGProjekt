using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ContextSteeringComparisonManager : MonoBehaviour
{

    List<Vector2> Field1EvacuationPoints = new List<Vector2> { new Vector2(-14.5f, 18.5f), new Vector2(8f, -10f) };
    public GameObject Field1;
    public Vector2 Field1BoundXY;
    public Vector2 Field1BoundNegativeXY;

    public GameObject Field2;
    public bool symmetrical = true;

    public GameObject redAgentPrefab;
    public GameObject blueAgentPrefab;
    public GameObject EvacuationPointPrefab;

    // Variables needed to track Evacuation Speed
    public GameObject RedAgentsEvacuatedText;
    public GameObject RedAgentsEvacuationTimeText;
    public GameObject BlueAgentsEvacuatedText;
    public GameObject BlueAgentsEvacuationTimeText;
    private List<GameObject> redAgentList = new List<GameObject>();
    private List<GameObject> blueAgentList = new List<GameObject>();
    private float redAgentTotalTime = 0f;
    private float blueAgentTotalTime = 0f;
    private float timePassed = 0f;
    private bool notEvaluatedYet = true;


    public int numberOfAgents = 10;

    // Start is called before the first frame update

    void Start()
    {
        System.Random rnd = new System.Random();

        if (Field1 == null || Field2 == null)
        {
            Debug.LogError("Context Steering Comparison Manager requires both Fields to be assigned!");
            return;
        }

        if (redAgentPrefab == null || blueAgentPrefab == null)
        {
            Debug.LogError("Context Steering Comparison Manager requires both Agent Prefabs to be assigned!");
            return;
        }

        GameObject redAgents = new GameObject("Red Agents");
        redAgents.transform.SetParent(Field1.transform);

        List<Vector3> agentPositions = new List<Vector3>();

        float maxX = (Field1BoundXY.x - Field1BoundNegativeXY.x);
        float maxY = (Field1BoundXY.y - Field1BoundNegativeXY.y);
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
                redAgent.GetComponent<ContextSteering_Interpol>().evacuationMode = true;
                redAgent.GetComponent<ContextSteering_Interpol>().evacuationPoints = Field1EvacuationPoints;
                redAgentList.Add(redAgent);
                agentPositions.Add(agentPos);
            }
        }

        foreach (Vector2 v in Field1EvacuationPoints)
        {
            GameObject go = Instantiate(EvacuationPointPrefab, v, new Quaternion());
            go.transform.SetParent(Field1.transform);
            go.GetComponent<EvacuationPoint>().cscm = this.GetComponent<ContextSteeringComparisonManager>();
        }

        if (symmetrical)
        {
            GameObject blueAgents = new GameObject("Blue Agents");
            blueAgents.transform.SetParent(Field2.transform);

            Vector3 offset = Field2.transform.position - Field1.transform.position;

            List<Vector2> Field2EvacuationPoints = new List<Vector2>();

            foreach (Vector2 vec in Field1EvacuationPoints)
            {
                Vector2 newVec = vec + new Vector2(offset.x, offset.y);
                Field2EvacuationPoints.Add(newVec);
                GameObject go = Instantiate(EvacuationPointPrefab, newVec, new Quaternion());
                go.transform.SetParent(Field1.transform);
                go.GetComponent<EvacuationPoint>().cscm = this.GetComponent<ContextSteeringComparisonManager>(); 
            }

            for (int index = 0; index < numberOfAgents; index++)
            {
                Vector3 agentPos = agentPositions[index] + offset;

                GameObject blueAgent = Instantiate(blueAgentPrefab, agentPos, new Quaternion());
                blueAgent.transform.parent = blueAgents.transform;
                blueAgent.name = "blueAgent" + index;
                blueAgent.GetComponent<ContextSteering_DempsterShafer>().evacuationMode = true;
                blueAgent.GetComponent<ContextSteering_DempsterShafer>().evacuationPoints = Field2EvacuationPoints;
                blueAgentList.Add(blueAgent);
            }
        }
        else
        {

        }
    }

    // Update is called once per frame
    void Update()
    {
        timePassed += Time.deltaTime;

        if(timePassed >= 60 && notEvaluatedYet)
        {
            notEvaluatedYet = false;
            EvaluateEvacuation();
        }
    }

    public void AgentEvacuated(int instanceID)
    {
        bool agentFound = false;
        GameObject go = null;

        foreach (GameObject agent in redAgentList)
        {
            if (agent.GetInstanceID() == instanceID)
            {
                agentFound = true;
                go = agent;
                break;
            }
        }

        if (agentFound)
        {
            redAgentTotalTime += timePassed;
            redAgentList.Remove(go);
            SetRedEvacuationText();
            return;
        }

        foreach (GameObject agent in blueAgentList)
        {
            if (agent.GetInstanceID() == instanceID)
            {
                agentFound = true;
                go = agent;
                break;
            }
        }

        if (agentFound)
        {
            blueAgentTotalTime += timePassed;
            blueAgentList.Remove(go);
            SetBlueEvacuationText();
            return;
        }
    }

    private void SetRedEvacuationText()
    {
        int agentsEvacuated = numberOfAgents - redAgentList.Count;
        RedAgentsEvacuatedText.GetComponent<Text>().text = agentsEvacuated.ToString();
        RedAgentsEvacuationTimeText.GetComponent<Text>().text = (redAgentTotalTime / agentsEvacuated).ToString();
    }

    private void SetBlueEvacuationText()
    {
        int agentsEvacuated = numberOfAgents - blueAgentList.Count;
        BlueAgentsEvacuatedText.GetComponent<Text>().text = agentsEvacuated.ToString();
        BlueAgentsEvacuationTimeText.GetComponent<Text>().text = (blueAgentTotalTime / agentsEvacuated).ToString();
    }

    private void EvaluateEvacuation()
    {
        int agentsLeft = redAgentList.Count;

        Debug.Log("Number of Red Agents that where not evacuated: " + agentsLeft);
        Debug.Log("Average time (seconds) for Red Agents to evacuate: " + redAgentTotalTime / (numberOfAgents - agentsLeft));


        agentsLeft = blueAgentList.Count;

        Debug.Log("Number of Blue Agents that where not evacuated: " + agentsLeft);
        Debug.Log("Average time (seconds) for Blue Agents to evacuate: " + blueAgentTotalTime / (numberOfAgents - agentsLeft));
    }
}