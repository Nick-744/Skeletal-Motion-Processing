// Fill out your copyright notice in the Description page of Project Settings.


#include "ManoTracker.h"
#include "OSCManager.h"
#include "Math/Quat.h"

// Sets default values
AManoTracker::AManoTracker()
{
 	// Set this actor to call Tick() every frame.  You can turn this off to improve performance if you don't need it.
	PrimaryActorTick.bCanEverTick = true;

	// Create a root component
	RootSceneComponent = CreateDefaultSubobject<USceneComponent>(TEXT("RootComponent"));
	RootComponent = RootSceneComponent;

	// Create the Poseable Meshes
	LeftHandMesh = CreateDefaultSubobject<UPoseableMeshComponent>(TEXT("LeftHandMesh"));
	LeftHandMesh->SetupAttachment(RootComponent);

	RightHandMesh = CreateDefaultSubobject<UPoseableMeshComponent>(TEXT("RightHandMesh"));
	RightHandMesh->SetupAttachment(RootComponent);
}

// Called when the game starts or when spawned
void AManoTracker::BeginPlay()
{
	Super::BeginPlay();
	
	// Initialize the OSC Server
	// The Python script is sending to 127.0.0.1:8000
	OSCServer = UOSCManager::CreateOSCServer(TEXT("127.0.0.1"), 8000, false, true, TEXT("ManoServer"), this);

	if (OSCServer)
	{
		// Bind custom function to the server's receive event
		OSCServer->OnOscMessageReceived.AddDynamic(this, &AManoTracker::OnOSCMessageReceived);

		// Start listening
		OSCServer->Listen();
		UE_LOG(LogTemp, Warning, TEXT("ManoTracker: OSC Server listening on port 8000."));
	}
	else
	{
		UE_LOG(LogTemp, Error, TEXT("ManoTracker: Failed to create OSC Server!"));
	}
}

void AManoTracker::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	// Clean up the server when the game stops
	if (OSCServer)
	{
		OSCServer->Stop();
		OSCServer->OnOscMessageReceived.RemoveDynamic(this, &AManoTracker::OnOSCMessageReceived);
	}

	Super::EndPlay(EndPlayReason);
}

// Called every frame
void AManoTracker::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

}

void AManoTracker::OnOSCMessageReceived(const FOSCMessage& Message, const FString& IPAddress, int32 Port)
{
	// Get the address
	FString Address = UOSCManager::GetOSCMessageAddress(Message).GetFullPath();

	if (Address == TEXT("/mano/left/root"))
	{
		TArray<float> RootData;
		UOSCManager::GetAllFloats(Message, RootData);

		if (RootData.Num() >= 3)
		{
			// Note: Unreal uses a Left-Handed Z-Up coordinate system.
			FVector LeftRootPos(RootData[0], RootData[1], RootData[2]);
			LeftHandMesh->SetRelativeLocation(LeftRootPos);
			UE_LOG(LogTemp, Log, TEXT("Left Root Received: %s"), *LeftRootPos.ToString());
		}
	}
	else if (Address == TEXT("/mano/left/pose"))
	{
		TArray<float> PoseData;
		UOSCManager::GetAllFloats(Message, PoseData);
		

		// MANO provides 15 joints * 3 floats = 45 floats
		if (PoseData.Num() == 45 && ManoBoneNames.Num() >= 15)
		{
			for (int i = 0; i < 15; i++)
			{
				int32 DataIndex = i * 3;

				// Assuming Axis-Angle representation from MANO - you will need to map this to an FRotator
				FVector AxisAngle(PoseData[DataIndex], PoseData[DataIndex + 1], PoseData[DataIndex + 2]);

				// Placeholder conversion: You will need to write a math helper to convert MANO Axis-Angle to Unreal FRotator
				FRotator BoneRotation = FRotator::MakeFromEuler(AxisAngle);

				// Apply to the specific bone
				LeftHandMesh->SetBoneRotationByName(ManoBoneNames[i], BoneRotation, EBoneSpaces::ComponentSpace);
			}
		}
	}
	else if (Address == TEXT("/mano/right/root"))
	{
		TArray<float> RootData;
		UOSCManager::GetAllFloats(Message, RootData);

		if (RootData.Num() >= 3)
		{
			FVector RightRootPos(RootData[0], RootData[1], RootData[2]);
			RightHandMesh->SetRelativeLocation(RightRootPos);
			UE_LOG(LogTemp, Log, TEXT("Right Root Received: %s"), *RightRootPos.ToString());
		}
	}
	else if (Address == TEXT("/mano/right/pose"))
	{
		TArray<float> PoseData;
		UOSCManager::GetAllFloats(Message, PoseData);
		
		if (PoseData.Num() == 45 && ManoBoneNames.Num() >= 15)
		{
			for (int i = 0; i < 15; i++)
			{
				int32 DataIndex = i * 3;
				FVector AxisAngle(PoseData[DataIndex], PoseData[DataIndex + 1], PoseData[DataIndex + 2]);
				FRotator BoneRotation = FRotator::MakeFromEuler(AxisAngle);
				RightHandMesh->SetBoneRotationByName(ManoBoneNames[i], BoneRotation, EBoneSpaces::ComponentSpace);
			}
		}
	}
}
