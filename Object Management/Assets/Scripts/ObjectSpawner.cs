﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectSpawner : PersistableObject
{
    const int buildVersion = 2;

    [SerializeField] private ShapeFactory shapeFactory;
    [SerializeField] private PersistantStorage storage;

    [SerializeField] private float spawnRadius = 5f;

    [SerializeField] private int levelCount;

    [SerializeField] private KeyCode createKey = KeyCode.C;
    [SerializeField] private KeyCode destroyKey = KeyCode.D;
    [SerializeField] private KeyCode newGameKey = KeyCode.N;
    [SerializeField] private KeyCode saveKey = KeyCode.S;
    [SerializeField] private KeyCode loadKey = KeyCode.L;

    private float creationProgress;
    private float destrucionProgress;

    private int loadedLevelBuildIndex;

    private List<Shape> shapes;

    public static ObjectSpawner Instance { get; private set; }

    public float CreationSpeed { get; set; }
    public float DestructionSpeed { get; set; }

    public SpawnZone SpawnZoneOfLevel { get; set; }

    private void Awake()
    {
        //Initializing the list of peristable objects
        shapes = new List<Shape>();
    }

    private void OnEnable()
    {
        //Set this object spawner as the static object spawner instance
        Instance = this;
    }

    private void Start()
    {
        if (Application.isEditor)
        {
            //Loop through all scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                //Get the currently loaded screen
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.name.Contains("Level "))
                {
                    //Set this scene to active if it contains level as string
                    SceneManager.SetActiveScene(loadedScene);
                    //Set the loaded level build index
                    loadedLevelBuildIndex = loadedScene.buildIndex;
                    return;
                }
            }
        }
        StartCoroutine(LoadLevel(1));
    }

    private void Update()
    {
        if (Input.GetKeyDown(createKey))
        {
            CreateShape();
        }
        else if (Input.GetKeyDown(destroyKey))
        {
            DestroyShape();
        }
        else if (Input.GetKeyDown(newGameKey))
        {
            BeginNewGame();
        }
        else if (Input.GetKeyDown(saveKey))
        {
            //Because the ObjectSpawner derives from PersitableObject we can save the spawner in the storage
            storage.Save(this, buildVersion);
        }
        else if (Input.GetKeyDown(loadKey))
        {
            //Beginning a new game before loading so the scene is empty
            BeginNewGame();
            //Then loading the saved spawner from the storage
            storage.Load(this);
        }
        else
        {
            //Loop through all levels
            for (int i = 1; i <= levelCount; i++)
            {
                //And check the all the number keys
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    //if we got a number press begin a new game
                    BeginNewGame();
                    //and start the coroutine to load the level of the corrisponding keypress
                    StartCoroutine(LoadLevel(i));
                    return;
                }
            }
        }

        //Adding time.deltatime to progress to count up every frame multiplied by creationspeed
        creationProgress += Time.deltaTime * CreationSpeed;
        //As long as creationprogress is bigger then 1 we create 1 shape and substract 1 from progress
        while (creationProgress >= 1f)
        {
            creationProgress -= 1f;
            CreateShape();
        }

        //Same logic for the destruction
        destrucionProgress += Time.deltaTime * DestructionSpeed;
        while (destrucionProgress >= 1f)
        {
            destrucionProgress -= 1f;
            DestroyShape();
        }
    }


    void BeginNewGame()
    {
        //Looping through all the objects and destroying their gameobjects
        for (int index = 0; index < shapes.Count; index++)
        {
            //Reclaiming all the shapes when beginning a new game
            shapeFactory.ReclaimShape(shapes[index]);
        }
        //The list still has references to the destroyed objects so its needed to be cleared as well
        shapes.Clear();
    }

    void CreateShape()
    {
        //Instantiate a persistable object
        Shape tempShape = shapeFactory.GetRandomShape();
        //And get the transform of this object to manipulate it
        Transform objectTransform = tempShape.transform;
        //Set the position to a random point in a sphere
        objectTransform.localPosition = SpawnZoneOfLevel.SpawnPoint;
        //Give it a random rotation
        objectTransform.localRotation = Random.rotation;
        //And scale
        objectTransform.localScale = Vector3.one * Random.Range(0.1f, 1f);
        //Set the color of the Shape
        tempShape.SetColor(Random.ColorHSV(
            hueMin: 0f, hueMax: 1f,
            saturationMin: 0.8f, saturationMax: 1f,
            valueMin: 0.5f, valueMax: 1f,
            alphaMin: 1f, alphaMax: 1f
        ));
        //And add it to our list of persistable objects
        shapes.Add(tempShape);
    }

    void DestroyShape()
    {
        if (shapes.Count > 0)
        {
            //Get a random index
            int index = Random.Range(0, shapes.Count);
            //Destroy the gameobject
            shapeFactory.ReclaimShape(shapes[index]);
            //Getting the last index of the array
            int lastIndex = shapes.Count - 1;
            //To shift the last shape to the spot we just removed from
            shapes[index] = shapes[lastIndex];
            //And then we remove the last shape
            shapes.RemoveAt(lastIndex);
        }
    }

    public override void Save(GameDataWriter writer)
    {
        //Write the current count of objects to the savefile
        writer.Write(shapes.Count);
        //Write the currently loaded level to the savefile
        writer.Write(loadedLevelBuildIndex);
        for (int i = 0; i < shapes.Count; i++)
        {
            //Write the shapeid of the current shape to the file 
            writer.Write(shapes[i].ShapeId);
            //Write the matrial id
            writer.Write(shapes[i].MaterialId);
            //and call the save method to store positon rotation and scale of the current shape in the savefile
            shapes[i].Save(writer);
        }
    }

    public override void Load(GameDataReader reader)
    {
        //Read the first int and flip it to do the version check
        int saveVersion = reader.VersionControl;
        //If the version is bigger then the saveVersion 
        if (saveVersion > buildVersion)
        {
            Debug.LogError("Unsupported future save version" + saveVersion + ". Currently running" + buildVersion);
            return;
        }
        int count;
        //If the first read value is smaller or equal to 0
        if (saveVersion <= 0)
        {
            //We set the count to the first value because it was the count
            count = -saveVersion;
        }
        else
        {
            //Else we read the count now because we read a version number
            count = reader.ReadInt();
        }
        //Set the level index to 1 if the saveVersion is smaller then 2 otherwise get the saved level from the memory
        StartCoroutine(LoadLevel(saveVersion < 2 ? 1 : reader.ReadInt()));
        //For every saved object
        for (int i = 0; i < count; i++)
        {
            //If the saveVersion is bigger then 0 read the shapeId from the memory otherwise just set it to 0
            int shapeId = saveVersion > 0 ? reader.ReadInt() : 0;
            //If the saveVersion is bigger then 0 read the materialId from the memory otherwise just set it to 0
            int materialId = saveVersion > 0 ? reader.ReadInt() : 0;
            //Instantiate a temporary persistable object
            Shape tempShape = shapeFactory.GetShape(shapeId, materialId);
            //Use the load function on the Shape object to retrieve the saved information
            tempShape.Load(reader);
            //And add the loaded object to the list
            shapes.Add(tempShape);
        }
    }

    IEnumerator LoadLevel(int levelBuildIndex)
    {
        //Disabling the objectspawner while loading the level
        enabled = false;
        //If loadedLevelBuildIndex is bigger then 0 we have loaded a scene already
        if(loadedLevelBuildIndex > 0)
        {
            //So unload the scene and yield until the unloading is finished
            yield return SceneManager.UnloadSceneAsync(loadedLevelBuildIndex);
        }
        //Loading the scene asynchronously and yielding until the scene is fully loaded
        yield return SceneManager.LoadSceneAsync(levelBuildIndex, LoadSceneMode.Additive);
        //then set this scene as active
        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(levelBuildIndex));
        //and set the loadedLevelSceneIndex to the index of the level we just load
        loadedLevelBuildIndex = levelBuildIndex;
        //Enabling the objectspawner again after loading is finished
        enabled = true;
    }

}
