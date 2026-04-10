using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "Inventory/Skill")]
public class SkillData : ScriptableObject
{
    public string skillID;
    public string skillName;
    public string phase;
    [TextArea(2, 4)]
    public string description;
    [TextArea(2, 4)]
    public string usage;
    public Sprite icon;
}
