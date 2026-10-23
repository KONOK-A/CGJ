using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Services.Resource
{
    /// <summary>Unity UI 图片消费者。只拥有自己的请求、Lease 与临时 Sprite，不拥有目标上原有的 Texture/Sprite。</summary>
    [AddComponentMenu("YLFramework/Resource/Resource Image")]
    [DisallowMultipleComponent]
    public sealed class ResourceImage : MonoBehaviour
    {
        public enum ImageSource
        {
            Location,
            Catalog,
            Atlas
        }

        [SerializeField, Tooltip("支持 Image 或 RawImage；留空时使用同一 GameObject 的组件。")]
        private Graphic target;
        [SerializeField] private ImageSource source;
        [SerializeField, Tooltip("完整 HTTP(S) URL 或绝对本地 PNG/JPEG 路径；保留大小写和 query。")]
        private string location;
        [SerializeField] private string projectId;
        [SerializeField] private string address;
        [SerializeField] private string subAsset;
        [SerializeField, Tooltip("显式SpriteAtlas中唯一的Sprite名称；克隆由此组件拥有，Atlas仍由原AssetLease保活。")]
        private string spriteName;
        [SerializeField] private bool loadOnEnable = true;
        [SerializeField] private bool setNativeSize;

        private ResourceRequest<AssetLease<Texture2D>> _imageRequest;
        private ResourceRequest<AssetLease<UnityEngine.Object>> _catalogRequest;
        private IDisposable _lease;
        private Graphic _displayTarget;
        private UnityEngine.Object _previous;
        private UnityEngine.Object _displayed;
        private UnityEngine.Object _leasedAsset;
        private Sprite _ownedSprite;
        private int _generation;
        private bool _hasDisplay;

        public ResourceRequestStatus Status { get; private set; } = ResourceRequestStatus.Canceled;
        public ResourceException Error
        {
            get; private set;
        }
        public bool IsLoading => _imageRequest != null && !_imageRequest.IsDone || _catalogRequest != null && !_catalogRequest.IsDone;
        public event Action<ResourceImage> Completed;

        public Graphic Target
        {
            get => target;
            set
            {
                if (value && !(value is Image) && !(value is RawImage))
                {
                    throw new ArgumentException("ResourceImage requires an Image or RawImage.", nameof(value));
                }

                if (target == value)
                {
                    return;
                }

                target = value;
                Refresh();
            }
        }

        public bool SetNativeSize
        {
            get => setNativeSize; set => setNativeSize = value;
        }
        private bool HasInput => source == ImageSource.Location ? !string.IsNullOrEmpty(location) : !string.IsNullOrEmpty(address);

        /// <summary>修改目标输入并发起新一代请求；停用时只记录输入，由 Load On Enable 或 Refresh 取得。</summary>
        public void SetImage(string imageLocation)
        {
            source = ImageSource.Location;
            location = imageLocation;
            Refresh();
        }

        /// <summary>正式 Catalog 的 Texture2D 或 Sprite 地址；借用资产仅由对应 Lease 保活。</summary>
        public void SetImage(ResourceAddress resourceAddress)
        {
            if (!resourceAddress.IsValid)
            {
                throw new ArgumentException("A valid image resource address is required.", nameof(resourceAddress));
            }

            source = ImageSource.Catalog;
            projectId = resourceAddress.ProjectId;
            address = resourceAddress.Path;
            subAsset = resourceAddress.SubAsset;
            Refresh();
        }

        /// <summary>显式取得Atlas中的Sprite克隆，仅供Image使用；不订阅全局atlasRequested或借用其他消费者的克隆。</summary>
        public void SetAtlasSprite(ResourceAddress atlasAddress, string name)
        {
            if (!atlasAddress.IsValid || atlasAddress.SubAsset != null)
            {
                throw new ArgumentException("An explicit main SpriteAtlas address is required.", nameof(atlasAddress));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A unique Sprite name is required.", nameof(name));
            }

            source = ImageSource.Atlas;
            projectId = atlasAddress.ProjectId;
            address = atlasAddress.Path;
            subAsset = null;
            spriteName = name;
            Refresh();
        }

        /// <summary>重新取得当前输入。远端内容换代使用不同版本 URL，不在组件内建立下载缓存或第二资源权威。</summary>
        public void Refresh()
        {
            var accepted = _generation + 1;
            try
            {
                ReleaseDisplay();
            }
            catch (Exception error)
            {
                if (_generation == accepted)
                {
                    Fail(new ResourceException(ResourceError.LoadFailed, "The previous image display could not be restored.", error));
                }

                return;
            }
            if (_generation != accepted)
            {
                return;
            }

            if (!isActiveAndEnabled || !HasInput)
            {
                Status = ResourceRequestStatus.Canceled;
                Error = null;
                return;
            }
            if (!target)
            {
                target = GetComponent<Graphic>();
            }

            if (!(target is Image) && !(target is RawImage))
            {
                Fail(new ResourceException(ResourceError.InvalidConfiguration, "ResourceImage requires an Image or RawImage target."));
                return;
            }
            if (source == ImageSource.Atlas && (!(target is Image) || string.IsNullOrWhiteSpace(spriteName)))
            {
                Fail(new ResourceException(ResourceError.InvalidConfiguration, "Atlas sprites require an Image target and a unique Sprite name."));
                return;
            }
            var system = ResourceSystem.Current;
            if (system == null || !system.IsAvailable)
            {
                Fail(new ResourceException(ResourceError.SessionUnavailable, "Initialize ResourceSystem before loading a ResourceImage."));
                return;
            }
            Status = ResourceRequestStatus.Pending;
            Error = null;
            var acceptedTarget = target;
            try
            {
                if (source == ImageSource.Location)
                {
                    _imageRequest = system.AcquireImageAsync(location);
                    _imageRequest.Completed += value => Apply(value, accepted, acceptedTarget);
                }
                else
                {
                    _catalogRequest = system.AcquireAsync<UnityEngine.Object>(new ResourceAddress(projectId, address,
                        source == ImageSource.Catalog && !string.IsNullOrEmpty(subAsset) ? subAsset : null));
                    _catalogRequest.Completed += value => Apply(value, accepted, acceptedTarget);
                }
            }
            catch (ArgumentException error)
            {
                Fail(new ResourceException(ResourceError.InvalidAddress, "Invalid image resource address.", error));
            }
        }

        public void Clear()
        {
            location = null;
            address = null;
            var accepted = _generation + 1;
            try
            {
                ReleaseDisplay();
            }
            finally
            {
                if (_generation == accepted)
                {
                    Error = null;
                    Status = ResourceRequestStatus.Canceled;
                }
            }
        }

        private void Apply<T>(ResourceRequest<AssetLease<T>> request, int accepted, Graphic acceptedTarget) where T : UnityEngine.Object
        {
            if (accepted != _generation || !isActiveAndEnabled)
            {
                request.Dispose();
                return;
            }
            if (!acceptedTarget || target != acceptedTarget)
            {
                try
                {
                    FailDisplay(accepted, new ResourceException(ResourceError.LoadFailed, "The image target changed or was destroyed before completion."));
                }
                finally
                {
                    request.Dispose();
                }
                return;
            }
            if (ReferenceEquals(_imageRequest, request))
            {
                _imageRequest = null;
            }

            if (ReferenceEquals(_catalogRequest, request))
            {
                _catalogRequest = null;
            }

            AssetLease<T> acquired = null;
            try
            {
                if (request.Status != ResourceRequestStatus.Succeeded)
                {
                    Status = request.Status;
                    Error = request.Error;
                    Notify();
                    return;
                }
                acquired = request.TakeResult();
                acquired.OwnerLabel = "ResourceImage: " + acceptedTarget.name + " (" + acceptedTarget.GetInstanceID() + ")";
                var asset = acquired.Asset;
                _previous = ReadTarget(acceptedTarget);
                _displayTarget = acceptedTarget;
                if (source == ImageSource.Atlas)
                {
                    if (!(asset is UnityEngine.U2D.SpriteAtlas atlas))
                    {
                        throw new ResourceException(ResourceError.TypeMismatch, "The explicit Atlas address must declare a SpriteAtlas.");
                    }

                    _ownedSprite = atlas.GetSprite(spriteName);
                    if (!_ownedSprite)
                    {
                        throw new ResourceException(ResourceError.AddressNotFound, "SpriteAtlas does not contain Sprite: " + spriteName);
                    }

                    if (!_ownedSprite.texture)
                    {
                        throw new ResourceException(ResourceError.LoadFailed, "The explicit SpriteAtlas has no packed Sprite texture.");
                    }

                    _displayed = _ownedSprite;
                }
                else if (acceptedTarget is RawImage)
                {
                    if (!(asset is Texture texture))
                    {
                        throw new ResourceException(ResourceError.TypeMismatch, "RawImage requires a Texture address, not a Sprite.");
                    }

                    _displayed = texture;
                }
                else
                {
                    if (asset is Sprite sprite)
                    {
                        _displayed = sprite;
                    }
                    else if (asset is Texture2D texture)
                    {
                        _ownedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
                        _displayed = _ownedSprite;
                    }
                    else
                    {
                        throw new ResourceException(ResourceError.TypeMismatch, "Image requires a Texture2D or Sprite address.");
                    }
                }
                // UI setter/尺寸回调可能停用、换代或销毁组件，必须先登记其可归还的真实责任。
                _hasDisplay = true;
                _leasedAsset = asset;
                _lease = acquired;
                acquired = null;
                if (acceptedTarget is RawImage rawTarget)
                {
                    rawTarget.texture = _displayed as Texture;
                    if (setNativeSize)
                    {
                        rawTarget.SetNativeSize();
                    }
                }
                else
                {
                    var image = (Image)acceptedTarget;
                    image.sprite = _displayed as Sprite;
                    if (setNativeSize)
                    {
                        image.SetNativeSize();
                    }
                }
                if (accepted != _generation || !isActiveAndEnabled || !acceptedTarget)
                {
                    return;
                }

                Status = ResourceRequestStatus.Succeeded;
                Error = null;
                Notify();
            }
            catch (Exception error)
            {
                FailDisplay(accepted, error as ResourceException ?? new ResourceException(ResourceError.LoadFailed, "The image could not be applied to its UI target.", error));
            }
            finally
            {
                acquired?.Dispose();
                request.Dispose();
            }
        }

        private static UnityEngine.Object ReadTarget(Graphic graphic) => graphic is Image image ? image.sprite : ((RawImage)graphic).texture;

        private void ReleaseDisplay()
        {
            _generation++;
            var pendingImage = _imageRequest;
            _imageRequest = null;
            var pendingCatalog = _catalogRequest;
            _catalogRequest = null;
            var oldTarget = _displayTarget;
            var oldDisplayed = _displayed;
            var oldPrevious = _previous;
            var hadDisplay = _hasDisplay;
            var sprite = _ownedSprite;
            var held = _lease;
            _hasDisplay = false;
            _displayed = _previous = _leasedAsset = null;
            _displayTarget = null;
            _ownedSprite = null;
            _lease = null;
            // 先移交局部清理责任，再调用可重入 UI setter；不覆盖外部代码替换的内容。
            try
            {
                try
                {
                    pendingImage?.Dispose();
                }
                finally
                {
                    pendingCatalog?.Dispose();
                }
                if (hadDisplay && oldTarget && ReferenceEquals(ReadTarget(oldTarget), oldDisplayed))
                {
                    if (oldTarget is Image image)
                    {
                        image.sprite = oldPrevious as Sprite;
                    }
                    else
                    {
                        ((RawImage)oldTarget).texture = oldPrevious as Texture;
                    }
                }
            }
            finally
            {
                try
                {
                    if (sprite)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(sprite);
                        }
                        else
                        {
                            DestroyImmediate(sprite);
                        }
                    }
                }
                finally
                {
                    held?.Dispose();
                }
            }
        }

        private void FailDisplay(int accepted, ResourceException failure)
        {
            if (accepted != _generation)
            {
                return;
            }

            var released = _generation + 1;
            try
            {
                ReleaseDisplay();
            }
            catch (Exception error)
            {
                failure = new ResourceException(ResourceError.LoadFailed, "The failed image display also failed to restore.", new AggregateException(failure, error));
            }
            if (_generation == released)
            {
                Fail(failure);
            }
        }

        private void Fail(ResourceException error)
        {
            Status = ResourceRequestStatus.Failed;
            Error = error;
            Notify();
        }

        private void Notify()
        {
            if (Completed == null)
            {
                return;
            }

            foreach (Action<ResourceImage> observer in Completed.GetInvocationList())
            {
                try
                {
                    observer(this);
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                }
            }
        }

        private void OnEnable()
        {
            if (loadOnEnable && HasInput)
            {
                Refresh();
            }
        }
        private void OnDisable()
        {
            try
            {
                ReleaseDisplay();
            }
            finally
            {
                if (Status == ResourceRequestStatus.Pending)
                {
                    Status = ResourceRequestStatus.Canceled;
                }
            }
        }
        private void OnDestroy() => ReleaseDisplay();
        private void LateUpdate()
        {
            if (!_hasDisplay || _displayTarget && _displayed && _leasedAsset && (!(_displayed is Sprite sprite) || sprite.texture))
            {
                return;
            }

            FailDisplay(_generation, new ResourceException(ResourceError.LoadFailed, "The image target or leased Unity object was destroyed externally."));
        }
    }
}
