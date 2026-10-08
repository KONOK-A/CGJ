using System.Collections;
using UnityEngine;
using XFABManager;
using XFGameFramework;

public class Main : MonoBehaviour
{
   // Start is called before the first frame update
   private IEnumerator Start()
   {
       // 准备资源
       yield return StartCoroutine(ReadyMainModuleRes());

       // 启动MainModule
       StartUpModuleRequest request = ModuleManager.StartUpModule<MainModule>();
       yield return request;
       if (!string.IsNullOrEmpty(request.error))
           Debug.LogErrorFormat("模块启动失败:{0}",request.error); 
       //启动模块成功
   }

   /// <summary>
   /// 准备MainModule资源
   /// </summary>
   /// <returns></returns>
   private IEnumerator ReadyMainModuleRes() 
   {
       //死循环作用:如果资源准备失败则重新准备,如果成功则跳出循环
       while (true)
       {
           // 准备资源
           ReadyResRequest request = AssetBundleManager.ReadyRes("MainModule");
           while (!request.isDone)
           {
               yield return null;
               // 可以在这里更新UI界面 TODO
               switch (request.ExecutionType)
               {
                   case ExecutionType.Download:
                       // 正在下载资源
                       break;
                   case ExecutionType.Decompression:
                       // 解压资源
                       break;
                   case ExecutionType.Verify:
                       // 校验资源(资源下载完成后,需要校验文件是否损坏)
                       break;
                   case ExecutionType.ExtractLocal:
                       // 释放资源(把资源从内置目录复制到数据目录)
                       break; 
               }
           }
           if (string.IsNullOrEmpty(request.error))
           {
               // 资源准备成功跳出循环
               break;
           }
           yield return new WaitForSeconds(1);
       } 
   }
}

