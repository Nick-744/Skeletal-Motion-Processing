// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "OSCServer.h"
#include "OSCMessage.h"
#include "Components/PoseableMeshComponent.h"
#include "ManoTracker.generated.h"

UCLASS()
class MY_HANDS_GAME_API AManoTracker : public AActor
{
	GENERATED_BODY()
	
public:	
	// Sets default values for this actor's properties
	AManoTracker();

protected:
	// Called when the game starts or when spawned
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

public:	
	// Called every frame
	virtual void Tick(float DeltaTime) override;

	// Components to hold and animate the 3D hand models
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Components")
	USceneComponent* RootSceneComponent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Components")
	UPoseableMeshComponent* LeftHandMesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Components")
	UPoseableMeshComponent* RightHandMesh;

	// OSC Server -> listen to Python
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "OSC")
	UOSCServer* OSCServer;

	// Callback function for incoming OSC data
	UFUNCTION()
	void OnOSCMessageReceived(const FOSCMessage& Message, const FString& IPAddress, int32 Port);

	// A helper array to store the names of your MANO bones in the correct order
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "MANO Settings")
	TArray<FName> ManoBoneNames;
};
