using UnityEngine;

public class BraceletMenuCity : BraceletMenuCore
{
    [Header("Mode Controllers")]
    [Tooltip("GameObject holding the CameraRingController.")]
    public CameraRingController cameraController;
    [Tooltip("GameObject holding the BuildingGrabber.")]
    public BuildingGrabber buildingGrabber;
    [Tooltip("GameObject holding the HandPointer.")]
    public HandPointer handPointer;
    [Tooltip("GameObject holding the BearODMController.")]
    public BearODMController bearODMController;

    [Tooltip("GameObject holding the PCDImporter.")]
    public PCDImporter pcdImporter;
    private bool hasPCDToImport = false;

    protected override void Start()
    {
        hasPCDToImport     = CheckForPCD();
        string thirdOption = hasPCDToImport ? "Import PCD" : "Bear Grappling";

        menuNames = new string[] { "Traverse", "Move", thirdOption };
        
        base.Start();
    }

    private bool CheckForPCD()
    {
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string[] files     = System.IO.Directory.GetFiles(desktopPath, "*.ply");

        return files.Length > 0;
    }

    protected override void ApplyCurrentMode()
    {
        // Reset everything
        if (cameraController != null)
        {
            cameraController.isRingMode      = false;
            cameraController.isGrapplingMode = false;
            cameraController.isODMMode       = false;
            cameraController.isTraverseMode  = false;
        }

        if (manoReceiver != null) manoReceiver.isGrapplingMode = false;

        if (buildingGrabber != null) buildingGrabber.enabled = false;
        if (handPointer     != null) handPointer.enabled     = false;

        if (bearODMController != null)
        {
            bearODMController.isODMMode = false;
            bearODMController.ForceCleanUp();
            
            if (bearODMController.bearRoot != null)
                bearODMController.bearRoot.gameObject.SetActive(false);
        }

        if (pcdImporter != null) pcdImporter.StopImport();

        // Reset time...
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;

        switch (currentActiveMode)
        {
            case -1:
                // Default Mode: Basic ring camera only...
                if (cameraController != null) cameraController.isRingMode = true;
                break;
            
            case 0:
                // Traverse mode
                if (cameraController != null) cameraController.isTraverseMode = true;
                break;
            
            case 1:
                // Move mode
                if (buildingGrabber != null) buildingGrabber.enabled = true;
                if (handPointer     != null) handPointer.enabled     = true;
                break;
            
            case 2:
                if (hasPCDToImport)
                {
                    // Import PCD mode
                    if (pcdImporter != null) pcdImporter.CheckAndStartImport();
                }
                else
                {
                    // Bear Grappling mode
                    if (cameraController != null) cameraController.isODMMode   = true;
                    if (manoReceiver     != null) manoReceiver.isGrapplingMode = true;

                    if (bearODMController != null)
                    {
                        bearODMController.isODMMode = true;
                        if (bearODMController.bearRoot != null)
                            bearODMController.bearRoot.gameObject.SetActive(true);
                    }
                }
                break;
        }
    }
}
