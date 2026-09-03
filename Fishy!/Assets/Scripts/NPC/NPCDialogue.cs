using UnityEngine;

[CreateAssetMenu(fileName ="NewNPCDialogue", menuName = "NPC Dialogue")]
public class NPCDialogue : ScriptableObject
{
    public string npcName;
    public Sprite npcPortrait;
    public string[] dialogueLines;
    public bool[] autoProgressLine;
    public float typingSpeed = 0.05f;
    public AudioClip voiceSound;
    public float voicePitch;

    public float autoProgressDelay;
}
