using UnityEngine;

namespace ConvenienceStore
{
    /// <summary>
    /// 연못 낚시 미니게임. 입질을 기다렸다가(Space) 움직이는 표시가 초록 칸에 있을 때 다시 Space 를 누르면 잡습니다.
    /// 잡은 물고기는 바로 돈이 됩니다. 낚시하는 동안은 움직일 수 없고 가게 시간은 그대로 흐릅니다.
    /// </summary>
    public class Fishing
    {
        private const float BiteWindow = 0.9f;
        private const float TimingSeconds = 4f;
        private const float MarkerSpeed = 1.3f;
        private const float ZoneWidth = 0.2f;

        private static readonly (string name, int value, float chance)[] Catches =
        {
            ("붕어", 1500, 0.5f), ("잉어", 3000, 0.3f), ("금붕어", 6000, 0.15f), ("낡은 장화", 0, 0.05f),
        };

        private enum Phase { None, Waiting, Bite, Timing }

        private readonly ConvenienceStoreGame _game;
        private readonly StoreOwner _owner;
        private readonly CharacterView _view;
        private readonly StoreAudio _audio;
        private Phase _phase;
        private float _timer, _zoneStart, _marker;

        public bool Active => _phase != Phase.None;
        /// <summary>화면 아래에 보여 줄 안내.</summary>
        public string Prompt { get; private set; }

        public Fishing(ConvenienceStoreGame game, StoreOwner owner, CharacterView view, StoreAudio audio)
        {
            _game = game;
            _owner = owner;
            _view = view;
            _audio = audio;
        }

        public void Start()
        {
            _phase = Phase.Waiting;
            _timer = Random.Range(1.5f, 4.5f);
            _owner.CanMove = false;
            _view.ShowBubble(_game.Art.Dots, 0f);
            Prompt = "낚시 중... 입질을 기다려요";
        }

        public void Update(bool pressed)
        {
            float dt = Time.deltaTime;
            switch (_phase)
            {
                case Phase.Waiting:
                    _timer -= dt;
                    if (pressed)
                    {
                        Finish("너무 일찍 챘어요...");
                    }
                    else if (_timer <= 0f)
                    {
                        _phase = Phase.Bite;
                        _timer = BiteWindow;
                        _view.ShowBubble(_game.Art.Exclaim, 0f);
                        _audio.Play(_audio.Chime);
                        Prompt = "입질! Space!";
                    }
                    break;

                case Phase.Bite:
                    _timer -= dt;
                    if (pressed)
                    {
                        _phase = Phase.Timing;
                        _timer = TimingSeconds;
                        _zoneStart = Random.Range(0.1f, 0.9f - ZoneWidth);
                        _marker = 0f;
                        _view.HideBubble();
                        Prompt = "초록 칸에서 Space!";
                    }
                    else if (_timer <= 0f)
                    {
                        Finish("입질을 놓쳤어요...");
                    }
                    break;

                case Phase.Timing:
                    _timer -= dt;
                    _marker = Mathf.PingPong(Time.time * MarkerSpeed, 1f);
                    if (pressed)
                    {
                        if (_marker >= _zoneStart && _marker <= _zoneStart + ZoneWidth) Catch();
                        else Finish("놓쳤어요...");
                    }
                    else if (_timer <= 0f)
                    {
                        Finish("물고기가 도망갔어요...");
                    }
                    break;
            }
        }

        private void Catch()
        {
            float roll = Random.value;
            (string name, int value, float chance) caught = Catches[0];
            foreach (var c in Catches)
            {
                caught = c;
                roll -= c.chance;
                if (roll < 0f) break;
            }
            if (caught.value > 0)
            {
                _game.Economy.Earn(caught.value);
                _audio.Play(_audio.Tip);
                Finish($"{caught.name} 낚았다! +{ConvenienceStoreGame.Won(caught.value)}");
            }
            else
            {
                _audio.Play(_audio.Angry, 0.5f);
                Finish($"{caught.name}... 꽝");
            }
        }

        private void Finish(string message)
        {
            _phase = Phase.None;
            _owner.CanMove = true;
            _view.HideBubble();
            _game.Float(_owner.Position + Vector2.up * 1.6f, message, new Color(0.2f, 0.45f, 0.75f));
        }

        /// <summary>표시가 오가는 막대. 초록 칸에 있을 때 Space.</summary>
        public void Draw(float s, GUIStyle panelStyle, GUIStyle labelStyle, Color ink)
        {
            if (_phase != Phase.Timing) return;
            float w = 400f * s, h = 84f * s;
            var panel = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - 140f * s, w, h);
            GUI.Box(panel, GUIContent.none, panelStyle);
            labelStyle.normal.textColor = ink;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(panel.x, panel.y + 8f * s, w, 30f * s), "초록 칸에서 Space!", labelStyle);

            var bar = new Rect(panel.x + 30f * s, panel.y + 46f * s, w - 60f * s, 20f * s);
            GUI.color = new Color(0.28f, 0.17f, 0.16f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.85f, 0.4f);
            GUI.DrawTexture(new Rect(bar.x + bar.width * _zoneStart, bar.y, bar.width * ZoneWidth, bar.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bar.x + bar.width * _marker - 2f * s, bar.y - 4f * s, 4f * s, bar.height + 8f * s),
                Texture2D.whiteTexture);
        }
    }
}
