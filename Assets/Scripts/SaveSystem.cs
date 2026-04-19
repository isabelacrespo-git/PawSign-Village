using UnityEngine;
using System.IO;

public class SaveSystem
{
    private string filePath = "./";
    private string positionFile = "posTracker.txt";

    public void Save() 
    {
        SavePosition();
    }

    public void SavePosition()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        Debug.Log("Saved position to:" + filePath + positionFile);

        File.Delete(Path.Combine(filePath, positionFile));

        using (StreamWriter output = new StreamWriter(Path.Combine(filePath, positionFile), true))
        {
            output.WriteLine(player.position);
        }
    }

    public void Load()
    {
        LoadPosition();
    }

    public void LoadPosition()
    {
        string line = File.ReadAllLines(Path.Combine(filePath, positionFile))[0];
        line = line.Trim('(', ')');
        string[] coordinates = line.Split(",");
        
        float x = float.Parse(coordinates[0].Trim());
        float y = float.Parse(coordinates[1].Trim());
        float z = float.Parse(coordinates[2].Trim());

        Vector3 position = new Vector3(x, y, z);

        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        player.position = position;

        Debug.Log("Loaded position: " + position);
    }
}
