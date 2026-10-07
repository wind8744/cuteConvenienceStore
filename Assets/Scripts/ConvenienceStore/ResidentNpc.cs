using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 집에 있는 마을 사람. 집 앞을 어슬렁거리며, 주인이 다가가 말을 걸거나 부탁한 물건을 가져다줄 수 있습니다.
    /// 장 보러 나간 동안에는 숨겨집니다.
    /// </summary>
    public class ResidentNpc : MonoBehaviour
    {
        private const float Speed = 1.4f;

        private VillageMap _village;
        private CharacterView _view;
        private Vector2Int _home;
        private Vector2 _target;
        private float _rest;

        public Vector2 Position => transform.position;

        public void Init(VillageMap village, CharacterView view, Vector2Int home)
        {
            _village = village;
            _view = view;
            _home = home;
            transform.position = _target = village.TileToWorld(home);
            _rest = Random.Range(1f, 4f);
            _view.Face(Vector2.down);
            _view.Tick(false);
        }

        private void Update()
        {
            Vector2 pos = transform.position;
            if ((_target - pos).sqrMagnitude > 0.0001f)
            {
                _view.Face(_target - pos);
                transform.position = Vector2.MoveTowards(pos, _target, Speed * Time.deltaTime);
                _view.Tick(true);
                return;
            }

            _view.Tick(false);
            _rest -= Time.deltaTime;
            if (_rest > 0f) return;
            _rest = Random.Range(2f, 6f);
            // 집 앞 한두 칸 안에서만 돌아다닌다. 길 위로는 안 나간다.
            Vector2Int next = _home + new Vector2Int(Random.Range(-1, 2), Random.Range(-1, 2));
            if (next == _home || !_village.IsWalkable(next) || _village.IsRoad(next)) next = _home;
            _target = _village.TileToWorld(next);
            if (next == _home) _view.Face(Vector2.down);
        }
    }
}
