using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>마을 길을 따라 걷는 사람. 집에서 편의점으로, 또는 편의점에서 집으로 가고 도착하면 사라집니다.</summary>
    public class VillageWalker : MonoBehaviour
    {
        private VillageMap _village;
        private CharacterView _view;
        private List<Vector2Int> _path;
        private int _pathIndex;
        private float _speed;
        private Vector2 _offset;
        private Action _onArrive;

        public void Init(VillageMap village, CharacterView view, Vector2Int start, List<Vector2Int> path, float speed,
            Action onArrive)
        {
            _village = village;
            _view = view;
            _path = path;
            _speed = speed;
            _onArrive = onArrive;
            // 여러 명이 한 줄로 겹쳐 걷지 않도록 길 위에서 조금씩 비켜 선다.
            _offset = new Vector2(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(-0.15f, 0.25f));
            transform.position = village.TileToWorld(start) + _offset;
        }

        private void Update()
        {
            if (_pathIndex >= _path.Count)
            {
                _onArrive?.Invoke();
                Destroy(gameObject);
                return;
            }

            Vector2 target = _village.TileToWorld(_path[_pathIndex]) + _offset;
            Vector2 pos = transform.position;
            _view.Face(target - pos);
            pos = Vector2.MoveTowards(pos, target, _speed * Time.deltaTime);
            transform.position = pos;
            if ((target - pos).sqrMagnitude < 0.0001f) _pathIndex++;
            _view.Tick(true);
        }
    }
}
