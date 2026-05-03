using UnityEngine;

public class BraceletMenuPaint : BraceletMenuCore
{
    [Header("Mode Controllers")]

    protected override void ApplyCurrentMode()
    {
        // Reset time...
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;

        switch (currentActiveMode)
        {
            case -1:
            
            case 0:
            
            case 1:
            
            case 2:
            
        }
    }
}
