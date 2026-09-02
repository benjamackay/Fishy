using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quests/Quest")]
public class Quest : ScriptableObject
{
    public string questId;
    public string questName;
    public string Description;
    public List<QuestObjective> objectives;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(questId))
        {
            questId = questName + Guid.NewGuid().ToString();
        }
    }
    [System.Serializable]
    public class QuestObjective
    {
        public string objectiveID;
        public string description;
        public ObjectiveType type;
        public int requiredAmount;
        public int currentAmount;
        public bool IsCompleted => currentAmount >= requiredAmount;
    }
    public enum ObjectiveType {CollectItem, DefeatEnemy, ReachLocation, TalkNPC, Custom}

    [System.Serializable]
    public class QuestProgress
    {
        public Quest quest;
        public List<QuestObjective> objectives;
        public QuestProgress(Quest quest)
        {
            this.quest = quest;
            objectives = new List<QuestObjective>();

            foreach(var obj in quest.objectives)
            {
                objectives.Add(new QuestObjective
                {
                    objectiveID = obj.objectiveID,
                    description = obj.description,
                    type = obj.type,
                    requiredAmount = obj.requiredAmount,
                    currentAmount = 0
                });
            }
        }
        public bool IsCompleted => objectives.TrueForAll(o => o.IsCompleted);
        public string QuestId => quest.questId;
    }
}
