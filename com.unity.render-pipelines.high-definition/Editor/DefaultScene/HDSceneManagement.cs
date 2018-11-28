using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;
using System;
using System.Reflection;
using System.Linq.Expressions;

[InitializeOnLoad]
public class HDSceneManagement : UnityEditor.AssetPostprocessor
{
    static Func<string, bool> s_CreateEmptySceneAsset;

    static HDSceneManagement()
    {
        EditorSceneManager.newSceneCreated += NewSceneCreated;
        
        var scenePathProperty = Expression.Parameter(typeof(string), "scenePath");
        var createSceneAssetInfo = typeof(EditorSceneManager)
            .GetMethod(
                "CreateSceneAsset",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                CallingConventions.Any,
                new[] { typeof(string), typeof(bool) },
                null);
        var createSceneAssetCall = Expression.Call(
            createSceneAssetInfo,
            scenePathProperty,
            Expression.Constant(false)
            );
        var lambda = Expression.Lambda<Func<string, bool>>(createSceneAssetCall, scenePathProperty);
        s_CreateEmptySceneAsset = lambda.Compile();
    }

    static void NewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
    {
        if (!InHDRP())
            return; // do not interfere outside of hdrp

        if(setup == NewSceneSetup.DefaultGameObjects)
        {
            ClearScene(scene);
            FillScene(scene);
        }
    }


    [MenuItem("File/New Empty Scene", true, 148)]
    [MenuItem("File/New Empty Scene Additive", true, 149)]
    [MenuItem("Assets/Create/Empty Scene", true, 200)]
    static bool InHDRP()
    {
        return GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset;
    }

    [MenuItem("File/New Empty Scene", false, 148)]
    static void CreateEmptyScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
    }

    [MenuItem("File/New Empty Scene Additive", false, 149)]
    static void CreateEmptySceneAdditive()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }

    class DoCreateScene : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            if (s_CreateEmptySceneAsset(pathName))
            {
                UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath(pathName, typeof(SceneAsset));
                ProjectWindowUtil.ShowCreatedAsset(sceneAsset);
            }
        }
    }

    [MenuItem("Assets/Create/Empty Scene", false, 200)]
    static void CreateEmptySceneAsset()
    {
        //cannot use ProjectWindowUtil.CreateScene() as it will fill the scene with Default
        var icon = EditorGUIUtility.FindTexture("SceneAsset Icon");
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateScene>(), "New Scene.unity", icon, null);
    }

    static void ClearScene(Scene scene)
    {
        GameObject[] gameObjects = scene.GetRootGameObjects();
        for (int index = gameObjects.Length - 1; index >= 0; --index)
        {
            GameObject.DestroyImmediate(gameObjects[index]);
        }
    }
    
    static void FillScene(Scene scene)
    {
        HDRenderPipelineAsset hdrpAsset = (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset);
        if (hdrpAsset == null || hdrpAsset.Equals(null))
            return;

        if (hdrpAsset.renderPipelineEditorResources == null)
            Debug.LogError("Missing HDRenderPipelineEditorResources in HDRenderPipelineAsset");

        GameObject root = GameObject.Instantiate(hdrpAsset.renderPipelineEditorResources.defaultScene);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.DetachChildren();
        GameObject.DestroyImmediate(root);
    }

    //workaround while newSceneCreated event is not raised in the Project Browser
    void OnPreprocessAsset()
    {
        if (!InHDRP())
            return;

        if (assetImporter.assetPath.EndsWith(".unity"))
        {
            Scene scene = EditorSceneManager.OpenScene(assetImporter.assetPath, OpenSceneMode.Additive);
            GameObject[] gameObjects = scene.GetRootGameObjects();

            //hard check if its default template configuration
            bool isDefaultTemplate = gameObjects.Length == 2;
            if (isDefaultTemplate)
            {
                isDefaultTemplate &= gameObjects[0].name == "Main Camera";
                isDefaultTemplate &= gameObjects[1].name == "Directional Light";
            }
            if (isDefaultTemplate)
            {
                Component[] cameraComponents = gameObjects[0].transform.GetComponents<Component>();
                isDefaultTemplate &= cameraComponents.Length == 3;
                if (isDefaultTemplate)
                {
                    isDefaultTemplate &= cameraComponents[0] is Transform;
                    isDefaultTemplate &= cameraComponents[1] is Camera;
                    isDefaultTemplate &= cameraComponents[2] is AudioListener;
                }
            }
            if (isDefaultTemplate)
            {
                Component[] lightComponents = gameObjects[1].transform.GetComponents<Component>();
                isDefaultTemplate &= lightComponents.Length == 2;
                if (isDefaultTemplate)
                {
                    isDefaultTemplate &= lightComponents[0] is Transform;
                    isDefaultTemplate &= lightComponents[1] is Light;
                }
            }

            if (isDefaultTemplate)
            {
                ClearScene(scene);
                FillScene(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
