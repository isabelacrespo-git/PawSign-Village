using UnityEngine;

[CreateAssetMenu(fileName = "NPCLessonData", menuName = "PawSign/NPC Lesson Data")]
public class NPCLessonData : ScriptableObject
{
    public string npcName;
    [TextArea(2, 6)]
    public string[] dialogue;
    public string[] lessonSigns;
    public string successFormat = "Good job! That was {0}.";
}
