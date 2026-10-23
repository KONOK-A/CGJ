
using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Utilities.Extensions;


namespace YLFramework.Platform2D
{
    [Serializable]
    public struct Area
    {
        public Vector3 from;
        public Vector3 to;
    }

    public class PlatformCatcher2D : MonoBehaviour
    {

        #region 字段

        [Tooltip("检测区域")]
        public Area[] areas;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[10];

        [Tooltip("当前平台接触到的游戏物体")]
        public List<RaycastHit2D> Contacts = new List<RaycastHit2D>();

        private LayerMask _layerMask;

        private int _layer;

        private List<ContactPoint2D> _contacts = new List<ContactPoint2D>();

        private readonly ContactPoint2D[] _contactsTemp = new ContactPoint2D[20];

        private List<ContactPoint2D> _allContacts = new List<ContactPoint2D>();

        private float _timer = 0;

        private Collider2D[] _collider2d;



        private float _checkTimer = 0;

        #endregion


        #region 生命周期

        private void Awake()
        {
            _layer = gameObject.layer;
            _layerMask.Physics2DSetting(gameObject.layer);
            _collider2d = GetComponentsInChildren<Collider2D>();
        }

        private void OnEnable()
        {
            _collider2d = GetComponentsInChildren<Collider2D>();
            _timer = _checkTimer = 0;
        }

        private void OnDisable()
        {
            Contacts.Clear();
            _contacts.Clear();
            _allContacts.Clear();
            Array.Clear(_hits, 0, _hits.Length);
            Array.Clear(_contactsTemp, 0, _contactsTemp.Length);
        }

        public void RefreshContacts()
        {
            if (!isActiveAndEnabled)
            {
                Contacts.Clear();
                return;
            }
            _layer = gameObject.layer;
            _layerMask.Physics2DSetting(_layer);
            _collider2d = GetComponentsInChildren<Collider2D>();
            Check();
        }


        private void Reset()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Vector2 center = box.bounds.center;
                Vector2 size = box.bounds.size;
                Vector2 from = new Vector2(center.x - size.x / 2, center.y + size.y / 2 + 0.1f);
                Vector2 to = new Vector2(center.x + size.x / 2, center.y + size.y / 2 + 0.1f);
                Area area = new Area();
                area.from = transform.worldToLocalMatrix.MultiplyPoint(from);
                area.to = transform.worldToLocalMatrix.MultiplyPoint(to);
                areas = new Area[] { area };
            }
        }

        private void Update()
        {
            if (gameObject.layer != _layer)
            {
                _layer = gameObject.layer;
                _layerMask.Physics2DSetting(gameObject.layer);
            }

            CheckWithTimer();

            // 如果检测的结果有游戏物体 那么1s检测10次 用来监听游戏物体的情况
            // 如果没有，可以不用检测 ， 等接触的游戏物体发生变化时再检测
            if (Contacts.Count > 0)
            {
                _checkTimer += Time.deltaTime;
                if (_checkTimer >= 0.1f)
                {
                    Check();
                    _checkTimer = 0;
                }
            }

        }


        #endregion

        #region 方法


        private void CheckWithTimer()
        {
            _timer += Time.deltaTime;

            if (_timer <= 1.0f / 30.0f)
            {
                return;
            }

            _timer = 0;

            if (_collider2d == null || _collider2d.Length == 0)
            {
                return;
            }

            _allContacts.Clear();

            for (int i = 0; i < _collider2d.Length; i++)
            {
                Collider2D collider = _collider2d[i];
                if (!collider)
                {
                    continue;
                }

                int contactsCount = collider.GetContacts(_contactsTemp);

                for (int j = 0; j < contactsCount; j++)
                {
                    _allContacts.Add(_contactsTemp[j]);
                }
            }


            if (!CompareTheSame())
            {
                Check();

                _contacts.Clear();
                _contacts.AddRange(_allContacts);
            }


        }

        // 检测
        private void Check()
        {
            Contacts.Clear();
            if (areas == null || areas.Length == 0)
            {
                return;
            }

            foreach (Area area in areas)
            {
                Vector3 from = transform.localToWorldMatrix.MultiplyPoint(area.from);
                Vector3 to = transform.localToWorldMatrix.MultiplyPoint(area.to);

#if UNITY_6000_0_OR_NEWER
                ContactFilter2D contactFilter = CreateLegacyFilter(_layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.defaultPhysicsScene.Linecast(from, to, contactFilter, _hits);
#else
                int count = Physics2D.LinecastNonAlloc(from, to, _hits, _layerMask.value);
#endif

                for (int i = 0; i < count; i++)
                {
                    if (!_hits[i].collider || _hits[i].collider.isTrigger || _hits[i].transform == transform || _hits[i].transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    Contacts.Add(_hits[i]);
                }
            }
            Array.Clear(_hits, 0, _hits.Length);
        }


        // 比较是否相同
        private bool CompareTheSame()
        {

            if (_allContacts.Count != _contacts.Count)
            {
                return false;
            }

            for (int i = 0; i < _allContacts.Count; i++)
            {
                if (!_allContacts[i].collider || !ContainsTransform(_allContacts[i].collider.transform))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ContainsTransform(Transform trans)
        {

            foreach (var item in _contacts)
            {
                if (item.collider && item.collider.transform == trans)
                {
                    return true;
                }
            }

            return false;
        }

        internal static ContactFilter2D CreateLegacyFilter(int layerMask, float minDepth, float maxDepth)
        {
            ContactFilter2D result = default(ContactFilter2D);
            result.useTriggers = Physics2D.queriesHitTriggers;
            result.SetLayerMask(layerMask);
            result.SetDepth(minDepth, maxDepth);
            return result;
        }


        #endregion





    }

}
