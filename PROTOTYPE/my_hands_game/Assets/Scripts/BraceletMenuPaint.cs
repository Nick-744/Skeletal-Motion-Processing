using UnityEngine;

public class BraceletMenuPaint : BraceletMenuCore
{
    [Header("Mode Controllers")]
    [Tooltip("GameObject with the LaserPainter script.")]
    public LaserPainter laserPainter;

    protected override void Start()
    {
        menuNames = new string[] { "Erase", "Save", "Lathe Toggle" };
        
        base.Start();
    }

    protected override void ExecuteSelection(int index)
    {
        // Save must work like a button, not a mode change!
        if (index == 1)
        {
            if (laserPainter != null) laserPainter.SaveDrawing();

            return;
        }

        // Lathe Toggle must work like a button, not a mode change!
        if (index == 2)
        {
            if (laserPainter != null)
                laserPainter.showRotationAxis = !laserPainter.showRotationAxis;

            return;
        }

        base.ExecuteSelection(index);
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
                // Erase mode
                laserPainter.currentMode = LaserPainter.PaintMode.Erase;
                break;
        }
    }
}
