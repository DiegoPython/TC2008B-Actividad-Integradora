
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

[Serializable]
public class ModelData // Class for model data 
{
    public string message;
    public string steps;
    public bool finished;
    // Attributes
    public ModelData(string message, string steps, bool finished)
    {
        this.message = message;
        this.steps = steps;
        this.finished = finished;
    }
}

[Serializable]
public class RobotData // Class for robot data
{
    // Attributes
    public string id;
    public string steps;
    public string grabbedBoxes;

    public RobotData(string id, string steps, string grabbedBoxes)
    {
        this.id = id;
        this.steps = steps;
        this.grabbedBoxes = grabbedBoxes;
    }
}

[Serializable]
public class RunData // Class for run data
{
    public List<RobotData> data;

    public RunData() => this.data = new List<RobotData>();
}
[Serializable]
public class AgentData // Class for agent data
{
    // Attributes
    public string id;
    public float x, y, z;
    public bool inStation;
    public int numBoxes;

    public AgentData(string id, float x, float y, float z)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.z = z;
        this.inStation = false;
        this.numBoxes = 0;
    }
}

[Serializable]

public class AgentsData // agents data 
{
    public List<AgentData> positions;

    public AgentsData() => this.positions = new List<AgentData>();
}

public class AgentController : MonoBehaviour // Class agent controller
{
    // private string url = "https://agents.us-south.cf.appdomain.cloud/";
    // Define variables 
    // Endpoints
    string serverUrl = "http://localhost:50000";
    string getAgentsEndpoint = "/getAgents";
    string getObstaclesEndpoint = "/getObstacles";
    string getStationsEndpoint = "/getStations";
    string getBoxesEndpoint = "/getBoxes";
    string sendConfigEndpoint = "/init";
    string updateEndpoint = "/update";
    string getDataEndpoint = "/runData";
    // Data instances
    AgentsData agentsData, obstacleData, stationData, boxData;
    RunData runData;
    ModelData modelData;
    // Dictionaries for agents and positions
    Dictionary<string, GameObject> agents;
    Dictionary<string, Vector3> prevPositions, currPositions;

    // Simulation states
    bool updated = false, started = false, finished = false;

    // Prefabs
    public GameObject agentPrefab, obstaclePrefab, floor, stationPrefab, boxPrefab;
    // Timer text
    public GameObject timerText;
    // params
    public int NAgents, width, height, box_num;
    // Time for simulation
    public float timeToUpdate = 5.0f;
    public float timeElapsed = 0;
    private float timer, dt;

    // Initialize
    void Start()
    {
        // initialize data
        agentsData = new AgentsData();
        obstacleData = new AgentsData();
        stationData = new AgentsData();
        boxData = new AgentsData();

        // Positions dictionaries
        prevPositions = new Dictionary<string, Vector3>();
        currPositions = new Dictionary<string, Vector3>();

        // agents dictionary
        agents = new Dictionary<string, GameObject>();

        // Scale floor 
        floor.transform.localScale = new Vector3((float)width / 10, 1, (float)height / 10);
        floor.transform.localPosition = new Vector3((float)width / 2 - 0.5f, 0.5f, (float)height / 2 - 0.5f);

        timer = timeToUpdate;
        // Send configuration coroutine
        StartCoroutine(SendConfiguration());
    }

    // update
    private void Update()
    {
        
        if (timer < 0)
        {
            timer = timeToUpdate;
            updated = false;
            StartCoroutine(UpdateSimulation());
        }
        // 
        if (updated)
        {
            if (finished)
            {
                StartCoroutine(GetRobotsData());
            }
            else
            {
                timeElapsed += Time.deltaTime;
                timer -= Time.deltaTime;
                dt = 1.0f - (timer / timeToUpdate);

                timerText.GetComponent<Text>().text = "Time Elapsed: " + Math.Round(timeElapsed, 2).ToString() + " s";

                foreach (var agent in currPositions)
                {
                    Vector3 currentPosition = agent.Value;
                    Vector3 previousPosition = prevPositions[agent.Key];

                    Vector3 interpolated = Vector3.Lerp(previousPosition, currentPosition, dt);
                    Vector3 direction = currentPosition - interpolated;

                    agents[agent.Key].transform.localPosition = interpolated;
                    //if (direction != Vector3.zero) agents[agent.Key].transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            

            // float t = (timer / timeToUpdate);
            // dt = t * t * ( 3f - 2f*t);
        }
    }

    // Update simulaton co routine 
    IEnumerator UpdateSimulation()
    {
        // Call endpoint
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + updateEndpoint);
        yield return www.SendWebRequest();
        // catch error
        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        // If there is no error
        else
        {
            modelData = JsonUtility.FromJson<ModelData>(www.downloadHandler.text);
            finished = modelData.finished;
            // Start coroutines
            StartCoroutine(GetAgentsData());
            StartCoroutine(GetBoxData());
            StartCoroutine(GetStationData());

        }
    }

    IEnumerator SendConfiguration()
    {
        WWWForm form = new WWWForm();

        form.AddField("NAgents", NAgents.ToString());
        form.AddField("width", width.ToString());
        form.AddField("height", height.ToString());
        form.AddField("box_num", box_num.ToString());

        UnityWebRequest www = UnityWebRequest.Post(serverUrl + sendConfigEndpoint, form);
        www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log("Configuration upload complete!");
            Debug.Log("Getting Agents positions");
            StartCoroutine(GetAgentsData());
            StartCoroutine(GetObstacleData());
            StartCoroutine(GetStationData());
            StartCoroutine(GetBoxData());
        }
    }

