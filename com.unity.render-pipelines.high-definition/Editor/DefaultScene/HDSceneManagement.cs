using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;

[InitializeOnLoad]
public class HDSceneManagement : UnityEditor.AssetPostprocessor
{
    static HDSceneManagement()
    {
        EditorSceneManager.newSceneCreated += NewSceneCreated;
        EditorSceneManager.sceneOpened += FillAndSave;
    }

    static void NewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
    {
        if (!(GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset))
            return; // do not interfere outside of hdrp

        ClearScene(scene);
        SceneFillerConditional(scene, (OpenSceneMode)mode);
    }

    static void FillAndSave(Scene scene, OpenSceneMode mode)
    {
        if (!(GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset))
            return; // do not interfere outside of hdrp

        if (SceneFillerConditional(scene, mode))
            EditorSceneManager.SaveScene(scene);
    }

    static void ClearScene(Scene scene)
    {
        GameObject[] gameObjects = scene.GetRootGameObjects();
        for (int index = gameObjects.Length - 1; index >= 0; --index)
        {
            GameObject.DestroyImmediate(gameObjects[index]);
        }
    }

    static bool SceneFillerConditional(Scene scene, OpenSceneMode mode)
    {
        //do nothing for non empty scene
        if (scene.rootCount != 0)
            return false;

        if (mode == OpenSceneMode.Additive || mode == OpenSceneMode.AdditiveWithoutLoading)
            return false;
        
        if(EditorUtility.DisplayDialog("Initialize empty scene", "This scene is empty. Do you want to populate it with basic content?", "Yes", "No"))
        {
            HDRenderPipelineEditorResources hdrpEditorResources = (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources;
            if (hdrpEditorResources == null)
                Debug.LogError("Missing HDRenderPipelineEditorResources in HDRenderPipelineAsset");
            
            GameObject root = GameObject.Instantiate(hdrpEditorResources.defaultScene);
            root.transform.DetachChildren();
            GameObject.DestroyImmediate(root);
            return true;
        }
        return false;
    }

    //workaround while newSceneCreated event is not raised in the Project Browser
    void OnPreprocessAsset()
    {
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
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
