using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFABManager;
using XFGameFramework;

public class SceneController : Controller
{
    
    public void LoadScene(string sceneName, Action onFinish, Action<float> onLoading = null)
    {
        CoroutineStarter.Start(LoadSceneExecute(sceneName, onFinish, onLoading));
    }

    private IEnumerator LoadSceneExecute(string sceneName, Action onFinish, Action<float> onLoading = null)
    {
        LoadSceneRequest request = AssetBundleManager.LoadSceneAsynchrony(Module.ProjectName,sceneName,LoadSceneMode.Single);
        var loadingPanel = Module.LoadPanel<LoadingPanel>(UIType.DontDestroyUI);
        while (!request.isDone || !loadingPanel.IsDone)
        {
            yield return null;
            //传递进度
            onLoading?.Invoke(request.progress);
            loadingPanel.UpdateProgress(request.progress);
        }
        //加载完成
        onFinish?.Invoke();
        loadingPanel.Close();
    }
}
