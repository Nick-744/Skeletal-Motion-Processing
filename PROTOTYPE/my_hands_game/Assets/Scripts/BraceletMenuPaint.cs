using UnityEngine;

public class BraceletMenuPaint : BraceletMenuCore
{
    [Header("Mode Controllers")]
    [Tooltip("GameObject with the LaserPainter script.")]
    public LaserPainter laserPainter;

    protected override void Start()
    {
        menuNames = new string[] { "Erase", "Empty 1", "Empty 2" };
        
        base.Start();
    }

    protected override void ApplyCurrentMode()
    {
        if (laserPainter == null) return;

        switch (currentActiveMode)
        {
            case -1:
                // Default case - Painting mode
                laserPainter.currentMode = LaserPainter.PaintMode.Paint;
                break;
            
            case 0:
                // Erase
                laserPainter.currentMode = LaserPainter.PaintMode.Erase;
                break;
            
            case 1:
                laserPainter.currentMode = LaserPainter.PaintMode.Paint;
                break;
            
            case 2:
                laserPainter.currentMode = LaserPainter.PaintMode.Paint;
                break;
        }
    }
}
