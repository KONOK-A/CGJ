using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Utilities.ZipTool.Zip;


namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 包内资源文件的访问契约：Android 非编辑器环境下包内文件位于 APK（zip）中；
    /// 其他环境把 StreamingAssets 当作普通目录处理。
    /// </summary>
    public interface IZipFileHelper
    {
        /// <summary>
        /// 在包内查找文件名匹配的文件（目录递归 / APK 条目遍历），把命中文件的路径追加到 results。
        /// </summary>
        void GetFileInInner(string filename,List<string> results);
        /// <summary>
        /// 打开文件的读取流；路径为空或文件不存在时返回 null。
        /// </summary>
        Stream OpenReadStream(string filePath);
    }
    
    /// <summary>
    /// <see cref="IZipFileHelper"/> 的默认实现（Singleton）。
    /// <para>
    /// 搜索顺序：先 external 搜索目录（persistentDataPath/Res），再包内（StreamingAssets）；
    /// Android 非编辑器环境下包内读取基于常驻的 APK <see cref="ZipFile"/>，在对象终结时关闭。
    /// </para>
    /// </summary>
    public class ZipFileHelper : Singleton<ZipFileHelper> ,IZipFileHelper
    {
        
        private readonly List<string> _searchDirList = new();
        private string _streamAssetsPath;
        private ZipFile _zipFile;

        /// <summary>返回持有的 APK ZipFile；仅 Android 非编辑器环境会创建，其他环境为 null。</summary>
        public ZipFile GetZipFile()
        {
            return _zipFile;
        }

        /// <summary>初始化搜索目录；Android 非编辑器环境下打开 APK zip 供包内读取。</summary>
        public ZipFileHelper()
        {
            _searchDirList.Add(AssetBundleHelper.PersistentDataPathForRes);
            _streamAssetsPath = AssetBundleHelper.StreamingAssetsPath;
#if (UNITY_ANDROID) && !UNITY_EDITOR
            if(_zipFile == null)
            {
                _zipFile = new ZipFile(File.Open(UnityEngine.Application.dataPath,FileMode.Open,FileAccess.Read));
            }
#endif
        }

        ~ZipFileHelper()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_zipFile != null)
            {
                _zipFile.Close();
                _zipFile = null;
            }
#endif
        }

        /// <summary>刷新内部缓存的 StreamingAssets 路径。</summary>
        public void InitStreamAssetsPath()
        {
            _streamAssetsPath = AssetBundleHelper.StreamingAssetsPath;
        }
        /// <summary>
        /// 在包内查找是否有该资源
        /// </summary>
        private bool FindResInAppInternal(string fileRelativePath)
        {
#if UNITY_IPHONE && !UNITY_EDITOR
			string absoluteFilePath = FindFilePathInternal(fileRelativePath);
			return !string.IsNullOrEmpty(absoluteFilePath);
#elif UNITY_ANDROID && !UNITY_EDITOR
			int entryIndex = _zipFile.FindEntry(string.Format("assets/{0}", fileRelativePath), false);
			return entryIndex != -1;
#else
            var absoluteFilePath = $"{_streamAssetsPath}{fileRelativePath}";
            return File.Exists(absoluteFilePath);
#endif
        }

        private void AddSearchDir(string dir)
        {
            _searchDirList.Add(dir);
        }

        /// <summary>文件是否存在于 external 搜索目录或包内。</summary>
        public bool FileExists(string fileRelativePath)
        {
#if UNITY_IPHONE && !UNITY_EDITOR
			string absoluteFilePath = FindFilePath(fileRelativePath);
			return (!string.IsNullOrEmpty(absoluteFilePath) && File.Exists(absoluteFilePath));
#elif UNITY_ANDROID && !UNITY_EDITOR
			string absoluteFilePath = FindFilePathInExternal(fileRelativePath);
			//先到外存去找
			if (!string.IsNullOrEmpty(absoluteFilePath))
			{
				return File.Exists(absoluteFilePath);
			}
			else
			{
			if (_zipFile == null)
			{
				return false;
			}

				return _zipFile.FindEntry($"assets/{fileRelativePath}", true) >= 0;
			}
#else
            string filePathStandalone = $"{_streamAssetsPath}{fileRelativePath}";
            return (!string.IsNullOrEmpty(filePathStandalone) && File.Exists(filePathStandalone));
#endif
        }

        /// <summary>
        /// 打开文件的读取流；Android 非编辑器环境下路径指向 APK 内时从 zip 读取；
        /// 路径为空或文件不存在返回 null。
        /// </summary>
        public Stream OpenReadStream(string absFilePath)
        {
	        if (string.IsNullOrEmpty(absFilePath))
	        {
		        return null;
	        }
	        
#if UNITY_ANDROID && !UNITY_EDITOR
			//Android 包内
			if (absFilePath.Contains(".apk/"))
			{
				return OpenStreamInZip(absFilePath);
			}
#endif
	        FileInfo fileInfo = new FileInfo(absFilePath);

	        if (!fileInfo.Exists)
	        {
		        return null;
	        }

	        return fileInfo.OpenRead();
        }

        /// <summary>
        /// 在包内查找文件：仅 Android 非编辑器环境从 APK zip 中遍历，其余环境在 StreamingAssets 目录下递归查找。
        /// </summary>
        public void GetFileInInner(string filename, List<string> results)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
	        GetFileInZip(_zipFile, filename, results);
	        return;
