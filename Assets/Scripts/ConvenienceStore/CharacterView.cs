using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 캐릭터 그리기: 방향별 걷기 애니메이션, 깊이 정렬, 머리 위 말풍선.
    /// 오브젝트의 위치가 곧 발 위치입니다.
    /// </summary>
    public class CharacterView : MonoBehaviour
    {
        private const float FramesPerSecond = 8f;
        private const int BubbleOrder = 9000;

        private SpriteRenderer _body, _bubble, _bubbleIcon, _badge;
        private CharacterSprites _sprites;
        private Vector2Int _facing = Vector2Int.down;
        private float _animTime;
        private float _bubbleTimer;

        public void Init(CharacterSprites sprites, Sprite bubble)
        {
            _sprites = sprites;
            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = sprites.Down[0];

            var bubbleGo = new GameObject("Bubble");
            bubbleGo.transform.SetParent(transform, false);
            bubbleGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            _bubble = bubbleGo.AddComponent<SpriteRenderer>();
            _bubble.sprite = bubble;
            _bubble.sortingOrder = BubbleOrder;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(bubbleGo.transform, false);
            iconGo.transform.localPosition = new Vector3(0f, 8f / StoreArt.Tile, 0f);
            _bubbleIcon = iconGo.AddComponent<SpriteRenderer>();
            _bubbleIcon.sortingOrder = BubbleOrder + 1;
            HideBubble();
        }

        /// <summary>머리 위에 늘 떠 있는 작은 표식(왕관 등)을 답니다.</summary>
        public void SetBadge(Sprite icon)
        {
            if (_badge == null)
            {
                var go = new GameObject("Badge");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.48f, 0f);
                _badge = go.AddComponent<SpriteRenderer>();
                _badge.sortingOrder = BubbleOrder - 2;
            }
            _badge.sprite = icon;
        }

        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                _facing = direction.x > 0f ? Vector2Int.right : Vector2Int.left;
            else
                _facing = direction.y > 0f ? Vector2Int.up : Vector2Int.down;
        }

        /// <summary>매 프레임 호출합니다. 움직이는 중이면 걷기 프레임을 넘깁니다.</summary>
        public void Tick(bool moving)
        {
            _animTime = moving ? _animTime + Time.deltaTime : 0f;
            Sprite[] frames = _facing.y < 0 ? _sprites.Down : _facing.y > 0 ? _sprites.Up : _sprites.Side;
            // 걷기 시작하자마자 발을 떼도록 1번 프레임부터 돈다.
            int frame = moving ? (1 + Mathf.FloorToInt(_animTime * FramesPerSecond)) % frames.Length : 0;
            _body.sprite = frames[frame];
            _body.flipX = _facing.x < 0;
        }

        /// <summary>말풍선을 띄웁니다. seconds 가 0 이하면 HideBubble 을 부를 때까지 유지합니다.</summary>
        public void ShowBubble(Sprite icon, float seconds)
        {
            _bubbleIcon.sprite = icon;
            // 상품 아이콘은 왼쪽 아래가, 감정 아이콘은 가운데가 기준점이라 가운데로 맞춘다.
            Vector2 size = icon.bounds.size, pivot = icon.pivot / icon.pixelsPerUnit;
            _bubbleIcon.transform.localPosition = new Vector3(pivot.x - size.x * 0.5f,
                8f / StoreArt.Tile + pivot.y - size.y * 0.5f, 0f);
            _bubble.enabled = _bubbleIcon.enabled = true;
            _bubbleTimer = seconds > 0f ? seconds : float.PositiveInfinity;
        }

        public void HideBubble()
        {
            _bubble.enabled = _bubbleIcon.enabled = false;
            _bubbleTimer = 0f;
        }

        private void LateUpdate()
        {
            _body.sortingOrder = StoreArt.SortOrder(transform.position.y);
            if (_bubbleTimer <= 0f) return;
            _bubbleTimer -= Time.deltaTime;
            if (_bubbleTimer <= 0f) HideBubble();
        }
    }
}
