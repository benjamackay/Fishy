using UnityEngine;

public static class GlobalHelper
{
    public static string GenerateUniqueID(GameObject obj)
    {
        return $"{obj.scene}_{obj.transform.position.x}_{obj.transform.position.y}"; 
    }
}