#endif
	        AssetBundleHelper.GetFileInFolder(AssetBundleHelper.StreamingAssetsPath, filename, results);
        }

        /// <summary>按相对路径读取文件字节：先查 external 搜索目录，再查包内；均未命中返回 null。</summary>
        public byte[] ReadSync(string fileRelativePath)
        {
	        string absoluteFilePath = FindFilePathInExternal(fileRelativePath);
	        if (!string.IsNullOrEmpty(absoluteFilePath))
	        {
		        return ReadSyncExternal(fileRelativePath);
	        }
	        return ReadSyncInternal(fileRelativePath);
        }

        /// <summary>按绝对路径读取文件字节；文件不存在返回 null。</summary>
        public byte[] ReadSyncByAbsoluteFilePath(string absoluteFilePath)
        {
	        if (File.Exists(absoluteFilePath))
	        {
		        var fileInfo = new FileInfo(absoluteFilePath);
		        return ReadFile(fileInfo);
	        }
	        return null;
        }

        private byte[] ReadSyncExternal(string fileRelativePath)
        {
	        string absoluteFilePath = FindFilePathInExternal(fileRelativePath);
	        if (!string.IsNullOrEmpty(absoluteFilePath))
	        {
		        FileInfo fileInfo = new FileInfo(absoluteFilePath);
		        return ReadFile(fileInfo);
	        }
	        return null;
        }

        private byte[] ReadSyncInternal(string fileRelativePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
	        return ReadDataInAndroidApk(fileRelativePath);
#else
	        string absoluteFilePath = FindFilePathInternal(fileRelativePath);
	        if (!string.IsNullOrEmpty(absoluteFilePath))
	        {
		        var fileInfo = new FileInfo(absoluteFilePath);
		        return ReadFile(fileInfo);
	        }
#endif
	        return null;
        }

        private static byte[] ReadFile(FileInfo fileInfo)
        {
	        using var fs = fileInfo.OpenRead();
	        var byteData = new byte[fs.Length];
	        fs.Read(byteData, 0, byteData.Length);
	        return byteData;
        }

        private string FindFilePath(string file)
        {
	        // 先到搜索列表里找
	        string filePath = FindFilePathInExternal(file);
	        if (!string.IsNullOrEmpty(filePath))
	        {
		        return filePath;
	        }
			// 在包内找
	        filePath = FindFilePathInternal(file);
	        if (!string.IsNullOrEmpty(filePath))
	        {
		        return filePath;
	        }

	        return null;
        }

        private string FindFilePathInExternal(string file)
        {
	        foreach (var dir in _searchDirList)
	        {
		        var filePath = $"{dir}/{file}";
		        if (File.Exists(filePath))
		        {
			        return filePath;
		        }
	        }

	        return string.Empty;
        }

        private string FindFilePathInternal(string file)
        {
	        string filePath = $"{_streamAssetsPath}{file}";
	        if (File.Exists(filePath))
	        {
		        return filePath;
	        }

	        return null;
        }

        private Stream OpenStreamInZip(string absFilePath)
        {
	        const string tag = "!/assets/";
	        var androidFolder = absFilePath[..absFilePath.IndexOf(tag)];
	        
	        int startIndex = androidFolder.Length + tag.Length;
	        string relativePath = absFilePath.Substring(startIndex,absFilePath.Length - startIndex);
	        var zipEntry = _zipFile.GetEntry($"assets/{relativePath}");
	        if (zipEntry != null)
	        {
		        return _zipFile.GetInputStream(zipEntry);
	        }
	        return null;
        }

        /// <summary>
        /// 遍历 zip 条目，把名称以 <paramref name="fileName"/> 结尾的文件条目路径追加到 results。
        /// </summary>
        public void GetFileInZip(ZipFile zipFile, string fileName, List<string> results)
        {
	        int totalCount = 0;

	        foreach (var entry in zipFile)
	        {
		        totalCount += 1;
		        if (entry is ZipEntry e)
		        {
			        if (e.IsFile)
			        {
				        if (e.Name.EndsWith(fileName))
				        {
					        results.Add(zipFile.Name + "/!/" + e.Name);
				        }
			        }
		        }
	        }
        }

        private byte[] ReadDataInAndroidApk(string filerRelativePath)
        {
	        byte[] byteData = null;
	        if (_zipFile == null)
	        {
		        Debug.LogError("can not find zip file");
		        return null;
	        }

	        var zipEntry = _zipFile.GetEntry($"assets/{filerRelativePath}");
	        if (zipEntry != null)
	        {
		        var stream = _zipFile.GetInputStream(zipEntry);
		        byteData = new byte[zipEntry.Size];
		        stream.Read(byteData, 0, byteData.Length);
		        stream.Close();
	        }
	        else
	        {
		        Debug.LogError($"can not find file {filerRelativePath}");
	        }
	        return byteData;
		}
        
    }
}