    IEnumerator GetAgentsData()
    {
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + getAgentsEndpoint);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        else
        {
            agentsData = JsonUtility.FromJson<AgentsData>(www.downloadHandler.text);

            foreach (AgentData agent in agentsData.positions)
            {
                Vector3 newAgentPosition = new Vector3(agent.x, agent.y, agent.z);

                if (!started)
                {
                    prevPositions[agent.id] = newAgentPosition;
                    agents[agent.id] = Instantiate(agentPrefab, newAgentPosition, agentPrefab.transform.rotation);
                }
                else
                {
                    Vector3 currentPosition = new Vector3();
                    if (currPositions.TryGetValue(agent.id, out currentPosition))
                        prevPositions[agent.id] = currentPosition;
                    currPositions[agent.id] = newAgentPosition;
                }
            }
            updated = true;
        }
    }

    IEnumerator GetBoxData()
    {
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + getBoxesEndpoint);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        else
        {
            boxData = JsonUtility.FromJson<AgentsData>(www.downloadHandler.text);
          
            foreach (AgentData agent in boxData.positions)
            {
                Vector3 newAgentPosition = new Vector3(agent.x, agent.y, agent.z);
       
                if (!started)
                {
                    //Debug.Log("Box Agent ID: " + agent.id);
                    prevPositions[agent.id] = newAgentPosition;
                    agents[agent.id] = Instantiate(boxPrefab, newAgentPosition, boxPrefab.transform.rotation);
                }
                else
                {
                    if(agent.inStation)
                    {
                        agents[agent.id].SetActive(false);
                    }
                    else
                    {
                        Vector3 currentPosition = new Vector3();
                        if (currPositions.TryGetValue(agent.id, out currentPosition))
                            prevPositions[agent.id] = currentPosition;
                        currPositions[agent.id] = newAgentPosition;
                    }
                }
            }

            updated = true;
            if (!started) started = true;
        }
    }

    IEnumerator GetObstacleData()
    {
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + getObstaclesEndpoint);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        else
        {
            obstacleData = JsonUtility.FromJson<AgentsData>(www.downloadHandler.text);
            //Debug.Log(obstacleData.positions);

            foreach (AgentData obstacle in obstacleData.positions)
            {
                Instantiate(obstaclePrefab, new Vector3(obstacle.x, obstacle.y, obstacle.z), obstaclePrefab.transform.rotation);
            }
        }
    }

    IEnumerator GetStationData()
    {
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + getStationsEndpoint);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        else
        {
            stationData = JsonUtility.FromJson<AgentsData>(www.downloadHandler.text);

            //Debug.Log(stationData.positions);

            foreach (AgentData station in stationData.positions)
            {
                //Debug.Log("Num Boxes of Station " + station.id + ": " + station.numBoxes);
                if (station.numBoxes == 0)
                {
                    Instantiate(stationPrefab, new Vector3(station.x, station.y, station.z), Quaternion.identity);
                }
                else
                {
                    Instantiate(boxPrefab, new Vector3(station.x, station.y+(station.numBoxes)-0.5f, station.z), Quaternion.identity);
                }
                
            }

            updated = true;
        }
    }

    IEnumerator GetRobotsData()
    {
        UnityWebRequest www = UnityWebRequest.Get(serverUrl + getDataEndpoint);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.Log(www.error);
        else
        {
            runData = JsonUtility.FromJson<RunData>(www.downloadHandler.text);
            //Debug.Log(runData.ToString());

            foreach (RobotData robotData in runData.data)
            {
                TextMesh robotText = (TextMesh)agents[robotData.id].transform.GetChild(0).gameObject.GetComponent(typeof(TextMesh));
                robotText.text = $"Setps: {robotData.steps} {Environment.NewLine} Boxes: {robotData.grabbedBoxes}";
            }
        }
    }

}