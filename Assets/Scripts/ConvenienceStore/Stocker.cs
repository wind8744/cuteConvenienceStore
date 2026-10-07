using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 진열 알바. 구석에서 기다리다가 재고가 떨어진 진열대로 걸어가 채우고 돌아옵니다.
    /// 채우는 값은 주인이 직접 채울 때와 똑같이 잔고에서 나갑니다.
    /// </summary>
    public class Stocker : MonoBehaviour
    {
        private const float WorkSeconds = 0.9f;
        private const float IdleCheckSeconds = 1.5f;

        private enum State { Idle, Walking, Working, Returning }

        private ConvenienceStoreGame _game;
        private CharacterView _view;
        private Vector2Int _home;
        private float _speed, _timer;
        private int _target = -1;
        private State _state;
        private List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIndex;
        private bool _moved;

        public void Init(ConvenienceStoreGame game, CharacterView view, Vector2Int home, float speed)
        {
            _game = game;
            _view = view;
            _home = home;
            _speed = speed;
            transform.position = StoreMap.FeetPos(home);
            _view.Face(Vector2.down);
            _view.Tick(false);
        }

        private void Update()
        {
            _moved = false;
            switch (_state)
            {
                case State.Idle:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    _timer = IdleCheckSeconds;
                    _target = _game.ShelfNeedingRestock();
                    if (_target < 0) break;
                    SetPath(_game.Map.ShopSpots(_target)[0]);
                    _state = State.Walking;
                    break;

                case State.Walking:
                    if (!FollowPath()) break;
                    _view.Face(Vector2.up);
                    _timer = WorkSeconds;
                    _state = State.Working;
                    break;

                case State.Working:
                    _timer -= Time.deltaTime;
                    if (_timer > 0f) break;
                    _game.RestockByStocker(_target);
                    _target = -1;
                    SetPath(_home);
                    _state = State.Returning;
                    break;

                case State.Returning:
                    if (!FollowPath()) break;
                    _view.Face(Vector2.down);
                    _timer = IdleCheckSeconds;
                    _state = State.Idle;
                    break;
            }
            _view.Tick(_moved);
        }

        private void SetPath(Vector2Int goal)
        {
            _path = _game.Map.FindPath(StoreMap.TileOf(transform.position), goal) ?? new List<Vector2Int>();
            _pathIndex = 0;
        }

        private bool FollowPath()
        {
            if (_pathIndex < _path.Count && MoveTo(StoreMap.FeetPos(_path[_pathIndex]))) _pathIndex++;
            return _pathIndex >= _path.Count;
        }

        private bool MoveTo(Vector2 target)
        {
            Vector2 pos = transform.position;
            Vector2 delta = target - pos;
            if (delta.sqrMagnitude < 0.0001f) return true;
            _view.Face(delta);
            _moved = true;
            pos = Vector2.MoveTowards(pos, target, _speed * Time.deltaTime);
            transform.position = pos;
            return (target - pos).sqrMagnitude < 0.0001f;
        }
    }
}
