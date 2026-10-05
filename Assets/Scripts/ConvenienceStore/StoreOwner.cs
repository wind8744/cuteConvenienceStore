using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>플레이어가 조종하는 편의점 주인. 방향키 또는 WASD로 가게 안을 돌아다닙니다.</summary>
    public class StoreOwner : MonoBehaviour
    {
        // 발 주변의 충돌 상자 (유닛)
        private const float HalfWidth = 0.3f;
        private const float Below = 0.1f;
        private const float Above = 0.2f;

        private static readonly (KeyCode[] keys, Vector2 dir)[] KeyMap =
        {
            (new[] { KeyCode.UpArrow, KeyCode.W }, Vector2.up),
            (new[] { KeyCode.DownArrow, KeyCode.S }, Vector2.down),
            (new[] { KeyCode.LeftArrow, KeyCode.A }, Vector2.left),
            (new[] { KeyCode.RightArrow, KeyCode.D }, Vector2.right),
        };

        private TileGrid _map;
        private CharacterView _view;
        private float _speed;

        public Vector2 Position => transform.position;
        /// <summary>이번 프레임에 누르고 있는 이동 방향.</summary>
        public Vector2 MoveInput { get; private set; }
        /// <summary>영업 중이 아닐 때는 false 로 두어 움직이지 못하게 합니다.</summary>
        public bool CanMove { get; set; } = true;

        public void Init(TileGrid map, CharacterView view, Vector2 start, float speed)
        {
            _map = map;
            _view = view;
            _speed = speed;
            transform.position = start;
        }

        private void Update()
        {
            Vector2 input = CanMove ? ReadInput() : Vector2.zero;
            MoveInput = input;
            bool moving = input != Vector2.zero;
            if (moving)
            {
                Vector2 step = input.normalized * (_speed * Time.deltaTime);
                Vector2 pos = transform.position;
                if (Fits(new Vector2(pos.x + step.x, pos.y))) pos.x += step.x;
                if (Fits(new Vector2(pos.x, pos.y + step.y))) pos.y += step.y;
                transform.position = pos;
                _view.Face(input);
            }
            _view.Tick(moving);
        }

        /// <summary>다른 맵(가게 안 ↔ 마을)의 지정한 위치로 옮깁니다.</summary>
        public void MoveTo(TileGrid map, Vector2 position)
        {
            _map = map;
            transform.position = position;
        }

        private static Vector2 ReadInput()
        {
            Vector2 input = Vector2.zero;
            foreach (var (keys, dir) in KeyMap)
            {
                foreach (KeyCode key in keys)
                {
                    if (!Input.GetKey(key)) continue;
                    input += dir;
                    break;
                }
            }
            return input;
        }

        private bool Fits(Vector2 feet)
        {
            return Walkable(feet.x - HalfWidth, feet.y - Below) && Walkable(feet.x + HalfWidth, feet.y - Below)
                && Walkable(feet.x - HalfWidth, feet.y + Above) && Walkable(feet.x + HalfWidth, feet.y + Above);
        }

        private bool Walkable(float x, float y) => _map.IsWalkable(_map.WorldToTile(new Vector2(x, y)));
    }
}
